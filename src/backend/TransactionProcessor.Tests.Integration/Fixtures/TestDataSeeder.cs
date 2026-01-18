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

            var fileId = Guid.NewGuid();
            var file = new FileEntity(fileId, $"test_file_{i + 1:D3}.txt")
            {
                FileSize = 1024 + i * 100,
                S3Key = $"cnab/test_file_{i + 1:D3}.txt"
            };

            // Apply status transitions using domain methods
            if (fileStatus == FileStatusCode.Processing)
            {
                file.StartProcessing();
            }
            else if (fileStatus == FileStatusCode.Processed)
            {
                file.StartProcessing();
                file.MarkAsProcessed();
            }
            else if (fileStatus == FileStatusCode.Rejected)
            {
                file.StartProcessing();
                file.MarkAsRejected("Invalid CNAB format: missing required fields");
            }

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
        var fileId = Guid.NewGuid();
        var file = new FileEntity(fileId, fileName)
        {
            FileSize = 1024,
            S3Key = $"cnab/{Guid.NewGuid():N}.txt"
        };

        // Apply status transitions using domain methods
        if (statusCode == FileStatusCode.Processing)
        {
            file.StartProcessing();
        }
        else if (statusCode == FileStatusCode.Processed)
        {
            file.StartProcessing();
            file.MarkAsProcessed();
        }
        else if (statusCode == FileStatusCode.Rejected)
        {
            file.StartProcessing();
            file.MarkAsRejected(errorMessage ?? "Processing failed");
        }

        if (transactionCount > 0)
        {
            file.Transactions = CreateTransactions(fileId, transactionCount);
        }

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
            var transaction = new Transaction(
                fileId: fileId,
                storeId: Guid.NewGuid(),
                transactionTypeCode: (1 + (i % 9)).ToString(), // Types 1-9 as strings
                amount: (decimal)(100.00 + i * 50.00),
                transactionDate: DateOnly.FromDateTime(now.AddMinutes(-i)),
                transactionTime: TimeOnly.FromDateTime(now),
                cpf: "12345678901",
                card: "123456789012"
            );
            transactions.Add(transaction);
        }
        return transactions;
    }
}
