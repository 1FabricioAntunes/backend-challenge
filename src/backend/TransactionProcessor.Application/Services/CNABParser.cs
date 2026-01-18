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
    /// </summary>
    /// <param name="line">The line to parse</param>
    /// <param name="lineNumber">Line number for error reporting</param>
    /// <returns>Parse result with data or errors</returns>
    private static (bool IsValid, CNABLineData? Data, List<string> Errors) ParseLine(string line, int lineNumber)
    {
        var errors = new List<string>();

        // Validate line length
        if (line.Length != ExpectedLineLength)
        {
            errors.Add($"Line {lineNumber}: Invalid length {line.Length}. Expected {ExpectedLineLength} characters.");
            return (false, null, errors);
        }

        try
        {
            var data = new CNABLineData();

            // Type (1 char, position 0)
            if (!int.TryParse(line[0].ToString(), out var type) || type < 1 || type > 9)
            {
                errors.Add($"Line {lineNumber}: Invalid transaction type '{line[0]}'. Must be 1-9.");
                return (false, null, errors);
            }
            data.Type = type;

            // Date (8 chars, positions 1-8): YYYYMMDD
            if (!TryParseDate(line.Substring(1, 8), out var date))
            {
                errors.Add($"Line {lineNumber}: Invalid date '{line.Substring(1, 8)}'. Format must be YYYYMMDD.");
                return (false, null, errors);
            }
            data.Date = date;

            // Validate date is in reasonable range (1900 to current year + 1)
            if (data.Date.Year < 1900 || data.Date.Year > DateTime.Now.Year + 1)
            {
                errors.Add($"Line {lineNumber}: Transaction date {data.Date:yyyy-MM-dd} is out of reasonable range (1900-{DateTime.Now.Year + 1}).");
            }

            // Amount (10 chars, positions 9-18): numeric in cents
            if (!decimal.TryParse(line.Substring(9, 10), out var amount) || amount < 0)
            {
                errors.Add($"Line {lineNumber}: Invalid amount '{line.Substring(9, 10)}'. Must be numeric and non-negative.");
                return (false, null, errors);
            }
            // Amount must be positive (at least 1 cent)
            if (amount == 0)
            {
                errors.Add($"Line {lineNumber}: Amount must be positive (greater than 0).");
                return (false, null, errors);
            }
            data.Amount = amount;

            // CPF (11 chars, positions 19-29): must be 11 digits
            var cpfRaw = line.Substring(19, 11).Trim();
            if (string.IsNullOrEmpty(cpfRaw))
            {
                errors.Add($"Line {lineNumber}: CPF is required and cannot be empty.");
                return (false, null, errors);
            }
            if (cpfRaw.Length != 11 || !cpfRaw.All(c => char.IsDigit(c)))
            {
                errors.Add($"Line {lineNumber}: Invalid CPF format '{cpfRaw}'. Must be exactly 11 digits.");
                return (false, null, errors);
            }
            if (!ContainsSafeCharacters(cpfRaw, "CPF"))
            {
                errors.Add($"Line {lineNumber}: CPF contains unsafe characters or SQL injection vectors.");
                return (false, null, errors);
            }
            data.CPF = cpfRaw;

            // Card (12 chars, positions 30-41): allow masked cards with asterisks
            var cardRaw = line.Substring(30, 12).Trim();
            if (string.IsNullOrEmpty(cardRaw))
            {
                errors.Add($"Line {lineNumber}: Card number is required and cannot be empty.");
                return (false, null, errors);
            }
            if (cardRaw.Length != 12)
            {
                errors.Add($"Line {lineNumber}: Invalid card format '{cardRaw}'. Must be exactly 12 characters (alphanumeric or asterisks for masked).");
                return (false, null, errors);
            }
            // Card can be alphanumeric or asterisks (for masked cards)
            if (!cardRaw.All(c => char.IsLetterOrDigit(c) || c == '*'))
            {
                errors.Add($"Line {lineNumber}: Invalid card format '{cardRaw}'. Must contain only alphanumeric characters or asterisks (*).");
                return (false, null, errors);
            }
            if (!ContainsSafeCharacters(cardRaw, "Card"))
            {
                errors.Add($"Line {lineNumber}: Card contains unsafe characters or SQL injection vectors.");
                return (false, null, errors);
            }
            data.Card = cardRaw;

            // Time (6 chars, positions 42-47): HHMMSS
            if (!TryParseTime(line.Substring(42, 6), out var time))
            {
                errors.Add($"Line {lineNumber}: Invalid time '{line.Substring(42, 6)}'. Format must be HHMMSS.");
                return (false, null, errors);
            }
            data.Time = time;

            // Store Owner (14 chars, positions 48-61): required, non-empty
            var storeOwnerRaw = line.Substring(48, 14).Trim();
            if (string.IsNullOrEmpty(storeOwnerRaw))
            {
                errors.Add($"Line {lineNumber}: Store owner name is required and cannot be empty.");
                return (false, null, errors);
            }
            if (!ContainsSafeCharacters(storeOwnerRaw, "StoreOwner"))
            {
                errors.Add($"Line {lineNumber}: Store owner name contains unsafe characters or SQL injection vectors.");
                return (false, null, errors);
            }
            data.StoreOwner = storeOwnerRaw;

            // Store Name (19 chars, positions 62-80): required, non-empty
            var storeNameRaw = line.Substring(62, 19).Trim();
            if (string.IsNullOrEmpty(storeNameRaw))
            {
                errors.Add($"Line {lineNumber}: Store name is required and cannot be empty.");
                return (false, null, errors);
            }
            if (!ContainsSafeCharacters(storeNameRaw, "StoreName"))
            {
                errors.Add($"Line {lineNumber}: Store name contains unsafe characters or SQL injection vectors.");
                return (false, null, errors);
            }
            data.StoreName = storeNameRaw;

            // Validate signed amount calculation doesn't throw
            _ = data.SignedAmount;

            // If we collected any validation errors (like date range), return them
            if (errors.Count > 0)
            {
                return (false, null, errors);
            }

            return (true, data, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Line {lineNumber}: Unexpected parsing error: {ex.Message}");
            return (false, null, errors);
        }
    }

    /// <summary>
    /// Validates string contains only safe characters (OWASP A03: Injection Prevention).
    /// Rejects strings containing SQL keywords, operators, or command injection vectors.
    /// </summary>
    private static bool ContainsSafeCharacters(string value, string fieldName)
    {
        if (string.IsNullOrEmpty(value))
            return true;

        // Characters that should never appear in CNAB fields
        var unsafePatterns = new[]
        {
            ";", "--", "/*", "*/", "xp_", "sp_", // SQL injection
            "'", "\"", "\\", // Quote escaping
            "<", ">", "&", // HTML/XML encoding
            "|", "$", "`", // Command injection
            "&&", "||", // Boolean operators
        };

        var upperValue = value.ToUpperInvariant();

        foreach (var pattern in unsafePatterns)
        {
            if (upperValue.Contains(pattern.ToUpperInvariant()))
                return false;
        }

        // Check for SQL keywords that might indicate injection
        var sqlKeywords = new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "UNION", "EXEC" };
        foreach (var keyword in sqlKeywords)
        {
            if (upperValue.Contains(keyword))
                return false;
        }

        return true;
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
