using RagApi.Application.Interfaces;

namespace RagApi.Infrastructure.Parsing;

public class CompositeDocumentParser : IDocumentParser
{
    private readonly IReadOnlyList<IDocumentParser> _parsers;

    public CompositeDocumentParser(IEnumerable<IDocumentParser> parsers)
    {
        _parsers = parsers.ToList();
    }

    public bool CanParse(string contentType) =>
        _parsers.Any(parser => parser.CanParse(contentType));

    public Task<string> ParseAsync(byte[] fileBytes, string contentType, CancellationToken ct = default)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(contentType))
            ?? throw new NotSupportedException($"No parser available for content type '{contentType}'.");

        return parser.ParseAsync(fileBytes, contentType, ct);
    }
}
