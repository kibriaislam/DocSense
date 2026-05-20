namespace RagApi.Infrastructure.Parsing;

internal static class ParsingTextHelper
{
    public static string Clean(string text) =>
        new(text.Where(c => !char.IsControl(c) || c is '\n' or '\r' or '\t').ToArray());
}
