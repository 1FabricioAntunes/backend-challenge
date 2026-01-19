using System.Diagnostics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using TransactionProcessor.Application.Models;
using TransactionProcessor.Application.Services;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.Interfaces;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Infrastructure.Metrics;
using TransactionProcessor.Infrastructure.Persistence;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Application.UseCases.Files.Process;

/// <summary>
/// Handler for processing CNAB files with complete transactional integrity.
/// 
/// Processing Flow:
/// 1. Load file and verify not already processed (idempotency)
/// 2. Update file status to Processing
/// 3. Download file from S3
/// 4. Parse all CNAB lines (80-character fixed format)
/// 5. Validate all parsed records against business rules
/// 6. If validation fails, mark file as Rejected and return
/// 7. Create Transaction entities from validated records
/// 8. Group by store and create/update Store entities
/// 9. Open database transaction
/// 10. Persist all entities atomically
/// 11. Mark file as Processed
/// 12. Commit transaction
/// 
/// On any error: Rollback database transaction and mark file as Rejected.
/// 
/// Reference: docs/async-processing.md § Processing Flow
/// Reference: technical-decisions.md § 6 Asynchronous Processing Flow
/// </summary>
public class ProcessFileCommandHandler : IRequestHandler<ProcessFileCommand, ProcessingResult>
{
    private readonly IFileRepository _fileRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFileStorageService _storageService;
    private readonly ICNABParser _parser;
    private readonly ICNABValidator _validator;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ProcessFileCommandHandler> _logger;

    /// <summary>
    /// Initializes the handler with required dependencies.
    /// </summary>
    public ProcessFileCommandHandler(
        IFileRepository fileRepository,
        IStoreRepository storeRepository,
        ITransactionRepository transactionRepository,
        IFileStorageService storageService,
        ICNABParser parser,
        ICNABValidator validator,
        ApplicationDbContext dbContext,
        ILogger<ProcessFileCommandHandler> logger)
    {
        _fileRepository = fileRepository;
        _storeRepository = storeRepository;
        _transactionRepository = transactionRepository;
        _storageService = storageService;
        _parser = parser;
        _validator = validator;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Processes a CNAB file with full transactional support.
    /// </summary>
    public async Task<ProcessingResult> Handle(ProcessFileCommand request, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var overallStopwatch = Stopwatch.StartNew();
        
        var result = new ProcessingResult
        {
            FileId = request.FileId,
            StartedAt = startTime
        };

        // Push correlation ID to log context for structured logging
        using (LogContext.PushProperty("CorrelationId", request.CorrelationId))
        {
            try
            {
                _logger.LogInformation(
                    "[{CorrelationId}] Starting file processing for FileId={FileId}, S3Key={S3Key}",
                    request.CorrelationId, request.FileId, request.S3Key);

            // Step 1: Get File entity from database
            var file = await _fileRepository.GetByIdAsync(request.FileId);
            if (file == null)
            {
                _logger.LogError(
                    "[{CorrelationId}] File not found. FileId={FileId}",
                    request.CorrelationId, request.FileId);
                result.Success = false;
                result.Status = "Rejected";
                result.ErrorMessage = "File not found in database.";
                return result;
            }

            // Idempotency check: skip if already processed
            if (file.StatusCode == FileStatusCode.Processed)
            {
                _logger.LogInformation(
                    "[{CorrelationId}] File already processed. FileId={FileId}",
                    request.CorrelationId, request.FileId);
                result.Success = true;
                result.Status = "Processed";
                result.TransactionsInserted = 0;
                result.StoresUpserted = 0;
                return result;
            }

            // Step 2: Update status to Processing
            file.StartProcessing();
            await _fileRepository.UpdateAsync(file);

            // Step 3: Download file from S3
            _logger.LogInformation(
                "[{CorrelationId}] Downloading file from S3. S3Key={S3Key}",
                request.CorrelationId, request.S3Key);

            using var fileStream = await _storageService.DownloadAsync(request.S3Key, cancellationToken);

            // Step 4: Parse all lines with CNABParser
            _logger.LogInformation(
                "[{CorrelationId}] Parsing CNAB file",
                request.CorrelationId);

            var parseStopwatch = Stopwatch.StartNew();
            var parseResult = await _parser.ParseAsync(fileStream, cancellationToken);
            parseStopwatch.Stop();

            // ========================================================================
            // METRICS: Record file parsing duration
            // ========================================================================
            MetricsService.FileParsingDurationSeconds
                .WithLabels("cnab")
                .Observe(parseStopwatch.Elapsed.TotalSeconds);

            if (!parseResult.IsValid)
            {
                // ========================================================================
                // METRICS: Record parsing error
                // ========================================================================
                MetricsService.RecordError("parsing_error");

                _logger.LogWarning(
                    "[{CorrelationId}] File parsing failed with {ErrorCount} errors",
                    request.CorrelationId, parseResult.Errors.Count);
                
                result.Success = false;
                result.Status = "Rejected";
                result.ErrorMessage = $"File parsing failed: {string.Join("; ", parseResult.Errors.Take(5))}";
                result.ValidationErrors = parseResult.Errors;
                await MarkFileAsRejected(file, result.ErrorMessage, cancellationToken);
                return result;
            }

            // Step 5: Validate all records with CNABValidator
            _logger.LogInformation(
                "[{CorrelationId}] Validating {LineCount} parsed records",
                request.CorrelationId, parseResult.ValidLines.Count);

            var validationStopwatch = Stopwatch.StartNew();
            var recordValidationErrors = new List<string>();
            var validatedRecords = new List<CNABLineData>();

            for (int i = 0; i < parseResult.ValidLines.Count; i++)
            {
                var line = parseResult.ValidLines[i];
                var lineNumber = i + 1;

                var validationResult = _validator.ValidateRecord(line, lineNumber);
                if (!validationResult.IsValid)
                {
                    recordValidationErrors.AddRange(validationResult.Errors);
                }
                else
                {
                    validatedRecords.Add(line);
                }
            }

            validationStopwatch.Stop();

            // ========================================================================
            // METRICS: Record validation duration
            // ========================================================================
            MetricsService.ValidationDurationSeconds
                .WithLabels("cnab")
                .Observe(validationStopwatch.Elapsed.TotalSeconds);

            // Step 6: If any validation fails, mark file as Rejected and return
            if (recordValidationErrors.Count > 0)
            {
                // ========================================================================
                // METRICS: Record validation error
                // ========================================================================
                MetricsService.RecordError("validation_error");

                _logger.LogWarning(
                    "[{CorrelationId}] Record validation failed with {ErrorCount} errors",
                    request.CorrelationId, recordValidationErrors.Count);

                result.Success = false;
                result.Status = "Rejected";
                result.ErrorMessage = $"Record validation failed: {string.Join("; ", recordValidationErrors.Take(5))}";
                result.ValidationErrors = recordValidationErrors;
                await MarkFileAsRejected(file, result.ErrorMessage, cancellationToken);
                return result;
            }

            // Step 7: Idempotency check - verify no transactions already exist for this file
            var existingTransactions = await _transactionRepository.GetByFileIdAsync(request.FileId);
            if (existingTransactions.Any())
            {
                _logger.LogInformation(
                    "[{CorrelationId}] File already has {TransactionCount} transactions, skipping",
                    request.CorrelationId, existingTransactions.Count());
                result.Success = true;
                result.Status = "Processed";
                result.TransactionsInserted = 0;
                result.StoresUpserted = 0;
                return result;
            }

            // Step 8 & 9: Group by store and prepare entities for transaction
            _logger.LogInformation(
                "[{CorrelationId}] Grouping {RecordCount} records by store",
                request.CorrelationId, validatedRecords.Count);

            var storeGroups = validatedRecords.GroupBy(r => (r.StoreOwner, r.StoreName)).ToList();
            var transactionsToAdd = new List<Transaction>();
            var storesToUpsert = new List<Store>();
            var storeMap = new Dictionary<(string Owner, string Name), Store>();

            // Process each store group
            foreach (var storeGroup in storeGroups)
            {
                var (storeOwner, storeName) = storeGroup.Key;
                
                // Get or create store (use cache if already loaded)
                if (!storeMap.TryGetValue((storeOwner, storeName), out var store))
                {
                    var fetchedStore = await _storeRepository.GetByNameAndOwnerAsync(storeName, storeOwner);
                    store = fetchedStore ?? new Store(Guid.NewGuid(), storeOwner, storeName);
                    
                    if (fetchedStore == null)
                    {
                        storesToUpsert.Add(store);
                    }
                    
                    storeMap[(storeOwner, storeName)] = store;
                }

                // Create transactions for this store
                foreach (var record in storeGroup)
                {
                    var transaction = new Transaction(
                        fileId: request.FileId,
                        storeId: store.Id,
                        transactionTypeCode: record.Type.ToString(),
                        amount: record.Amount,
                        transactionDate: DateOnly.FromDateTime(record.Date),
                        transactionTime: TimeOnly.FromTimeSpan(record.Time),
                        cpf: record.CPF,
                        card: record.Card);

                    transactionsToAdd.Add(transaction);
                }
            }

            // Step 9: Open database transaction
            _logger.LogInformation(
                "[{CorrelationId}] Starting database transaction. Stores={StoreCount}, Transactions={TransactionCount}",
                request.CorrelationId, storesToUpsert.Count, transactionsToAdd.Count);

            using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Step 10: Save all entities
                // Add new stores
                foreach (var store in storesToUpsert)
                {
                    await _storeRepository.AddAsync(store);
                }

                // ========================================================================
                // METRICS: Record store insert operations
                // ========================================================================
                if (storesToUpsert.Count > 0)
                {
                    MetricsService.RecordDatabaseOperation("insert");
                }

                // Add all transactions
                var txnStopwatch = Stopwatch.StartNew();
                await _transactionRepository.AddRangeAsync(transactionsToAdd);
                txnStopwatch.Stop();

                // ========================================================================
                // METRICS: Record bulk transaction insert
                // ========================================================================
                MetricsService.DatabaseQueryDurationSeconds
                    .WithLabels("insert", "transaction")
                    .Observe(txnStopwatch.Elapsed.TotalSeconds);
                MetricsService.RecordDatabaseOperation("insert");

                // Step 11: Update store balances
                foreach (var (storeKey, store) in storeMap)
                {
                    var storeTransactions = transactionsToAdd
                        .Where(t => t.StoreId == store.Id)
                        .ToList();

                    if (storeTransactions.Count == 0)
                        continue;

                    // Calculate balance from signed amounts
                    decimal balance = 0m;
                    foreach (var t in storeTransactions)
                    {
                        if (!int.TryParse(t.TransactionTypeCode, out var typeCode))
                            continue;

                        decimal signedAmount = typeCode switch
                        {
                            // Debit types: negative
                            1 or 4 or 5 or 6 or 7 or 8 => -(t.Amount / 100m),
                            // Credit types: positive
                            2 or 3 or 9 => (t.Amount / 100m),
                            _ => 0m
                        };
                        balance += signedAmount;
                    }

                    store.UpdateBalance(balance);

                    var storeUpdateStopwatch = Stopwatch.StartNew();
                    await _storeRepository.UpdateAsync(store);
                    storeUpdateStopwatch.Stop();

                    // ========================================================================
                    // METRICS: Record store balance update duration
                    // ========================================================================
                    MetricsService.DatabaseQueryDurationSeconds
                        .WithLabels("update", "store")
                        .Observe(storeUpdateStopwatch.Elapsed.TotalSeconds);
                    MetricsService.RecordDatabaseOperation("update");
                }

                // Step 12: Mark file as Processed
                file.MarkAsProcessed();
                await _fileRepository.UpdateAsync(file);

                // Save all changes
                await _dbContext.SaveChangesAsync(cancellationToken);

                // Step 13: Commit transaction
                await dbTransaction.CommitAsync(cancellationToken);

                overallStopwatch.Stop();

                // ========================================================================
                // METRICS: Record successful file processing
                // ========================================================================
                MetricsService.RecordFileProcessed("success");
                MetricsService.FileProcessingDurationSeconds
                    .WithLabels("cnab")
                    .Observe(overallStopwatch.Elapsed.TotalSeconds);

                _logger.LogInformation(
                    "[{CorrelationId}] File processing completed successfully. " +
                    "Transactions={TransactionCount}, Stores={StoreCount}",
                    request.CorrelationId, transactionsToAdd.Count, storesToUpsert.Count);

                result.Success = true;
                result.Status = "Processed";
                result.TransactionsInserted = transactionsToAdd.Count;
                result.StoresUpserted = storesToUpsert.Count;
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                
                // ========================================================================
                // METRICS: Record database error and rollback
                // ========================================================================
                MetricsService.RecordError("database_error");
                overallStopwatch.Stop();
                MetricsService.FileProcessingDurationSeconds
                    .WithLabels("cnab")
                    .Observe(overallStopwatch.Elapsed.TotalSeconds);

                _logger.LogError(
                    ex,
                    "[{CorrelationId}] Database transaction failed, rolled back. Error={Error}",
                    request.CorrelationId, ex.Message);

                await MarkFileAsRejected(
                    file,
                    $"Database error during processing: {ex.Message}",
                    cancellationToken);

                result.Success = false;
                result.Status = "Rejected";
                result.ErrorMessage = "Database error during transaction persistence.";
                throw;
            }
        }
        catch (Exception ex)
        {
            overallStopwatch.Stop();

            // ========================================================================
            // METRICS: Record unhandled exception
            // ========================================================================
            MetricsService.RecordError("unhandled_exception");
            MetricsService.FileProcessingDurationSeconds
                .WithLabels("cnab")
                .Observe(overallStopwatch.Elapsed.TotalSeconds);

            _logger.LogError(
                ex,
                "[{CorrelationId}] File processing failed with exception. FileId={FileId}",
                request.CorrelationId, request.FileId);

            result.Success = false;
            result.Status = "Rejected";
            result.ErrorMessage = $"Processing error: {ex.Message}";
        }
        finally
        {
            result.CompletedAt = DateTime.UtcNow;
            result.ProcessingDuration = result.CompletedAt - result.StartedAt;
            
            _logger.LogInformation(
                "[{CorrelationId}] File processing finished. Success={Success}, Duration={DurationMs}ms, " +
                "Status={Status}, Transactions={TransactionCount}, Stores={StoreCount}",
                request.CorrelationId, result.Success, result.ProcessingDuration.TotalMilliseconds,
                result.Status, result.TransactionsInserted, result.StoresUpserted);
        }
        }

        return result;
    }

    /// <summary>
    /// Helper method to mark a file as rejected with error message.
    /// </summary>
    private async Task MarkFileAsRejected(FileEntity file, string errorMessage, CancellationToken cancellationToken)
    {
        file.MarkAsRejected(errorMessage);
        await _fileRepository.UpdateAsync(file);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
