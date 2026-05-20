namespace RagApi.Infrastructure.Options;

public class ChunkingOptions
{
    public const string SectionName = "Chunking";

    public int ChunkSize { get; set; }

    public int Overlap { get; set; }
}
