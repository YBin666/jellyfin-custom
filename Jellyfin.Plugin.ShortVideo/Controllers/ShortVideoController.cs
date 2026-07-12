using Jellyfin.Plugin.ShortVideo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShortVideo.Controllers;

/// <summary>
/// 短视频业务接口控制器。
/// 仅提供短视频相关的 API，不负责 Web 资源注入（由 WebAssetController 处理）。
/// </summary>
[ApiController]
[Route("ShortVideo")]
public class ShortVideoController : ControllerBase
{
    private readonly IFeedService _feedService;
    private readonly ILogger<ShortVideoController> _logger;

    public ShortVideoController(
        IFeedService feedService,
        ILogger<ShortVideoController> logger)
    {
        _feedService = feedService;
        _logger = logger;
    }

    [HttpGet("NextBatch")]
    [AllowAnonymous]
    public IActionResult NextBatch()
    {
        _logger.LogInformation("ShortVideo Controller: 收到 /ShortVideo/NextBatch 请求");
        var batch = _feedService.NextBatch();
        var token = GetApiKeyFromRequest();
        foreach (var i in batch)
        {
            i.StreamUrl = i.StreamUrl.Replace("__APIKEY__", token);
        }

        _logger.LogInformation("ShortVideo Controller: /NextBatch 返回 {Count} 条", batch.Count);
        return Ok(batch);
    }

    [HttpPost("Reload")]
    [AllowAnonymous]
    public IActionResult Reload()
    {
        _logger.LogInformation("ShortVideo Controller: 收到 /ShortVideo/Reload 请求");
        _feedService.Reload();
        return Ok(new { message = "reloaded" });
    }

    private string GetApiKeyFromRequest()
    {
        if (Request.Query.TryGetValue("api_key", out var q) && !string.IsNullOrEmpty(q))
        {
            return q.ToString();
        }

        if (Request.Headers.TryGetValue("X-Emby-Token", out var h) && !string.IsNullOrEmpty(h))
        {
            return h.ToString();
        }

        if (Request.Cookies.TryGetValue("X-Emby-Token", out var c) && !string.IsNullOrEmpty(c))
        {
            return c.ToString();
        }

        return string.Empty;
    }
}
