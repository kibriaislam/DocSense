using FluentValidation;
using RagApi.Application.Models;

namespace RagApi.Application.Validators;

public class IngestDocumentRequestValidator : AbstractValidator<IngestDocumentRequest>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain"
    };

    public IngestDocumentRequestValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.ContentType)
            .Must(contentType => AllowedContentTypes.Contains(contentType))
            .WithMessage("ContentType must be application/pdf, application/vnd.openxmlformats-officedocument.wordprocessingml.document, or text/plain.");

        RuleFor(x => x.FileBytes)
            .NotEmpty()
            .Must(bytes => bytes.Length is >= 1 and <= 52_428_800)
            .WithMessage("FileBytes must be between 1 byte and 50MB (52,428,800 bytes).");
    }
}
