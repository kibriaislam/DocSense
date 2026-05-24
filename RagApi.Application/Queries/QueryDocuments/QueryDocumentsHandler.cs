using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using RagApi.Application.Interfaces;
using RagApi.Application.Models;

namespace RagApi.Application.Queries.QueryDocuments;

public class QueryDocumentsHandler : IRequestHandler<QueryDocumentsQuery, QueryResult>
{
    private const string SystemPrompt =
        "You are a helpful assistant. Answer the question using ONLY the context provided. If the answer is not in the context, respond with: I don't know based on the provided documents.";

    private readonly IEmbeddingService _embedder;
    private readonly IVectorRepository _repository;
    private readonly IGenerationService _generator;
    private readonly ILogger<QueryDocumentsHandler> _logger;

    public QueryDocumentsHandler(
        IEmbeddingService embedder,
        IVectorRepository repository,
        IGenerationService generator,
        ILogger<QueryDocumentsHandler> logger)
    {
        _embedder = embedder;
        _repository = repository;
        _generator = generator;
        _logger = logger;
    }

    public async Task<QueryResult> Handle(QueryDocumentsQuery query, CancellationToken ct)
    {
        var embedStopwatch = Stopwatch.StartNew();
        var queryVector = await _embedder.GetEmbeddingAsync(query.Question, ct);
        embedStopwatch.Stop();
        var embedLatencyMs = embedStopwatch.ElapsedMilliseconds;
        _logger.LogInformation("Query embedding completed in {EmbedLatencyMs}ms", embedLatencyMs);

        var searchStopwatch = Stopwatch.StartNew();
        var chunks = await _repository.SearchSimilarAsync(queryVector, query.TopK, ct);
        searchStopwatch.Stop();
        var searchLatencyMs = searchStopwatch.ElapsedMilliseconds;
        _logger.LogInformation("Similarity search completed in {SearchLatencyMs}ms", searchLatencyMs);

        var topScore = chunks.Count > 0 ? chunks[0].SimilarityScore ?? 0f : 0f;
        _logger.LogInformation(
            "Retrieved {ChunkCount} chunks for question. Top score: {TopScore:F4}",
            chunks.Count,
            topScore);

        if (chunks.Count == 0)
        {
            return new QueryResult(
                "No relevant documents found.",
                Array.Empty<SourceReference>(),
                embedLatencyMs,
                searchLatencyMs,
                0);
        }

        var generateStopwatch = Stopwatch.StartNew();
        var answer = await _generator.CompleteAsync(SystemPrompt, query.Question, chunks, ct);
        generateStopwatch.Stop();
        var generateLatencyMs = generateStopwatch.ElapsedMilliseconds;
        _logger.LogInformation("Answer generation completed in {GenerateLatencyMs}ms", generateLatencyMs);

        var sources = chunks
            .Select(chunk => new SourceReference(
                chunk.DocumentId,
                chunk.Document.FileName,
                chunk.ChunkIndex,
                chunk.SimilarityScore ?? 0f,
                chunk.Content[..Math.Min(200, chunk.Content.Length)]))
            .ToList();

        return new QueryResult(answer, sources, embedLatencyMs, searchLatencyMs, generateLatencyMs);
    }
}
