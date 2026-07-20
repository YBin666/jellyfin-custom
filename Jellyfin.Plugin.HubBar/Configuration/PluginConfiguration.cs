using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.HubBar.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public bool EnableHubBar { get; set; } = true;
    
    public bool EnableHomeButton { get; set; } = true;
    
    public bool EnableShortVideoButton { get; set; } = true;
    
    public bool EnableSettingsButton { get; set; } = true;
    
    public string HubBarColor { get; set; } = "dark";
}