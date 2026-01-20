using System;
using System.Linq;
using FluentAssertions;
using TransactionProcessor.Domain.ValueObjects;
using Xunit;

namespace TransactionProcessor.Domain.UnitTests.Tests.ValueObjects;

/// <summary>
/// Unit tests for TransactionType value object conversions and equality.
/// </summary>
public class TransactionTypeValueObjectTests
{
    [Fact]
    public void FromCode_WithValidCode_ReturnsStaticInstance()
    {
        // Act
        var type = TransactionType.FromCode(4);

        // Assert
        type.Should().BeSameAs(TransactionType.Type4);
        type.Code.Should().Be(4);
        type.Name.Should().Contain("Cr");
        type.IsCredit.Should().BeTrue();
        type.Sign.Should().Be(1);
    }

    [Theory]
    [InlineData(1, false, -1)]
    [InlineData(2, false, -1)]
    [InlineData(3, false, -1)]
    [InlineData(4, true, 1)]
    [InlineData(5, true, 1)]
    [InlineData(6, true, 1)]
    [InlineData(7, true, 1)]
    [InlineData(8, true, 1)]
    [InlineData(9, true, 1)]
    public void FromCode_ReturnsCorrectCreditFlagAndSign(int code, bool expectedCredit, int expectedSign)
    {
        // Act
        var type = TransactionType.FromCode(code);

        // Assert
        type.IsCredit.Should().Be(expectedCredit);
        type.Sign.Should().Be(expectedSign);
    }

    [Fact]
    public void FromCode_WithInvalidCode_ThrowsArgumentException()
    {
        // Act
        Action act = () => TransactionType.FromCode(0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("code")
            .WithMessage("*Invalid transaction type code*");
    }

    [Fact]
    public void GetAllTypes_ReturnsNineDistinctInstances()
    {
        // Act
        var all = TransactionType.GetAllTypes();

        // Assert
        all.Should().HaveCount(9);
        all.Distinct().Count().Should().Be(9);
        all.Should().Contain(TransactionType.Type1);
        all.Should().Contain(TransactionType.Type9);
    }

    [Fact]
    public void Equality_ByCode_ShouldMatch()
    {
        // Arrange
        var typeA = TransactionType.FromCode(1);
        var typeB = TransactionType.FromCode(1);
        var typeC = TransactionType.FromCode(2);

        // Assert
        (typeA == typeB).Should().BeTrue();
        (typeA != typeC).Should().BeTrue();
        typeA.Equals(typeB).Should().BeTrue();
        typeA.Equals(typeC).Should().BeFalse();
    }

    [Fact]
    public void ToString_FormatsWithCodeAndName()
    {
        // Act
        var text = TransactionType.Type4.ToString();

        // Assert
        text.Should().Contain("4");
        text.Should().Contain("Cr");
    }
}
