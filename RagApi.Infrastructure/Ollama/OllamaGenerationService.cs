using System.Runtime.CompilerServices;
using System.Text;
using RagApi.Application.Interfaces;
using RagApi.Domain.Entities;

namespace RagApi.Infrastructure.Ollama;

public class OllamaGenerationService : IGenerationService
{
    private readonly OllamaClientService _client;

    public OllamaGenerationService(OllamaClientService client)
    {
        _client = client;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string question,
        IReadOnlyList<DocumentChunk> context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var prompt = BuildPrompt(systemPrompt, question, context);

        await foreach (var token in _client.GenerateStreamAsync(prompt, ct))
        {
            yield return token;
        }
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string question,
        IReadOnlyList<DocumentChunk> context,
        CancellationToken ct = default)
    {
        var builder = new StringBuilder();

        await foreach (var token in StreamAsync(systemPrompt, question, context, ct))
        {
            builder.Append(token);
        }

        return builder.ToString();
    }

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
