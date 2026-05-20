using RagApi.Application.Interfaces;
using UglyToad.PdfPig;

namespace RagApi.Infrastructure.Parsing;

public class PdfDocumentParser : IDocumentParser
{
    private const string PdfContentType = "application/pdf";

    public bool CanParse(string contentType) =>
        string.Equals(contentType, PdfContentType, StringComparison.OrdinalIgnoreCase);

    public Task<string> ParseAsync(byte[] fileBytes, string contentType, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        using var document = PdfDocument.Open(fileBytes);
        var pages = document.GetPages()
            .Select(page => page.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text));

        var combined = string.Join("\n\n", pages);
        return Task.FromResult(ParsingTextHelper.Clean(combined));
    }
}
