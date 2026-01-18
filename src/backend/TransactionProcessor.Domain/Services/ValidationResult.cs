namespace TransactionProcessor.Domain.Services;

/// <summary>
/// Value object representing the result of file validation.
/// 
/// This immutable object encapsulates the outcome of CNAB file structure validation,
/// including whether the file is valid and a list of any validation errors encountered.
/// 
/// Validation Scope:
/// This result contains only structural validation errors (file size, line format, encoding).
/// It does NOT include business rule validation (transaction type validity, amount constraints, etc.),
/// which is performed separately during CNAB content parsing and processing.
/// 
/// Error Categories:
/// - File Size Errors: File exceeds maximum size limit (10MB)
/// - Line Format Errors: Lines don't match CNAB fixed-width format (80 characters)
/// - Encoding Errors: File contains non-ASCII characters
/// - Structure Errors: File is empty or has fewer lines than required
/// 
/// Reference: docs/business-rules.md § File Validation for complete validation rules
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Indicates whether the file passed all structural validations.
    /// 
    /// True: File structure is valid; safe to proceed with content parsing
    /// False: File has structural issues; should be rejected without further processing
    /// 
    /// When IsValid is false, the Errors list contains one or more validation failure reasons.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// List of validation error messages encountered during file validation.
    /// 
    /// Structure:
    /// - Empty list when IsValid is true
    /// - One or more error messages when IsValid is false
    /// - Each error message is a human-readable description of what validation failed
    /// 
    /// Error Examples:
    /// - "File size exceeds maximum limit of 10MB (actual: 15.5MB)"
    /// - "Line 1: Expected 80 characters, found 79"
    /// - "Line 2: Contains non-ASCII characters at position 45"
    /// - "File contains no transaction lines (empty file)"
    /// 
    /// Usage:
    /// If validation fails, these errors should be returned to the user
    /// and stored in the File entity's ErrorMessage field for audit purposes.
    /// </summary>
    public List<string> Errors { get; }

    /// <summary>
    /// Constructor for creating a validation result.
    /// </summary>
    /// <param name="isValid">Whether validation succeeded</param>
    /// <param name="errors">List of validation error messages (empty if valid)</param>
    private ValidationResult(bool isValid, List<string> errors)
    {
        IsValid = isValid;
        Errors = errors ?? new List<string>();
    }

    /// <summary>
    /// Factory method to create a successful validation result.
    /// 
    /// Use this when file structure validation passes all checks.
    /// </summary>
    /// <returns>ValidationResult with IsValid = true and empty Errors list</returns>
    public static ValidationResult Success() => new ValidationResult(true, new List<string>());

    /// <summary>
    /// Factory method to create a failed validation result.
    /// 
    /// Use this when file structure validation detects errors.
    /// </summary>
    /// <param name="errors">List of error messages explaining validation failures</param>
    /// <returns>ValidationResult with IsValid = false and provided error list</returns>
    /// <exception cref="ArgumentNullException">Thrown if errors list is null</exception>
    /// <exception cref="ArgumentException">Thrown if errors list is empty (use Success() for valid results)</exception>
    public static ValidationResult Failure(List<string> errors)
    {
        if (errors == null)
            throw new ArgumentNullException(nameof(errors), "Errors list cannot be null");

        if (errors.Count == 0)
            throw new ArgumentException(
                "Errors list must contain at least one error. Use Success() for valid results.",
                nameof(errors));

        return new ValidationResult(false, errors);
    }

    /// <summary>
    /// Factory method to create a failed validation result with a single error.
    /// 
    /// Convenience method for single-error failure scenarios.
    /// </summary>
    /// <param name="error">Single error message</param>
    /// <returns>ValidationResult with IsValid = false and single error</returns>
    /// <exception cref="ArgumentNullException">Thrown if error is null or empty</exception>
    public static ValidationResult Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentNullException(nameof(error), "Error message cannot be null or empty");

        return new ValidationResult(false, new List<string> { error });
    }

    /// <summary>
    /// Get a human-readable summary of all validation errors.
    /// 
    /// Useful for logging or displaying to users.
    /// </summary>
    /// <returns>
    /// Single string with all errors joined by newlines.
    /// Empty string if no errors.
    /// </returns>
    public string GetErrorSummary()
    {
        if (Errors.Count == 0)
            return string.Empty;

        return string.Join(Environment.NewLine, Errors);
    }
}
