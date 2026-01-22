using FluentAssertions;
using TransactionProcessor.Domain.ValueObjects;
using Xunit;

namespace TransactionProcessor.Domain.UnitTests.Tests.ValueObjects;

/// <summary>
/// Unit tests for FileStatusCode value object.
/// Tests status validation and terminal state checking.
/// </summary>
public class FileStatusCodeTests
{
    #region IsValid Tests

    [Theory]
    [InlineData(FileStatusCode.Uploaded)]
    [InlineData(FileStatusCode.Processing)]
    [InlineData(FileStatusCode.Processed)]
    [InlineData(FileStatusCode.Rejected)]
    public void IsValid_WithValidStatusCode_ReturnsTrue(string statusCode)
    {
        // Act
        var result = FileStatusCode.IsValid(statusCode);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Invalid")]
    [InlineData("uploaded")] // Case sensitive
    [InlineData("UPLOADED")] // Case sensitive
    [InlineData(null)]
    public void IsValid_WithInvalidStatusCode_ReturnsFalse(string? statusCode)
    {
        // Act
        var result = FileStatusCode.IsValid(statusCode!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsTerminal Tests

    [Theory]
    [InlineData(FileStatusCode.Processed)]
    [InlineData(FileStatusCode.Rejected)]
    public void IsTerminal_WithTerminalStatus_ReturnsTrue(string statusCode)
    {
        // Act
        var result = FileStatusCode.IsTerminal(statusCode);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(FileStatusCode.Uploaded)]
    [InlineData(FileStatusCode.Processing)]
    public void IsTerminal_WithNonTerminalStatus_ReturnsFalse(string statusCode)
    {
        // Act
        var result = FileStatusCode.IsTerminal(statusCode);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Constants

    [Fact]
    public void Constants_AllStatusCodes_AreValid()
    {
        // Assert
        FileStatusCode.Uploaded.Should().Be("Uploaded");
        FileStatusCode.Processing.Should().Be("Processing");
        FileStatusCode.Processed.Should().Be("Processed");
        FileStatusCode.Rejected.Should().Be("Rejected");
    }

    [Fact]
    public void AllStatuses_ContainsAllValidStatuses()
    {
        // Act
        var allStatuses = FileStatusCode.AllStatuses;

        // Assert
        allStatuses.Should().HaveCount(4);
        allStatuses.Should().Contain(FileStatusCode.Uploaded);
        allStatuses.Should().Contain(FileStatusCode.Processing);
        allStatuses.Should().Contain(FileStatusCode.Processed);
        allStatuses.Should().Contain(FileStatusCode.Rejected);
    }

    [Fact]
    public void AllStatuses_EachStatusIsValid()
    {
        // Act
        var allStatuses = FileStatusCode.AllStatuses;

        // Assert
        foreach (var status in allStatuses)
        {
            FileStatusCode.IsValid(status).Should().BeTrue();
        }
    }

    #endregion
}
