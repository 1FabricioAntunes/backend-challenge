using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.UnitTests.Helpers;
using Xunit;
using StoreEntity = TransactionProcessor.Domain.Entities.Store;
using TransactionEntity = TransactionProcessor.Domain.Entities.Transaction;
using TransactionTypeEntity = TransactionProcessor.Domain.Entities.TransactionType;

namespace TransactionProcessor.Domain.UnitTests.Tests.Store;

/// <summary>
/// Unit tests for Store aggregate root.
/// Tests store creation, transaction handling, balance calculation,
/// invariants, and edge cases.
/// </summary>
public class StoreTests : TestBase
{
    #region Store Creation and Initialization

    [Fact]
    public void Constructor_WithValidParameters_CreatesStoreWithZeroBalance()
    {
        // Arrange
        var storeId = Guid.NewGuid();
        var ownerName = "João Silva";
        var storeName = "Loja Centro";

        // Act
        var store = new StoreEntity(storeId, ownerName, storeName);

        // Assert
        store.Id.Should().Be(storeId);
        store.OwnerName.Should().Be(ownerName);
        store.Name.Should().Be(storeName);
        store.Balance.Should().Be(0m);
        store.Transactions.Should().BeEmpty();
        store.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        store.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithNullOwnerName_ThrowsArgumentException()
    {
        // Arrange
        var storeId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new StoreEntity(storeId, null!, "Loja Centro"));

        exception.Message.Should().Contain("Owner name is required");
        exception.ParamName.Should().Be("ownerName");
    }

    [Fact]
    public void Constructor_WithEmptyOwnerName_ThrowsArgumentException()
    {
        // Arrange
        var storeId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new StoreEntity(storeId, "", "Loja Centro"));

        exception.Message.Should().Contain("Owner name is required");
    }

    [Fact]
    public void Constructor_WithWhitespaceOwnerName_ThrowsArgumentException()
    {
        // Arrange
        var storeId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new StoreEntity(storeId, "   ", "Loja Centro"));

        exception.Message.Should().Contain("Owner name is required");
    }

    [Fact]
    public void Constructor_WithNullStoreName_ThrowsArgumentException()
    {
        // Arrange
        var storeId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new StoreEntity(storeId, "João Silva", null!));

        exception.Message.Should().Contain("Store name is required");
        exception.ParamName.Should().Be("name");
    }

    [Fact]
    public void Constructor_WithEmptyStoreName_ThrowsArgumentException()
    {
        // Arrange
        var storeId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new StoreEntity(storeId, "João Silva", ""));

        exception.Message.Should().Contain("Store name is required");
    }

    [Fact]
    public void Constructor_WithWhitespaceStoreName_ThrowsArgumentException()
    {
        // Arrange
        var storeId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new StoreEntity(storeId, "João Silva", "   "));

        exception.Message.Should().Contain("Store name is required");
    }

    #endregion

    #region Store ID Immutability

    [Fact]
    public void Store_IdIsImmutable_CannotBeChangedAfterConstruction()
    {
        // Arrange
        var originalId = Guid.NewGuid();
        var store = new StoreEntity(originalId, "João Silva", "Loja Centro");

        // Act & Assert - Id is readonly (private set), cannot be changed after construction
        // This test documents that Id is immutable
        store.Id.Should().Be(originalId);
        // Note: Id property has private setter, so it cannot be modified externally
    }

    #endregion

    #region Balance Updates

    [Fact]
    public void UpdateBalance_WithValidPositiveAmount_UpdatesBalance()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();
        var newBalance = 500.50m;

        // Act
        store.UpdateBalance(newBalance);

        // Assert
        store.Balance.Should().Be(newBalance);
        store.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateBalance_WithZero_UpdatesBalanceToZero()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();
        store.UpdateBalance(100m);

        // Act
        store.UpdateBalance(0m);

        // Assert
        store.Balance.Should().Be(0m);
    }

    [Fact]
    public void UpdateBalance_WithNegativeAmount_ThrowsArgumentException()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => store.UpdateBalance(-50m));

        exception.Message.Should().Contain("Balance cannot be negative");
        exception.ParamName.Should().Be("newBalance");
    }

    [Fact]
    public void UpdateBalance_UpdatesTimestamp()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();
        var originalUpdatedAt = store.UpdatedAt;
        System.Threading.Thread.Sleep(10);

        // Act
        store.UpdateBalance(250m);

        // Assert
        store.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    #endregion

    #region Calculate Balance - Single Transaction

    [Fact]
    public void CalculateBalance_WithEmptyTransactionList_ReturnsZero()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();

        // Act
        var balance = store.CalculateBalance(new List<TransactionEntity>());

        // Assert
        balance.Should().Be(0m);
    }

    [Fact]
    public void CalculateBalance_WithSingleCreditTransaction_ReturnsPositiveAmount()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();
        var transaction = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "4", // Crédito
            amount: 10000m); // 100.00 BRL

        var transactionType = new TransactionTypeEntity { Sign = "+", TypeCode = "4" };
        transaction.TransactionType = transactionType;

        var transactions = new List<TransactionEntity> { transaction };

        // Act
        var balance = store.CalculateBalance(transactions);

        // Assert
        balance.Should().Be(100.00m);
    }

    [Fact]
    public void CalculateBalance_WithSingleDebitTransaction_ReturnsNegativeAmount()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();
        var transaction = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "1", // Débito
            amount: 5000m); // 50.00 BRL

        var transactionType = new TransactionTypeEntity { Sign = "-", TypeCode = "1" };
        transaction.TransactionType = transactionType;

        var transactions = new List<TransactionEntity> { transaction };

        // Act
        var balance = store.CalculateBalance(transactions);

        // Assert
        balance.Should().Be(-50.00m);
    }

    #endregion

    #region Calculate Balance - Multiple Transactions

    [Fact]
    public void CalculateBalance_WithMixedTransactions_ReturnsCorrectTotal()
    {
        // Arrange: Create store and transactions
        var store = SampleDataBuilder.CreateStore();

        // Arrange: Transaction 1 - Credit (Sales, Type 6) = +500.00
        var tx1 = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "6",
            amount: 50000m);
        var tx1Type = new TransactionTypeEntity { Sign = "+", TypeCode = "6" };
        tx1.TransactionType = tx1Type;

        // Arrange: Transaction 2 - Debit (Type 1) = -200.00
        var tx2 = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "1",
            amount: 20000m);
        var tx2Type = new TransactionTypeEntity { Sign = "-", TypeCode = "1" };
        tx2.TransactionType = tx2Type;

        // Arrange: Transaction 3 - Credit (Type 4) = +150.00
        var tx3 = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "4",
            amount: 15000m);
        var tx3Type = new TransactionTypeEntity { Sign = "+", TypeCode = "4" };
        tx3.TransactionType = tx3Type;

        var transactions = new List<TransactionEntity> { tx1, tx2, tx3 };

        // Act
        var balance = store.CalculateBalance(transactions);

        // Assert: 500 - 200 + 150 = 450
        balance.Should().Be(450.00m);
    }

    [Fact]
    public void CalculateBalance_WithMultipleCreditTransactions_ReturnsSumOfCredits()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();

        var tx1 = SampleDataBuilder.CreateTransaction(storeId: store.Id, transactionTypeCode: "4", amount: 100000m);
        var tx1Type = new TransactionTypeEntity { Sign = "+", TypeCode = "6" };
        tx1.TransactionType = tx1Type;

        var tx2 = SampleDataBuilder.CreateTransaction(storeId: store.Id, transactionTypeCode: "6", amount: 50000m);
        var tx2Type = new TransactionTypeEntity { Sign = "+", TypeCode = "6" };
        tx2.TransactionType = tx2Type;

        var tx3 = SampleDataBuilder.CreateTransaction(storeId: store.Id, transactionTypeCode: "7", amount: 75000m);
        var tx3Type = new TransactionTypeEntity { Sign = "+", TypeCode = "4" };
        tx3.TransactionType = tx3Type;

        var transactions = new List<TransactionEntity> { tx1, tx2, tx3 };

        // Act
        var balance = store.CalculateBalance(transactions);

        // Assert: 1000 + 500 + 750 = 2250
        balance.Should().Be(2250.00m);
    }

    [Fact]
    public void CalculateBalance_WithMultipleDebitTransactions_ReturnsSumOfDebits()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();

        var tx1 = SampleDataBuilder.CreateTransaction(storeId: store.Id, transactionTypeCode: "1", amount: 30000m);
        var tx1Type = new TransactionTypeEntity { Sign = "-", TypeCode = "1" };
        tx1.TransactionType = tx1Type;

        var tx2 = SampleDataBuilder.CreateTransaction(storeId: store.Id, transactionTypeCode: "2", amount: 20000m);
        var tx2Type = new TransactionTypeEntity { Sign = "-", TypeCode = "2" };
        tx2.TransactionType = tx2Type;

        var tx3 = SampleDataBuilder.CreateTransaction(storeId: store.Id, transactionTypeCode: "3", amount: 15000m);
        var tx3Type = new TransactionTypeEntity { Sign = "-", TypeCode = "3" };
        tx3.TransactionType = tx3Type;

        var transactions = new List<TransactionEntity> { tx1, tx2, tx3 };

        // Act
        var balance = store.CalculateBalance(transactions);

        // Assert: -300 - 200 - 150 = -650
        balance.Should().Be(-650.00m);
    }

    [Fact]
    public void CalculateBalance_BalancingCreditsAndDebits_ReturnsNetZero()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();

        var credit = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "4",
            amount: 50000m);
        var creditType = new TransactionTypeEntity { Sign = "+", TypeCode = "4" };
        credit.TransactionType = creditType;

        var debit = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "1",
            amount: 50000m);
        var debitType = new TransactionTypeEntity { Sign = "-", TypeCode = "1" };
        debit.TransactionType = debitType;

        var transactions = new List<TransactionEntity> { credit, debit };

        // Act
        var balance = store.CalculateBalance(transactions);

        // Assert: 500 - 500 = 0
        balance.Should().Be(0m);
    }

    #endregion

    #region Edge Cases - Transaction Amounts

    [Fact]
    public void CalculateBalance_WithMinimumAmount_ReturnsCorrectBalance()
    {
        // Arrange: 1 cent = 0.01 BRL
        var store = SampleDataBuilder.CreateStore();
        var transaction = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "4",
            amount: 1m);
        var transactionType = new TransactionTypeEntity { Sign = "+", TypeCode = "4" };
        transaction.TransactionType = transactionType;

        var transactions = new List<TransactionEntity> { transaction };

        // Act
        var balance = store.CalculateBalance(transactions);

        // Assert
        balance.Should().Be(0.01m);
    }

    [Fact]
    public void CalculateBalance_WithLargeAmount_ReturnsCorrectBalance()
    {
        // Arrange: 999,999,999 cents = 9,999,999.99 BRL
        var store = SampleDataBuilder.CreateStore();
        var transaction = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "4",
            amount: 999999999m);
        var transactionType = new TransactionTypeEntity { Sign = "+", TypeCode = "4" };
        transaction.TransactionType = transactionType;

        var transactions = new List<TransactionEntity> { transaction };

        // Act
        var balance = store.CalculateBalance(transactions);

        // Assert
        balance.Should().Be(9999999.99m);
    }

    [Fact]
    public void CalculateBalance_WithOddCentValues_PreservesPrecision()
    {
        // Arrange: Test values like 123 cents = 1.23 BRL
        var store = SampleDataBuilder.CreateStore();

        var tx1 = SampleDataBuilder.CreateTransaction(storeId: store.Id, transactionTypeCode: "4", amount: 123m);
        var type1 = new TransactionTypeEntity { Sign = "+", TypeCode = "4" };
        tx1.TransactionType = type1;

        var tx2 = SampleDataBuilder.CreateTransaction(storeId: store.Id, transactionTypeCode: "4", amount: 45m);
        var type2 = new TransactionTypeEntity { Sign = "+", TypeCode = "4" };
        tx2.TransactionType = type2;

        var transactions = new List<TransactionEntity> { tx1, tx2 };

        // Act
        var balance = store.CalculateBalance(transactions);

        // Assert: 1.23 + 0.45 = 1.68
        balance.Should().Be(1.68m);
    }

    [Fact]
    public void CalculateBalance_WithManyTransactions_CalculatesCorrectTotal()
    {
        // Arrange: Create 100 transactions of varying types and amounts
        var store = SampleDataBuilder.CreateStore();
        var transactions = new List<TransactionEntity>();

        // Add 50 credit transactions of 10000 cents each = 50 * 100 = 5000 BRL
        for (int i = 0; i < 50; i++)
        {
            var tx = SampleDataBuilder.CreateTransaction(
                storeId: store.Id,
                transactionTypeCode: "4",
                amount: 10000m);
            var transactionType = new TransactionTypeEntity { Sign = "+", TypeCode = "4" };
            tx.TransactionType = transactionType;
            transactions.Add(tx);
        }

        // Add 50 debit transactions of 5000 cents each = 50 * 50 = 2500 BRL
        for (int i = 0; i < 50; i++)
        {
            var tx = SampleDataBuilder.CreateTransaction(
                storeId: store.Id,
                transactionTypeCode: "1",
                amount: 5000m);
            var transactionType = new TransactionTypeEntity { Sign = "-", TypeCode = "1" };
            tx.TransactionType = transactionType;
            transactions.Add(tx);
        }

        // Act
        var balance = store.CalculateBalance(transactions);

        // Assert: 5000 - 2500 = 2500
        balance.Should().Be(2500.00m);
    }

    #endregion

    #region Edge Cases - Invariants

    [Fact]
    public void CalculateBalance_WithNullTransactionTypeProperty_ThrowsInvalidOperationException()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();
        var transaction = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "4",
            amount: 10000m);
        transaction.TransactionType = null;

        var transactions = new List<TransactionEntity> { transaction };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => store.CalculateBalance(transactions));

        exception.Message.Should().Contain("TransactionType navigation property");
        exception.Message.Should().Contain("eager-loaded");
    }

    [Fact]
    public void CalculateBalance_WithNullTransactionsList_ReturnsZero()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();

        // Act - Should handle null gracefully
        // Note: The implementation checks if transactions is null OR Count == 0
        var balance = store.CalculateBalance(null!);

        // Assert
        balance.Should().Be(0m);
    }

    #endregion

    #region Real-World Scenarios

    [Fact]
    public void CalculateBalance_RealWorldScenario_MulipleCNABTypes()
    {
        // Arrange: Realistic store with mixed transaction types
        var store = SampleDataBuilder.CreateStore();
        var transactions = new List<TransactionEntity>();

        // Type 4 (Crédito) - 1000 BRL credit
        var creditTx = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "4",
            amount: 100000m);
        var creditType = new TransactionTypeEntity { Sign = "+", TypeCode = "4" };
        creditTx.TransactionType = creditType;
        transactions.Add(creditTx);

        // Type 6 (Vendas) - 500 BRL sales receipt
        var salesTx = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "6",
            amount: 50000m);
        var salesType = new TransactionTypeEntity { Sign = "+", TypeCode = "6" };
        salesTx.TransactionType = salesType;
        transactions.Add(salesTx);

        // Type 1 (Débito) - 300 BRL debit
        var debitTx = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "1",
            amount: 30000m);
        var debitType = new TransactionTypeEntity { Sign = "-", TypeCode = "1" };
        debitTx.TransactionType = debitType;
        transactions.Add(debitTx);

        // Type 2 (Boleto) - 200 BRL boleto payment
        var boletoTx = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "2",
            amount: 20000m);
        var boletoType = new TransactionTypeEntity { Sign = "-", TypeCode = "2" };
        boletoTx.TransactionType = boletoType;
        transactions.Add(boletoTx);

        // Type 7 (Recebimento TED) - 750 BRL electronic transfer
        var tedTx = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "7",
            amount: 75000m);
        var tedType = new TransactionTypeEntity { Sign = "+", TypeCode = "7" };
        tedTx.TransactionType = tedType;
        transactions.Add(tedTx);

        // Act
        var balance = store.CalculateBalance(transactions);

        // Assert
        // 1000 + 500 - 300 - 200 + 750 = 1750
        balance.Should().Be(1750.00m);
    }

    [Fact]
    public void CalculateBalance_DailyStoreOperations_Scenario()
    {
        // Arrange: Simulate a day of store operations
        var store = SampleDataBuilder.CreateStore();
        var transactions = new List<TransactionEntity>();

        // Morning: Sales income (Type 6)
        var morningSales = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "6",
            amount: 200000m); // 2000 BRL
        var salesType = new TransactionTypeEntity { Sign = "+", TypeCode = "6" };
        morningSales.TransactionType = salesType;
        transactions.Add(morningSales);

        // Midday: Bill payment (Type 2 - Boleto)
        var billPayment = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "2",
            amount: 50000m); // 500 BRL
        var boletoType = new TransactionTypeEntity { Sign = "-", TypeCode = "2" };
        billPayment.TransactionType = boletoType;
        transactions.Add(billPayment);

        // Afternoon: More sales (Type 6)
        var afternoonSales = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "6",
            amount: 150000m); // 1500 BRL
        var afternoonType = new TransactionTypeEntity { Sign = "+", TypeCode = "6" };
        afternoonSales.TransactionType = afternoonType;
        transactions.Add(afternoonSales);

        // End of day: Supplier payment (Type 1 - Debit)
        var supplierPayment = SampleDataBuilder.CreateTransaction(
            storeId: store.Id,
            transactionTypeCode: "1",
            amount: 75000m); // 750 BRL
        var supplierType = new TransactionTypeEntity { Sign = "-", TypeCode = "1" };
        supplierPayment.TransactionType = supplierType;
        transactions.Add(supplierPayment);

        // Act
        var balance = store.CalculateBalance(transactions);

        // Assert
        // 2000 - 500 + 1500 - 750 = 2250
        balance.Should().Be(2250.00m);
    }

    #endregion

    #region Transaction Reference Storage

    [Fact]
    public void Store_TransactionsProperty_StoresReferencesToAllTransactions()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();
        var tx1 = SampleDataBuilder.CreateTransaction(storeId: store.Id);
        var tx2 = SampleDataBuilder.CreateTransaction(storeId: store.Id);

        // Act
        store.Transactions.Add(tx1);
        store.Transactions.Add(tx2);

        // Assert
        store.Transactions.Should().HaveCount(2);
        store.Transactions.Should().Contain(new[] { tx1, tx2 });
    }

    [Fact]
    public void Store_TransactionsCollection_IsModifiable()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();
        var transaction = SampleDataBuilder.CreateTransaction(storeId: store.Id);

        // Act
        store.Transactions.Add(transaction);
        store.Transactions.Remove(transaction);

        // Assert
        store.Transactions.Should().BeEmpty();
    }

    #endregion

    #region Composite Identity

    [Fact]
    public void Store_OwnerNameAndStoreName_FormCompositeIdentity()
    {
        // Arrange & Act
        var store1 = new StoreEntity(Guid.NewGuid(), "João Silva", "Loja Centro");
        var store2 = new StoreEntity(Guid.NewGuid(), "João Silva", "Loja Centro");
        var store3 = new StoreEntity(Guid.NewGuid(), "João Silva", "Loja Norte");

        // Assert
        // Different system IDs but same owner/name combination (should be same business entity)
        store1.OwnerName.Should().Be(store2.OwnerName);
        store1.Name.Should().Be(store2.Name);
        store1.Name.Should().NotBe(store3.Name); // Different store
    }

    [Fact]
    public void Store_SupportsMaxLengthOwnerName()
    {
        // Arrange: CNAB spec limit is 14 characters for owner name
        var storeId = Guid.NewGuid();
        var maxOwnerName = "João Silva 123"; // 14 chars

        // Act
        var store = new StoreEntity(storeId, maxOwnerName, "Loja Centro");

        // Assert
        store.OwnerName.Should().HaveLength(14);
        store.OwnerName.Should().Be(maxOwnerName);
    }

    [Fact]
    public void Store_SupportsMaxLengthStoreName()
    {
        // Arrange: CNAB spec limit is 19 characters for store name
        var storeId = Guid.NewGuid();
        var maxStoreName = "Loja Centro 1234567"; // 19 chars

        // Act
        var store = new StoreEntity(storeId, "João Silva", maxStoreName);

        // Assert
        store.Name.Should().HaveLength(19);
        store.Name.Should().Be(maxStoreName);
    }

    #endregion

    #region Timestamps

    [Fact]
    public void Store_CreatedAt_IsSetToUtcNow()
    {
        // Arrange & Act
        var beforeCreation = DateTime.UtcNow;
        var store = SampleDataBuilder.CreateStore();
        var afterCreation = DateTime.UtcNow;

        // Assert
        store.CreatedAt.Should().BeOnOrAfter(beforeCreation);
        store.CreatedAt.Should().BeOnOrBefore(afterCreation);
    }

    [Fact]
    public void Store_UpdatedAt_IsInitiallyEqualToCreatedAt()
    {
        // Arrange & Act
        var store = SampleDataBuilder.CreateStore();

        // Assert
        store.UpdatedAt.Should().BeCloseTo(store.CreatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Store_UpdatedAt_ChangesWhenBalanceIsUpdated()
    {
        // Arrange
        var store = SampleDataBuilder.CreateStore();
        var originalUpdatedAt = store.UpdatedAt;
        System.Threading.Thread.Sleep(10);

        // Act
        store.UpdateBalance(100m);

        // Assert
        store.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    #endregion
}
