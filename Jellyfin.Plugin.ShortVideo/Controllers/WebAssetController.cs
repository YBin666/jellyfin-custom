using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShortVideo.Controllers;

/// <summary>
/// Web 资源控制器：专门负责向前端提供插件构建产物（JS bundle、CSS）。
/// 与业务 Controller 分离，业务接口不应放在此处。
/// </summary>
[ApiController]
[Route("ShortVideo/Web")]
public class WebAssetController : ControllerBase
{
    private readonly ILogger<WebAssetController> _logger;

    public WebAssetController(ILogger<WebAssetController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 返回 React 引导脚本（IIFE 格式的单文件 bundle）。
    /// 该脚本负责注册路由、注入抽屉菜单/悬浮按钮、挂载 React 组件。
    /// </summary>
    [HttpGet("bootstrap.js")]
    [AllowAnonymous]
    public IActionResult BootstrapJs()
    {
        _logger.LogInformation("WebAsset: 收到 /ShortVideo/Web/bootstrap.js 请求");

        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        var bundleResource = resourceNames.FirstOrDefault(r => r.EndsWith("bootstrap.js"));
        if (bundleResource == null)
        {
            _logger.LogWarning("WebAsset: bootstrap.js 嵌入资源未找到，可用资源: {Names}", string.Join(", ", resourceNames));
            return NotFound();
        }

        using var stream = assembly.GetManifestResourceStream(bundleResource);
        if (stream == null)
        {
            _logger.LogWarning("WebAsset: 无法打开 bootstrap.js 资源流");
            return NotFound();
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        _logger.LogInformation("WebAsset: 返回 bootstrap.js，大小 = {Size} 字节", content.Length);
        return Content(content, "application/javascript");
    }

    /// <summary>
    /// 返回插件 CSS 样式表。
    /// </summary>
    [HttpGet("styles.css")]
    [AllowAnonymous]
    public IActionResult StylesCss()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        var cssResource = resourceNames.FirstOrDefault(r => r.EndsWith("shortvideo.css"));
        if (cssResource == null) return NotFound();

        using var stream = assembly.GetManifestResourceStream(cssResource);
        if (stream == null) return NotFound();
        using var reader = new StreamReader(stream);
        return Content(reader.ReadToEnd(), "text/css");
    }
}
