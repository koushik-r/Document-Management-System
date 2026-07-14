using Microsoft.AspNetCore.Mvc;
using DocumentManagement.Api.Services;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/upload")]
public class UploadController : ControllerBase
{
    private readonly IBlobStorageService _blob;
    public UploadController(IBlobStorageService blob) => _blob = blob;

    // ── Small file (< 10 MB) ─────────────────────────────────────────────────

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        var safeName = Path.GetFileName(file.FileName);
        await using var stream = file.OpenReadStream();
        var info = await _blob.UploadAsync(
            safeName, stream, file.ContentType ?? "application/octet-stream");
        return Ok(info);
    }

    // ── Chunked: stage one block ─────────────────────────────────────────────

    [HttpPost("block/{uploadId}/{blockIndex:int}")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> StageBlock(string uploadId, int blockIndex)
    {
        if (string.IsNullOrWhiteSpace(uploadId) || blockIndex < 0)
            return BadRequest("Invalid uploadId or blockIndex.");

        await _blob.StageBlockAsync(uploadId, blockIndex, Request.Body);
        return Ok();
    }

    // ── Chunked: commit all staged blocks ────────────────────────────────────

    [HttpPost("commit")]
    public async Task<IActionResult> Commit([FromBody] CommitRequest req)
    {
        if (req is null || req.TotalBlocks <= 0)
            return BadRequest("totalBlocks must be > 0.");

        var safeName = Path.GetFileName(req.FileName);
        var info = await _blob.CommitBlocksAsync(
            req.UploadId, safeName, req.ContentType, req.TotalBlocks);
        return Ok(info);
    }

    // ── Chunked: abort / cleanup ─────────────────────────────────────────────

    [HttpDelete("abort/{uploadId}")]
    public async Task<IActionResult> Abort(string uploadId)
    {
        await _blob.AbortUploadAsync(uploadId);
        return NoContent();
    }
}

public record CommitRequest(
    string UploadId,
    string FileName,
    string ContentType,
    int    TotalBlocks);