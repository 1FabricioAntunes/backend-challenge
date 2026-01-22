using FluentAssertions;
using TransactionProcessor.Domain.ValueObjects;
using Xunit;

namespace TransactionProcessor.Domain.UnitTests.Tests.ValueObjects;

/// <summary>
/// Unit tests for TransactionTypeCode value object.
/// Tests validation of transaction type codes (1-9).
/// </summary>
public class TransactionTypeCodeTests
{
    #region IsValid Tests

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("5")]
    [InlineData("6")]
    [InlineData("7")]
    [InlineData("8")]
    [InlineData("9")]
    public void IsValid_WithValidCode_ReturnsTrue(string typeCode)
    {
        // Act
        var result = TransactionTypeCode.IsValid(typeCode);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("10")]
    [InlineData("A")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void IsValid_WithInvalidCode_ReturnsFalse(string? typeCode)
    {
        // Act
        var result = TransactionTypeCode.IsValid(typeCode!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Constants

    [Fact]
    public void Constants_AllTypeCodes_AreValid()
    {
        // Assert
        TransactionTypeCode.Debit.Should().Be("1");
        TransactionTypeCode.Boleto.Should().Be("2");
        TransactionTypeCode.Financing.Should().Be("3");
        TransactionTypeCode.Credit.Should().Be("4");
        TransactionTypeCode.CompanyReceipt.Should().Be("5");
        TransactionTypeCode.Sales.Should().Be("6");
        TransactionTypeCode.TEDReceipt.Should().Be("7");
        TransactionTypeCode.DOCReceipt.Should().Be("8");
        TransactionTypeCode.Rent.Should().Be("9");
    }

    [Fact]
    public void AllTypeCodes_ContainsAllValidCodes()
    {
        // Act
        var allCodes = TransactionTypeCode.AllTypeCodes;

        // Assert
        allCodes.Should().HaveCount(9);
        allCodes.Should().Contain("1");
        allCodes.Should().Contain("2");
        allCodes.Should().Contain("3");
        allCodes.Should().Contain("4");
        allCodes.Should().Contain("5");
        allCodes.Should().Contain("6");
        allCodes.Should().Contain("7");
        allCodes.Should().Contain("8");
        allCodes.Should().Contain("9");
    }

    [Fact]
    public void AllTypeCodes_EachCodeIsValid()
    {
        // Act
        var allCodes = TransactionTypeCode.AllTypeCodes;

        // Assert
        foreach (var code in allCodes)
        {
            TransactionTypeCode.IsValid(code).Should().BeTrue();
        }
    }

    #endregion
}
