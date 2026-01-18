namespace TransactionProcessor.Domain.Services;

/// <summary>
/// Domain service for validating CNAB file structure before content processing.
/// 
/// This service performs structural validation only - checking that the file
/// conforms to the CNAB 240 fixed-width format without parsing transaction content.
/// 
/// CNAB Format Specification (80-character fixed-width lines):
/// Reference: docs/business-rules.md § CNAB File Format
/// 
/// Each line must contain exactly 80 characters with the following positions:
/// ┌──────────┬────────────────────────────────────────────────────────────────┐
/// │ Position │ Field Contents (80 characters total per line)                   │
/// ├──────────┼────────────────────────────────────────────────────────────────┤
/// │  1       │ Transaction Type (1 digit: 1-9)                               │
/// │  2-9     │ Date (8 digits: YYYYMMDD)                                     │
/// │  10-19   │ Amount (10 digits in cents)                                   │
/// │  20-30   │ CPF (11 digits)                                               │
/// │  31-42   │ Card (12 characters)                                          │
/// │  43-48   │ Time (6 digits: HHMMSS)                                       │
/// │  49-62   │ Store Owner Name (14 characters)                              │
/// │  63-80   │ Store Name (18 characters, may be padded)                     │
/// └──────────┴────────────────────────────────────────────────────────────────┘
/// 
/// Validation Rules (Structural Only):
/// 1. **File Size**: File must not exceed 10MB (10,485,760 bytes)
///    - Protects against accidentally uploaded files
///    - Memory-efficient processing
/// 
/// 2. **Line Format**: Each line must be exactly 80 bytes long
///    - Fixed-width format requirement from CNAB specification
///    - Deviation indicates format corruption or incorrect file type
/// 
/// 3. **Line Count**: File must contain at least 1 transaction line
///    - Empty files are rejected
///    - Ensures meaningful processing
/// 
/// 4. **Character Encoding**: File must contain only ASCII characters
///    - CNAB 240 specification requires ASCII encoding
///    - Non-ASCII characters indicate encoding issues or wrong file type
/// 
/// NOT Validated Here (Content Parsing Phase):
/// - Transaction type validity (1-9)
/// - Date format and validity
/// - Amount validity (must be > 0)
/// - CPF format validity
/// - Card number format
/// - Field content constraints
/// 
/// These are validated later during actual CNAB parsing and processing.
/// 
/// Design Rationale:
/// Separating structural validation from content validation allows:
/// - Fast rejection of malformed files before expensive parsing
/// - Clear error messages about file format issues vs. business rule violations
/// - Efficient memory usage (streaming validation, no full file buffering)
/// - Clean separation of concerns
/// 
/// Reference: docs/business-rules.md § File Validation § Line-Level Validation
/// Reference: technical-decisions.md § Code Documentation Standards
/// </summary>
public interface IFileValidator
{
    /// <summary>
    /// Validate CNAB file structure without parsing content.
    /// 
    /// This method checks:
    /// 1. File size does not exceed 10MB
    /// 2. Each line is exactly 80 bytes long
    /// 3. File contains at least one line
    /// 4. All characters are valid ASCII
    /// 
    /// The validation is performed by streaming the file line-by-line to minimize
    /// memory usage and allow early rejection of invalid files.
    /// 
    /// Validation Process:
    /// 1. Check file size before processing
    /// 2. Open file stream and read lines sequentially
    /// 3. For each line:
    ///    a. Verify length is exactly 80 bytes
    ///    b. Verify all characters are ASCII (0-127)
    ///    c. Collect any validation errors
    /// 4. Verify at least one line was processed
    /// 5. Return validation result with all errors
    /// 
    /// Stream Handling:
    /// - Stream position is NOT reset after validation (stream is consumed)
    /// - Caller is responsible for stream disposal
    /// - For re-validation, provide a new stream with same content
    /// 
    /// </summary>
    /// <param name="fileStream">
    /// File stream to validate. Must be readable and seekable.
    /// Stream position should be at the beginning (position 0).
    /// </param>
    /// <returns>
    /// ValidationResult with IsValid = true if all checks pass,
    /// or IsValid = false with detailed error messages if any validation fails.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if fileStream is null
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown if stream cannot be read or accessed
    /// </exception>
    /// <remarks>
    /// Error Examples:
    /// 
    /// Example 1 - File Too Large:
    ///   Input: Stream with 15MB of data
    ///   Result: IsValid = false, Errors = ["File size 15728640 bytes exceeds maximum 10485760 bytes"]
    /// 
    /// Example 2 - Line Length Mismatch:
    ///   Input: CNAB file with line 5 containing 79 characters
    ///   Result: IsValid = false, Errors = ["Line 5: Expected 80 bytes, found 79 bytes"]
    /// 
    /// Example 3 - Non-ASCII Characters:
    ///   Input: CNAB file with UTF-8 encoded character at line 3, position 42
    ///   Result: IsValid = false, Errors = ["Line 3: Non-ASCII character (code 195) at position 42"]
    /// 
    /// Example 4 - Empty File:
    ///   Input: Empty stream
    ///   Result: IsValid = false, Errors = ["File contains no transaction lines"]
    /// 
    /// Example 5 - Multiple Errors:
    ///   Input: File with multiple issues
    ///   Result: IsValid = false, Errors = [
    ///     "Line 1: Expected 80 bytes, found 78 bytes",
    ///     "Line 3: Non-ASCII character (code 233) at position 15",
    ///     "Line 7: Expected 80 bytes, found 85 bytes"
    ///   ]
    /// 
    /// Performance Characteristics:
    /// - Time Complexity: O(n) where n = number of bytes in file
    /// - Space Complexity: O(1) for validation (only stores error messages)
    /// - Memory Usage: Bounded by error message count, not file size
    /// - Streaming: Validates as it reads, no full file buffering
    /// 
    /// Usage Pattern:
    /// 
    /// using (var fileStream = new FileStream("cnab_file.txt", FileMode.Open))
    /// {
    ///     var result = _fileValidator.Validate(fileStream);
    ///     if (!result.IsValid)
    ///     {
    ///         // Reject file
    ///         file.MarkAsRejected(result.GetErrorSummary());
    ///         return;
    ///     }
    ///     // Proceed with content parsing
    ///     await ProcessCnabContent(fileStream);
    /// }
    /// 
    /// See: ValidationResult for detailed result structure
    /// See: docs/business-rules.md § File Validation for complete validation rules
    /// </remarks>
    Task<ValidationResult> Validate(Stream fileStream);
}
