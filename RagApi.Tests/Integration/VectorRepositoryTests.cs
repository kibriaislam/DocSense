using Microsoft.EntityFrameworkCore;
using RagApi.Domain.Entities;
using RagApi.Domain.Enums;
using RagApi.Infrastructure.Persistence;

namespace RagApi.Tests.Integration;

public class VectorRepositoryTests : IAsyncLifetime
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("TEST_DB")
        ?? "Host=localhost;Database=ragdb_test;Username=postgres;Password=postgres";

    private DbContextOptions<AppDbContext> _options = null!;
    private AppDbContext _context = null!;
    private PgVectorRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString, options => options.UseVector())
            .Options;

        _context = new AppDbContext(_options);
        await _context.Database.MigrateAsync();
        await CleanupDocumentsAsync();

        _repository = new PgVectorRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task CleanupDocumentsAsync()
    {
        await _context.DocumentChunks.ExecuteDeleteAsync();
        await _context.Documents.ExecuteDeleteAsync();
    }

    private static float[] CreateEmbedding(float value)
    {
        var embedding = new float[768];
        Array.Fill(embedding, value);
        return embedding;
    }

    [Fact]
    public async Task SaveAndRetrieve()
    {
        var document = new Document
        {
            FileName = "test.txt",
            ContentType = "text/plain",
            FileSizeBytes = 100,
            Status = DocumentStatus.Ready,
            Chunks =
            [
                new DocumentChunk
                {
                    ChunkIndex = 0,
                    Content = "The cat sat on the mat",
                    Embedding = CreateEmbedding(0.1f)
                },
                new DocumentChunk
                {
                    ChunkIndex = 1,
                    Content = "Dogs like to fetch balls",
                    Embedding = CreateEmbedding(0.5f)
                },
                new DocumentChunk
                {
                    ChunkIndex = 2,
                    Content = "Cats sleep a lot",
                    Embedding = CreateEmbedding(0.15f)
                }
            ]
        };

        await _repository.SaveDocumentAsync(document);

        var queryVector = CreateEmbedding(0.12f);
        var results = await _repository.SearchSimilarAsync(queryVector, topK: 2);

        Assert.Equal(2, results.Count);
        Assert.Contains(
            results[0].Content,
            new[] { "The cat sat on the mat", "Cats sleep a lot" });
        Assert.All(results, chunk => Assert.NotNull(chunk.SimilarityScore));
    }

    [Fact]
    public async Task DeleteRemovesChunks()
    {
        var document = new Document
        {
            FileName = "delete-test.txt",
            ContentType = "text/plain",
            FileSizeBytes = 50,
            Status = DocumentStatus.Ready,
            Chunks =
            [
                new DocumentChunk
                {
                    ChunkIndex = 0,
                    Content = "First chunk",
                    Embedding = CreateEmbedding(0.1f)
                },
                new DocumentChunk
                {
                    ChunkIndex = 1,
                    Content = "Second chunk",
                    Embedding = CreateEmbedding(0.2f)
                }
            ]
        };

        await _repository.SaveDocumentAsync(document);
        await _repository.DeleteDocumentAsync(document.Id);

        var documents = await _repository.GetAllDocumentsAsync();

        Assert.Empty(documents);
    }
}
