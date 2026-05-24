using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using RagApi.Application.Interfaces;
using RagApi.Domain.Entities;
using RagApi.Domain.Enums;

namespace RagApi.Infrastructure.Persistence;

public class PgVectorRepository : IVectorRepository
{
    private readonly AppDbContext _context;

    public PgVectorRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task SaveDocumentAsync(Document document, CancellationToken ct = default)
    {
        var exists = await _context.Documents.AnyAsync(d => d.Id == document.Id, ct);

        if (exists)
        {
            foreach (var chunk in document.Chunks)
            {
                chunk.DocumentId = document.Id;
                _context.DocumentChunks.Add(chunk);
            }
        }
        else
        {
            _context.Documents.Add(document);
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentChunk>> SearchSimilarAsync(
        float[] queryVector,
        int topK,
        CancellationToken ct = default)
    {
        var queryVectorParam = new NpgsqlParameter("queryVector", new Vector(queryVector));
        var topKParam = new NpgsqlParameter("topK", topK);

        const string chunkSql = """
            SELECT dc."Id", dc."DocumentId", dc."ChunkIndex", dc."Content", dc."Embedding"
            FROM "DocumentChunks" dc
            INNER JOIN "Documents" d ON dc."DocumentId" = d."Id"
            ORDER BY dc."Embedding" <=> @queryVector
            LIMIT @topK
            """;

        var chunks = await _context.DocumentChunks
            .FromSqlRaw(chunkSql, queryVectorParam, topKParam)
            .AsNoTracking()
            .ToListAsync(ct);

        if (chunks.Count == 0)
        {
            return chunks;
        }

        const string similaritySql = """
            SELECT dc."Id", d."FileName", (dc."Embedding" <=> @queryVector) AS "SimilarityScore"
            FROM "DocumentChunks" dc
            INNER JOIN "Documents" d ON dc."DocumentId" = d."Id"
            ORDER BY dc."Embedding" <=> @queryVector
            LIMIT @topK
            """;

        var similarityRows = await _context.Database
            .SqlQueryRaw<SimilarChunkSearchRow>(similaritySql, queryVectorParam, topKParam)
            .ToListAsync(ct);

        var similarityById = similarityRows.ToDictionary(row => row.Id);

        var documents = await _context.Documents
            .AsNoTracking()
            .Where(document => chunks.Select(chunk => chunk.DocumentId).Contains(document.Id))
            .ToDictionaryAsync(document => document.Id, ct);

        foreach (var chunk in chunks)
        {
            var similarity = similarityById[chunk.Id];
            chunk.SimilarityScore = (float)similarity.SimilarityScore;
            chunk.Document = documents[chunk.DocumentId];
        }

        return chunks;
    }

    public async Task<IReadOnlyList<Document>> GetAllDocumentsAsync(CancellationToken ct = default) =>
        await _context.Documents
            .Include(document => document.Chunks)
            .OrderByDescending(document => document.CreatedAt)
            .ToListAsync(ct);

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await _context.Documents
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document is null)
        {
            return;
        }

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateDocumentStatusAsync(
        Guid documentId,
        DocumentStatus status,
        string? errorMessage = null,
        CancellationToken ct = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document is null)
        {
            return;
        }

        document.Status = status;
        document.ErrorMessage = errorMessage;

        if (status is DocumentStatus.Ready or DocumentStatus.Failed)
        {
            document.ProcessedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }

    private sealed record SimilarChunkSearchRow(Guid Id, string FileName, double SimilarityScore);
}
