using System;
using FluentAssertions;
using TransactionProcessor.Domain.Entities;
using Xunit;

namespace TransactionProcessor.Domain.UnitTests.Tests.Entities;

/// <summary>
/// Unit tests for TransactionType entity (database lookup entity).
/// Tests GetSignMultiplier() method with valid and invalid sign values.
/// </summary>
public class TransactionTypeEntityTests
{
    [Fact]
    public void GetSignMultiplier_WithPlusSign_ReturnsPositiveOne()
    {
        // Arrange
        var transactionType = new TransactionType
        {
            TypeCode = "4",
            Description = "Crédito",
            Nature = "Income",
            Sign = "+"
        };

        // Act
        var result = transactionType.GetSignMultiplier();

        // Assert
        result.Should().Be(1.0m);
    }

    [Fact]
    public void GetSignMultiplier_WithMinusSign_ReturnsNegativeOne()
    {
        // Arrange
        var transactionType = new TransactionType
        {
            TypeCode = "1",
            Description = "Débito",
            Nature = "Expense",
            Sign = "-"
        };

        // Act
        var result = transactionType.GetSignMultiplier();

        // Assert
        result.Should().Be(-1.0m);
    }

    [Fact]
    public void GetSignMultiplier_WithInvalidSign_ThrowsInvalidOperationException()
    {
        // Arrange
        var transactionType = new TransactionType
        {
            TypeCode = "1",
            Description = "Invalid",
            Nature = "Unknown",
            Sign = "X" // Invalid sign
        };

        // Act
        Action act = () => transactionType.GetSignMultiplier();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Invalid sign value: X. Must be '+' or '-'.");
    }

    [Fact]
    public void GetSignMultiplier_WithEmptySign_ThrowsInvalidOperationException()
    {
        // Arrange
        var transactionType = new TransactionType
        {
            TypeCode = "1",
            Description = "Invalid",
            Nature = "Unknown",
            Sign = string.Empty
        };

        // Act
        Action act = () => transactionType.GetSignMultiplier();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Invalid sign value: . Must be '+' or '-'.");
    }

    [Fact]
    public void GetSignMultiplier_WithNullSign_ThrowsInvalidOperationException()
    {
        // Arrange
        var transactionType = new TransactionType
        {
            TypeCode = "1",
            Description = "Invalid",
            Nature = "Unknown",
            Sign = null!
        };

        // Act
        Action act = () => transactionType.GetSignMultiplier();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Invalid sign value: . Must be '+' or '-'.");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("*")]
    [InlineData("++")]
    [InlineData("--")]
    [InlineData(" ")]
    public void GetSignMultiplier_WithVariousInvalidSigns_ThrowsInvalidOperationException(string invalidSign)
    {
        // Arrange
        var transactionType = new TransactionType
        {
            TypeCode = "1",
            Description = "Invalid",
            Nature = "Unknown",
            Sign = invalidSign
        };

        // Act
        Action act = () => transactionType.GetSignMultiplier();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Invalid sign value: {invalidSign}. Must be '+' or '-'.");
    }

    #region IsCredit and IsDebit Tests

    [Fact]
    public void IsCredit_WithPlusSign_ReturnsTrue()
    {
        // Arrange
        var transactionType = new TransactionType
        {
            TypeCode = "4",
            Description = "Crédito",
            Nature = "Income",
            Sign = "+"
        };

        // Act
        var result = transactionType.IsCredit();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsCredit_WithMinusSign_ReturnsFalse()
    {
        // Arrange
        var transactionType = new TransactionType
        {
            TypeCode = "1",
            Description = "Débito",
            Nature = "Expense",
            Sign = "-"
        };

        // Act
        var result = transactionType.IsCredit();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDebit_WithMinusSign_ReturnsTrue()
    {
        // Arrange
        var transactionType = new TransactionType
        {
            TypeCode = "1",
            Description = "Débito",
            Nature = "Expense",
            Sign = "-"
        };

        // Act
        var result = transactionType.IsDebit();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDebit_WithPlusSign_ReturnsFalse()
    {
        // Arrange
        var transactionType = new TransactionType
        {
            TypeCode = "4",
            Description = "Crédito",
            Nature = "Income",
            Sign = "+"
        };

        // Act
        var result = transactionType.IsDebit();

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
