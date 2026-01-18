using System.IO;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace TransactionProcessor.Api.Endpoints.Files.Upload;

/// <summary>
/// Validates upload requests for CNAB files.
/// </summary>
public class UploadFileValidator : Validator<UploadFileRequest>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public UploadFileValidator()
    {
        RuleFor(x => x.File)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("File is required.")
            .Must(file => file!.Length > 0).WithMessage("File cannot be empty.")
            .Must(file => file!.Length <= MaxFileSizeBytes).WithMessage("File size must not exceed 10MB.")
            .Must(HasSafeFileName).WithMessage("File name is invalid.")
            .Must(HasTxtExtension).WithMessage("Only .txt files are allowed.");
    }

    private static bool HasSafeFileName(IFormFile file)
    {
        if (file is null)
        {
            return false;
        }

        var rawName = file.FileName ?? string.Empty;
        var trimmedName = rawName.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName) || trimmedName.Length > 255)
        {
            return false;
        }

        var fileNameOnly = Path.GetFileName(trimmedName);
        return fileNameOnly == trimmedName && fileNameOnly.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    private static bool HasTxtExtension(IFormFile file)
    {
        if (file is null)
        {
            return false;
        }

        var fileName = file.FileName ?? string.Empty;
        var extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase);
    }
}
