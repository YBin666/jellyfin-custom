using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ShortVideo.Configuration;

/// <summary>
/// 插件配置。字段会自动出现在仪表盘 → 插件 → ShortVideo 的配置页上
/// （因为 BasePlugin 会用反射生成默认配置 UI）。
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>被视为“短视频”的最大时长（秒）。默认 5 分钟。</summary>
    public int MaxDurationSeconds { get; set; } = 300;

    /// <summary>被视为“短视频”的最小时长（秒），用于过滤过短的片头/广告。</summary>
    public int MinDurationSeconds { get; set; } = 5;

    /// <summary>是否随机顺序播放。</summary>
    public bool Shuffle { get; set; } = true;

    /// <summary>每次 /Next 返回的预取数量，前端可预加载下几条。</summary>
    public int PrefetchCount { get; set; } = 5;
}
