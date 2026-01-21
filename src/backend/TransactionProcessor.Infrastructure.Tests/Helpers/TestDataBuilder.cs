using System;
using System.Collections.Generic;
using TransactionProcessor.Domain.Entities;
using System.Linq;
using System.Threading.Tasks;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Infrastructure.Tests.Helpers;

/// <summary>
/// Test data builder for creating domain entities with sensible defaults.
/// Useful for test setup to reduce boilerplate code.
/// </summary>
public static class TestDataBuilder
{
    /// <summary>
    /// Creates a test Store with default values.
    /// </summary>
    /// <param name="name">Store name (default: "Test Store")</param>
    /// <param name="ownerName">Store owner name (default: "Test Owner")</param>
    /// <returns>Store instance for testing</returns>
    public static Store CreateTestStore(
        string name = "Test Store",
        string ownerName = "Test Owner")
    {
        return new Store(id: Guid.NewGuid(), ownerName: ownerName, name: name)
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a test File with default values.
    /// </summary>
    /// <param name="fileName">File name (default: "test-file.txt")</param>
    /// <returns>File instance for testing</returns>
    public static FileEntity CreateTestFile(string fileName = "test-file.txt")
    {
        return new FileEntity(id: Guid.NewGuid(), fileName: fileName)
        {
            FileSize = 1024,
            S3Key = $"cnab/{Guid.NewGuid()}/{fileName}",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a test Transaction with default values.
    /// </summary>
    /// <param name="fileId">File ID reference</param>
    /// <param name="storeId">Store ID reference</param>
    /// <param name="transactionTypeCode">Transaction type code (1-9)</param>
    /// <param name="amount">Transaction amount in BRL</param>
    /// <returns>Transaction instance for testing</returns>
    public static Transaction CreateTestTransaction(
        Guid fileId,
        Guid storeId,
        string transactionTypeCode = "1",
        decimal amount = 100.00m)
    {
        return new Transaction(
            fileId: fileId,
            storeId: storeId,
            transactionTypeCode: transactionTypeCode,
            amount: amount,
            cpf: "12345678901",
            card: "123456789012",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
            transactionTime: TimeOnly.FromTimeSpan(DateTime.UtcNow.TimeOfDay));
    }

    /// <summary>
    /// Generates a valid CNAB 240 file content for testing.
    /// Includes header, transactions, and trailer.
    /// </summary>
    /// <param name="storeName">Store name in header</param>
    /// <param name="ownerName">Store owner name in header</param>
    /// <param name="transactionCount">Number of transactions to include</param>
    /// <returns>CNAB file content as string</returns>
    public static string GenerateValidCnabContent(
        string storeName = "TestStore",
        string ownerName = "TestOwner",
        int transactionCount = 1)
    {
        var lines = new List<string>();

        // Header line
        lines.Add($"034000000         000001201208072011               {storeName.PadRight(19)}          00000000000000000000000000000000000000000000");

        // Transaction lines
        for (int i = 0; i < transactionCount; i++)
        {
            var typeCode = ((i % 9) + 1).ToString();
            lines.Add($"10713700         1212655010154         000000001212{typeCode}150101000000000001234D000000000000000000000000000000000000000000000");
            lines.Add($"20713700         1212655010154         000000001212C150101000000000012340000000000000000000000000000000000000000000000000");
            lines.Add($"30713700         1212655010154         000000001212S150101000000000001234123456789                     000000000000000000000000");
        }

        // Trailer line
        var totalLines = 2 + (transactionCount * 3); // header + transactions + trailer
        lines.Add($"9{totalLines.ToString().PadLeft(6, '0')}00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");

        return string.Join(Environment.NewLine, lines);
    }
}
