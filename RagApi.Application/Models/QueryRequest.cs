namespace RagApi.Application.Models;

public record QueryRequest(string Question, int TopK = 5);
