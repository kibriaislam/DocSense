namespace RagApi.Application.Interfaces;

public interface IDocumentParser
{
    Task<string> ParseAsync(byte[] fileBytes, string contentType, CancellationToken ct = default);

    bool CanParse(string contentType);
}
