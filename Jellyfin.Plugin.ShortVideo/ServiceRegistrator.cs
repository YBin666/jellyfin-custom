using Jellyfin.Plugin.ShortVideo.Services;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ShortVideo;

public class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IFeedService, FeedService>();
    }
}
