using System.Reflection;
using System.Text.Json;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NavBar;

public class Plugin : BasePlugin<Configuration.PluginConfiguration>, IDisposable
{
    public const string PluginGuidString = "8a4b5c6d-7e8f-9a0b-1c2d-3e4f5a6b7c8d";

    private readonly IApplicationPaths _appPaths;
    private readonly ILogger<Plugin> _logger;
    private bool _disposed;

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        _appPaths = applicationPaths;
        _logger = logger;
        Logger = logger;
        Instance = this;

        logger.LogInformation("==== NavBar Plugin: 构造函数开始执行 ====");

        logger.LogInformation("NavBar Plugin: 开始执行 JS 注入...");
        var injectResult = Infrastructure.ScriptHost.SelfInjector.TryInject(applicationPaths, logger);
        logger.LogInformation("NavBar Plugin: JS 注入结果 = {Result}", injectResult);

        logger.LogInformation("==== NavBar Plugin: 构造函数执行完毕 ====");
    }

    public static Plugin? Instance { get; private set; }

    public ILogger<Plugin> Logger { get; }

    public override string Name => "NavBar";

    public override Guid Id => Guid.Parse(PluginGuidString);

    public override string Description => "iOS风格毛玻璃底部导航栏";

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            try
            {
                if (IsMarkedForDeletion())
                {
                    _logger.LogInformation("NavBar Plugin: 检测到插件已标记为删除，开始清理 index.html 注入...");
                    Infrastructure.ScriptHost.SelfInjector.TryUninject(_appPaths, _logger);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NavBar Plugin: Dispose 时清理注入失败");
            }
        }
    }

    private bool IsMarkedForDeletion()
    {
        try
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var pluginDir = Path.GetDirectoryName(assemblyLocation);
            if (string.IsNullOrEmpty(pluginDir)) return false;

            var metaPath = Path.Combine(pluginDir, "meta.json");
            if (!File.Exists(metaPath))
            {
                _logger.LogDebug("NavBar Plugin: meta.json 不存在，无法判断卸载状态");
                return false;
            }

            var json = File.ReadAllText(metaPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("status", out var statusProp))
            {
                var status = statusProp.GetString();
                _logger.LogDebug("NavBar Plugin: meta.json status = {Status}", status);
                return string.Equals(status, "Deleted", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NavBar Plugin: 读取 meta.json 失败");
        }
        return false;
    }
}