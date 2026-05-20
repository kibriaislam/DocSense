namespace RagApi.Infrastructure.Options;

public class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = string.Empty;

    public string EmbedModel { get; set; } = string.Empty;

    public string GenerateModel { get; set; } = string.Empty;
}
