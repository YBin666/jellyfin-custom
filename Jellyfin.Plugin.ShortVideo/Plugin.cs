using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShortVideo;

/// <summary>
/// 短视频插件主入口。
/// 继承 BasePlugin&lt;T&gt; 会自动让 Jellyfin 在启动时发现并加载本插件，
/// 并把 T 作为插件配置类型持久化到 plugins/configurations 目录。
/// </summary>
public class Plugin : BasePlugin<Configuration.PluginConfiguration>
{
    /// <summary>插件固定 GUID，Jellyfin 用它来唯一标识一个插件。</summary>
    public const string PluginGuidString = "7f3a9c2e-1b4d-4e6a-8f0b-2c5d7e9a1b3f";

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Logger = logger;
        Instance = this;

        logger.LogInformation("==== ShortVideo Plugin: 构造函数开始执行 ====");
        logger.LogInformation("ShortVideo Plugin: 程序集路径 = {Path}", AppContext.BaseDirectory);
        logger.LogInformation("ShortVideo Plugin: 插件 GUID = {Guid}", PluginGuidString);
        logger.LogInformation("ShortVideo Plugin: ApplicationPaths.DataPath = {Path}", applicationPaths.DataPath);
        logger.LogInformation("ShortVideo Plugin: ApplicationPaths.ProgramDataPath = {Path}", applicationPaths.ProgramDataPath);
        logger.LogInformation("ShortVideo Plugin: ApplicationPaths.PluginsPath = {Path}", applicationPaths.PluginsPath);

        // 自实现 JS 注入：直接改 jellyfin-web/index.html，在 </body> 前插入
        // <script src="/ShortVideo/Inject.js"></script>，由 Controller 动态返回 JS。
        // 不依赖任何第三方插件。失败时静默跳过，插件仍可通过直接 URL 访问。
        logger.LogInformation("ShortVideo Plugin: 开始执行 JS 注入...");
        var injectResult = SelfInjector.TryInject(applicationPaths, logger);
        logger.LogInformation("ShortVideo Plugin: JS 注入结果 = {Result}", injectResult);
        logger.LogInformation("==== ShortVideo Plugin: 构造函数执行完毕 ====");
    }

    public static Plugin? Instance { get; private set; }

    public ILogger<Plugin> Logger { get; }

    public override string Name => "ShortVideo";

    public override Guid Id => Guid.Parse(PluginGuidString);

    public override string Description => "抖音式刷短视频入口";
}
