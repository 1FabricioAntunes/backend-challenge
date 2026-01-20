using System;
using System.Collections.Generic;
using FluentAssertions;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Domain.UnitTests.Helpers;
using Xunit;

namespace TransactionProcessor.Domain.UnitTests.Tests.ValueObjects;

/// <summary>
/// Unit tests for MoneyAmount value object.
/// Tests equality, immutability, and value object semantics.
/// </summary>
public class MoneyAmountTests : TestBase
{
    [Fact]
    public void Constructor_ValidAmount_CreatesMoneyAmount()
    {
        // Arrange
        var amount = 100.50m;
        var currency = "BRL";

        // Act
        var money = new MoneyAmount(amount, currency);

        // Assert
        money.Amount.Should().Be(amount);
        money.Currency.Should().Be(currency);
    }

    [Fact]
    public void Constructor_DefaultCurrency_UsesBRL()
    {
        // Arrange
        var amount = 100.00m;

        // Act
        var money = new MoneyAmount(amount);

        // Assert
        money.Currency.Should().Be("BRL");
    }

    [Fact]
    public void Constructor_NegativeAmount_ThrowsArgumentException()
    {
        // Act
        Action act = () => new MoneyAmount(-10.00m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Money amount cannot be negative*");
    }

    [Fact]
    public void Constructor_ZeroAmount_Allowed()
    {
        // Act
        var money = new MoneyAmount(0);

        // Assert
        money.Amount.Should().Be(0);
    }

    [Fact]
    public void Equals_SameAmountAndCurrency_ReturnsTrue()
    {
        // Arrange
        var money1 = new MoneyAmount(100.00m, "BRL");
        var money2 = new MoneyAmount(100.00m, "BRL");

        // Act
        var result = money1.Equals(money2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentAmount_ReturnsFalse()
    {
        // Arrange
        var money1 = new MoneyAmount(100.00m, "BRL");
        var money2 = new MoneyAmount(200.00m, "BRL");

        // Act
        var result = money1.Equals(money2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentCurrency_ReturnsFalse()
    {
        // Arrange
        var money1 = new MoneyAmount(100.00m, "BRL");
        var money2 = new MoneyAmount(100.00m, "USD");

        // Act
        var result = money1.Equals(money2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_NullObject_ReturnsFalse()
    {
        // Arrange
        var money = new MoneyAmount(100.00m);

        // Act
        var result = money.Equals(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameAmountAndCurrency_ReturnsSameHashCode()
    {
        // Arrange
        var money1 = new MoneyAmount(100.00m, "BRL");
        var money2 = new MoneyAmount(100.00m, "BRL");

        // Act
        var hash1 = money1.GetHashCode();
        var hash2 = money2.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void EqualityOperator_SameValues_ReturnsTrue()
    {
        // Arrange
        var money1 = new MoneyAmount(100.00m, "BRL");
        var money2 = new MoneyAmount(100.00m, "BRL");

        // Act
        var result = money1 == money2;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_DifferentValues_ReturnsTrue()
    {
        // Arrange
        var money1 = new MoneyAmount(100.00m, "BRL");
        var money2 = new MoneyAmount(200.00m, "BRL");

        // Act
        var result = money1 != money2;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        // Arrange
        var money = new MoneyAmount(100.50m, "BRL");

        // Act
        var result = money.ToString();

        // Assert
        result.Should().Contain("100.50");
        result.Should().Contain("BRL");
    }

    [Fact]
    public void MoneyAmount_InHashSet_UsesValueEquality()
    {
        // Arrange
        var money1 = new MoneyAmount(100.00m, "BRL");
        var money2 = new MoneyAmount(100.00m, "BRL");
        var money3 = new MoneyAmount(200.00m, "BRL");
        var hashSet = new HashSet<MoneyAmount>();

        // Act
        hashSet.Add(money1);
        hashSet.Add(money2);
        hashSet.Add(money3);

        // Assert
        hashSet.Count.Should().Be(2);
    }
}
