using TransactionProcessor.Application.Models;

namespace TransactionProcessor.Application.Services;

/// <summary>
/// Service for validating individual CNAB records against business rules.
/// Works with parsed CNABLineData to enforce domain constraints.
/// 
/// Separation of Concerns:
/// - CNABParser: Extracts fixed-position fields from 80-char lines
/// - CNABValidator: Validates extracted fields against business rules
/// 
/// Reference: docs/business-rules.md § Validation Rules
/// Reference: technical-decisions.md § Input Validation and Sanitization (OWASP A03)
/// </summary>
public interface ICNABValidator
{
    /// <summary>
    /// Validates a parsed CNAB record against all business rules.
    /// </summary>
    /// <param name="record">The parsed CNAB record to validate</param>
    /// <param name="lineNumber">Line number for error reporting</param>
    /// <returns>Validation result with any errors found</returns>
    CNABValidationResult ValidateRecord(CNABLineData record, int lineNumber);
}

/// <summary>
/// Implementation of CNAB record validator with business rule enforcement.
/// Validates parsed data without modifying it.
/// </summary>
public class CNABValidator : ICNABValidator
{
    /// <summary>
    /// Validates a parsed CNAB record against all business rules.
    /// Collects ALL errors before returning (not fail-fast).
    /// </summary>
    public CNABValidationResult ValidateRecord(CNABLineData record, int lineNumber)
    {
        var result = new CNABValidationResult { IsValid = true };

        // Validate transaction type: must be 1-9
        if (record.Type < 1 || record.Type > 9)
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Invalid transaction type {record.Type}. Must be 1-9.");
        }

        // Validate date: must be in reasonable range (1900 to current year + 1)
        if (record.Date.Year < 1900 || record.Date.Year > DateTime.Now.Year + 1)
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Transaction date {record.Date:yyyy-MM-dd} is out of reasonable range (1900-{DateTime.Now.Year + 1}).");
        }

        // Validate amount: must be positive (> 0)
        if (record.Amount <= 0)
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Amount must be positive (greater than 0 cents), found {record.Amount}.");
        }

        // Validate CPF: must be exactly 11 digits
        if (string.IsNullOrWhiteSpace(record.CPF))
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: CPF is required and cannot be empty.");
        }
        else if (record.CPF.Length != 11 || !record.CPF.All(c => char.IsDigit(c)))
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Invalid CPF format '{record.CPF}'. Must be exactly 11 digits.");
        }
        else if (!ContainsSafeCharacters(record.CPF, "CPF"))
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: CPF contains unsafe characters (OWASP A03 injection prevention).");
        }

        // Validate Card: must be exactly 12 chars (alphanumeric or asterisks for masked)
        if (string.IsNullOrWhiteSpace(record.Card))
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Card number is required and cannot be empty.");
        }
        else if (record.Card.Length != 12)
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Invalid card format '{record.Card}'. Must be exactly 12 characters.");
        }
        else if (!record.Card.All(c => char.IsLetterOrDigit(c) || c == '*'))
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Card format invalid. Must contain only alphanumeric or asterisks (*).");
        }
        else if (!ContainsSafeCharacters(record.Card, "Card"))
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Card contains unsafe characters (OWASP A03 injection prevention).");
        }

        // Validate Store Owner: required, not empty
        if (string.IsNullOrWhiteSpace(record.StoreOwner))
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Store owner name is required and cannot be empty.");
        }
        else if (record.StoreOwner.Length > 14)
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Store owner name exceeds maximum length (14 chars, found {record.StoreOwner.Length}).");
        }
        else if (!ContainsSafeCharacters(record.StoreOwner, "StoreOwner"))
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Store owner name contains unsafe characters (OWASP A03 injection prevention).");
        }

        // Validate Store Name: required, not empty
        if (string.IsNullOrWhiteSpace(record.StoreName))
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Store name is required and cannot be empty.");
        }
        else if (record.StoreName.Length > 19)
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Store name exceeds maximum length (19 chars, found {record.StoreName.Length}).");
        }
        else if (!ContainsSafeCharacters(record.StoreName, "StoreName"))
        {
            result.IsValid = false;
            result.Errors.Add($"Line {lineNumber}: Store name contains unsafe characters (OWASP A03 injection prevention).");
        }

        return result;
    }

    /// <summary>
    /// Validates string contains only safe characters (OWASP A03: Injection Prevention).
    /// Rejects strings containing SQL keywords, operators, or command injection vectors.
    /// </summary>
    private static bool ContainsSafeCharacters(string value, string fieldName)
    {
        if (string.IsNullOrEmpty(value))
            return true;

        // Unsafe patterns: SQL injection, command injection, encoding tricks
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

        // SQL keywords that indicate injection attempts
        var sqlKeywords = new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "UNION", "EXEC" };
        foreach (var keyword in sqlKeywords)
        {
            if (upperValue.Contains(keyword))
                return false;
        }

        return true;
    }
}
