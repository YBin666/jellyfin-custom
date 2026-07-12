using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShortVideo;

public class Plugin : BasePlugin<Configuration.PluginConfiguration>
{
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

        // ScriptHost 基础设施：注入 JS 到 index.html
        logger.LogInformation("ShortVideo Plugin: 开始执行 JS 注入...");
        var injectResult = Infrastructure.ScriptHost.SelfInjector.TryInject(applicationPaths, logger);
        logger.LogInformation("ShortVideo Plugin: JS 注入结果 = {Result}", injectResult);

        logger.LogInformation("==== ShortVideo Plugin: 构造函数执行完毕 ====");
    }

    public static Plugin? Instance { get; private set; }

    public ILogger<Plugin> Logger { get; }

    public override string Name => "ShortVideo";

    public override Guid Id => Guid.Parse(PluginGuidString);

    public override string Description => "抖音式刷短视频入口";
}
