using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using RagApi.Domain.Entities;

namespace RagApi.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(document => document.Id);

            entity.Property(document => document.FileName)
                .HasMaxLength(255);

            entity.Property(document => document.ContentType)
                .HasMaxLength(100);

            entity.HasIndex(document => document.CreatedAt)
                .IsDescending();
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(chunk => chunk.Id);

            entity.Property(chunk => chunk.Content)
                .HasColumnType("text");

            var embeddingComparer = new ValueComparer<float[]>(
                (left, right) => (left ?? Array.Empty<float>()).SequenceEqual(right ?? Array.Empty<float>()),
                value => value.Aggregate(0, (hash, element) => HashCode.Combine(hash, element.GetHashCode())),
                value => value.ToArray());

            entity.Property(chunk => chunk.Embedding)
                .HasColumnType("vector(768)")
                .HasConversion(
                    embedding => new Vector(embedding),
                    vector => vector.ToArray())
                .Metadata.SetValueComparer(embeddingComparer);

            entity.Ignore(chunk => chunk.SimilarityScore);

            entity.HasIndex(chunk => chunk.DocumentId);

            entity.HasOne(chunk => chunk.Document)
                .WithMany(document => document.Chunks)
                .HasForeignKey(chunk => chunk.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
