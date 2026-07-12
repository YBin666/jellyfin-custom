using Jellyfin.Plugin.ShortVideo.Configuration;

namespace Jellyfin.Plugin.ShortVideo.Services;

/// <summary>
/// 短视频流聚合服务。从媒体库中随机分页抽取短时长视频，
/// 确保所有视频都有均等机会进入播放队列。
/// </summary>
public interface IFeedService
{
    /// <summary>获取下一条短视频。</summary>
    ShortVideoItem? Next();

    /// <summary>获取接下来 N 条短视频（用于前端预取）。</summary>
    /// <param name="count">条数，为空则使用配置 PrefetchCount</param>
    IReadOnlyList<ShortVideoItem> NextBatch(int? count = null);

    /// <summary>强制刷新缓存（重置总数和去重记录）。</summary>
    void Reload();
}
