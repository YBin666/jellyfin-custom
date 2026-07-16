using Jellyfin.Plugin.FileUpload.Models;
using Jellyfin.Plugin.FileUpload.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileUpload.Controllers;

/// <summary>
/// 文件上传接口控制器。
/// 路由前缀：/FileUpload
/// </summary>
[ApiController]
[Route("FileUpload")]
[Authorize]
public class FileUploadController : ControllerBase
{
    private readonly IFileUploadService _service;
    private readonly ILogger<FileUploadController> _logger;

    public FileUploadController(
        IFileUploadService service,
        ILogger<FileUploadController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>获取所有 Jellyfin 媒体库及其物理路径。</summary>
    [HttpGet("Libraries")]
    public IActionResult Libraries()
    {
        var libs = _service.GetMediaLibraries();
        return Ok(new { libraries = libs });
    }

    /// <summary>浏览指定路径下的子目录。</summary>
    [HttpGet("Browse")]
    public IActionResult Browse([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { error = "path 不能为空。" });
        }

        try
        {
            var result = _service.BrowseDirectories(path);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DirectoryNotFoundException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>初始化一次分片上传。</summary>
    [HttpPost("Init")]
    public async Task<IActionResult> Init([FromBody] InitUploadRequest request)
    {
        try
        {
            var resp = await _service.InitUploadAsync(request).ConfigureAwait(false);
            return Ok(resp);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>上传单个分片（multipart/form-data）。</summary>
    [HttpPost("Chunk")]
    [RequestSizeLimit(long.MaxValue)]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> Chunk(
        [FromForm] Guid uploadId,
        [FromForm] int chunkIndex,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "分片内容为空。" });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            await _service.SaveChunkAsync(uploadId, chunkIndex, stream).ConfigureAwait(false);
            return Ok(new { uploadId, chunkIndex, received = file.Length });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>合并分片并完成上传。</summary>
    [HttpPost("Complete")]
    public async Task<IActionResult> Complete([FromBody] CompleteRequest req)
    {
        if (req == null || req.UploadId == Guid.Empty)
        {
            return BadRequest(new { error = "UploadId 无效。" });
        }

        try
        {
            var resp = await _service.CompleteUploadAsync(req.UploadId).ConfigureAwait(false);
            return Ok(resp);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>取消并清理指定上传。</summary>
    [HttpDelete("Cancel")]
    public async Task<IActionResult> Cancel([FromQuery] Guid uploadId)
    {
        await _service.CancelUploadAsync(uploadId).ConfigureAwait(false);
        return Ok(new { cancelled = true, uploadId });
    }

    /// <summary>手动触发指定目录所属媒体库的扫描。</summary>
    [HttpPost("Scan")]
    public async Task<IActionResult> Scan([FromBody] ScanRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Directory))
        {
            return BadRequest(new { error = "Directory 不能为空。" });
        }

        try
        {
            var result = await _service.TriggerScanAsync(req.Directory).ConfigureAwait(false);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }
}

public sealed class CompleteRequest
{
    public Guid UploadId { get; set; }
}

public sealed class ScanRequest
{
    public string Directory { get; set; } = string.Empty;
}
