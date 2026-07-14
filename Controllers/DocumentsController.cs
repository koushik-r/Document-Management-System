using Microsoft.AspNetCore.Mvc;
using DocumentManagement.Api.Services;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IBlobStorageService _blob;
    public DocumentsController(IBlobStorageService blob) => _blob = blob;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var docs = await _blob.ListAsync();
        return Ok(docs);
    }

    [HttpGet("{fileName}")]
    public async Task<IActionResult> ViewDocument(string fileName)
    {
        var (stream, contentType) = await _blob.DownloadAsync(fileName);
        return File(stream, contentType, enableRangeProcessing: true);
    }

    [HttpDelete("{fileName}")]
    public async Task<IActionResult> DeleteDocument(string fileName)
    {
        await _blob.DeleteAsync(fileName);
        return NoContent();
    }
}