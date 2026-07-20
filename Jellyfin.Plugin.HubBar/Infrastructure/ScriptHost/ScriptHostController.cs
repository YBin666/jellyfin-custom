using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HubBar.Infrastructure.ScriptHost;

[ApiController]
[Route("HubBar")]
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
        _logger.LogInformation("HubBar: 收到 /HubBar/Inject.js 请求");

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith("inject.js"));

        if (resourceName == null)
        {
            _logger.LogWarning("HubBar: inject.js 嵌入资源未找到");
            return Content("console.log('[HubBar] inject.js not found');", "application/javascript");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogWarning("HubBar: 无法打开 inject.js 资源流");
            return Content("console.log('[HubBar] inject.js stream error');", "application/javascript");
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        _logger.LogInformation("HubBar: 返回 Inject.js，大小 = {Size} 字节", content.Length);
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

    [HttpGet("Config")]
    [AllowAnonymous]
    public IActionResult GetConfig()
    {
        return Ok(new
        {
            enableHubBar = true,
            enableHomeButton = true,
            enableShortVideoButton = true,
            enableSettingsButton = true,
            hubBarColor = "dark"
        });
    }

    [HttpPost("Config")]
    [AllowAnonymous]
    public IActionResult SaveConfig([FromBody] dynamic config)
    {
        _logger.LogInformation("HubBar: 收到配置保存请求");
        return Ok(new { success = true });
    }

    [HttpGet("main.js")]
    [AllowAnonymous]
    public IActionResult GetMainJs()
    {
        _logger.LogInformation("HubBar: 收到 /HubBar/main.js 请求");

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith("main.js"));

        if (resourceName == null)
        {
            _logger.LogWarning("HubBar: main.js 嵌入资源未找到");
            return Content("console.log('[HubBar] main.js not found');", "application/javascript");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogWarning("HubBar: 无法打开 main.js 资源流");
            return Content("console.log('[HubBar] main.js stream error');", "application/javascript");
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        _logger.LogInformation("HubBar: 返回 main.js，大小 = {Size} 字节", content.Length);
        return Content(content, "application/javascript");
    }

    [HttpGet("{fileName}.js")]
    [AllowAnonymous]
    public IActionResult GetChunkJs(string fileName)
    {
        _logger.LogInformation("HubBar: 收到 /HubBar/{FileName}.js 请求", fileName);

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith($"{fileName}.js"));

        if (resourceName == null)
        {
            _logger.LogWarning("HubBar: {FileName}.js 嵌入资源未找到", fileName);
            return NotFound();
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogWarning("HubBar: 无法打开 {FileName}.js 资源流", fileName);
            return NotFound();
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        return Content(content, "application/javascript");
    }

    [HttpGet("assets/{fileName}")]
    [AllowAnonymous]
    public IActionResult GetAsset(string fileName)
    {
        _logger.LogInformation("HubBar: 收到 /HubBar/assets/{FileName} 请求", fileName);

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith($"assets/{fileName}"));

        if (resourceName == null)
        {
            _logger.LogWarning("HubBar: assets/{FileName} 嵌入资源未找到", fileName);
            return NotFound();
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogWarning("HubBar: 无法打开 assets/{FileName} 资源流", fileName);
            return NotFound();
        }

        var contentType = "application/octet-stream";
        if (fileName.EndsWith(".css")) contentType = "text/css";
        if (fileName.EndsWith(".png")) contentType = "image/png";
        if (fileName.EndsWith(".svg")) contentType = "image/svg+xml";

        return File(stream, contentType);
    }
}