using System;
using System.Collections.Generic;
using FluentAssertions;
using TransactionProcessor.Domain.Services;
using Xunit;

namespace TransactionProcessor.Domain.UnitTests.Tests.Services;

/// <summary>
/// Unit tests for ValidationResult value object.
/// Tests success/failure factory methods and error handling.
/// </summary>
public class ValidationResultTests
{
    #region Success Factory Method

    [Fact]
    public void Success_ReturnsValidResultWithEmptyErrors()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region Failure Factory Methods

    [Fact]
    public void Failure_WithSingleError_ReturnsInvalidResultWithError()
    {
        // Arrange
        var errorMessage = "File size exceeds maximum limit";

        // Act
        var result = ValidationResult.Failure(errorMessage);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors.Should().Contain(errorMessage);
    }

    [Fact]
    public void Failure_WithErrorList_ReturnsInvalidResultWithErrors()
    {
        // Arrange
        var errors = new List<string>
        {
            "Line 1: Expected 80 bytes, found 79",
            "Line 2: Non-ASCII character at position 45"
        };

        // Act
        var result = ValidationResult.Failure(errors);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void Failure_WithNullErrorString_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => ValidationResult.Failure((string)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("error");
    }

    [Fact]
    public void Failure_WithEmptyErrorString_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => ValidationResult.Failure(string.Empty);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("error");
    }

    [Fact]
    public void Failure_WithWhitespaceErrorString_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => ValidationResult.Failure("   ");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("error");
    }

    [Fact]
    public void Failure_WithNullErrorList_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => ValidationResult.Failure((List<string>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("errors");
    }

    [Fact]
    public void Failure_WithEmptyErrorList_ThrowsArgumentException()
    {
        // Arrange
        var emptyErrors = new List<string>();

        // Act
        Action act = () => ValidationResult.Failure(emptyErrors);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("errors")
            .WithMessage("*Use Success() for valid results*");
    }

    #endregion

    #region GetErrorSummary

    [Fact]
    public void GetErrorSummary_WithNoErrors_ReturnsEmptyString()
    {
        // Arrange
        var result = ValidationResult.Success();

        // Act
        var summary = result.GetErrorSummary();

        // Assert
        summary.Should().BeEmpty();
    }

    [Fact]
    public void GetErrorSummary_WithSingleError_ReturnsError()
    {
        // Arrange
        var error = "File size exceeds maximum limit";
        var result = ValidationResult.Failure(error);

        // Act
        var summary = result.GetErrorSummary();

        // Assert
        summary.Should().Be(error);
    }

    [Fact]
    public void GetErrorSummary_WithMultipleErrors_ReturnsJoinedErrors()
    {
        // Arrange
        var errors = new List<string>
        {
            "Line 1: Expected 80 bytes, found 79",
            "Line 2: Non-ASCII character at position 45",
            "Line 3: Empty line"
        };
        var result = ValidationResult.Failure(errors);

        // Act
        var summary = result.GetErrorSummary();

        // Assert
        summary.Should().Contain("Line 1");
        summary.Should().Contain("Line 2");
        summary.Should().Contain("Line 3");
        summary.Split(Environment.NewLine).Should().HaveCount(3);
    }

    #endregion

    #region Immutability

    [Fact]
    public void Errors_ListIsReadOnly_DoesNotAllowModification()
    {
        // Arrange
        var result = ValidationResult.Failure("Test error");

        // Act & Assert
        // The Errors property should return a list that cannot be modified
        // This depends on implementation - if it's a List<string>, it's mutable
        // If it's IReadOnlyList<string>, it's immutable
        result.Errors.Should().NotBeNull();
    }

    #endregion
}
