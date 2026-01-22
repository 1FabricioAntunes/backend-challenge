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
        result.Should().MatchRegex(@"100[.,]50"); // Handle both comma and period for locale
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

    #region Null Currency Handling

    [Fact]
    public void Constructor_WithNullCurrency_DefaultsToBRL()
    {
        // Arrange
        var amount = 100.00m;

        // Act
        var money = new MoneyAmount(amount, null!);

        // Assert
        money.Currency.Should().Be("BRL");
        money.Amount.Should().Be(amount);
    }

    [Fact]
    public void Constructor_WithEmptyCurrency_AllowsEmptyString()
    {
        // Arrange
        var amount = 100.00m;

        // Act
        var money = new MoneyAmount(amount, string.Empty);

        // Assert
        money.Currency.Should().Be(string.Empty);
        money.Amount.Should().Be(amount);
    }

    #endregion

    #region Boundary Values

    [Fact]
    public void Constructor_WithZeroAmount_Allowed()
    {
        // Act
        var money = new MoneyAmount(0m);

        // Assert
        money.Amount.Should().Be(0m);
        money.Currency.Should().Be("BRL");
    }

    [Fact]
    public void Constructor_WithVeryLargeAmount_HandlesCorrectly()
    {
        // Arrange
        var largeAmount = 999999999.99m;

        // Act
        var money = new MoneyAmount(largeAmount);

        // Assert
        money.Amount.Should().Be(largeAmount);
    }

    [Fact]
    public void Constructor_WithMaximumDecimalPrecision_PreservesPrecision()
    {
        // Arrange
        var preciseAmount = 0.00000001m;

        // Act
        var money = new MoneyAmount(preciseAmount);

        // Assert
        money.Amount.Should().Be(preciseAmount);
    }

    #endregion

    #region Equals(object) Override

    [Fact]
    public void Equals_ObjectOverride_WithMoneyAmount_ReturnsTrue()
    {
        // Arrange
        var money1 = new MoneyAmount(100.00m, "BRL");
        var money2 = new MoneyAmount(100.00m, "BRL");
        object obj = money2;

        // Act
        var result = money1.Equals(obj);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_ObjectOverride_WithDifferentMoneyAmount_ReturnsFalse()
    {
        // Arrange
        var money1 = new MoneyAmount(100.00m, "BRL");
        var money2 = new MoneyAmount(200.00m, "BRL");
        object obj = money2;

        // Act
        var result = money1.Equals(obj);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_ObjectOverride_WithNull_ReturnsFalse()
    {
        // Arrange
        var money = new MoneyAmount(100.00m, "BRL");
        object? obj = null;

        // Act
        var result = money.Equals(obj);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_ObjectOverride_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        var money = new MoneyAmount(100.00m, "BRL");
        object obj = "not a MoneyAmount";

        // Act
        var result = money.Equals(obj);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Operator Overloads - Complete Coverage

    [Fact]
    public void EqualityOperator_WithNullLeft_ReturnsTrueWhenRightIsNull()
    {
        // Arrange
        MoneyAmount? left = null;
        MoneyAmount? right = null;

        // Act
        var result = left == right;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EqualityOperator_WithNullLeft_ReturnsFalseWhenRightIsNotNull()
    {
        // Arrange
        MoneyAmount? left = null;
        var right = new MoneyAmount(100.00m, "BRL");

        // Act
        var result = left == right;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EqualityOperator_WithNullRight_ReturnsFalseWhenLeftIsNotNull()
    {
        // Arrange
        var left = new MoneyAmount(100.00m, "BRL");
        MoneyAmount? right = null;

        // Act
        var result = left == right;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void InequalityOperator_WithNullLeft_ReturnsFalseWhenRightIsNull()
    {
        // Arrange
        MoneyAmount? left = null;
        MoneyAmount? right = null;

        // Act
        var result = left != right;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void InequalityOperator_WithNullLeft_ReturnsTrueWhenRightIsNotNull()
    {
        // Arrange
        MoneyAmount? left = null;
        var right = new MoneyAmount(100.00m, "BRL");

        // Act
        var result = left != right;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_WithNullRight_ReturnsTrueWhenLeftIsNotNull()
    {
        // Arrange
        var left = new MoneyAmount(100.00m, "BRL");
        MoneyAmount? right = null;

        // Act
        var result = left != right;

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Implicit Operators

    [Fact]
    public void ImplicitOperator_DecimalToMoneyAmount_CreatesMoneyAmountWithBRL()
    {
        // Arrange
        decimal amount = 100.50m;

        // Act
        MoneyAmount money = amount;

        // Assert
        money.Amount.Should().Be(amount);
        money.Currency.Should().Be("BRL");
    }

    [Fact]
    public void ImplicitOperator_MoneyAmountToDecimal_ReturnsAmount()
    {
        // Arrange
        var money = new MoneyAmount(100.50m, "BRL");

        // Act
        decimal amount = money;

        // Assert
        amount.Should().Be(100.50m);
    }

    [Fact]
    public void ImplicitOperator_NullMoneyAmountToDecimal_ReturnsZero()
    {
        // Arrange
        MoneyAmount? money = null;

        // Act
        decimal amount = money!;

        // Assert
        amount.Should().Be(0m);
    }

    #endregion
}
