using Jellyfin.Plugin.FileUpload.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileUpload;

/// <summary>
/// 文件上传插件入口。
/// 通过 <see cref="IHasWebPages"/> 暴露配置页 /web/configurationpage?name=FileUpload，
/// 该页面同时承担"目录白名单管理 + 文件上传区域"的职责。
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public const string PluginGuidString = "e8b1c4d7-3a5e-4f8a-9c2b-6d7e8f9a0b1c";

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Logger = logger;
        Instance = this;
        logger.LogInformation("==== FileUpload Plugin: 已加载 ====");
    }

    public static Plugin? Instance { get; private set; }

    public ILogger<Plugin> Logger { get; }

    public override string Name => "FileUpload";

    public override Guid Id => Guid.Parse(PluginGuidString);

    public override string Description => "从配置页上传文件到指定目录，并自动触发媒体库扫描";

    /// <summary>
    /// 暴露配置页 HTML。Jellyfin 会以 /web/configurationpage?name=FileUpload 提供。
    /// </summary>
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
}
