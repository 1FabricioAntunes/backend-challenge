using System.Text;
using TransactionProcessor.Application.Models;

namespace TransactionProcessor.Application.Services;

/// <summary>
/// Service for parsing CNAB fixed-width format files (80 characters per line).
/// Implements CNAB 240 specification handling.
/// Reference: docs/business-rules.md - CNAB File Format section.
/// </summary>
public interface ICNABParser
{
    /// <summary>
    /// Parses CNAB file content from a stream.
    /// </summary>
    /// <param name="fileStream">Stream containing CNAB file content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with parsed lines or errors</returns>
    Task<CNABValidationResult> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of CNAB parser for fixed-width format (80-character lines).
/// </summary>
public class CNABParser : ICNABParser
{
    /// <summary>
    /// Expected line length in CNAB format (80 characters).
    /// </summary>
    private const int ExpectedLineLength = 80;

    /// <summary>
    /// Parses CNAB file content from stream.
    /// Validates each line structure and extracts transaction data.
    /// </summary>
    /// <param name="fileStream">Stream containing CNAB file content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with all parsed lines or detailed errors</returns>
    public async Task<CNABValidationResult> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        var result = new CNABValidationResult { IsValid = true };
        var lines = new List<string>();

        try
        {
            using (var reader = new StreamReader(fileStream, Encoding.ASCII))
            {
                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    lines.Add(line);
                }
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Failed to read file: {ex.Message}");
            return result;
        }

        if (lines.Count == 0)
        {
            result.IsValid = false;
            result.Errors.Add("File is empty. At least one transaction line is required.");
            return result;
        }

        // Parse each line
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            var parseResult = ParseLine(line, lineNumber);
            if (!parseResult.IsValid)
            {
                result.IsValid = false;
                result.Errors.AddRange(parseResult.Errors);
            }
            else if (parseResult.Data != null)
            {
                result.ValidLines.Add(parseResult.Data);
            }
        }

        return result;
    }

    /// <summary>
    /// Parses a single CNAB line (80-character fixed-width format).
    /// Focuses on PARSING/EXTRACTING fields only, not validating them.
    /// Validation is delegated to CNABValidator.
    /// </summary>
    /// <param name="line">The line to parse</param>
    /// <param name="lineNumber">Line number for error reporting</param>
    /// <returns>Parse result with data or errors</returns>
    private static (bool IsValid, CNABLineData? Data, List<string> Errors) ParseLine(string line, int lineNumber)
    {
        var errors = new List<string>();

        // Validate line length (structural validation)
        if (line.Length != ExpectedLineLength)
        {
            errors.Add($"Line {lineNumber}: Invalid length {line.Length}. Expected {ExpectedLineLength} characters.");
            return (false, null, errors);
        }

        try
        {
            var data = new CNABLineData();

            // Type (1 char, position 0) - parse only, validation done in CNABValidator
            if (!int.TryParse(line[0].ToString(), out var type))
            {
                errors.Add($"Line {lineNumber}: Invalid transaction type '{line[0]}'. Must be a digit.");
                return (false, null, errors);
            }
            data.Type = type;

            // Date (8 chars, positions 1-8): YYYYMMDD - parse only
            if (!TryParseDate(line.Substring(1, 8), out var date))
            {
                errors.Add($"Line {lineNumber}: Invalid date format '{line.Substring(1, 8)}'. Expected YYYYMMDD.");
                return (false, null, errors);
            }
            data.Date = date;

            // Amount (10 chars, positions 9-18): numeric in cents - parse only
            if (!decimal.TryParse(line.Substring(9, 10), out var amount))
            {
                errors.Add($"Line {lineNumber}: Invalid amount format '{line.Substring(9, 10)}'. Must be numeric.");
                return (false, null, errors);
            }
            data.Amount = amount;

            // CPF (11 chars, positions 19-29) - extract only, validation in CNABValidator
            data.CPF = line.Substring(19, 11).Trim();

            // Card (12 chars, positions 30-41) - extract only, validation in CNABValidator
            data.Card = line.Substring(30, 12).Trim();

            // Time (6 chars, positions 42-47): HHMMSS - parse only
            if (!TryParseTime(line.Substring(42, 6), out var time))
            {
                errors.Add($"Line {lineNumber}: Invalid time format '{line.Substring(42, 6)}'. Expected HHMMSS.");
                return (false, null, errors);
            }
            data.Time = time;

            // Store Owner (14 chars, positions 48-61) - extract only, validation in CNABValidator
            data.StoreOwner = line.Substring(48, 14).Trim();

            // Store Name (19 chars, positions 62-80) - extract only, validation in CNABValidator
            data.StoreName = line.Substring(62, 19).Trim();

            // Try to calculate signed amount to ensure type is valid for that operation
            // This will throw if Type is invalid, which is caught below
            _ = data.SignedAmount;

            return (true, data, errors);
        }
        catch (InvalidOperationException ex)
        {
            errors.Add($"Line {lineNumber}: {ex.Message}");
            return (false, null, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Line {lineNumber}: Unexpected parsing error: {ex.Message}");
            return (false, null, errors);
        }
    }

    /// <summary>
    /// Attempts to parse date from YYYYMMDD format.
    /// </summary>
    private static bool TryParseDate(string dateStr, out DateTime date)
    {
        date = default;
        if (dateStr.Length != 8)
            return false;

        if (!int.TryParse(dateStr.Substring(0, 4), out var year) ||
            !int.TryParse(dateStr.Substring(4, 2), out var month) ||
            !int.TryParse(dateStr.Substring(6, 2), out var day))
            return false;

        try
        {
            date = new DateTime(year, month, day);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to parse time from HHMMSS format.
    /// </summary>
    private static bool TryParseTime(string timeStr, out TimeSpan time)
    {
        time = default;
        if (timeStr.Length != 6)
            return false;

        if (!int.TryParse(timeStr.Substring(0, 2), out var hours) ||
            !int.TryParse(timeStr.Substring(2, 2), out var minutes) ||
            !int.TryParse(timeStr.Substring(4, 2), out var seconds))
            return false;

        if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59 || seconds < 0 || seconds > 59)
            return false;

        try
        {
            time = new TimeSpan(hours, minutes, seconds);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
