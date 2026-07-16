using Jellyfin.Plugin.FileUpload.Services;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.FileUpload;

/// <summary>
/// 向 Jellyfin DI 容器注册插件服务。
/// </summary>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IFileUploadService, FileUploadService>();
    }
}
