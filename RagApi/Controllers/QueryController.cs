using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RagApi.Application.Interfaces;
using RagApi.Application.Models;
using RagApi.Application.Queries.QueryDocuments;
using RagApi.Domain.Entities;

namespace RagApi.Controllers;

/// <summary>
/// Query ingested documents using natural-language questions.
/// </summary>
[ApiController]
[Route("api/query")]
[EnableRateLimiting("query-limit")]
public class QueryController : ControllerBase
{
    private const string SystemPrompt =
        "You are a helpful assistant. Answer the question using ONLY the context provided. If the answer is not in the context, respond with: I don't know based on the provided documents.";

    private readonly ISender _mediator;
    private readonly IEmbeddingService _embedder;
    private readonly IVectorRepository _repository;
    private readonly IGenerationService _generator;

    public QueryController(
        ISender mediator,
        IEmbeddingService embedder,
        IVectorRepository repository,
        IGenerationService generator)
    {
        _mediator = mediator;
        _embedder = embedder;
        _repository = repository;
        _generator = generator;
    }

    /// <summary>
    /// Ask a question and receive a complete answer with source references and latency stats.
    /// </summary>
    /// <param name="request">The question and number of context chunks to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated answer, sources, and timing breakdown.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(QueryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Query([FromBody] QueryRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(
                new QueryDocumentsQuery(request.Question, request.TopK),
                ct);

            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return BadRequest(new
            {
                errors = exception.Errors.Select(error => error.ErrorMessage)
            });
        }
    }

    /// <summary>
    /// Ask a question and stream the answer as Server-Sent Events.
    /// </summary>
    /// <param name="request">The question and number of context chunks to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("stream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task Stream([FromBody] QueryRequest request, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var queryVector = await _embedder.GetEmbeddingAsync(request.Question, ct);
        var chunks = await _repository.SearchSimilarAsync(queryVector, request.TopK, ct);

        if (chunks.Count == 0)
        {
            await Response.WriteAsync("data: No relevant documents found.\n\n", ct);
            return;
        }

        var sources = BuildSourceReferences(chunks);
        await Response.WriteAsync(
            "event: sources\ndata: " + JsonSerializer.Serialize(sources) + "\n\n",
            ct);

        await foreach (var token in _generator.StreamAsync(SystemPrompt, request.Question, chunks, ct))
        {
            await Response.WriteAsync("data: " + token + "\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        await Response.WriteAsync("data: [DONE]\n\n", ct);
    }

    /// <summary>
    /// Ask a question and return an extended debug payload with vectors, scores, and prompt preview.
    /// </summary>
    /// <param name="request">The question and number of context chunks to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Answer, sources, latency stats, and diagnostic details.</returns>
    [HttpPost("debug")]
    [ProducesResponseType(typeof(QueryDebugResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Debug([FromBody] QueryRequest request, CancellationToken ct)
    {
        var embedStopwatch = Stopwatch.StartNew();
        var queryVector = await _embedder.GetEmbeddingAsync(request.Question, ct);
        embedStopwatch.Stop();
        var embedLatencyMs = embedStopwatch.ElapsedMilliseconds;

        var searchStopwatch = Stopwatch.StartNew();
        var chunks = await _repository.SearchSimilarAsync(queryVector, request.TopK, ct);
        searchStopwatch.Stop();
        var searchLatencyMs = searchStopwatch.ElapsedMilliseconds;

        if (chunks.Count == 0)
        {
            return Ok(new QueryDebugResult(
                "No relevant documents found.",
                Array.Empty<SourceReference>(),
                embedLatencyMs,
                searchLatencyMs,
                0,
                queryVector.Take(10).ToList(),
                Array.Empty<ChunkScore>(),
                string.Empty));
        }

        var prompt = BuildPrompt(SystemPrompt, request.Question, chunks);
        var promptPreview = prompt.Length <= 500 ? prompt : prompt[..500];

        var generateStopwatch = Stopwatch.StartNew();
        var answer = await _generator.CompleteAsync(SystemPrompt, request.Question, chunks, ct);
        generateStopwatch.Stop();
        var generateLatencyMs = generateStopwatch.ElapsedMilliseconds;

        var sources = BuildSourceReferences(chunks);
        var chunkScores = chunks
            .Select(chunk => new ChunkScore(
                chunk.DocumentId,
                chunk.Document.FileName,
                chunk.ChunkIndex,
                chunk.SimilarityScore ?? 0f))
            .ToList();

        return Ok(new QueryDebugResult(
            answer,
            sources,
            embedLatencyMs,
            searchLatencyMs,
            generateLatencyMs,
            queryVector.Take(10).ToList(),
            chunkScores,
            promptPreview));
    }

    private static IReadOnlyList<SourceReference> BuildSourceReferences(IReadOnlyList<DocumentChunk> chunks) =>
        chunks
            .Select(chunk => new SourceReference(
                chunk.DocumentId,
                chunk.Document.FileName,
                chunk.ChunkIndex,
                chunk.SimilarityScore ?? 0f,
                chunk.Content[..Math.Min(200, chunk.Content.Length)]))
            .ToList();

    private static string BuildPrompt(
        string systemPrompt,
        string question,
        IReadOnlyList<DocumentChunk> context)
    {
        var formattedChunks = context
            .Select((chunk, index) =>
                $"[{index + 1}] (from: {chunk.Document.FileName})\n{chunk.Content}");

        var contextBlock = string.Join("\n\n", formattedChunks);

        return string.Concat(
            systemPrompt,
            "\n\nContext:\n",
            contextBlock,
            "\n\nQuestion: ",
            question,
            "\n\nAnswer:");
    }
}
