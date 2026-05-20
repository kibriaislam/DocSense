using System.Text;
using RagApi.Application.Interfaces;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;
using RagApi.Infrastructure.Chunking;
using RagApi.Infrastructure.Options;
using RagApi.Infrastructure.Parsing;
using Xunit.Abstractions;

namespace RagApi.Tests.Integration;

public class ParseAndChunkTests
{
    private const string LoremIpsumParagraph =
        "lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et dolore magna aliqua " +
        "ut enim ad minim veniam quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat duis aute irure " +
        "dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur excepteur sint occaecat cupidatat non " +
        "proident sunt in culpa qui officia deserunt mollit anim id est laborum";

    private static readonly string LoremIpsum500Words = BuildLoremIpsum500Words();

    private static string BuildLoremIpsum500Words()
    {
        var words = LoremIpsumParagraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', Enumerable.Range(0, 500).Select(i => words[i % words.Length]));
    }

    private readonly ITestOutputHelper _output;

    public ParseAndChunkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ParseAndChunk_LoremIpsum500Words_ProducesValidChunks()
    {
        var parser = new TxtDocumentParser();
        var chunker = new SlidingWindowChunker(Options.Create(new ChunkingOptions
        {
            ChunkSize = 100,
            Overlap = 20
        }));

        Assert.Equal(500, LoremIpsum500Words.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

        var parsedText = await parser.ParseAsync(
            Encoding.UTF8.GetBytes(LoremIpsum500Words),
            "text/plain");

        var chunks = chunker.Chunk(parsedText);

        _output.WriteLine($"Chunk count: {chunks.Count}");
        foreach (var preview in chunks.Take(3))
        {
            var snippet = preview.Length <= 50 ? preview : preview[..50];
            _output.WriteLine($"Preview: {snippet}");
        }

        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk => Assert.False(string.IsNullOrWhiteSpace(chunk)));
        Assert.All(chunks, chunk => Assert.InRange(chunk.Split(' ').Length, 1, 120));

        var firstWords = chunks[0].Split(' ');
        var secondWords = chunks[1].Split(' ');
        var sharedWords = firstWords.TakeLast(20).ToArray();
        Assert.True(sharedWords.Length >= 15);
        Assert.Equal(sharedWords.Take(15), secondWords.Take(15));
    }

    [Fact]
    public async Task CompositeDocumentParser_SelectsTxtParser_ForPlainText()
    {
        const string expected = "Composite parser txt selection marker";
        var composite = CreateCompositeParser();

        var result = await composite.ParseAsync(
            Encoding.UTF8.GetBytes(expected),
            "text/plain");

        Assert.Contains("marker", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompositeDocumentParser_SelectsDocxParser_ForWordDocument()
    {
        const string expected = "Composite parser docx selection marker";
        var composite = CreateCompositeParser();

        var result = await composite.ParseAsync(
            CreateDocxBytes(expected),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        Assert.Contains("marker", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompositeDocumentParser_SelectsPdfParser_ForPdfDocument()
    {
        var composite = CreateCompositeParser();

        var result = await composite.ParseAsync(
            MinimalPdfBytes,
            "application/pdf");

        Assert.Contains("PDF", result, StringComparison.OrdinalIgnoreCase);
    }

    private static CompositeDocumentParser CreateCompositeParser() =>
        new(new IDocumentParser[]
        {
            new PdfDocumentParser(),
            new DocxDocumentParser(),
            new TxtDocumentParser()
        });

    private static byte[] CreateDocxBytes(string text)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(
                new Body(
                    new Paragraph(
                        new Run(
                            new Text(text)))));
        }

        return stream.ToArray();
    }

    private static readonly byte[] MinimalPdfBytes = Encoding.ASCII.GetBytes(
        """
        %PDF-1.4
        1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj
        2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj
        3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 300 144] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >> endobj
        4 0 obj << /Length 55 >> stream
        BT /F1 18 Tf 50 100 Td (PDF integration test) Tj ET
        endstream
        endobj
        5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj
        xref
        0 6
        0000000000 65535 f
        0000000009 00000 n
        0000000058 00000 n
        0000000115 00000 n
        0000000274 00000 n
        0000000379 00000 n
        trailer << /Size 6 /Root 1 0 R >>
        startxref
        456
        %%EOF
        """);
}
