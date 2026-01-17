using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Infrastructure.Persistence;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Tests.Integration.Fixtures;

/// <summary>
/// Test data seeder for populating database with test files and transactions
/// Supports creating files with various statuses for comprehensive testing
/// </summary>
public class TestDataSeeder
{
    private readonly ApplicationDbContext _dbContext;

    public TestDataSeeder(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Seeds database with test files of various statuses
    /// </summary>
    /// <param name="count">Number of files to create</param>
    /// <returns>List of created files</returns>
    public async Task<List<FileEntity>> SeedFilesAsync(int count = 15)
    {
        var files = new List<FileEntity>();
        var now = DateTime.UtcNow;

        // Create files with different statuses
        for (int i = 0; i < count; i++)
        {
            var fileStatus = (i % 4) switch
            {
                0 => FileStatusCode.Uploaded,
                1 => FileStatusCode.Processing,
                2 => FileStatusCode.Processed,
                _ => FileStatusCode.Rejected
            };

            var file = new FileEntity
            {
                Id = Guid.NewGuid(),
                FileName = $"test_file_{i + 1:D3}.txt",
                StatusCode = fileStatus,
                FileSize = 1024 + i * 100,
                S3Key = $"cnab/test_file_{i + 1:D3}.txt",
                UploadedAt = now.AddHours(-i),
                ProcessedAt = fileStatus is FileStatusCode.Processed or FileStatusCode.Rejected
                    ? now.AddHours(-i + 1)
                    : null,
                ErrorMessage = fileStatus == FileStatusCode.Rejected
                    ? "Invalid CNAB format: missing required fields"
                    : null,
                Transactions = new List<Transaction>()
            };

            // Add transactions for processed files
            if (fileStatus == FileStatusCode.Processed)
            {
                file.Transactions = CreateTransactions(file.Id, 5);
            }

            files.Add(file);
        }

        _dbContext.Files.AddRange(files);
        await _dbContext.SaveChangesAsync();

        return files;
    }

    /// <summary>
    /// Creates a single file with specified status
    /// </summary>
    public async Task<FileEntity> CreateFileAsync(
        string fileName = "test_file.txt",
        string statusCode = FileStatusCode.Uploaded,
        DateTime? uploadedAt = null,
        DateTime? processedAt = null,
        string? errorMessage = null,
        int transactionCount = 0)
    {
        var file = new FileEntity
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            StatusCode = statusCode,
            FileSize = 1024,
            S3Key = $"cnab/{Guid.NewGuid():N}.txt",
            UploadedAt = uploadedAt ?? DateTime.UtcNow,
            ProcessedAt = processedAt,
            ErrorMessage = errorMessage,
            Transactions = transactionCount > 0 
                ? CreateTransactions(Guid.NewGuid(), transactionCount)
                : new List<Transaction>()
        };

        _dbContext.Files.Add(file);
        await _dbContext.SaveChangesAsync();

        return file;
    }

    /// <summary>
    /// Creates transactions associated with a file
    /// </summary>
    private static List<Transaction> CreateTransactions(Guid fileId, int count)
    {
        var transactions = new List<Transaction>();
        for (int i = 0; i < count; i++)
        {
            var now = DateTime.UtcNow;
            transactions.Add(new Transaction
            {
                FileId = fileId,
                StoreId = Guid.NewGuid(),
                TransactionTypeCode = (1 + (i % 9)).ToString(), // Types 1-9 as strings
                Amount = (decimal)(100.00 + i * 50.00),
                TransactionDate = DateOnly.FromDateTime(now.AddMinutes(-i)),
                TransactionTime = TimeOnly.FromDateTime(now),
                CPF = "12345678901",
                Card = "123456789012",
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        return transactions;
    }
}
