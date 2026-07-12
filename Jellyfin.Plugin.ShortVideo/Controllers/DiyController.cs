using System.Net.Mime;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShortVideo.Controllers;

/// <summary>
/// DIY 模块控制器。
/// 路由前缀固定为 Diy，提供 DIY 页面的注入脚本。
/// </summary>
[ApiController]
[Route("Diy")]
public class DiyController : ControllerBase
{
    private readonly ILogger<DiyController> _logger;

    public DiyController(ILogger<DiyController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// DIY 模块注入脚本。
    /// 包含 DiyModule 定义 + #/diy 路由注册。
    /// 依赖 ShortVideo/Inject.js 提供的公共基础设施（路由注册表、抽屉菜单工具等）。
    /// </summary>
    [HttpGet("Inject.js")]
    [AllowAnonymous]
    public IActionResult InjectJs()
    {
        _logger.LogInformation("Diy Controller: 收到 /Diy/Inject.js 请求");
        var js = GetDiyScript();
        return Content(js, "application/javascript");
    }

    /// <summary>
    /// 读取 diy.js 嵌入资源，并追加路由注册代码。
    /// </summary>
    private string GetDiyScript()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Jellyfin.Plugin.ShortVideo.Web.diy.js";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogWarning("Diy Controller: diy.js 嵌入资源缺失");
            return "// diy.js 嵌入资源缺失";
        }

        using var reader = new StreamReader(stream);
        var diyModule = reader.ReadToEnd();

        var registerCode = @"
(function() {
    var registerRoute = window.__svRegisterRoute;
    var goBack = window.__svGoBack;
    if (!registerRoute || !window.DiyModule) {
        console.warn('[Diy] 公共基础设施未就绪，跳过路由注册');
        return;
    }

    registerRoute({
        name: 'diy',
        title: 'DIY - Jellyfin',
        show: function() {
            var container = window.DiyModule.buildContainer(goBack);
            document.body.appendChild(container);
            var reactRoot = document.getElementById('reactRoot');
            if (reactRoot) reactRoot.style.display = 'none';
            console.log('[Diy] #/diy 路由激活，空页面占位');
            return { container: container, state: null };
        }
    });

    console.log('[Diy] 路由已注册');
})();
";

        return diyModule + "\n" + registerCode;
    }
}
