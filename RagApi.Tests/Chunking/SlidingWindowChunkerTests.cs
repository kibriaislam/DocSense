using Microsoft.Extensions.Options;
using RagApi.Infrastructure.Chunking;
using RagApi.Infrastructure.Options;

namespace RagApi.Tests.Chunking;

public class SlidingWindowChunkerTests
{
    [Fact]
    public void Chunk_1000Words_WithSize100Overlap10_ProducesCorrectChunkCount()
    {
        var chunker = CreateChunker(chunkSize: 100, overlap: 10);
        var text = string.Join(' ', Enumerable.Range(1, 1000).Select(i => $"word{i}"));

        var chunks = chunker.Chunk(text);

        Assert.Equal(12, chunks.Count);
    }

    [Fact]
    public void Chunk_AdjacentChunksShareExactlyOverlapWords()
    {
        const int chunkSize = 100;
        const int overlap = 10;
        var chunker = CreateChunker(chunkSize, overlap);
        var words = Enumerable.Range(1, 250).Select(i => $"word{i}").ToArray();
        var text = string.Join(' ', words);

        var chunks = chunker.Chunk(text);

        for (var i = 0; i < chunks.Count - 1; i++)
        {
            var currentWords = chunks[i].Split(' ');
            var nextWords = chunks[i + 1].Split(' ');
            var shared = currentWords.TakeLast(overlap).ToArray();

            Assert.Equal(overlap, shared.Length);
            Assert.Equal(shared, nextWords.Take(overlap).ToArray());
        }
    }

    [Fact]
    public void Chunk_EmptyInput_ReturnsEmptyList()
    {
        var chunker = CreateChunker(chunkSize: 100, overlap: 10);

        Assert.Empty(chunker.Chunk(string.Empty));
        Assert.Empty(chunker.Chunk("   "));
    }

    [Fact]
    public void Chunk_TextShorterThanChunkSize_ReturnsSingleChunk()
    {
        var chunker = CreateChunker(chunkSize: 100, overlap: 10);
        var text = "one two three four five";

        var chunks = chunker.Chunk(text);

        Assert.Single(chunks);
        Assert.Equal(text, chunks[0]);
    }

    [Fact]
    public void Chunk_OverlapGreaterThanOrEqualChunkSize_ThrowsArgumentException()
    {
        var equalOverlap = CreateChunker(chunkSize: 10, overlap: 10);
        var greaterOverlap = CreateChunker(chunkSize: 10, overlap: 11);

        Assert.Throws<ArgumentException>(() => equalOverlap.Chunk("one two three"));
        Assert.Throws<ArgumentException>(() => greaterOverlap.Chunk("one two three"));
    }

    private static SlidingWindowChunker CreateChunker(int chunkSize, int overlap) =>
        new(Options.Create(new ChunkingOptions { ChunkSize = chunkSize, Overlap = overlap }));
}
