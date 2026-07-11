using System.Net.Mime;
using System.Reflection;
using Jellyfin.Plugin.ShortVideo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShortVideo.Controllers;

/// <summary>
/// 短视频插件的 REST API 与页面入口。
/// 路由前缀固定为插件名，最终端点形如 /ShortVideo/Page。
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
    /// 返回竖屏滑动播放器 HTML 页面。
    /// 访问方式：/ShortVideo/Page?api_key=YOUR_KEY
    ///
    /// 注意：端点本身 AllowAnonymous（因为主界面点击跳转过来时不一定带 cookie），
    /// 但页面内 JS 调用 /NextBatch 必须认证通过。api_key 通过 query 传入并注入页面。
    /// </summary>
    [HttpGet("Page")]
    [AllowAnonymous]
    public IActionResult Page([FromQuery] string? api_key)
    {
        _logger.LogInformation("ShortVideo Controller: 收到 /ShortVideo/Page 请求, api_key={HasKey}, UserAgent={UA}",
            string.IsNullOrEmpty(api_key) ? "(空,尝试从请求获取)" : "(有)", Request.Headers.UserAgent.ToString());

        var html = LoadEmbeddedHtml();
        var token = api_key ?? GetApiKeyFromRequest();
        _logger.LogInformation("ShortVideo Controller: Page 使用 token = {Token}", string.IsNullOrEmpty(token) ? "(空)" : token[..Math.Min(8, token.Length)] + "...");
        html = html.Replace("__APIKEY__", token);
        html = html.Replace("__BASEURL__", GetBaseUrl());
        _logger.LogInformation("ShortVideo Controller: 返回 HTML 页面, 大小={Bytes} 字节", html.Length);
        return Content(html, MediaTypeNames.Text.Html);
    }

    /// <summary>
    /// 返回下一条短视频信息。
    /// </summary>
    [HttpGet("Next")]
    [AllowAnonymous]
    public IActionResult Next()
    {
        _logger.LogInformation("ShortVideo Controller: 收到 /ShortVideo/Next 请求");
        var item = _feedService.Next();
        if (item == null)
        {
            _logger.LogWarning("ShortVideo Controller: /Next 返回 null (没有短视频)");
            return NotFound(new { message = "No short videos found. Check library and MaxDurationSeconds." });
        }

        // 用当前请求的 token 替换占位符
        var token = GetApiKey();
        item.StreamUrl = item.StreamUrl.Replace("__APIKEY__", token);
        _logger.LogInformation("ShortVideo Controller: /Next 返回 item: Name={Name}, Duration={Dur}s", item.Name, item.DurationSeconds);
        return Ok(item);
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
        var token = GetApiKey();
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
    /// index.html 里的 &lt;script src="/ShortVideo/Inject.js"&gt; 会加载本端点。
    /// 作用：在左侧主导航插入「短视频」入口按钮，点击跳转 /ShortVideo/Page。
    /// </summary>
    [HttpGet("Inject.js")]
    [AllowAnonymous]
    public IActionResult InjectJs()
    {
        _logger.LogInformation("ShortVideo Controller: 收到 /ShortVideo/Inject.js 请求 (来自主界面加载)");
        var js = NavEntryScript;
        return Content(js, "application/javascript");
    }

    // ---- 工具方法 ----

    /// <summary>
    /// 从当前请求获取 API Key：优先 query，再 header。
    /// 用于 [Authorize] 端点，请求已认证，所以 key 一定存在（或为空但鉴权已通过）。
    /// </summary>
    private string GetApiKey()
    {
        return GetApiKeyFromRequest();
    }

    /// <summary>
    /// 从请求中解析 API Key。AllowAnonymous 端点也可用。
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

        // 兜底：Authorization header
        var auth = Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return auth.Substring("Bearer ".Length);
        }

        // 兜底：Cookie（Jellyfin 登录后可能设置此 cookie）
        if (Request.Cookies.TryGetValue("X-Emby-Token", out var c) && !string.IsNullOrEmpty(c))
        {
            return c;
        }

        return string.Empty;
    }

    private string GetBaseUrl()
    {
        var url = $"{Request.Scheme}://{Request.Host}";
        return url;
    }

    /// <summary>
    /// 读取嵌入资源 Web/shortvideo.html。
    /// </summary>
    private static string LoadEmbeddedHtml()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Jellyfin.Plugin.ShortVideo.Web.shortvideo.html";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return "<html><body><h1>shortvideo.html 嵌入资源缺失</h1></body></html>";
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 注入到主界面的 JS：在主导航插入「短视频」入口。
    /// 轮询等待 SPA 渲染出导航容器后插入，监听 URL 变化防止 SPA 重渲染丢失。
    /// </summary>
    private const string NavEntryScript = @"
(function() {
    'use strict';

    var injected = false;

    function injectButton() {
        if (injected) return;
        if (document.getElementById('shortvideo-fab')) return;
        if (!document.body) return;

        var token = '';
        try {
            if (typeof ApiClient !== 'undefined') {
                if (typeof ApiClient.accessToken === 'function') {
                    token = ApiClient.accessToken() || '';
                } else if (ApiClient.accessToken) {
                    token = ApiClient.accessToken;
                }
            }
            if (!token) {
                var m = document.cookie.match(/X-Emby-Token=([^;]+)/i);
                if (m && m[1]) token = decodeURIComponent(m[1]);
            }
        } catch(e) {}

        var link = document.createElement('a');
        link.id = 'shortvideo-fab';
        link.href = '/ShortVideo/Page' + (token ? '?api_key=' + encodeURIComponent(token) : '');
        link.textContent = '短视频';
        link.style.cssText = [
            'position: fixed',
            'right: 24px',
            'bottom: 24px',
            'padding: 12px 24px',
            'border-radius: 999px',
            'background: rgba(255,255,255,0.65)',
            'backdrop-filter: blur(20px) saturate(180%)',
            '-webkit-backdrop-filter: blur(20px) saturate(180%)',
            'color: #000',
            'font-size: 14px',
            'font-weight: 500',
            'font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, sans-serif',
            'text-decoration: none',
            'box-shadow: 0 4px 24px rgba(0,0,0,0.12), 0 1px 4px rgba(0,0,0,0.08)',
            'border: 1px solid rgba(255,255,255,0.8)',
            'z-index: 9999',
            'transition: transform 0.2s ease, box-shadow 0.2s ease, background 0.2s ease',
            'cursor: pointer',
            'user-select: none',
            'display: inline-flex',
            'align-items: center',
            'gap: 8px'
        ].join(';');

        var icon = document.createElement('span');
        icon.textContent = '▶';
        icon.style.cssText = 'font-size: 12px; opacity: 0.8;';
        link.insertBefore(icon, link.firstChild);

        link.addEventListener('mouseenter', function() {
            link.style.transform = 'translateY(-2px)';
            link.style.boxShadow = '0 8px 32px rgba(0,0,0,0.16), 0 2px 8px rgba(0,0,0,0.1)';
            link.style.background = 'rgba(255,255,255,0.8)';
        });
        link.addEventListener('mouseleave', function() {
            link.style.transform = 'translateY(0)';
            link.style.boxShadow = '0 4px 24px rgba(0,0,0,0.12), 0 1px 4px rgba(0,0,0,0.08)';
            link.style.background = 'rgba(255,255,255,0.65)';
        });

        document.body.appendChild(link);
        injected = true;
        console.log('[ShortVideo] 右下角悬浮按钮已注入');
    }

    // 页面加载完成后注入
    if (document.body) {
        injectButton();
    } else {
        document.addEventListener('DOMContentLoaded', injectButton);
    }

    // SPA 路由切换时也确保按钮存在
    var lastUrl = location.href;
    setInterval(function() {
        if (location.href !== lastUrl) {
            lastUrl = location.href;
            // 延迟一点，等新页面渲染完
            setTimeout(injectButton, 500);
        }
    }, 1000);
})();
";
}
