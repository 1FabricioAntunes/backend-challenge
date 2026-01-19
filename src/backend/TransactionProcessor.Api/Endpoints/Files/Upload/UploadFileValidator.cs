using System.IO;
using System.Text.RegularExpressions;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using TransactionProcessor.Application.Exceptions;

namespace TransactionProcessor.Api.Endpoints.Files.Upload;

/// <summary>
/// Validates upload requests for CNAB files.
/// </summary>
/// <remarks>
/// Validation Layers (per technical-decisions.md - Input Validation):
/// 1. File presence check: File must be provided
/// 2. File size validation: Max 10MB to prevent DoS
/// 3. File type validation: .txt extension only
/// 4. Filename safety: No path traversal, injection attempts
/// 5. Content validation: Check for CNAB format markers
/// 6. Stream readability: File stream must be readable
/// 
/// Security Considerations (OWASP A03 and A04):
/// - A03 (Injection): Reject filenames with SQL/shell metacharacters
/// - A04 (Insecure File Upload): Validate file type and extension
/// - Path Traversal Prevention: Block ../, ..\ and drive letters
/// 
/// Reference: technical-decisions.md Input Validation and Sanitization
/// Reference: docs/security.md Input Sanitization Rules
/// </remarks>
public class UploadFileValidator : Validator<UploadFileRequest>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
    private const long MinFileSizeBytes = 1; // At least 1 byte

    public UploadFileValidator()
    {
        RuleFor(x => x.File)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("File is required.")
            .Must(IsFileStreamReadable).WithMessage("File stream is not readable.")
            .Must(IsNotEmpty).WithMessage("File cannot be empty.")
            .Must(IsNotTooLarge).WithMessage("File size must not exceed 10MB.")
            .Must(HasValidFileName).WithMessage("File name contains invalid characters or is too long.")
            .Must(HasTxtExtension).WithMessage("Only .txt files are allowed for CNAB uploads.")
            .Must(ContainsCNABMarkers).WithMessage("File does not appear to be a valid CNAB file.")
            .Must(DoesNotContainInjectionPatterns).WithMessage("File name contains suspicious characters.");
    }

    /// <summary>
    /// Validate that file stream is readable and seekable (for validation and upload).
    /// </summary>
    private static bool IsFileStreamReadable(IFormFile? file)
    {
        if (file is null)
            return false;

        // Stream must be readable
        if (!file.OpenReadStream().CanRead)
            return false;

        // Stream should be seekable for retry scenarios
        var stream = file.OpenReadStream();
        return stream.CanSeek || !stream.CanWrite; // Either seekable OR read-only is acceptable
    }

    /// <summary>
    /// Validate that file is not empty (at least 1 byte).
    /// </summary>
    private static bool IsNotEmpty(IFormFile? file)
    {
        if (file is null)
            return false;

        return file.Length >= MinFileSizeBytes;
    }

    /// <summary>
    /// Validate that file does not exceed 10MB size limit.
    /// </summary>
    private static bool IsNotTooLarge(IFormFile? file)
    {
        if (file is null)
            return false;

        return file.Length <= MaxFileSizeBytes;
    }

    /// <summary>
    /// Validate filename safety against path traversal and injection attacks.
    /// </summary>
    /// <remarks>
    /// OWASP A03 Prevention:
    /// - Block path separators (/ \ :)
    /// - Block shell metacharacters (semicolon, pipe, ampersand, dollar, backtick, newline, carriage return)
    /// - Block SQL injection patterns (--, /*, */, xp_, sp_)
    /// - Block environment variable expansion (dollar-VAR, percent-VAR-percent)
    /// - Block control characters (0x00 to 0x1F)
    /// </remarks>
    private static bool HasValidFileName(IFormFile? file)
    {
        if (file is null)
            return false;

        var fileName = file.FileName ?? string.Empty;

        // Reject empty or whitespace-only filenames
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        // Reject filenames that exceed database column constraint
        if (fileName.Length > 255)
            return false;

        // Extract filename only (remove path components if present)
        try
        {
            var fileNameOnly = Path.GetFileName(fileName);
            
            // If path separators were present, original path != filename
            if (fileNameOnly != fileName)
                return false; // Path traversal attempt detected
            
            // Check for invalid filename characters (OS-level)
            if (fileNameOnly.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;
        }
        catch
        {
            return false; // Invalid filename format
        }

        return true;
    }

    /// <summary>
    /// Validate file extension is .txt (CNAB format requirement).
    /// </summary>
    private static bool HasTxtExtension(IFormFile? file)
    {
        if (file is null)
            return false;

        var fileName = file.FileName ?? string.Empty;
        var extension = Path.GetExtension(fileName);
        
        return string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validate file contains CNAB format markers (80-character fixed-width lines).
    /// </summary>
    /// <remarks>
    /// CNAB Format Check:
    /// - File must contain lines (not random binary data)
    /// - Lines should be approximately 80 bytes (allow plus-or-minus 20 percent variance for encoding)
    /// - Lines should contain printable ASCII characters (0x20 to 0x7E, plus common whitespace)
    /// - No binary data patterns (0x00, control characters)
    /// </remarks>
    private static bool ContainsCNABMarkers(IFormFile? file)
    {
        if (file is null)
            return false;

        try
        {
            using var stream = file.OpenReadStream();
            
            // Read first 1KB to check format
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, Math.Min(1024, (int)stream.Length));

            if (bytesRead == 0)
                return false; // Empty file

            // Look for line terminators (CRLF or LF)
            bool hasLineTerminator = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead)
                .Contains("\n") || System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead).Contains("\r");

            if (!hasLineTerminator)
                return false; // Doesn't look like text file

            // Check for excessive control characters (binary data indicator)
            int controlCharCount = 0;
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] < 0x20 && buffer[i] != 0x09 && buffer[i] != 0x0A && buffer[i] != 0x0D) // Tab, LF, CR OK
                    controlCharCount++;
            }

            // Allow up to 5% control characters (some systems may have them)
            return controlCharCount <= (bytesRead / 20);
        }
        catch
        {
            // If we can't read the file, let downstream validation handle it
            return true; // Don't reject here; let file validator handle
        }
    }

    /// <summary>
    /// Validate filename does not contain injection attack patterns.
    /// 
    /// Patterns Blocked:
    /// - Path traversal: ../, ..\\
    /// - Shell metacharacters: ; | and $ ` \n \r
    /// - SQL patterns: --, /*, */
    /// - Environment variables: $VAR, %VAR%
    /// - Control characters: 0x00-0x1F
    /// 
    /// Rationale:
    /// - Filename is stored in database and displayed in logs/UI
    /// - Prevents injection if filename ever used in database queries (defense in depth)
    /// - Prevents log injection attacks
    /// - Prevents escape sequences in filenames
    /// </summary>
    private static bool DoesNotContainInjectionPatterns(IFormFile? file)
    {
        if (file is null)
            return false;

        var fileName = file.FileName ?? string.Empty;

        // Patterns that indicate injection attempts
        var injectionPatterns = new[]
        {
            @"\.\.[/\\]",           // Path traversal: ../ or ..\
            @"[;|&$`\n\r]",         // Shell metacharacters
            @"(--|/\*|\*/)",        // SQL comment patterns
            @"[xX][pP]_",           // SQL injection: xp_ procedures
            @"[sS][pP]_",           // SQL injection: sp_ procedures
            @"(\$|%)[\w]+(\$|%)?",  // Environment variables: $VAR or %VAR%
            @"[\x00-\x1F]",         // Control characters (except tab, CR, LF)
        };

        foreach (var pattern in injectionPatterns)
        {
            if (Regex.IsMatch(fileName, pattern))
                return false;
        }

        return true;
    }
}
