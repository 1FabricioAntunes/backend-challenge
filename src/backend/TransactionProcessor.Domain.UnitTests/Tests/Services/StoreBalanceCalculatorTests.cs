using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.Services;
using TransactionProcessor.Domain.UnitTests.Helpers;
using Xunit;
using TransactionEntity = TransactionProcessor.Domain.Entities.Transaction;
using TransactionTypeEntity = TransactionProcessor.Domain.Entities.TransactionType;

namespace TransactionProcessor.Domain.UnitTests.Tests.Services;

/// <summary>
/// Unit tests for StoreBalanceCalculator domain service.
/// Tests balance calculation from transaction lists with various scenarios.
/// </summary>
public class StoreBalanceCalculatorTests : TestBase
{
    private readonly Mock<ISignedAmountCalculator> _mockSignedAmountCalculator;
    private readonly StoreBalanceCalculator _calculator;

    public StoreBalanceCalculatorTests()
    {
        _mockSignedAmountCalculator = new Mock<ISignedAmountCalculator>();
        _calculator = new StoreBalanceCalculator(_mockSignedAmountCalculator.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullSignedAmountCalculator_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new StoreBalanceCalculator(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("signedAmountCalculator");
    }

    [Fact]
    public void Constructor_WithValidSignedAmountCalculator_CreatesInstance()
    {
        // Arrange
        var mockCalculator = new Mock<ISignedAmountCalculator>();

        // Act
        var calculator = new StoreBalanceCalculator(mockCalculator.Object);

        // Assert
        calculator.Should().NotBeNull();
    }

    #endregion

    #region Empty Transaction List

    [Fact]
    public void CalculateBalance_WithEmptyList_ReturnsZero()
    {
        // Arrange
        var transactions = new List<TransactionEntity>();

        // Act
        var result = _calculator.CalculateBalance(transactions);

        // Assert
        result.Should().Be(0m);
        _mockSignedAmountCalculator.VerifyNoOtherCalls();
    }

    [Fact]
    public void CalculateBalance_WithNullList_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _calculator.CalculateBalance(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("transactions");
    }

    #endregion

    #region Single Transaction

    [Fact]
    public void CalculateBalance_WithSingleCreditTransaction_ReturnsPositiveAmount()
    {
        // Arrange
        var transaction = SampleDataBuilder.CreateTransaction(
            storeId: Guid.NewGuid(),
            transactionTypeCode: "4",
            amount: 10000m); // 100.00 BRL

        _mockSignedAmountCalculator
            .Setup(x => x.Calculate(It.IsAny<TransactionEntity>()))
            .Returns(100.00m);

        var transactions = new List<TransactionEntity> { transaction };

        // Act
        var result = _calculator.CalculateBalance(transactions);

        // Assert
        result.Should().Be(100.00m);
        _mockSignedAmountCalculator.Verify(x => x.Calculate(transaction), Times.Once);
    }

    [Fact]
    public void CalculateBalance_WithSingleDebitTransaction_ReturnsNegativeAmount()
    {
        // Arrange
        var transaction = SampleDataBuilder.CreateTransaction(
            storeId: Guid.NewGuid(),
            transactionTypeCode: "1",
            amount: 5000m); // 50.00 BRL

        _mockSignedAmountCalculator
            .Setup(x => x.Calculate(It.IsAny<TransactionEntity>()))
            .Returns(-50.00m);

        var transactions = new List<TransactionEntity> { transaction };

        // Act
        var result = _calculator.CalculateBalance(transactions);

        // Assert
        result.Should().Be(-50.00m);
        _mockSignedAmountCalculator.Verify(x => x.Calculate(transaction), Times.Once);
    }

    #endregion

    #region Multiple Transactions

    [Fact]
    public void CalculateBalance_WithMultipleCreditTransactions_ReturnsSum()
    {
        // Arrange
        var transaction1 = SampleDataBuilder.CreateTransaction(
            storeId: Guid.NewGuid(),
            transactionTypeCode: "4",
            amount: 10000m);
        var transaction2 = SampleDataBuilder.CreateTransaction(
            storeId: Guid.NewGuid(),
            transactionTypeCode: "6",
            amount: 20000m);

        _mockSignedAmountCalculator
            .Setup(x => x.Calculate(transaction1))
            .Returns(100.00m);
        _mockSignedAmountCalculator
            .Setup(x => x.Calculate(transaction2))
            .Returns(200.00m);

        var transactions = new List<TransactionEntity> { transaction1, transaction2 };

        // Act
        var result = _calculator.CalculateBalance(transactions);

        // Assert
        result.Should().Be(300.00m);
        _mockSignedAmountCalculator.Verify(x => x.Calculate(transaction1), Times.Once);
        _mockSignedAmountCalculator.Verify(x => x.Calculate(transaction2), Times.Once);
    }

    [Fact]
    public void CalculateBalance_WithMixedCreditAndDebitTransactions_ReturnsNetBalance()
    {
        // Arrange
        var creditTx = SampleDataBuilder.CreateTransaction(
            storeId: Guid.NewGuid(),
            transactionTypeCode: "4",
            amount: 10000m);
        var debitTx = SampleDataBuilder.CreateTransaction(
            storeId: Guid.NewGuid(),
            transactionTypeCode: "1",
            amount: 5000m);

        _mockSignedAmountCalculator
            .Setup(x => x.Calculate(creditTx))
            .Returns(100.00m);
        _mockSignedAmountCalculator
            .Setup(x => x.Calculate(debitTx))
            .Returns(-50.00m);

        var transactions = new List<TransactionEntity> { creditTx, debitTx };

        // Act
        var result = _calculator.CalculateBalance(transactions);

        // Assert
        result.Should().Be(50.00m);
    }

    [Fact]
    public void CalculateBalance_WithBalancingTransactions_ReturnsZero()
    {
        // Arrange
        var creditTx = SampleDataBuilder.CreateTransaction(
            storeId: Guid.NewGuid(),
            transactionTypeCode: "4",
            amount: 10000m);
        var debitTx = SampleDataBuilder.CreateTransaction(
            storeId: Guid.NewGuid(),
            transactionTypeCode: "1",
            amount: 10000m);

        _mockSignedAmountCalculator
            .Setup(x => x.Calculate(creditTx))
            .Returns(100.00m);
        _mockSignedAmountCalculator
            .Setup(x => x.Calculate(debitTx))
            .Returns(-100.00m);

        var transactions = new List<TransactionEntity> { creditTx, debitTx };

        // Act
        var result = _calculator.CalculateBalance(transactions);

        // Assert
        result.Should().Be(0m);
    }

    #endregion

    #region Null Transaction Handling

    [Fact]
    public void CalculateBalance_WithNullTransactionInList_ThrowsArgumentNullException()
    {
        // Arrange
        var validTransaction = SampleDataBuilder.CreateTransaction(
            storeId: Guid.NewGuid(),
            transactionTypeCode: "4",
            amount: 10000m);
        var transactions = new List<TransactionEntity> { validTransaction, null! };

        // Act
        Action act = () => _calculator.CalculateBalance(transactions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("transactions")
            .WithMessage("*index 1*");
    }

    [Fact]
    public void CalculateBalance_WithNullTransactionAtFirstIndex_ThrowsArgumentNullException()
    {
        // Arrange
        var transactions = new List<TransactionEntity> { null! };

        // Act
        Action act = () => _calculator.CalculateBalance(transactions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("transactions")
            .WithMessage("*index 0*");
    }

    #endregion

    #region Error Handling

    [Fact]
    public void CalculateBalance_WhenSignedAmountCalculatorThrowsArgumentNullException_WrapsInInvalidOperationException()
    {
        // Arrange
        var transaction = SampleDataBuilder.CreateTransaction(
            storeId: Guid.NewGuid(),
            transactionTypeCode: "4",
            amount: 10000m);

        _mockSignedAmountCalculator
            .Setup(x => x.Calculate(It.IsAny<TransactionEntity>()))
            .Throws(new ArgumentNullException("transaction"));

        var transactions = new List<TransactionEntity> { transaction };

        // Act
        Action act = () => _calculator.CalculateBalance(transactions);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing TransactionType*")
            .WithInnerException<ArgumentNullException>();
    }

    [Fact]
    public void CalculateBalance_WhenSignedAmountCalculatorThrowsInvalidOperationException_WrapsInInvalidOperationException()
    {
        // Arrange
        var transaction = SampleDataBuilder.CreateTransaction(
            storeId: Guid.NewGuid(),
            transactionTypeCode: "4",
            amount: 10000m);

        _mockSignedAmountCalculator
            .Setup(x => x.Calculate(It.IsAny<TransactionEntity>()))
            .Throws(new InvalidOperationException("Invalid sign"));

        var transactions = new List<TransactionEntity> { transaction };

        // Act
        Action act = () => _calculator.CalculateBalance(transactions);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*invalid TransactionType.Sign*")
            .WithInnerException<InvalidOperationException>();
    }

    #endregion

    #region Real-World Scenarios

    [Fact]
    public void CalculateBalance_WithRealWorldScenario_CalculatesCorrectly()
    {
        // Arrange: Multiple transactions as in documentation example
        var tx1 = SampleDataBuilder.CreateTransaction(storeId: Guid.NewGuid(), transactionTypeCode: "6", amount: 50000m);
        var tx2 = SampleDataBuilder.CreateTransaction(storeId: Guid.NewGuid(), transactionTypeCode: "1", amount: 15000m);
        var tx3 = SampleDataBuilder.CreateTransaction(storeId: Guid.NewGuid(), transactionTypeCode: "4", amount: 30000m);
        var tx4 = SampleDataBuilder.CreateTransaction(storeId: Guid.NewGuid(), transactionTypeCode: "2", amount: 10000m);
        var tx5 = SampleDataBuilder.CreateTransaction(storeId: Guid.NewGuid(), transactionTypeCode: "7", amount: 75000m);
        var tx6 = SampleDataBuilder.CreateTransaction(storeId: Guid.NewGuid(), transactionTypeCode: "3", amount: 25000m);
        var tx7 = SampleDataBuilder.CreateTransaction(storeId: Guid.NewGuid(), transactionTypeCode: "6", amount: 40000m);
        var tx8 = SampleDataBuilder.CreateTransaction(storeId: Guid.NewGuid(), transactionTypeCode: "9", amount: 20000m);

        _mockSignedAmountCalculator.Setup(x => x.Calculate(tx1)).Returns(500.00m);
        _mockSignedAmountCalculator.Setup(x => x.Calculate(tx2)).Returns(-150.00m);
        _mockSignedAmountCalculator.Setup(x => x.Calculate(tx3)).Returns(300.00m);
        _mockSignedAmountCalculator.Setup(x => x.Calculate(tx4)).Returns(-100.00m);
        _mockSignedAmountCalculator.Setup(x => x.Calculate(tx5)).Returns(750.00m);
        _mockSignedAmountCalculator.Setup(x => x.Calculate(tx6)).Returns(-250.00m);
        _mockSignedAmountCalculator.Setup(x => x.Calculate(tx7)).Returns(400.00m);
        _mockSignedAmountCalculator.Setup(x => x.Calculate(tx8)).Returns(-200.00m);

        var transactions = new List<TransactionEntity> { tx1, tx2, tx3, tx4, tx5, tx6, tx7, tx8 };

        // Act
        var result = _calculator.CalculateBalance(transactions);

        // Assert
        // Expected: 500 - 150 + 300 - 100 + 750 - 250 + 400 - 200 = 1250.00
        result.Should().Be(1250.00m);
    }

    [Fact]
    public void CalculateBalance_WithNegativeBalanceScenario_ReturnsNegative()
    {
        // Arrange
        var tx1 = SampleDataBuilder.CreateTransaction(storeId: Guid.NewGuid(), transactionTypeCode: "6", amount: 20000m);
        var tx2 = SampleDataBuilder.CreateTransaction(storeId: Guid.NewGuid(), transactionTypeCode: "2", amount: 35000m);
        var tx3 = SampleDataBuilder.CreateTransaction(storeId: Guid.NewGuid(), transactionTypeCode: "3", amount: 15000m);

        _mockSignedAmountCalculator.Setup(x => x.Calculate(tx1)).Returns(200.00m);
        _mockSignedAmountCalculator.Setup(x => x.Calculate(tx2)).Returns(-350.00m);
        _mockSignedAmountCalculator.Setup(x => x.Calculate(tx3)).Returns(-150.00m);

        var transactions = new List<TransactionEntity> { tx1, tx2, tx3 };

        // Act
        var result = _calculator.CalculateBalance(transactions);

        // Assert
        // Expected: 200 - 350 - 150 = -300.00
        result.Should().Be(-300.00m);
    }

    #endregion

    #region Precision Tests

    [Fact]
    public void CalculateBalance_WithOddCentValues_PreservesPrecision()
    {
        // Arrange
        var tx1 = SampleDataBuilder.CreateTransaction(storeId: Guid.NewGuid(), transactionTypeCode: "4", amount: 10033m); // 100.33 BRL
        var tx2 = SampleDataBuilder.CreateTransaction(storeId: Guid.NewGuid(), transactionTypeCode: "1", amount: 5001m);  // 50.01 BRL

        _mockSignedAmountCalculator.Setup(x => x.Calculate(tx1)).Returns(100.33m);
        _mockSignedAmountCalculator.Setup(x => x.Calculate(tx2)).Returns(-50.01m);

        var transactions = new List<TransactionEntity> { tx1, tx2 };

        // Act
        var result = _calculator.CalculateBalance(transactions);

        // Assert
        result.Should().Be(50.32m);
    }

    #endregion
}
