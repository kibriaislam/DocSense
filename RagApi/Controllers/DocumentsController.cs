using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RagApi.Application.Commands.DeleteDocument;
using RagApi.Application.Commands.IngestDocument;
using RagApi.Application.Models;
using RagApi.Application.Queries.GetAllDocuments;

namespace RagApi.Controllers;

/// <summary>
/// Upload, list, and delete ingested documents.
/// </summary>
[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(ISender mediator, ILogger<DocumentsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Upload a document for ingestion and embedding.
    /// </summary>
    /// <param name="file">The document file (PDF, DOCX, or plain text).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The document identifier and a processing message.</returns>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "A non-empty file is required." });
        }

        if (file.Length > 52_428_800)
        {
            return BadRequest(new { error = "File size must not exceed 50MB (52,428,800 bytes)." });
        }

        try
        {
            _logger.LogInformation("Upload request received for {FileName}", file.FileName);

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, ct);
            var bytes = memoryStream.ToArray();

            var documentId = await _mediator.Send(
                new IngestDocumentCommand(file.FileName, file.ContentType, bytes),
                ct);

            return Accepted(new
            {
                documentId,
                message = "Document queued for processing"
            });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new
            {
                errors = exception.Errors.Select(error => error.ErrorMessage)
            });
        }
    }

    /// <summary>
    /// List all ingested documents and their processing status.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All documents in the system.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var documents = await _mediator.Send(new GetAllDocumentsQuery(), ct);
        return Ok(documents);
    }

    /// <summary>
    /// Delete a document and all of its chunks.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteDocumentCommand(id), ct);
        return NoContent();
    }
}
