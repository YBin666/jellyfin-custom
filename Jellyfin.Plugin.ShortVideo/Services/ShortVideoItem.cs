namespace Jellyfin.Plugin.ShortVideo.Services;

/// <summary>
/// 一条短视频的精简信息，供前端播放器使用。
/// </summary>
public record ShortVideoItem
{
    /// <summary>媒体项 Id。</summary>
    public Guid Id { get; set; }

    /// <summary>标题。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>时长（秒）。</summary>
    public double DurationSeconds { get; set; }

    /// <summary>可直接喂给 &lt;video src&gt; 的播放地址（已带 api_key）。</summary>
    public string StreamUrl { get; set; } = string.Empty;

    /// <summary>封面图地址（可选）。</summary>
    public string? PrimaryImageTag { get; set; }

    /// <summary>视频编码（小写，如 h264, hevc, vp9, mpeg4）。为空表示未知。</summary>
    public string? VideoCodec { get; set; }

    /// <summary>音频编码（小写，如 aac, mp3, opus, ac3）。为空表示未知。</summary>
    public string? AudioCodec { get; set; }

    /// <summary>容器格式（小写，如 mp4, mkv, avi, webm）。为空表示未知。</summary>
    public string? Container { get; set; }
}
