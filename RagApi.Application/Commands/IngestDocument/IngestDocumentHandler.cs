using MediatR;
using Microsoft.Extensions.Logging;
using RagApi.Application.Interfaces;
using RagApi.Domain.Entities;
using RagApi.Domain.Enums;

namespace RagApi.Application.Commands.IngestDocument;

public class IngestDocumentHandler : IRequestHandler<IngestDocumentCommand, Guid>
{
    private readonly IDocumentParser _parser;
    private readonly ITextChunker _chunker;
    private readonly IEmbeddingService _embedder;
    private readonly IVectorRepository _repository;
    private readonly ILogger<IngestDocumentHandler> _logger;

    public IngestDocumentHandler(
        IDocumentParser parser,
        ITextChunker chunker,
        IEmbeddingService embedder,
        IVectorRepository repository,
        ILogger<IngestDocumentHandler> logger)
    {
        _parser = parser;
        _chunker = chunker;
        _embedder = embedder;
        _repository = repository;
        _logger = logger;
    }

    public async Task<Guid> Handle(IngestDocumentCommand command, CancellationToken ct)
    {
        var document = new Document
        {
            FileName = command.FileName,
            ContentType = command.ContentType,
            FileSizeBytes = command.FileBytes.Length,
            Status = DocumentStatus.Pending
        };

        await _repository.SaveDocumentAsync(document, ct);

        try
        {
            await _repository.UpdateDocumentStatusAsync(document.Id, DocumentStatus.Processing, ct: ct);

            _logger.LogInformation(
                "Starting ingestion for document {DocumentId} {FileName}",
                document.Id,
                document.FileName);

            var text = await _parser.ParseAsync(command.FileBytes, command.ContentType, ct);

            if (string.IsNullOrWhiteSpace(text))
            {
                await _repository.UpdateDocumentStatusAsync(
                    document.Id,
                    DocumentStatus.Failed,
                    errorMessage: "No text extracted",
                    ct);
                return document.Id;
            }

            var chunkTexts = _chunker.Chunk(text);

            _logger.LogInformation(
                "Document {DocumentId} split into {ChunkCount} chunks",
                document.Id,
                chunkTexts.Count);

            var embeddings = await _embedder.GetEmbeddingsBatchAsync(chunkTexts, ct);

            for (var index = 0; index < chunkTexts.Count; index++)
            {
                document.Chunks.Add(new DocumentChunk
                {
                    DocumentId = document.Id,
                    ChunkIndex = index,
                    Content = chunkTexts[index],
                    Embedding = embeddings[index]
                });
            }

            await _repository.SaveDocumentAsync(document, ct);

            await _repository.UpdateDocumentStatusAsync(document.Id, DocumentStatus.Ready, ct: ct);

            _logger.LogInformation("Ingestion complete for {DocumentId}", document.Id);

            return document.Id;
        }
        catch (Exception ex)
        {
            await _repository.UpdateDocumentStatusAsync(
                document.Id,
                DocumentStatus.Failed,
                errorMessage: ex.Message,
                ct);
            throw;
        }
    }
}
