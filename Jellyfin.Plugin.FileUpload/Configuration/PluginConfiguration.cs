using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.FileUpload.Configuration;

/// <summary>
/// 插件配置。上传目标直接从 Jellyfin 媒体库中选择，无需手动配置目录白名单。
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>单文件大小上限（MB）。0 表示不限制。默认 0。</summary>
    public int MaxFileSizeMb { get; set; } = 0;

    /// <summary>分片大小（MB）。默认 5MB。</summary>
    public int ChunkSizeMb { get; set; } = 5;

    /// <summary>上传完成后是否自动触发对应媒体库扫描。默认开启。</summary>
    public bool AutoScanAfterUpload { get; set; } = true;

    /// <summary>
    /// 临时分片存放目录。留空则使用 Jellyfin 的 CacheDirectory/file-upload/。
    /// </summary>
    public string TempDirectory { get; set; } = string.Empty;
}
