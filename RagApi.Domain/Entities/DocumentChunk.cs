using System.ComponentModel.DataAnnotations.Schema;

namespace RagApi.Domain.Entities;

public class DocumentChunk
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public required string Content { get; set; }

    public float[] Embedding { get; init; } = [];

    [NotMapped]
    public float? SimilarityScore { get; set; }

    public Document Document { get; set; } = null!;
}
