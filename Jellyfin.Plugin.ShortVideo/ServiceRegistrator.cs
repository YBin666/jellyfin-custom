using Jellyfin.Plugin.ShortVideo.Services;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ShortVideo;

/// <summary>
/// 服务注册器：Jellyfin 启动时会找到实现了 IPluginServiceRegistrator 的类，
/// 并调用 RegisterServices 把服务添加到 DI 容器。
/// 注意：此阶段 DI 容器尚未构建完成，不能调用 applicationHost.Resolve&lt;T&gt;()。
/// </summary>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // FeedService 作为单例：候选池在内存中常驻，避免每次 /Next 都查库
        serviceCollection.AddSingleton<IFeedService, FeedService>();
    }
}
