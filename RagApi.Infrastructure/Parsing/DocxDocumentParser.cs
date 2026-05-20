using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RagApi.Application.Interfaces;

namespace RagApi.Infrastructure.Parsing;

public class DocxDocumentParser : IDocumentParser
{
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public bool CanParse(string contentType) =>
        string.Equals(contentType, DocxContentType, StringComparison.OrdinalIgnoreCase);

    public Task<string> ParseAsync(byte[] fileBytes, string contentType, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        using var stream = new MemoryStream(fileBytes, writable: false);
        using var wordDocument = WordprocessingDocument.Open(stream, false);

        var body = wordDocument.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return Task.FromResult(string.Empty);
        }

        var paragraphs = body.Descendants<Paragraph>()
            .Select(paragraph => paragraph.InnerText);

        var combined = string.Join("\n", paragraphs);
        return Task.FromResult(ParsingTextHelper.Clean(combined));
    }
}
