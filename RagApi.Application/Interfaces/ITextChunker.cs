namespace RagApi.Application.Interfaces;

public interface ITextChunker
{
    IReadOnlyList<string> Chunk(string text);
}
