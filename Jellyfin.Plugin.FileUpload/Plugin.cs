using System.Reflection;
using System.Text.Json;
using Jellyfin.Plugin.FileUpload.Configuration;
using Jellyfin.Plugin.FileUpload.Infrastructure;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileUpload;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IDisposable
{
    public const string PluginGuidString = "e8b1c4d7-3a5e-4f8a-9c2b-6d7e8f9a0b1c";

    private readonly IApplicationPaths _appPaths;
    private bool _disposed;

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        _appPaths = applicationPaths;
        Logger = logger;
        Instance = this;
        logger.LogInformation("==== FileUpload Plugin: 已加载 ====");

        // 历史版本会在 index.html 注入 overlay.js 浮动按钮，新版本已移除该功能
        // 启动时主动清理历史残留注入（如有），保证升级后无遗留按钮
        logger.LogInformation("FileUpload Plugin: 检查并清理历史 index.html 注入残留...");
        var cleanResult = SelfInjector.TryUninject(applicationPaths, logger);
        logger.LogInformation("FileUpload Plugin: 历史残留清理结果 = {Result}", cleanResult);
    }

    public static Plugin? Instance { get; private set; }

    public ILogger<Plugin> Logger { get; }

    public override string Name => "FileUpload";

    public override Guid Id => Guid.Parse(PluginGuidString);

    public override string Description => "从配置页上传文件到指定目录，并自动触发媒体库扫描";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "FileUpload",
                EmbeddedResourcePath = $"{GetType().Namespace}.Web.configPage.html"
            }
        };
    }

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
                    Logger.LogInformation("FileUpload Plugin: 检测到插件已标记为删除，开始清理 index.html 注入...");
                    SelfInjector.TryUninject(_appPaths, Logger);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "FileUpload Plugin: Dispose 时清理注入失败");
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
                Logger.LogDebug("FileUpload Plugin: meta.json 不存在，无法判断卸载状态");
                return false;
            }

            var json = File.ReadAllText(metaPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("status", out var statusProp))
            {
                var status = statusProp.GetString();
                Logger.LogDebug("FileUpload Plugin: meta.json status = {Status}", status);
                return string.Equals(status, "Deleted", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "FileUpload Plugin: 读取 meta.json 失败");
        }
        return false;
    }
}
