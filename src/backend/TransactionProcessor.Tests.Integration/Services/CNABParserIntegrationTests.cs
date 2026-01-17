using FluentAssertions;
using TransactionProcessor.Application.Models;
using TransactionProcessor.Application.Services;
using Xunit;

namespace TransactionProcessor.Tests.Integration.Services;

/// <summary>
/// Integration tests for CNAB parser service.
/// Tests parsing and validation of CNAB 80-character fixed-width format.
/// </summary>
public class CNABParserIntegrationTests
{
    private readonly ICNABParser _parser = new CNABParser();

    [Fact]
    public async Task ParseAsync_ValidCNABLines_ReturnsValidLines()
    {
        // Arrange
        var cnabContent = @"1202501031000000100011111111111234567890123456123456789012345678901234567890123456789012
2202501041000000100011111111111234567890123456123456789012345678901234567890123456789012";
        using var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(cnabContent));

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ValidLines.Should().HaveCount(2);
        result.Errors.Should().BeEmpty();

        result.ValidLines[0].Type.Should().Be(1);
        result.ValidLines[0].Amount.Should().Be(10000000m);
        
        result.ValidLines[1].Type.Should().Be(2);
        result.ValidLines[1].Amount.Should().Be(10000000m);
    }

    [Fact]
    public async Task ParseAsync_InvalidLineLength_ReturnsError()
    {
        // Arrange
        var cnabContent = "INVALID_SHORT_LINE"; // Not 80 characters
        using var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(cnabContent));

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ValidLines.Should().BeEmpty();
        result.Errors.Should().NotBeEmpty();
        result.Errors[0].Should().Contain("Invalid length");
    }

    [Fact]
    public async Task ParseAsync_InvalidTransactionType_ReturnsError()
    {
        // Arrange
        // Line with invalid type (0)
        var cnabContent = "0" + new string('0', 79); // Type 0 is invalid (must be 1-9)
        using var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(cnabContent));

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors[0].Should().Contain("Invalid transaction type");
    }

    [Fact]
    public async Task ParseAsync_EmptyFile_ReturnsError()
    {
        // Arrange
        using var stream = new MemoryStream(System.Array.Empty<byte>());

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors[0].Should().Contain("empty");
    }

    [Fact]
    public async Task ParseAsync_InvalidDate_ReturnsError()
    {
        // Arrange
        // Invalid date: 20251301 (month 13)
        var line = "1" + "20251301" + "0000000100" + new string('0', 61);
        line = line.PadRight(80);
        using var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(line));

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors[0].Should().Contain("Invalid date");
    }

    [Fact]
    public async Task ParseAsync_CalculatesSignedAmountCorrectly()
    {
        // Arrange
        // Type 1 (Debit/Inflow) - should be positive
        var debLine = "1" + "20250103" + "0000000100" + new string('0', 61);
        debLine = debLine.PadRight(80);
        
        // Type 2 (Outflow/Credit) - should be negative
        var credLine = "2" + "20250103" + "0000000100" + new string('0', 61);
        credLine = credLine.PadRight(80);

        using var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(debLine + "\n" + credLine));

        // Act
        var result = await _parser.ParseAsync(stream);

        // Assert
        result.ValidLines[0].SignedAmount.Should().Be(1000m); // Positive
        result.ValidLines[1].SignedAmount.Should().Be(-1000m); // Negative
    }
}
