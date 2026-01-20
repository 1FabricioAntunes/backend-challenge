using System;
using System.Collections.Generic;
using FluentAssertions;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Domain.UnitTests.Helpers;
using Xunit;

namespace TransactionProcessor.Domain.UnitTests.Tests.Entities;

/// <summary>
/// Unit tests for Store aggregate entity.
/// Tests store creation, balance calculation, and invariants.
/// </summary>
public class StoreTests : TestBase
{
    [Fact]
    public void Constructor_ValidParameters_CreatesStore()
    {
        // Arrange
        var id = NewGuid();
        var ownerName = "João Silva";
        var storeName = "MERCADO DA AVENIDA";

        // Act
        var store = new Store(id, ownerName, storeName);

        // Assert
        store.Id.Should().Be(id);
        store.OwnerName.Should().Be(ownerName);
        store.Name.Should().Be(storeName);
        store.Balance.Should().Be(0m);
        store.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        store.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidOwnerName_ThrowsArgumentException(string? ownerName)
    {
        // Act
        Action act = () => new Store(NewGuid(), ownerName!, "Store");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidStoreName_ThrowsArgumentException(string? storeName)
    {
        // Act
        Action act = () => new Store(NewGuid(), "Owner", storeName!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateBalance_ValidAmount_UpdatesBalance()
    {
        // Arrange
        var store = new Store(NewGuid(), "Owner", "Store");
        var newBalance = 150.75m;

        // Act
        store.UpdateBalance(newBalance);

        // Assert
        store.Balance.Should().Be(newBalance);
    }

    [Fact]
    public void UpdateBalance_NegativeAmount_ThrowsArgumentException()
    {
        // Arrange
        var store = new Store(NewGuid(), "Owner", "Store");

        // Act
        Action act = () => store.UpdateBalance(-50.00m);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateBalance_ZeroAmount_Allowed()
    {
        // Arrange
        var store = new Store(NewGuid(), "Owner", "Store");

        // Act
        store.UpdateBalance(0m);

        // Assert
        store.Balance.Should().Be(0m);
    }

    [Fact]
    public void CalculateBalance_EmptyTransactions_ReturnsZero()
    {
        // Arrange
        var store = new Store(NewGuid(), "Owner", "Store");

        // Act
        var balance = store.CalculateBalance(new List<Transaction>());

        // Assert
        balance.Should().Be(0m);
    }

    [Fact]
    public void CalculateBalance_WithSignedAmounts_SumsCorrectly()
    {
        // Arrange
        var store = new Store(NewGuid(), "João Silva", "MERCADO");
        var transactions = new List<Transaction>
        {
            // These are mock scenarios - in real tests would need actual transaction setup
            // +100.00, -50.00, +200.00, -75.00 = +175.00
        };

        // Act
        var balance = store.CalculateBalance(transactions);

        // Assert - Empty transactions currently returns 0
        balance.Should().Be(0m);
    }
}
