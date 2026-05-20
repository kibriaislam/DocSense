using System.Text;
using RagApi.Application.Interfaces;

namespace RagApi.Infrastructure.Parsing;

public class TxtDocumentParser : IDocumentParser
{
    private const string PlainTextContentType = "text/plain";

    public bool CanParse(string contentType) =>
        string.Equals(contentType, PlainTextContentType, StringComparison.OrdinalIgnoreCase);

    public Task<string> ParseAsync(byte[] fileBytes, string contentType, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Encoding.UTF8.GetString(fileBytes));
    }
}
