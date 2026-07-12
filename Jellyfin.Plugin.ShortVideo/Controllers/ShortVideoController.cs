using System.Net.Mime;
using System.Reflection;
using Jellyfin.Plugin.ShortVideo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShortVideo.Controllers;

/// <summary>
/// 短视频插件的 REST API 与注入脚本入口。
/// 路由前缀固定为插件名。
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

    /// <summary>
    /// 批量预取下 N 条。
    /// </summary>
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

    /// <summary>
    /// 强制刷新候选池（手动触发，便于测试）。
    /// </summary>
    [HttpPost("Reload")]
    [AllowAnonymous]
    public IActionResult Reload()
    {
        _logger.LogInformation("ShortVideo Controller: 收到 /ShortVideo/Reload 请求");
        _feedService.Reload();
        return Ok(new { message = "reloaded" });
    }

    /// <summary>
    /// 注入到 Jellyfin 主界面的 JS 脚本。
    /// 包含公共基础设施（路由管理、抽屉菜单工具）+ ShortsModule + #/shorts 路由注册。
    /// </summary>
    [HttpGet("Inject.js")]
    [AllowAnonymous]
    public IActionResult InjectJs()
    {
        _logger.LogInformation("ShortVideo Controller: 收到 /ShortVideo/Inject.js 请求 (来自主界面加载)");
        var js = GetShortVideoScript();
        return Content(js, "application/javascript");
    }

    // ---- 工具方法 ----

    /// <summary>
    /// 拼接 common.js（公共基础设施）+ shorts.js（短视频模块）+ shorts 路由注册 + 初始化调用。
    /// </summary>
    private string GetShortVideoScript()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var ns = "Jellyfin.Plugin.ShortVideo.Web";
        var files = new[] { "common.js", "shorts.js" };
        var parts = new List<string>();

        foreach (var f in files)
        {
            var resourceName = $"{ns}.{f}";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                _logger.LogWarning("嵌入资源缺失: {Name}", resourceName);
                continue;
            }
            using var reader = new StreamReader(stream);
            parts.Add(reader.ReadToEnd());
        }

        var shortsRegister = @"
(function() {
    var registerRoute = window.__svRegisterRoute;
    if (!registerRoute || !window.ShortsModule) {
        console.warn('[ShortVideo] 公共基础设施未就绪，跳过路由注册');
        return;
    }

    registerRoute({
        name: 'shorts',
        title: '短视频 - Jellyfin',
        show: function() {
            var container = window.ShortsModule.buildContainer();
            document.body.appendChild(container);
            var reactRoot = document.getElementById('reactRoot');
            if (reactRoot) reactRoot.style.display = 'none';
            console.log('[ShortVideo] #/shorts 路由激活，初始化内嵌播放器');
            var feed = container.querySelector('.sv-feed');
            var state = window.ShortsModule.initPlayer(container, feed);
            return { container: container, state: state };
        }
    });

    console.log('[ShortVideo] 路由已注册，开始初始化');
    window.__svInit();
})();
";

        parts.Add(shortsRegister);
        return string.Join("\n", parts);
    }

    /// <summary>
    /// 从请求中解析 API Key。
    /// </summary>
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
            return c;
        }

        return string.Empty;
    }
}
