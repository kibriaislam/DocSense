using Microsoft.Extensions.Options;
using RagApi.Application.Interfaces;
using RagApi.Infrastructure.Options;

namespace RagApi.Infrastructure.Chunking;

public class SlidingWindowChunker : ITextChunker
{
    private readonly ChunkingOptions _options;

    public SlidingWindowChunker(IOptions<ChunkingOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<string> Chunk(string text)
    {
        var chunkSize = _options.ChunkSize;
        var overlap = _options.Overlap;

        if (overlap >= chunkSize)
        {
            throw new ArgumentException("Overlap must be strictly less than ChunkSize.", nameof(_options.Overlap));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length <= chunkSize)
        {
            return new[] { string.Join(' ', words) };
        }

        var chunks = new List<string>();
        var step = chunkSize - overlap;

        for (var i = 0; i < words.Length; i += step)
        {
            var length = Math.Min(chunkSize, words.Length - i);
            chunks.Add(string.Join(' ', words.Skip(i).Take(length)));
        }

        return chunks;
    }
}
