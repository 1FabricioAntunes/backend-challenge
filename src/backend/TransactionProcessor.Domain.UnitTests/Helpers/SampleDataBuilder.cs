using System;
using System.Collections.Generic;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.ValueObjects;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Domain.UnitTests.Helpers;

/// <summary>
/// Builder pattern for creating test data with sensible defaults.
/// Provides fluent API for customizing test entities.
/// </summary>
public class SampleDataBuilder
{
    /// <summary>
    /// Creates a sample Store entity with default values.
    /// </summary>
    public static Store CreateStore(
        Guid? id = null,
        string ownerName = "João Silva",
        string name = "MERCADO DA AVENIDA")
    {
        var storeId = id ?? Guid.NewGuid();
        var store = new Store(storeId, ownerName, name);

        return store;
    }

    /// <summary>
    /// Creates a sample File entity with default values.
    /// </summary>
    public static FileEntity CreateFile(
        Guid? id = null,
        string fileName = "test-cnab.txt",
        string statusCode = FileStatusCode.Uploaded,
        long fileSize = 1024,
        string s3Key = "cnab/test/test-cnab.txt")
    {
        FileEntity file = new FileEntity(id: Guid.NewGuid(), fileName: fileName)
        {
            FileSize = fileSize,
            S3Key = s3Key,
            UploadedByUserId = Guid.NewGuid()
        };

        if (id.HasValue)
        {
            // Use reflection to set private Id property for testing
            var idProperty = typeof(FileEntity).GetProperty("Id");
            idProperty?.SetValue(file, id.Value);
        }

        return file;
    }

    /// <summary>
    /// Creates a sample Transaction entity with default values.
    /// </summary>
    public static Transaction CreateTransaction(
        long? id = null,
        Guid? fileId = null,
        Guid? storeId = null,
        string transactionTypeCode = "4",
        decimal amount = 10000, // 100.00 BRL in cents
        DateOnly? transactionDate = null,
        TimeOnly? transactionTime = null,
        string cpf = "12345678901",
        string card = "123456789012")
    {
        var transaction = new Transaction(
            fileId: fileId ?? Guid.NewGuid(),
            storeId: storeId ?? Guid.NewGuid(),
            transactionTypeCode: transactionTypeCode,
            amount: amount,
            transactionDate: transactionDate ?? new DateOnly(2024, 1, 15),
            transactionTime: transactionTime ?? new TimeOnly(10, 30, 0),
            cpf: cpf,
            card: card);

        if (id.HasValue)
        {
            var idProperty = typeof(Transaction).GetProperty("Id");
            idProperty?.SetValue(transaction, id.Value);
        }

        if (fileId.HasValue)
        {
            transaction.FileId = fileId.Value;
        }

        if (storeId.HasValue)
        {
            var storeIdProperty = typeof(Transaction).GetProperty("StoreId");
            storeIdProperty?.SetValue(transaction, storeId.Value);
        }

        return transaction;
    }

    /// <summary>
    /// Creates a sample MoneyAmount value object.
    /// </summary>
    public static MoneyAmount CreateMoneyAmount(
        decimal amount = 100.00m,
        string currency = "BRL")
    {
        return new MoneyAmount(amount, currency);
    }

    /// <summary>
    /// Creates a list of transactions with different types for balance testing.
    /// </summary>
    public static List<Transaction> CreateMixedTransactions(Guid storeId)
    {
        return new List<Transaction>
        {
            CreateTransaction(storeId: storeId, transactionTypeCode: "4", amount: 10000), // Crédito (Credit) +100.00
            CreateTransaction(storeId: storeId, transactionTypeCode: "1", amount: 5000),   // Débito (Debit) -50.00
            CreateTransaction(storeId: storeId, transactionTypeCode: "6", amount: 20000),  // Vendas (Sales) +200.00
            CreateTransaction(storeId: storeId, transactionTypeCode: "2", amount: 7500),  // Boleto -75.00
        };
    }
}
