using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShortVideo.Infrastructure.ScriptHost;

[ApiController]
[Route("ScriptHost")]
public class ScriptHostController : ControllerBase
{
    private readonly ILogger<ScriptHostController> _logger;

    public ScriptHostController(ILogger<ScriptHostController> logger)
    {
        _logger = logger;
    }

    [HttpGet("Inject.js")]
    [AllowAnonymous]
    public IActionResult InjectJs()
    {
        _logger.LogInformation("ScriptHost: 收到 /ScriptHost/Inject.js 请求");

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith("inject.js"));

        if (resourceName == null)
        {
            _logger.LogWarning("ScriptHost: inject.js 嵌入资源未找到");
            return Content("console.log('[ScriptHost] inject.js not found');", "application/javascript");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogWarning("ScriptHost: 无法打开 inject.js 资源流");
            return Content("console.log('[ScriptHost] inject.js stream error');", "application/javascript");
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        _logger.LogInformation("ScriptHost: 返回 Inject.js，大小 = {Size} 字节", content.Length);
        return Content(content, "application/javascript");
    }

    [HttpGet("Status")]
    [AllowAnonymous]
    public IActionResult Status()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var hasInject = assembly.GetManifestResourceNames()
            .Any(r => r.EndsWith("inject.js"));

        return Ok(new
        {
            status = "healthy",
            injectAvailable = hasInject
        });
    }
}
