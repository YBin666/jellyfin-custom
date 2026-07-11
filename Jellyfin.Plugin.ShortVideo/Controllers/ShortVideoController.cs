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
    /// 注入到主界面的 JS：
    /// 1. 在 #/home 和 #/list 路由下显示悬浮按钮
    /// 2. 点击按钮导航到 #/shorts 路由
    /// 3. #/shorts 路由激活时，在 Jellyfin SPA 内部注入全屏短视频容器
    /// 4. #/diy 路由为空页面占位，未来可扩展
    /// 路由系统采用注册表，新增自定义路由只需在 customRoutes 注册一项
    /// </summary>
    private const string NavEntryScript = @"
(function() {
    'use strict';

    var link = null;
    var originalTitle = document.title;
    var previousHash = null; // 进入自定义路由前记录的上一个 hash
    var isNavigatingBack = false; // 防止 history.back() 触发重复处理

    // ---- Token 获取 ----
    function getToken() {
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
        return token;
    }

    // ---- 路由判断 ----
    function isAllowedRoute() {
        return /^#\/(home|list)([\/?]|$)/.test(location.hash || '');
    }

    // 判断当前 hash 是否匹配某个路由名
    function matchRoute(name) {
        return new RegExp('^#/' + name + '([/?]|$)').test(location.hash || '');
    }

    // 是否处于任意自定义路由
    function isCustomRoute() {
        return customRoutes.some(function(r) { return matchRoute(r.name); });
    }

    // ---- 自定义路由注册表 ----
    // 每项结构：{ name, title, show, hide, state }
    //   name:  路由名（不含 #/）
    //   title: 进入时设置的文档标题
    //   show(): 创建容器并渲染页面，返回容器 DOM
    //   hide(): 可选，在路由之间切换时静默清理（默认走通用 cleanup）
    //   state: 运行时状态对象，由 show() 设置
    var customRoutes = [];

    // 注册一个自定义路由
    function registerRoute(cfg) {
        customRoutes.push({
            name: cfg.name,
            title: cfg.title,
            show: cfg.show,
            hide: cfg.hide || null,
            state: null,
            container: null
        });
    }

    // 查找当前激活的自定义路由
    function getActiveRoute() {
        for (var i = 0; i < customRoutes.length; i++) {
            if (matchRoute(customRoutes[i].name)) return customRoutes[i];
        }
        return null;
    }

    // 查找指定名字的路由对象
    function getRoute(name) {
        for (var i = 0; i < customRoutes.length; i++) {
            if (customRoutes[i].name === name) return customRoutes[i];
        }
        return null;
    }

    // ---- 从自定义路由返回 ----
    function goBackFromCustomRoute() {
        cleanupCustomPages();
        isNavigatingBack = true;
        if (previousHash) {
            location.hash = previousHash;
        } else {
            location.hash = '#/home';
        }
        setTimeout(function() { isNavigatingBack = false; }, 100);
    }

    // ---- 纯 DOM 清理（不触发路由跳转） ----
    function cleanupCustomPages() {
        customRoutes.forEach(function(r) {
            if (r.container) {
                if (r.state && r.state.destroy) {
                    try { r.state.destroy(); } catch(e) {}
                }
                r.state = null;
                r.container.remove();
                r.container = null;
            }
        });

        var reactRoot = document.getElementById('reactRoot');
        if (reactRoot) reactRoot.style.display = '';
        document.title = originalTitle;
    }

    // 静默清理某个路由（用于自定义路由之间切换）
    function silentHideRoute(r) {
        if (!r || !r.container) return;
        if (r.hide) {
            r.hide(r);
        } else {
            if (r.state && r.state.destroy) {
                try { r.state.destroy(); } catch(e) {}
            }
            r.state = null;
            r.container.remove();
            r.container = null;
        }
    }

    // ---- 悬浮按钮显示/隐藏 ----
    function updateVisibility() {
        if (!link) return;
        link.style.display = isAllowedRoute() ? 'inline-flex' : 'none';
    }

    // ============================================================
    // ShortsModule：短视频播放器模块（#/shorts 独占）
    // ============================================================
    var ShortsModule = (function() {
        var P = 'sv'; // 样式前缀
        var CONTAINER_ID = 'shortvideo-spa-view';

        var styles = [
            '#' + CONTAINER_ID + ' * { margin: 0; padding: 0; box-sizing: border-box; }',
            '#' + CONTAINER_ID + ' .' + P + '-feed { width: 100%; height: 100%; overflow-y: scroll; scroll-snap-type: y mandatory; scrollbar-width: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-feed::-webkit-scrollbar { display: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-card { width: 100%; height: 100%; scroll-snap-align: start; scroll-snap-stop: always; position: relative; background: #000; display: flex; align-items: center; justify-content: center; overflow: hidden; }',
            '#' + CONTAINER_ID + ' video { max-width: 100%; max-height: 100%; object-fit: contain; background: #000; pointer-events: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-overlay { position: absolute; top: 0; left: 0; width: 100%; height: 100%; z-index: 3; pointer-events: auto; -webkit-tap-highlight-color: transparent; }',
            '#' + CONTAINER_ID + ' .' + P + '-poster { position: absolute; top: 0; left: 0; width: 100%; height: 100%; background-size: cover; background-position: center; background-color: #000; z-index: 1; transition: opacity 0.3s ease; pointer-events: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-poster.hidden { opacity: 0; pointer-events: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-loader { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); z-index: 2; width: 50px; height: 50px; border: 4px solid rgba(255,255,255,0.2); border-top-color: #fff; border-radius: 50%; animation: ' + P + '-spin 0.8s linear infinite; display: none; pointer-events: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-loader.show { display: block; }',
            '@keyframes ' + P + '-spin { to { transform: translate(-50%, -50%) rotate(360deg); } }',
            '#' + CONTAINER_ID + ' .' + P + '-top-gradient { position: absolute; top: 0; left: 0; right: 0; height: 120px; background: linear-gradient(to bottom, rgba(0,0,0,0.6), transparent); z-index: 5; pointer-events: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-bottom-gradient { position: absolute; bottom: 0; left: 0; right: 0; height: 200px; background: linear-gradient(to top, rgba(0,0,0,0.7), transparent); z-index: 5; pointer-events: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-actions { position: absolute; right: 12px; bottom: 120px; display: flex; flex-direction: column; gap: 20px; color: #fff; font-size: 12px; text-align: center; z-index: 10; }',
            '#' + CONTAINER_ID + ' .' + P + '-actions .action-item { display: flex; flex-direction: column; align-items: center; gap: 6px; }',
            '#' + CONTAINER_ID + ' .' + P + '-actions .icon { width: 48px; height: 48px; border-radius: 50%; background: rgba(255,255,255,0.15); backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px); display: flex; align-items: center; justify-content: center; font-size: 24px; cursor: pointer; transition: transform 0.15s ease, background 0.15s ease; border: 1px solid rgba(255,255,255,0.1); }',
            '#' + CONTAINER_ID + ' .' + P + '-actions .icon:active { transform: scale(0.9); background: rgba(255,255,255,0.25); }',
            '#' + CONTAINER_ID + ' .' + P + '-actions .icon.like.liked { color: #ff4757; background: rgba(255,71,87,0.2); }',
            '#' + CONTAINER_ID + ' .' + P + '-actions .count { font-size: 12px; text-shadow: 0 1px 3px rgba(0,0,0,0.8); font-weight: 500; }',
            '#' + CONTAINER_ID + ' .' + P + '-caption { position: absolute; left: 16px; right: 80px; bottom: 80px; color: #fff; z-index: 10; }',
            '#' + CONTAINER_ID + ' .' + P + '-caption .title { font-weight: 600; font-size: 16px; margin-bottom: 6px; text-shadow: 0 2px 8px rgba(0,0,0,0.8); line-height: 1.4; }',
            '#' + CONTAINER_ID + ' .' + P + '-caption .meta { font-size: 13px; opacity: 0.85; text-shadow: 0 1px 4px rgba(0,0,0,0.8); }',
            '#' + CONTAINER_ID + ' .' + P + '-back { position: absolute; top: 16px; left: 16px; z-index: 20; width: 40px; height: 40px; border-radius: 50%; background: rgba(0,0,0,0.3); backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px); display: flex; align-items: center; justify-content: center; color: #fff; font-size: 28px; text-decoration: none; line-height: 1; border: 1px solid rgba(255,255,255,0.1); cursor: pointer; }',
            '#' + CONTAINER_ID + ' .' + P + '-back svg { display: block; }',
            '#' + CONTAINER_ID + ' .' + P + '-empty { color: #888; text-align: center; padding: 40px; }',
            '#' + CONTAINER_ID + ' .' + P + '-center-anim { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%) scale(0.5); width: 80px; height: 80px; border-radius: 50%; background: rgba(0,0,0,0.5); backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px); display: flex; align-items: center; justify-content: center; color: #fff; font-size: 36px; opacity: 0; pointer-events: none; z-index: 15; transition: opacity 0.2s ease, transform 0.2s ease; }',
            '#' + CONTAINER_ID + ' .' + P + '-center-anim.show { opacity: 1; transform: translate(-50%, -50%) scale(1); }',
            '#' + CONTAINER_ID + ' .' + P + '-heart { position: absolute; pointer-events: none; z-index: 20; font-size: 80px; animation: ' + P + '-heart-pop 0.8s ease forwards; }',
            '@keyframes ' + P + '-heart-pop { 0% { opacity: 0; transform: translate(-50%, -50%) scale(0.3); } 20% { opacity: 1; transform: translate(-50%, -50%) scale(1.2); } 40% { transform: translate(-50%, -50%) scale(0.95); } 60% { transform: translate(-50%, -50%) scale(1); } 100% { opacity: 0; transform: translate(-50%, -50%) scale(0.8) translateY(-30px); } }',
            '#' + CONTAINER_ID + ' .' + P + '-controls { position: absolute; left: 0; right: 0; bottom: 0; padding: 12px 16px 16px; z-index: 10; background: linear-gradient(to top, rgba(0,0,0,0.6), transparent); }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-container { position: relative; width: 100%; height: 20px; display: flex; align-items: center; cursor: pointer; margin-top: 4px; }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-bg { position: absolute; width: 100%; height: 3px; background: rgba(255,255,255,0.25); border-radius: 2px; }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-buffer { position: absolute; height: 3px; background: rgba(255,255,255,0.4); border-radius: 2px; width: 0%; }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-played { position: absolute; height: 3px; background: #fff; border-radius: 2px; width: 0%; }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-handle { position: absolute; width: 14px; height: 14px; background: #fff; border-radius: 50%; transform: translateX(-50%) scale(0); transition: transform 0.15s ease; box-shadow: 0 2px 8px rgba(0,0,0,0.3); }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-container:hover .' + P + '-progress-handle, #' + CONTAINER_ID + ' .' + P + '-progress-container.dragging .' + P + '-progress-handle { transform: translateX(-50%) scale(1); }',
            '#' + CONTAINER_ID + ' .' + P + '-controls-bar { display: flex; align-items: center; gap: 12px; color: #fff; }',
            '#' + CONTAINER_ID + ' .' + P + '-controls-bar button { background: none; border: none; color: #fff; cursor: pointer; padding: 4px; font-size: 20px; display: flex; align-items: center; justify-content: center; transition: transform 0.15s ease; }',
            '#' + CONTAINER_ID + ' .' + P + '-controls-bar button:active { transform: scale(0.85); }',
            '#' + CONTAINER_ID + ' .' + P + '-time { font-size: 13px; font-variant-numeric: tabular-nums; text-shadow: 0 1px 3px rgba(0,0,0,0.8); min-width: 40px; }',
            '#' + CONTAINER_ID + ' .' + P + '-time-sep { font-size: 13px; opacity: 0.6; }',
            '#' + CONTAINER_ID + ' .' + P + '-spacer { flex: 1; }',
            '#' + CONTAINER_ID + ' .' + P + '-rate { font-size: 13px; font-weight: 500; padding: 4px 8px; border-radius: 4px; background: rgba(255,255,255,0.15); backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px); cursor: pointer; user-select: none; min-width: 40px; text-align: center; transition: background 0.15s ease; }',
            '#' + CONTAINER_ID + ' .' + P + '-rate:active { background: rgba(255,255,255,0.25); }',
            '#' + CONTAINER_ID + ' .' + P + '-rate-menu { position: absolute; bottom: 50px; right: 16px; background: rgba(0,0,0,0.75); backdrop-filter: blur(20px); -webkit-backdrop-filter: blur(20px); border-radius: 12px; padding: 8px; display: none; flex-direction: column; gap: 4px; min-width: 80px; border: 1px solid rgba(255,255,255,0.1); z-index: 30; }',
            '#' + CONTAINER_ID + ' .' + P + '-rate-menu.show { display: flex; }',
            '#' + CONTAINER_ID + ' .' + P + '-rate-menu .rate-option { padding: 8px 12px; border-radius: 8px; cursor: pointer; font-size: 14px; text-align: center; color: #fff; transition: background 0.15s ease; }',
            '#' + CONTAINER_ID + ' .' + P + '-rate-menu .rate-option:hover { background: rgba(255,255,255,0.1); }',
            '#' + CONTAINER_ID + ' .' + P + '-rate-menu .rate-option.active { background: rgba(255,255,255,0.2); font-weight: 600; }',
            '#' + CONTAINER_ID + ' .' + P + '-lucide { width: 20px; height: 20px; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; fill: none; vertical-align: middle; }'
        ].join('\n');

        function buildContainer() {
            var container = document.createElement('div');
            container.id = CONTAINER_ID;
            container.style.cssText = [
                'position: fixed',
                'top: 0', 'left: 0',
                'width: 100%', 'height: 100%',
                'z-index: 9998',
                'background: #000',
                'overflow: hidden',
                'font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, sans-serif',
                '-webkit-user-select: none', 'user-select: none',
                '-webkit-tap-highlight-color: transparent'
            ].join(';');

            var style = document.createElement('style');
            style.textContent = styles;
            container.appendChild(style);

            var backBtn = document.createElement('div');
            backBtn.className = P + '-back';
            backBtn.innerHTML = '<svg class=""' + P + '-lucide"" xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><polyline points=""15 18 9 12 15 6""></polyline></svg>';
            backBtn.addEventListener('click', function(e) {
                e.preventDefault();
                goBackFromCustomRoute();
            });

            var feed = document.createElement('div');
            feed.className = P + '-feed';

            container.appendChild(backBtn);
            container.appendChild(feed);
            return container;
        }

        // 内嵌播放器初始化（ShortsModule 独占）
        function initPlayer(container, feed) {
            var BASE = window.location.origin;
            var API_KEY = getToken();
            var globalMuted = true;
            var globalVolume = 1;
            var firstBatchLoaded = false;
            var isLoading = false;

            var transcodeQueue = [];
            var transcodeActive = 0;
            var MAX_CONCURRENT = 2;

            function scheduleTranscode(fn) {
                if (transcodeActive < MAX_CONCURRENT) { transcodeActive++; fn(); }
                else { transcodeQueue.push(fn); }
            }
            function transcodeDone() {
                if (transcodeActive > 0) transcodeActive--;
                if (transcodeQueue.length > 0 && transcodeActive < MAX_CONCURRENT) {
                    var next = transcodeQueue.shift();
                    transcodeActive++;
                    next();
                }
            }

            function apiUrl(path) {
                var url = BASE + path;
                if (API_KEY) {
                    var sep = path.indexOf('?') >= 0 ? '&' : '?';
                    url += sep + 'api_key=' + encodeURIComponent(API_KEY);
                }
                return url;
            }

            function formatTime(s) {
                if (!isFinite(s) || s < 0) return '00:00';
                var m = Math.floor(s / 60), sec = Math.floor(s % 60);
                return (m < 10 ? '0' : '') + m + ':' + (sec < 10 ? '0' : '') + sec;
            }

            function formatLikeCount(n) {
                if (n >= 10000) return (n / 10000).toFixed(1) + 'w';
                if (n >= 1000) return (n / 1000).toFixed(1) + 'k';
                return n.toString();
            }

            function setIcon(el, name) {
                if (!el) return;
                if (window.lucide && lucide.icons && lucide.icons[name]) {
                    el.innerHTML = lucide.icons[name].toSvg({
                        'stroke-width': 2, 'stroke-linecap': 'round', 'stroke-linejoin': 'round', fill: 'none'
                    });
                }
            }

            function ensureApiKey(url, key) {
                if (key && key.length >= 10) {
                    if (url.indexOf('api_key=') >= 0) {
                        return url.replace(/([?&])api_key=[^&]*/, '$1api_key=' + encodeURIComponent(key));
                    }
                    var sep = url.indexOf('?') >= 0 ? '&' : '?';
                    return url + sep + 'api_key=' + encodeURIComponent(key);
                }
                return url;
            }

            function syncMuteAll() {
                feed.querySelectorAll('.' + P + '-card').forEach(function(card) {
                    var v = card._video;
                    var muteBtn = card.querySelector('.mute-icon');
                    if (v) { v.muted = globalMuted; v.volume = globalVolume; }
                    if (muteBtn && muteBtn._setIcon) muteBtn._setIcon(globalMuted ? 'volume-x' : 'volume-2');
                });
            }

            function loadBatch() {
                if (isLoading) return;
                isLoading = true;
                fetch(apiUrl('/ShortVideo/NextBatch'), { credentials: 'include' })
                    .then(function(r) {
                        if (!r.ok) throw new Error('HTTP ' + r.status);
                        return r.json();
                    })
                    .then(function(items) {
                        isLoading = false;
                        if (!items || items.length === 0) {
                            if (!firstBatchLoaded) {
                                feed.innerHTML = '<div class=""' + P + '-empty"">没有找到短视频。</div>';
                            }
                            return;
                        }
                        var isFirst = !firstBatchLoaded;
                        items.forEach(appendCard);
                        firstBatchLoaded = true;
                        if (isFirst) {
                            setTimeout(function() { playVisible(); }, 200);
                        }
                    })
                    .catch(function(e) {
                        isLoading = false;
                        console.error('[ShortsModule] loadBatch error:', e);
                        if (!firstBatchLoaded) {
                            feed.innerHTML = '<div class=""' + P + '-empty"">加载失败：' + e.message + '</div>';
                        }
                    });
            }

            function appendCard(item) {
                var card = document.createElement('div');
                card.className = P + '-card';
                card.dataset.id = item.id || item.Id || '';

                var streamUrl = item.streamUrl || item.StreamUrl || '';
                var name = item.name || item.Name || '';
                var duration = item.durationSeconds || item.DurationSeconds || 0;
                var videoCodec = (item.videoCodec || item.VideoCodec || '').toLowerCase();
                var audioCodec = (item.audioCodec || item.AudioCodec || '').toLowerCase();
                var containerFmt = (item.container || item.Container || '').toLowerCase();

                if (!streamUrl) return;

                var src = streamUrl.indexOf('http') === 0 ? streamUrl : BASE + streamUrl;
                src = ensureApiKey(src, API_KEY);

                var transcodeParams = 'VideoCodec=h264&AudioCodec=aac&VideoBitrate=4000000&AudioBitrate=192000';
                var hlsSrc = '';
                var streamMatch = src.match(/\/Videos\/([^/]+)\/stream\?(.*)/);
                if (streamMatch) {
                    var videoId = streamMatch[1];
                    var qs = streamMatch[2]
                        .replace(/(^|&)static=true&?/i, '$1')
                        .replace(/(^|&)api_key=[^&]*/i, '$1')
                        .replace(/^&+/, '').replace(/&+$/, '');
                    hlsSrc = BASE + '/Videos/' + videoId + '/main.m3u8?'
                        + (qs ? qs + '&' : '')
                        + 'api_key=' + encodeURIComponent(API_KEY) + '&'
                        + transcodeParams;
                } else {
                    var idForHls = card.dataset.id;
                    if (idForHls) {
                        hlsSrc = BASE + '/Videos/' + idForHls + '/main.m3u8?api_key='
                            + encodeURIComponent(API_KEY) + '&' + transcodeParams;
                    }
                }

                card.innerHTML =
                    '<div class=""' + P + '-top-gradient""></div>' +
                    '<div class=""' + P + '-bottom-gradient""></div>' +
                    '<div class=""' + P + '-poster""></div>' +
                    '<div class=""' + P + '-loader""></div>' +
                    '<video preload=""metadata"" playsinline webkit-playsinline x-webkit-airplay=""deny"" disablepictureinpicture controlsList=""nodownload noplaybackrate noremoteplayback"" loop muted></video>' +
                    '<div class=""' + P + '-overlay""></div>' +
                    '<div class=""' + P + '-center-anim""></div>' +
                    '<div class=""' + P + '-caption""><div class=""title""></div><div class=""meta""></div></div>' +
                    '<div class=""' + P + '-actions"">' +
                      '<div class=""action-item""><div class=""icon like""><svg class=""' + P + '-lucide"" xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><path d=""M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z""></path></svg></div><span class=""count like-count"">0</span></div>' +
                      '<div class=""action-item""><div class=""icon mute-icon""><svg class=""' + P + '-lucide"" xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><polygon points=""11 5 6 9 2 9 2 15 6 15 11 19 11 5""></polygon><line x1=""23"" y1=""9"" x2=""17"" y2=""15""></line><line x1=""17"" y1=""9"" x2=""23"" y2=""15""></line></svg></div></div>' +
                    '</div>' +
                    '<div class=""' + P + '-controls"">' +
                      '<div class=""' + P + '-controls-bar"">' +
                        '<button class=""btn-play""><svg class=""' + P + '-lucide"" xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><polygon points=""5 3 19 12 5 21 5 3""></polygon></svg></button>' +
                        '<span class=""' + P + '-time time-current"">00:00</span>' +
                        '<span class=""' + P + '-time-sep"">/</span>' +
                        '<span class=""' + P + '-time time-total"">00:00</span>' +
                        '<div class=""' + P + '-spacer""></div>' +
                        '<div class=""' + P + '-rate"">1x</div>' +
                      '</div>' +
                      '<div class=""' + P + '-progress-container"">' +
                        '<div class=""' + P + '-progress-bg""></div>' +
                        '<div class=""' + P + '-progress-buffer""></div>' +
                        '<div class=""' + P + '-progress-played""></div>' +
                        '<div class=""' + P + '-progress-handle""></div>' +
                      '</div>' +
                      '<div class=""' + P + '-rate-menu"">' +
                        '<div class=""rate-option"" data-rate=""0.5"">0.5x</div>' +
                        '<div class=""rate-option active"" data-rate=""1"">1x</div>' +
                        '<div class=""rate-option"" data-rate=""1.5"">1.5x</div>' +
                        '<div class=""rate-option"" data-rate=""2"">2x</div>' +
                      '</div>' +
                    '</div>';

                card.querySelector('.title').textContent = name;
                card.querySelector('.meta').textContent = Math.round(duration) + 's';

                var posterEl = card.querySelector('.' + P + '-poster');
                var loaderEl = card.querySelector('.' + P + '-loader');
                posterEl.style.backgroundImage = 'url(""' + BASE + '/Items/' + card.dataset.id + '/Images/Primary?fillHeight=1080&fillWidth=720&quality=80"")';

                var v = card.querySelector('video');
                var initialized = false;
                var triedTranscode = false;
                var hls = null;
                var srcLoaded = false;
                var isDragging = false;
                var likeCount = Math.floor(Math.random() * 1000) + 100;
                var isLiked = false;

                var likeIcon = card.querySelector('.icon.like');
                var likeCountEl = card.querySelector('.like-count');
                var btnPlay = card.querySelector('.btn-play');
                var muteIcon = card.querySelector('.mute-icon');
                var timeCurrent = card.querySelector('.time-current');
                var timeTotal = card.querySelector('.time-total');
                var progressContainer = card.querySelector('.' + P + '-progress-container');
                var progressPlayed = card.querySelector('.' + P + '-progress-played');
                var progressBuffer = card.querySelector('.' + P + '-progress-buffer');
                var progressHandle = card.querySelector('.' + P + '-progress-handle');
                var rateBtn = card.querySelector('.' + P + '-rate');
                var rateMenu = card.querySelector('.' + P + '-rate-menu');
                var centerAnim = card.querySelector('.' + P + '-center-anim');

                likeCountEl.textContent = formatLikeCount(likeCount);

                function showLoading() { loaderEl.classList.add('show'); posterEl.classList.remove('hidden'); }
                function hideLoading() { loaderEl.classList.remove('show'); posterEl.classList.add('hidden'); }

                function isVideoCodecSupported(codec) {
                    if (!codec) return null;
                    var supported = ['h264', 'avc', 'avc1', 'mpeg4', 'mp4v'];
                    if (codec === 'vp8' || codec === 'vp9') {
                        try { return v.canPlayType('video/webm; codecs=' + codec) !== ''; } catch(e) { return false; }
                    }
                    if (codec === 'hevc' || codec === 'h265') {
                        try { return v.canPlayType('video/mp4; codecs=hev1') !== '' || v.canPlayType('video/mp4; codecs=hvc1') !== ''; } catch(e) { return false; }
                    }
                    if (codec === 'av1' || codec === 'av01') {
                        try { return v.canPlayType('video/mp4; codecs=av01.0.05M.08') !== ''; } catch(e) { return false; }
                    }
                    if (codec.indexOf('mpeg2') >= 0 || codec.indexOf('msmpeg4') >= 0 || codec === 'wmv3' || codec === 'wmv2' || codec === 'vc1') return false;
                    return supported.indexOf(codec) >= 0;
                }

                function isAudioCodecSupported(codec) {
                    if (!codec) return null;
                    var supported = ['aac', 'mp3', 'mp2', 'opus', 'vorbis', 'flac', 'pcm'];
                    if (codec === 'ac3' || codec === 'eac3' || codec === 'dts' || codec === 'truehd') return false;
                    return supported.indexOf(codec) >= 0;
                }

                function shouldDirectStream() {
                    var videoOk = isVideoCodecSupported(videoCodec);
                    var audioOk = isAudioCodecSupported(audioCodec);
                    if (videoOk === false || audioOk === false) return false;
                    var unsupported = ['avi', 'mkv', 'mov', 'wmv', 'flv', 'rmvb', 'rm', 'ts', 'm2ts'];
                    if (containerFmt && unsupported.indexOf(containerFmt) >= 0) return false;
                    return true;
                }

                var useTranscode = !shouldDirectStream();

                function initPlayer() {
                    if (initialized) return;
                    initialized = true;
                    showLoading();
                    if (useTranscode && hlsSrc) {
                        if (hls) { v.play().catch(function(){}); }
                        else { triedTranscode = true; fallbackToHls(); }
                    } else {
                        if (srcLoaded) { v.play().catch(function(){}); }
                        else { v.src = src; v.load(); }
                    }
                }

                v.addEventListener('playing', function() { hideLoading(); setIcon(btnPlay, 'pause'); });
                v.addEventListener('pause', function() { setIcon(btnPlay, 'play'); });
                v.addEventListener('waiting', function() { showLoading(); });

                v.addEventListener('timeupdate', function() {
                    if (isDragging) return;
                    var pct = v.duration ? (v.currentTime / v.duration) * 100 : 0;
                    progressPlayed.style.width = pct + '%';
                    progressHandle.style.left = pct + '%';
                    timeCurrent.textContent = formatTime(v.currentTime);
                });

                v.addEventListener('loadedmetadata', function() {
                    timeTotal.textContent = formatTime(v.duration);
                    if (triedTranscode) return;
                    var w = v.videoWidth || 0, h = v.videoHeight || 0;
                    if (w === 0 || h === 0) {
                        triedTranscode = true;
                        console.warn('[ShortsModule] no video track, falling back to HLS');
                        fallbackToHls();
                    }
                });

                v.addEventListener('progress', function() {
                    if (v.buffered && v.buffered.length > 0 && v.duration) {
                        progressBuffer.style.width = ((v.buffered.end(v.buffered.length - 1) / v.duration) * 100) + '%';
                    }
                });

                function destroyPlayer() {
                    if (hls) {
                        try { hls.stopLoad(); hls.detachMedia(); hls.destroy(); } catch(e) {}
                        hls = null;
                    }
                    v.pause();
                    v.removeAttribute('src');
                    v.load();
                    initialized = false;
                    triedTranscode = false;
                    srcLoaded = false;
                    card._prefetched = false;
                    if (card._prefetchSlotTaken) {
                        card._prefetchSlotTaken = false;
                        transcodeDone();
                    }
                    progressPlayed.style.width = '0%';
                    progressBuffer.style.width = '0%';
                    progressHandle.style.left = '0%';
                    timeCurrent.textContent = '00:00';
                    timeTotal.textContent = '00:00';
                    showLoading();
                }

                v.addEventListener('error', function() {
                    if (!triedTranscode && hlsSrc && hlsSrc !== src) {
                        triedTranscode = true;
                        fallbackToHls();
                    }
                });

                v.addEventListener('play', function() {
                    if (triedTranscode) return;
                    setTimeout(function() {
                        if (triedTranscode) return;
                        if ((v.videoWidth || 0) === 0 || (v.videoHeight || 0) === 0) {
                            triedTranscode = true;
                            fallbackToHls();
                        }
                    }, 2000);
                });

                function fallbackToHls() {
                    if (hls) { v.play().catch(function(){}); return; }
                    v.pause();
                    v.removeAttribute('src');
                    if (v.canPlayType('application/vnd.apple.mpegurl')) {
                        v.src = hlsSrc; v.load(); v.play().catch(function(){}); return;
                    }
                    if (window.Hls && Hls.isSupported()) {
                        var retries = 0;
                        hls = new Hls({
                            enableWorker: true, lowLatencyMode: false,
                            backBufferLength: 30, maxBufferLength: 15, maxMaxBufferLength: 30, startFragPrefetch: true
                        });
                        hls.loadSource(hlsSrc);
                        hls.attachMedia(v);
                        hls.on(Hls.Events.MANIFEST_PARSED, function() { v.play().catch(function(){}); });
                        hls.on(Hls.Events.ERROR, function(event, data) {
                            if (data.fatal) {
                                if (data.type === Hls.ErrorTypes.NETWORK_ERROR && retries < 2) {
                                    retries++;
                                    setTimeout(function() { hls.startLoad(); }, 1000 * retries);
                                }
                            }
                        });
                        hls.on(Hls.Events.FRAG_BUFFERED, function() {
                            if (v.buffered && v.buffered.length > 0 && v.duration) {
                                progressBuffer.style.width = ((v.buffered.end(v.buffered.length - 1) / v.duration) * 100) + '%';
                            }
                        });
                    }
                }

                function showCenterAnim(playState) {
                    setIcon(centerAnim, playState ? 'play' : 'pause');
                    centerAnim.classList.add('show');
                    setTimeout(function() { centerAnim.classList.remove('show'); }, 300);
                }

                function togglePlay() {
                    if (v.paused) { v.play().catch(function(){}); showCenterAnim(true); }
                    else { v.pause(); showCenterAnim(false); }
                }

                var lastClickTime = 0;
                card.addEventListener('click', function(e) {
                    if (e.target.closest('.' + P + '-actions')) return;
                    if (e.target.closest('.' + P + '-controls')) return;
                    var now = Date.now();
                    if (now - lastClickTime < 300) {
                        e.preventDefault();
                        handleDoubleTap(e);
                        lastClickTime = 0;
                        return;
                    }
                    lastClickTime = now;
                    setTimeout(function() {
                        if (Date.now() - lastClickTime >= 290 && lastClickTime !== 0) {
                            togglePlay();
                            lastClickTime = 0;
                        }
                    }, 300);
                });

                function handleDoubleTap(e) {
                    toggleLike();
                    var heart = document.createElement('div');
                    heart.className = P + '-heart';
                    heart.innerHTML = '<svg width=""80"" height=""80"" viewBox=""0 0 24 24"" fill=""#ff4757"" stroke=""#ff4757"" stroke-width=""2""><path d=""M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z""></path></svg>';
                    var rect = card.getBoundingClientRect();
                    heart.style.left = (e.clientX - rect.left) + 'px';
                    heart.style.top = (e.clientY - rect.top) + 'px';
                    heart.style.transform = 'translate(-50%, -50%)';
                    card.appendChild(heart);
                    setTimeout(function() { heart.remove(); }, 800);
                }

                function toggleLike() {
                    isLiked = !isLiked;
                    if (isLiked) { likeCount++; likeIcon.classList.add('liked'); }
                    else { likeCount--; likeIcon.classList.remove('liked'); }
                    likeCountEl.textContent = formatLikeCount(likeCount);
                }

                likeIcon.addEventListener('click', function(e) { e.stopPropagation(); toggleLike(); });
                btnPlay.addEventListener('click', function(e) { e.stopPropagation(); togglePlay(); });
                muteIcon.addEventListener('click', function(e) {
                    e.stopPropagation();
                    globalMuted = !globalMuted;
                    syncMuteAll();
                });

                progressContainer.addEventListener('mousedown', function(e) { e.stopPropagation(); startDrag(e); });
                progressContainer.addEventListener('touchstart', function(e) { e.stopPropagation(); startDrag(e.touches[0]); });

                function startDrag(e) {
                    isDragging = true;
                    progressContainer.classList.add('dragging');
                    updateProgressFromEvent(e);
                    document.addEventListener('mousemove', onDragMove);
                    document.addEventListener('mouseup', onDragEnd);
                    document.addEventListener('touchmove', onTouchMove);
                    document.addEventListener('touchend', onTouchEnd);
                }
                function onDragMove(e) { if (isDragging) updateProgressFromEvent(e); }
                function onTouchMove(e) { if (isDragging) updateProgressFromEvent(e.touches[0]); }
                function onDragEnd(e) { if (isDragging) { updateProgressFromEvent(e); finishDrag(); } }
                function onTouchEnd(e) { if (isDragging) finishDrag(); }
                function finishDrag() {
                    isDragging = false;
                    progressContainer.classList.remove('dragging');
                    document.removeEventListener('mousemove', onDragMove);
                    document.removeEventListener('mouseup', onDragEnd);
                    document.removeEventListener('touchmove', onTouchMove);
                    document.removeEventListener('touchend', onTouchEnd);
                }
                function updateProgressFromEvent(e) {
                    var rect = progressContainer.getBoundingClientRect();
                    var pct = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
                    progressPlayed.style.width = (pct * 100) + '%';
                    progressHandle.style.left = (pct * 100) + '%';
                    if (v.duration) { v.currentTime = pct * v.duration; timeCurrent.textContent = formatTime(pct * v.duration); }
                }

                rateBtn.addEventListener('click', function(e) { e.stopPropagation(); rateMenu.classList.toggle('show'); });
                rateMenu.querySelectorAll('.rate-option').forEach(function(opt) {
                    opt.addEventListener('click', function(e) {
                        e.stopPropagation();
                        var rate = parseFloat(opt.dataset.rate);
                        v.playbackRate = rate;
                        rateBtn.textContent = rate + 'x';
                        rateMenu.querySelectorAll('.rate-option').forEach(function(o) { o.classList.remove('active'); });
                        opt.classList.add('active');
                        rateMenu.classList.remove('show');
                    });
                });

                card._initPlayer = initPlayer;
                card._destroyPlayer = destroyPlayer;
                card._video = v;
                card._prefetched = false;
                card._prefetchSlotTaken = false;

                card._prefetch = function() {
                    if (card._prefetched) return;
                    card._prefetched = true;
                    if (useTranscode && hlsSrc) {
                        var doPrefetch = function() {
                            if (!card._prefetched) { transcodeDone(); return; }
                            card._prefetchSlotTaken = true;
                            if (window.Hls && Hls.isSupported()) {
                                var retries = 0;
                                hls = new Hls({
                                    enableWorker: true, lowLatencyMode: false,
                                    backBufferLength: 30, maxBufferLength: 15, maxMaxBufferLength: 30, startFragPrefetch: true
                                });
                                hls.loadSource(hlsSrc);
                                hls.attachMedia(v);
                                triedTranscode = true;
                                hls.on(Hls.Events.MANIFEST_PARSED, function() {
                                    card._prefetchSlotTaken = false;
                                    transcodeDone();
                                });
                                hls.on(Hls.Events.ERROR, function(event, data) {
                                    if (data.fatal) {
                                        if (data.type === Hls.ErrorTypes.NETWORK_ERROR && retries < 2) {
                                            retries++;
                                            setTimeout(function() { hls.startLoad(); }, 1000 * retries);
                                        } else {
                                            try { hls.destroy(); } catch(e) {}
                                            hls = null;
                                            card._prefetched = false;
                                            card._prefetchSlotTaken = false;
                                            triedTranscode = false;
                                            transcodeDone();
                                        }
                                    }
                                });
                            } else if (v.canPlayType('application/vnd.apple.mpegurl')) {
                                v.src = hlsSrc; v.preload = 'auto'; v.load();
                                triedTranscode = true;
                                card._prefetchSlotTaken = false;
                                transcodeDone();
                            } else {
                                card._prefetchSlotTaken = false;
                                transcodeDone();
                            }
                        };
                        scheduleTranscode(doPrefetch);
                    } else {
                        v.src = src; v.preload = 'auto'; v.load();
                        srcLoaded = true;
                    }
                };

                v.muted = globalMuted;
                v.volume = globalVolume;
                btnPlay._setIcon = function(name) { setIcon(btnPlay, name); };
                muteIcon._setIcon = function(name) { setIcon(muteIcon, name); };

                feed.appendChild(card);
            }

            var snapTimer;
            function onScroll() {
                clearTimeout(snapTimer);
                snapTimer = setTimeout(function() {
                    playVisible();
                    if (feed.scrollTop + feed.clientHeight > feed.scrollHeight - feed.clientHeight) {
                        loadBatch();
                    }
                }, 120);
            }
            feed.addEventListener('scroll', onScroll);

            function playVisible() {
                var cards = feed.querySelectorAll('.' + P + '-card');
                var center = feed.scrollTop + feed.clientHeight / 2;
                var visibleIndex = -1;
                for (var i = 0; i < cards.length; i++) {
                    var top = cards[i].offsetTop, bottom = top + cards[i].offsetHeight;
                    if (center >= top && center < bottom) { visibleIndex = i; break; }
                }
                cards.forEach(function(card, i) {
                    var v = card._video;
                    var isVisible = i === visibleIndex;
                    if (v) { v.muted = globalMuted; v.volume = globalVolume; }
                    if (isVisible) {
                        if (card._initPlayer) card._initPlayer();
                        v.play().catch(function() {});
                    } else {
                        v.pause();
                        if (Math.abs(i - visibleIndex) > 3 && card._destroyPlayer) card._destroyPlayer();
                    }
                });
                if (visibleIndex >= 0) {
                    for (var j = 1; j <= 3; j++) {
                        var nextIdx = visibleIndex + j;
                        if (nextIdx < cards.length && cards[nextIdx]._prefetch) cards[nextIdx]._prefetch();
                    }
                    var remaining = cards.length - visibleIndex - 1;
                    if (remaining <= 3 && !isLoading) loadBatch();
                }
            }

            function onKeydown(e) {
                if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
                    e.preventDefault();
                    feed.scrollBy({ top: e.key === 'ArrowDown' ? feed.clientHeight : -feed.clientHeight, behavior: 'smooth' });
                }
            }
            document.addEventListener('keydown', onKeydown);

            var state = {
                feed: feed,
                onScroll: onScroll,
                onKeydown: onKeydown,
                destroy: function() {
                    feed.querySelectorAll('.' + P + '-card').forEach(function(card) {
                        if (card._destroyPlayer) card._destroyPlayer();
                    });
                    feed.removeEventListener('scroll', onScroll);
                    document.removeEventListener('keydown', onKeydown);
                }
            };

            var scriptsToLoad = [];
            if (!window.Hls) {
                scriptsToLoad.push('https://cdn.jsdelivr.net/npm/hls.js@1.5.13/dist/hls.min.js');
            }
            if (!window.lucide) {
                scriptsToLoad.push('https://unpkg.com/lucide@latest/dist/umd/lucide.js');
            }

            function loadScripts(urls, callback) {
                if (urls.length === 0) { callback(); return; }
                var url = urls.shift();
                var s = document.createElement('script');
                s.src = url;
                s.onload = function() { loadScripts(urls, callback); };
                s.onerror = function() { console.warn('[ShortsModule] failed to load:', url); loadScripts(urls, callback); };
                document.head.appendChild(s);
            }

            loadScripts(scriptsToLoad, function() {
                console.log('[ShortsModule] libraries loaded, starting feed');
                loadBatch();
            });

            return state;
        }

        // 对外暴露
        return {
            buildContainer: buildContainer,
            initPlayer: initPlayer
        };
    })();

    // ============================================================
    // DiyModule：#/diy 空页面占位
    // ============================================================
    var DiyModule = (function() {
        var CONTAINER_ID = 'shortvideo-diy-view';

        function buildContainer() {
            var container = document.createElement('div');
            container.id = CONTAINER_ID;
            container.style.cssText = [
                'position: fixed',
                'top: 0', 'left: 0',
                'width: 100%', 'height: 100%',
                'z-index: 9997',
                'background: #101010',
                'overflow: hidden',
                'font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, sans-serif',
                '-webkit-user-select: none', 'user-select: none',
                '-webkit-tap-highlight-color: transparent',
                'color: #fff'
            ].join(';');

            var style = document.createElement('style');
            style.textContent = [
                '#' + CONTAINER_ID + ' .diy-header { position: absolute; top: 0; left: 0; right: 0; height: 56px; display: flex; align-items: center; padding: 0 16px; background: rgba(0,0,0,0.4); backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px); z-index: 20; border-bottom: 1px solid rgba(255,255,255,0.08); }',
                '#' + CONTAINER_ID + ' .diy-back { width: 40px; height: 40px; border-radius: 50%; background: rgba(255,255,255,0.1); display: flex; align-items: center; justify-content: center; cursor: pointer; transition: background 0.15s ease; border: none; color: #fff; }',
                '#' + CONTAINER_ID + ' .diy-back:active { background: rgba(255,255,255,0.2); }',
                '#' + CONTAINER_ID + ' .diy-back svg { display: block; width: 24px; height: 24px; }',
                '#' + CONTAINER_ID + ' .diy-title { flex: 1; text-align: center; font-size: 16px; font-weight: 600; }',
                '#' + CONTAINER_ID + ' .diy-spacer { width: 40px; }',
                '#' + CONTAINER_ID + ' .diy-content { position: absolute; top: 56px; left: 0; right: 0; bottom: 0; overflow-y: auto; padding: 24px; color: #888; text-align: center; }'
            ].join('\n');
            container.appendChild(style);

            // 顶部 header（返回按钮 + 标题）
            var header = document.createElement('div');
            header.className = 'diy-header';

            var backBtn = document.createElement('button');
            backBtn.className = 'diy-back';
            backBtn.innerHTML = '<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""><polyline points=""15 18 9 12 15 6""></polyline></svg>';
            backBtn.addEventListener('click', function(e) {
                e.preventDefault();
                goBackFromCustomRoute();
            });

            var title = document.createElement('div');
            title.className = 'diy-title';
            title.textContent = 'DIY';

            var spacer = document.createElement('div');
            spacer.className = 'diy-spacer';

            header.appendChild(backBtn);
            header.appendChild(title);
            header.appendChild(spacer);

            // 空内容容器
            var content = document.createElement('div');
            content.className = 'diy-content';
            content.textContent = '这是一个空白的 DIY 页面，待填充内容。';

            container.appendChild(header);
            container.appendChild(content);
            return container;
        }

        return {
            buildContainer: buildContainer
        };
    })();

    // ---- 注册所有自定义路由 ----
    // #/shorts：短视频播放器
    registerRoute({
        name: 'shorts',
        title: '短视频 - Jellyfin',
        show: function() {
            var container = ShortsModule.buildContainer();
            document.body.appendChild(container);
            var reactRoot = document.getElementById('reactRoot');
            if (reactRoot) reactRoot.style.display = 'none';
            console.log('[ShortVideo] #/shorts 路由激活，初始化内嵌播放器');
            var feed = container.querySelector('.sv-feed');
            var state = ShortsModule.initPlayer(container, feed);
            return { container: container, state: state };
        }
    });

    // #/diy：空页面占位
    registerRoute({
        name: 'diy',
        title: 'DIY - Jellyfin',
        show: function() {
            var container = DiyModule.buildContainer();
            document.body.appendChild(container);
            var reactRoot = document.getElementById('reactRoot');
            if (reactRoot) reactRoot.style.display = 'none';
            console.log('[ShortVideo] #/diy 路由激活，空页面占位');
            return { container: container, state: null };
        }
    });

    // ---- 激活某个自定义路由 ----
    function activateRoute(route) {
        if (route.container) return; // 已激活

        if (!previousHash && !isCustomRoute()) {
            previousHash = location.hash || '#/home';
        }

        var result = route.show();
        route.container = result.container;
        route.state = result.state;
        document.title = route.title;
    }

    // ---- 路由变化处理 ----
    function handleRouteChange() {
        if (isNavigatingBack) return;

        var active = getActiveRoute();
        if (active) {
            // 静默清理其他自定义路由
            customRoutes.forEach(function(r) {
                if (r !== active && r.container) {
                    silentHideRoute(r);
                }
            });
            activateRoute(active);
        } else {
            var wasCustom = customRoutes.some(function(r) { return !!r.container; });
            cleanupCustomPages();
            previousHash = null;
            if (wasCustom) {
                console.log('[ShortVideo] 已退出自定义路由，当前:', location.hash);
            }
        }
        updateVisibility();
    }

    // ---- 悬浮按钮注入 ----
    function injectButton() {
        if (link) return;
        if (document.getElementById('shortvideo-fab')) return;
        if (!document.body) return;

        link = document.createElement('a');
        link.id = 'shortvideo-fab';
        link.href = '#/shorts';
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
        handleRouteChange();
        console.log('[ShortVideo] 悬浮按钮已注入, 当前路由:', location.hash);
    }

    // ---- 左侧抽屉菜单注入 ----
    var drawerObserver = null;

    // 创建一个抽屉菜单项（通用方法）
    function createDrawerItem(id, href, iconChar, label) {
        var item = document.createElement('a');
        item.id = id;
        item.href = href;
        item.className = 'navMenuOption emby-button';
        item.style.cssText = [
            'display: flex',
            'align-items: center',
            'gap: 12px',
            'padding: 12px 1.5em',
            'color: inherit',
            'text-decoration: none',
            'font-size: inherit',
            'font-weight: 400',
            'cursor: pointer',
            'width: 100%',
            'box-sizing: border-box'
        ].join(';');
        item.addEventListener('mouseenter', function() { item.style.background = 'rgba(255,255,255,0.08)'; });
        item.addEventListener('mouseleave', function() { item.style.background = ''; });

        var iconEl = document.createElement('span');
        iconEl.textContent = iconChar;
        iconEl.style.cssText = 'width: 1.6em; text-align: center; opacity: 0.8;';

        var labelEl = document.createElement('span');
        labelEl.textContent = label;

        item.appendChild(iconEl);
        item.appendChild(labelEl);
        return item;
    }

    function injectDrawerMenuItem() {
        if (document.getElementById('shortvideo-drawer-item')) return;

        // 尝试找到抽屉菜单容器
        var drawer = document.querySelector('.mainDrawer');
        if (!drawer) {
            drawer = document.querySelector('.mainDrawerMenu');
            if (!drawer) return;
        }

        // 查找所有菜单项
        var menuItems = drawer.querySelectorAll('a.itemAction, a.emby-button, button.itemAction, .navMenuOption, .listItem');
        if (!menuItems || menuItems.length === 0) return;

        // 查找【首页】菜单项
        var homeItem = null;
        for (var i = 0; i < menuItems.length; i++) {
            var text = menuItems[i].textContent.trim().toLowerCase();
            var href = menuItems[i].getAttribute('href') || '';
            if (text === '首页' || text === 'home' || href.indexOf('#/home') >= 0) {
                homeItem = menuItems[i];
                break;
            }
        }

        // 创建短视频 + DIY 两个菜单项
        var shortsItem = createDrawerItem('shortvideo-drawer-item', '#/shorts', '\u25B6', '短视频');
        var diyItem = createDrawerItem('shortvideo-diy-drawer-item', '#/diy', '\u2728', 'DIY');

        // 插入位置：首页 -> 短视频 -> DIY
        if (homeItem && homeItem.parentNode) {
            homeItem.parentNode.insertBefore(diyItem, homeItem.nextSibling);
            homeItem.parentNode.insertBefore(shortsItem, diyItem);
            console.log('[ShortVideo] 短视频+DIY 已插入到「首页」下方');
        } else {
            if (menuItems[0] && menuItems[0].parentNode) {
                menuItems[0].parentNode.insertBefore(diyItem, menuItems[0].nextSibling);
                menuItems[0].parentNode.insertBefore(shortsItem, diyItem);
                console.log('[ShortVideo] 短视频+DIY 已插入到第一个菜单项后面');
            }
        }
    }

    function observeDrawer() {
        if (drawerObserver) return;
        drawerObserver = new MutationObserver(function() {
            injectDrawerMenuItem();
        });
        drawerObserver.observe(document.body, { childList: true, subtree: true });
    }

    // ---- 初始化 ----
    if (document.body) {
        injectButton();
        injectDrawerMenuItem();
        observeDrawer();
    } else {
        document.addEventListener('DOMContentLoaded', function() {
            injectButton();
            injectDrawerMenuItem();
            observeDrawer();
        });
    }

    // 主要监听：hashchange 事件
    window.addEventListener('hashchange', function() {
        if (!link) {
            injectButton();
        } else {
            handleRouteChange();
        }
    });

    // 兜底：低频轮询（2秒）
    setInterval(function() {
        if (!link) {
            injectButton();
        } else {
            handleRouteChange();
        }
        injectDrawerMenuItem();
    }, 2000);
})();
";
}
