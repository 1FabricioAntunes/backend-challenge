using System;
using FluentAssertions;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.UnitTests.Helpers;
using Xunit;

namespace TransactionProcessor.Domain.UnitTests.Tests.Transaction;

/// <summary>
/// Unit tests for Transaction entity.
/// Covers constructor validation (type, amount, date),
/// property initialization, and guard clauses.
/// </summary>
public class TransactionTests : TestBase
{
    #region Constructor - Happy Path

    [Fact]
    public void Constructor_WithValidInputs_CreatesTransaction()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var typeCode = "4";
        var amount = 15000m; // 150.00 BRL
        var date = new DateOnly(2024, 1, 15);
        var time = new TimeOnly(12, 30, 0);
        var cpf = "12345678901";
        var card = "123456789012";

        // Act
        var transaction = new Transaction(
            fileId,
            storeId,
            typeCode,
            amount,
            date,
            time,
            cpf,
            card);

        // Assert
        transaction.FileId.Should().Be(fileId);
        transaction.StoreId.Should().Be(storeId);
        transaction.TransactionTypeCode.Should().Be(typeCode);
        transaction.Amount.Should().Be(amount);
        transaction.TransactionDate.Should().Be(date);
        transaction.TransactionTime.Should().Be(time);
        transaction.CPF.Should().Be(cpf);
        transaction.Card.Should().Be(card);
        transaction.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        transaction.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_AllowsNullCpfAndCard_ReplacesWithEmptyStrings()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        // Act
        var transaction = new Transaction(
            fileId,
            storeId,
            transactionTypeCode: "1",
            amount: 1000m,
            transactionDate: new DateOnly(2024, 1, 1),
            transactionTime: new TimeOnly(8, 0, 0),
            cpf: null!,
            card: null!);

        // Assert
        transaction.CPF.Should().BeEmpty();
        transaction.Card.Should().BeEmpty();
    }

    #endregion

    #region Constructor - Validation

    [Theory]
    [InlineData("0")]
    [InlineData("10")]
    [InlineData("abc")]
    public void Constructor_WithInvalidTypeCode_ThrowsArgumentException(string invalidCode)
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        // Act
        Action act = () => new Transaction(
            fileId,
            storeId,
            invalidCode,
            amount: 1000m,
            transactionDate: new DateOnly(2024, 1, 1),
            transactionTime: new TimeOnly(8, 0, 0),
            cpf: "12345678901",
            card: "123456789012");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("transactionTypeCode")
            .WithMessage("*between 1 and 9*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Constructor_WithNonPositiveAmount_ThrowsArgumentException(decimal invalidAmount)
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        // Act
        Action act = () => new Transaction(
            fileId,
            storeId,
            transactionTypeCode: "4",
            amount: invalidAmount,
            transactionDate: new DateOnly(2024, 1, 1),
            transactionTime: new TimeOnly(8, 0, 0),
            cpf: "12345678901",
            card: "123456789012");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("amount")
            .WithMessage("*greater than 0*");
    }

    [Fact]
    public void Constructor_WithFutureDate_ThrowsArgumentException()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        // Act
        Action act = () => new Transaction(
            fileId,
            storeId,
            transactionTypeCode: "4",
            amount: 1000m,
            transactionDate: futureDate,
            transactionTime: new TimeOnly(8, 0, 0),
            cpf: "12345678901",
            card: "123456789012");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("transactionDate")
            .WithMessage("*future*");
    }

    [Fact]
    public void Constructor_WithTodayDate_AllowsCreation()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var transaction = new Transaction(
            Guid.NewGuid(),
            Guid.NewGuid(),
            transactionTypeCode: "2",
            amount: 5000m,
            transactionDate: today,
            transactionTime: new TimeOnly(12, 0, 0),
            cpf: "12345678901",
            card: "123456789012");

        // Assert
        transaction.TransactionDate.Should().Be(today);
    }

    #endregion
}
