using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Application.Models;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.Interfaces;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Infrastructure.Persistence;

namespace TransactionProcessor.Application.Services;

/// <summary>
/// Implementation of IFileProcessingService that orchestrates CNAB file processing.
/// Handles parsing, validation, and persistence with transactional guarantees.
/// </summary>
public class FileProcessingService : IFileProcessingService
{
    private readonly IFileRepository _fileRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFileStorageService _storageService;
    private readonly ICNABParser _parser;
    private readonly ICNABValidator _validator;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<FileProcessingService> _logger;

    public FileProcessingService(
        IFileRepository fileRepository,
        IStoreRepository storeRepository,
        ITransactionRepository transactionRepository,
        IFileStorageService storageService,
        ICNABParser parser,
        ICNABValidator validator,
        ApplicationDbContext dbContext,
        ILogger<FileProcessingService> logger)
    {
        _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
        _storeRepository = storeRepository ?? throw new ArgumentNullException(nameof(storeRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a CNAB file asynchronously.
    /// 
    /// Flow:
    /// 1. Load file entity and update status to Processing
    /// 2. Download file from S3
    /// 3. Parse and validate CNAB format
    /// 4. Check idempotency (skip if already processed)
    /// 5. In transaction: upsert stores, insert transactions, update balances
    /// 6. Mark file as Processed
    /// 7. On validation error: mark file as Rejected
    /// 8. On processing error: mark file as Rejected with error details
    /// </summary>
    public async Task<FileProcessingResult> ProcessFileAsync(
        Guid fileId,
        string s3Key,
        string fileName,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            { "CorrelationId", correlationId },
            { "FileId", fileId },
            { "FileName", fileName },
            { "S3Key", s3Key }
        });

        var result = new FileProcessingResult();

        try
        {
            // Step 1: Load file and update status to Processing
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
            {
                result.Success = false;
                result.ErrorMessage = $"File not found: {fileId}";
                _logger.LogError("File not found: {FileId}", fileId);
                return result;
            }

            // Check if already processed (idempotency)
            if (file.StatusCode == FileStatusCode.Processed || file.StatusCode == FileStatusCode.Rejected)
            {
                result.Success = file.StatusCode == FileStatusCode.Processed;
                result.ErrorMessage = file.ErrorMessage ?? (file.StatusCode == FileStatusCode.Rejected ? "File was previously rejected" : null);
                _logger.LogInformation("File already in terminal state {Status}, skipping processing", file.StatusCode);
                return result;
            }

            // Update file status to Processing using domain method
            file.StartProcessing();
            await _fileRepository.UpdateAsync(file);
            _logger.LogInformation("Updated file status to Processing");

            // Step 2: Download file from S3
            _logger.LogInformation("Downloading file from S3: {S3Key}", s3Key);
            using var fileStream = await _storageService.DownloadAsync(s3Key, cancellationToken);
            _logger.LogInformation("File downloaded successfully");

            // Step 3: Parse and validate CNAB
            _logger.LogInformation("Parsing CNAB file");
            var parseResult = await _parser.ParseAsync(fileStream, cancellationToken);
            _logger.LogInformation("CNAB parsing completed. Valid lines: {ValidLineCount}, Errors: {ErrorCount}",
                parseResult.ValidLines.Count, parseResult.Errors.Count);

            if (!parseResult.IsValid || parseResult.ValidLines.Count == 0)
            {
                result.Success = false;
                result.ValidationErrors = parseResult.Errors;
                result.ErrorMessage = $"File validation failed: {string.Join("; ", parseResult.Errors.Take(5))}";
                
                // Mark file as Rejected using domain method
                file.MarkAsRejected(result.ErrorMessage);
                await _fileRepository.UpdateAsync(file);
                
                stopwatch.Stop();
                _logger.LogWarning("File validation failed. Marking as Rejected. Errors: {@ValidationErrors}", parseResult.Errors);
                _logger.LogInformation("Metrics: durationMs={DurationMs}, validationErrors={ErrorCount}", stopwatch.ElapsedMilliseconds, parseResult.Errors.Count);
                return result;
            }

            // Step 3b: Validate each record against business rules
            _logger.LogInformation("Validating CNAB records against business rules");
            var recordValidationErrors = new List<string>();
            for (int i = 0; i < parseResult.ValidLines.Count; i++)
            {
                var line = parseResult.ValidLines[i];
                var lineNumber = i + 1;
                var validationResult = _validator.ValidateRecord(line, lineNumber);
                
                if (!validationResult.IsValid)
                {
                    recordValidationErrors.AddRange(validationResult.Errors);
                }
            }

            // If any record validation errors, reject entire file
            if (recordValidationErrors.Count > 0)
            {
                result.Success = false;
                result.ValidationErrors = recordValidationErrors;
                result.ErrorMessage = $"Record validation failed: {string.Join("; ", recordValidationErrors.Take(5))}";
                
                // Mark file as Rejected using domain method
                file.MarkAsRejected(result.ErrorMessage);
                await _fileRepository.UpdateAsync(file);
                
                stopwatch.Stop();
                _logger.LogWarning("Record validation failed. Marking as Rejected. Errors: {@ValidationErrors}", recordValidationErrors);
                _logger.LogInformation("Metrics: durationMs={DurationMs}, validationErrors={ErrorCount}", stopwatch.ElapsedMilliseconds, recordValidationErrors.Count);
                return result;
            }

            _logger.LogInformation("All records validated successfully");

            // Step 4: Check idempotency - verify no transactions already exist for this file
            var existingTransactions = await _transactionRepository.GetByFileIdAsync(fileId);
            if (existingTransactions.Any())
            {
                _logger.LogInformation("Transactions already exist for this file. Skipping processing (idempotent)");
                result.Success = true;
                result.ErrorMessage = null;
                result.TransactionsInserted = existingTransactions.Count();
                return result;
            }

            // Step 5: Process transactions in a database transaction
            _logger.LogInformation("Starting database transaction for file processing");
            IDbContextTransaction? dbTransaction = null;
            
            try
            {
                dbTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                
                var stores = new Dictionary<string, Store>();
                var transactions = new List<Transaction>();

            // Group lines by store and create stores
            var linesByStoreName = parseResult.ValidLines.GroupBy(l => l.StoreName);
            foreach (var storeLines in linesByStoreName)
            {
                var storeName = storeLines.Key;
                var storeOwnerName = storeLines.First().StoreOwner.Trim();

                // Get or create store using composite key (Name, OwnerName)
                // Check once and reuse the result to avoid entity tracking conflicts
                var storeKey = $"{storeName}|{storeOwnerName}";
                Store? store;
                
                if (!stores.TryGetValue(storeKey, out store) || store == null)
                {
                    // First time seeing this store in this file - check if it exists in DB
                    store = await _storeRepository.GetByNameAndOwnerAsync(storeName, storeOwnerName);
                    if (store == null)
                    {
                        // Create new store using constructor for immutable Id
                        store = new Store(
                            id: Guid.NewGuid(),
                            ownerName: storeOwnerName,
                            name: storeName
                        );
                    }
                    // Store the result (either existing from DB or newly created) in dictionary
                    stores[storeKey] = store;
                }

                // Create transactions - Balance is computed on-demand, not persisted
                foreach (var line in storeLines)
                {
                    // Convert DateTime to DateOnly and TimeSpan to TimeOnly (normalized schema)
                    var transactionDate = DateOnly.FromDateTime(line.Date);
                    var transactionTime = TimeOnly.FromTimeSpan(line.Time);
                    
                    // Create transaction using constructor - TypeCode is a string (1-9), not an int
                    // Constructor sets immutable properties; FileId set separately by File aggregate
                    var transaction = new Transaction(
                        fileId: fileId,
                        storeId: store.Id,
                        transactionTypeCode: line.Type.ToString(),
                        amount: (decimal)line.Amount,
                        transactionDate: transactionDate,
                        transactionTime: transactionTime,
                        cpf: line.CPF,
                        card: line.Card
                    );
                    transactions.Add(transaction);
                }

                store.UpdatedAt = DateTime.UtcNow;
            }

            // Upsert stores - Handle entity tracking correctly
            // Stores retrieved from DB use AsNoTracking, so we need to check if they're already tracked
            // or attach them properly to avoid tracking conflicts
            foreach (var store in stores.Values)
            {
                // Check if store already exists in database by querying the context directly
                // Use FindAsync which returns a tracked entity if it exists, or null if not
                var trackedStore = await _dbContext.Stores.FindAsync(new object[] { store.Id }, cancellationToken);
                
                if (trackedStore == null)
                {
                    // Store doesn't exist in DB - add it as new
                    await _storeRepository.AddAsync(store);
                }
                else
                {
                    // Store exists in DB and is now tracked - update the tracked entity
                    trackedStore.UpdatedAt = DateTime.UtcNow;
                    // Update tracked entity properties if needed (Name/OwnerName shouldn't change)
                    await _storeRepository.UpdateAsync(trackedStore);
                }
                result.StoresUpserted++;
            }

                // Insert transactions in batch
                await _transactionRepository.AddRangeAsync(transactions);
                result.TransactionsInserted = transactions.Count;

                _logger.LogInformation("Persisted {StoreCount} stores and {TransactionCount} transactions",
                    result.StoresUpserted, result.TransactionsInserted);

                // Step 6: Mark file as Processed using domain method
                file.MarkAsProcessed();
                await _fileRepository.UpdateAsync(file);

                // Commit transaction - all or nothing
                await dbTransaction.CommitAsync(cancellationToken);
                _logger.LogInformation("Database transaction committed successfully");

                stopwatch.Stop();
                result.Success = true;
                _logger.LogInformation("File processing completed successfully");
                _logger.LogInformation("Metrics: durationMs={DurationMs}, transactionsInserted={Transactions}, storesUpserted={Stores}", stopwatch.ElapsedMilliseconds, result.TransactionsInserted, result.StoresUpserted);
                return result;
            }
            catch (Exception)
            {
                // Rollback transaction on any error
                if (dbTransaction != null)
                {
                    _logger.LogWarning("Rolling back database transaction due to error");
                    await dbTransaction.RollbackAsync(cancellationToken);
                }
                throw; // Re-throw to be caught by outer exception handler
            }
            finally
            {
                dbTransaction?.Dispose();
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Processing error: {ex.Message}";
            stopwatch.Stop();

            _logger.LogError(ex, "Unexpected error during file processing. FileId: {FileId}", fileId);

            // Try to mark file as rejected with error details
            try
            {
                var file = await _fileRepository.GetByIdAsync(fileId);
                if (file != null && file.StatusCode == FileStatusCode.Processing)
                {
                    file.MarkAsRejected($"Processing error: {ex.Message}");
                    await _fileRepository.UpdateAsync(file);
                    _logger.LogInformation("Marked file as Rejected with error details");
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update file status after processing error");
            }

            _logger.LogInformation("Metrics: durationMs={DurationMs}, errorType=processing", stopwatch.ElapsedMilliseconds);

            // Re-throw for HostedService to handle (leave message for retry on processing errors)
            throw;
        }
    }
}
