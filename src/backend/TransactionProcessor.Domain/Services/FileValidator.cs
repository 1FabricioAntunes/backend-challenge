using System.Text;

namespace TransactionProcessor.Domain.Services;

/// <summary>
/// Domain service implementation for CNAB file structure validation.
/// 
/// This stateless service validates that uploaded CNAB files conform to the
/// fixed-width format specification before processing begins. Validation is
/// performed by streaming the file to minimize memory usage.
/// 
/// CNAB File Format Constraints:
/// - **File Size Limit**: 10MB maximum (10,485,760 bytes)
///   Rationale: Protects against accidental large uploads, ensures memory efficiency
/// 
/// - **Line Length**: Exactly 80 bytes per line
///   CNAB 240 specification requirement: fixed-width format with 80-character lines
/// 
/// - **Line Count**: At least 1 line required
///   Empty files are rejected as containing no transaction data
/// 
/// - **Encoding**: ASCII only (characters 0-127)
///   CNAB specification requires ASCII encoding for all fields
/// 
/// Validation Flow:
/// 1. Check file size (quick rejection of oversized files)
/// 2. Stream file line-by-line
/// 3. For each line: validate length and character encoding
/// 4. Collect all errors (not just first error)
/// 5. Return detailed ValidationResult
/// 
/// Design Characteristics:
/// - **Streaming**: Reads file line-by-line without buffering entire content
/// - **Memory Efficient**: Constant memory usage regardless of file size
/// - **Fail-Fast**: Can reject files early without reading entire content
/// - **Detailed Errors**: Reports all validation failures, not just the first
/// - **Stateless**: No internal state, thread-safe
/// 
/// Line-Level Validation Details:
/// 
/// Length Validation:
/// - Uses byte-length, not character count (important for multi-byte encodings)
/// - Rejects any line not exactly 80 bytes
/// - Includes both too-short and too-long lines in error reporting
/// - Reports exact difference: "Expected 80 bytes, found 85 bytes"
/// 
/// Encoding Validation:
/// - Checks each byte is in range 0-127 (valid ASCII)
/// - Reports non-ASCII bytes with their numeric code (e.g., "Non-ASCII character (code 233)")
/// - Identifies exact position of invalid character in line
/// - Helps distinguish between encoding issues and data corruption
/// 
/// Error Reporting:
/// - Line numbers are 1-based (user-friendly)
/// - All errors collected before returning result
/// - Multiple validation errors don't short-circuit processing
/// - Errors include actionable information (actual vs. expected values)
/// 
/// Examples:
/// 
/// Example 1 - Valid File:
///   Input: File with 5 lines, each exactly 80 ASCII characters
///   Output: ValidationResult with IsValid = true
/// 
/// Example 2 - File Size Exceeded:
///   Input: 15MB file
///   Validation Stops At: Size check, before reading content
///   Output: IsValid = false, Error: "File size 15728640 bytes exceeds maximum 10485760 bytes"
/// 
/// Example 3 - Line Length Issues:
///   Input: File with lines 1-3 valid (80 bytes each), line 4 has 85 bytes
///   Output: IsValid = false, Error: "Line 4: Expected 80 bytes, found 85 bytes"
/// 
/// Example 4 - Non-ASCII Characters:
///   Input: Line 2 contains UTF-8 sequence "São Paulo" at position 20
///   Output: IsValid = false, Error: "Line 2: Non-ASCII character (code 195) at position 20"
///   Note: First non-ASCII byte of multi-byte UTF-8 sequence is reported
/// 
/// Example 5 - Empty File:
///   Input: Stream with 0 bytes
///   Output: IsValid = false, Error: "File contains no transaction lines"
/// 
/// Reference: docs/business-rules.md § File Validation
/// Reference: CNAB 240 Specification (80-character fixed-width format)
/// </summary>
public class FileValidator : IFileValidator
{
    // Constants for CNAB format validation
    private const int MaxFileSize = 10 * 1024 * 1024; // 10MB
    private const int CnabLineLength = 80; // CNAB 240 specification: 80-character lines
    private const byte AsciiMaxValue = 127; // ASCII range: 0-127

    /// <summary>
    /// Validate CNAB file structure by checking size, line format, and encoding.
    /// 
    /// Implementation Details:
    /// 1. File Size Check:
    ///    - Seeks to end of stream to determine total size
    ///    - Resets stream position to beginning after check
    ///    - Fast rejection for oversized files
    /// 
    /// 2. Line-by-Line Validation:
    ///    - Reads file using UTF-8 encoding (to properly detect non-ASCII)
    ///    - For each line:
    ///      a. Check byte length (not character count)
    ///      b. Validate all bytes are ASCII (0-127)
    ///      c. Collect errors for this line
    ///    - Continues to end of file (collects all errors)
    /// 
    /// 3. Final Checks:
    ///    - Verify at least one line was read
    ///    - Return comprehensive validation result
    /// 
    /// Performance:
    /// - File Size Check: O(1) - just seeks to end
    /// - Content Validation: O(n) - must read all bytes
    /// - Memory Usage: O(1) - only stores error strings, not file content
    /// - Streaming: Does not buffer entire file in memory
    /// 
    /// </summary>
    /// <param name="fileStream">
    /// Stream to validate. Must support seeking (Stream.CanSeek = true).
    /// Should be positioned at beginning (position = 0).
    /// </param>
    /// <returns>
    /// Task resolving to ValidationResult containing validation outcome
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if fileStream is null
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown if stream cannot be read or seeked
    /// </exception>
    public async Task<ValidationResult> Validate(Stream fileStream)
    {
        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream), "File stream cannot be null");

        var errors = new List<string>();

        // Step 1: Check file size
        try
        {
            long fileSize = fileStream.Length;
            if (fileSize > MaxFileSize)
            {
                return ValidationResult.Failure(
                    $"File size {fileSize} bytes exceeds maximum {MaxFileSize} bytes");
            }
        }
        catch (IOException ex)
        {
            throw new IOException("Unable to determine file size", ex);
        }

        // Step 2: Reset stream position to beginning for content reading
        fileStream.Seek(0, SeekOrigin.Begin);

        // Step 3: Validate file content line by line
        var reader = new StreamReader(fileStream, Encoding.UTF8);
        int lineNumber = 0;

        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;
                ValidateLine(line, lineNumber, errors);
            }
        }
        catch (IOException ex)
        {
            throw new IOException("Error reading file stream", ex);
        }

        // Step 4: Check if file contains at least one line
        if (lineNumber == 0)
        {
            return ValidationResult.Failure("File contains no transaction lines");
        }

        // Step 5: Return validation result
        if (errors.Count > 0)
        {
            return ValidationResult.Failure(errors);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate a single line from the CNAB file.
    /// 
    /// Checks:
    /// 1. Byte length is exactly 80 (CNAB specification)
    /// 2. All bytes are valid ASCII (0-127)
    /// 
    /// Validation is non-destructive - errors are added to the list
    /// without throwing exceptions, allowing all lines to be validated.
    /// 
    /// </summary>
    /// <param name="line">Line content to validate</param>
    /// <param name="lineNumber">1-based line number for error reporting</param>
    /// <param name="errors">List to accumulate validation errors</param>
    private void ValidateLine(string line, int lineNumber, List<string> errors)
    {
        if (string.IsNullOrEmpty(line))
        {
            errors.Add($"Line {lineNumber}: Empty line (expected 80 bytes)");
            return;
        }

        // Get byte length using UTF-8 encoding to match actual transmitted data
        byte[] lineBytes = Encoding.UTF8.GetBytes(line);
        int byteLength = lineBytes.Length;

        // Check line length
        if (byteLength != CnabLineLength)
        {
            errors.Add(
                $"Line {lineNumber}: Expected {CnabLineLength} bytes, found {byteLength} bytes");
        }

        // Check for non-ASCII characters
        // Important: Check actual bytes, not characters (handles multi-byte encodings)
        for (int i = 0; i < lineBytes.Length; i++)
        {
            byte currentByte = lineBytes[i];

            // ASCII range is 0-127; values 128-255 are non-ASCII
            if (currentByte > AsciiMaxValue)
            {
                errors.Add(
                    $"Line {lineNumber}: Non-ASCII character (code {currentByte}) at position {i + 1}");
                // Report only first non-ASCII character per line to avoid excessive errors
                break;
            }
        }
    }
}
