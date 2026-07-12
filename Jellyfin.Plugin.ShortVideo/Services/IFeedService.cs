using Jellyfin.Plugin.ShortVideo.Configuration;

namespace Jellyfin.Plugin.ShortVideo.Services;

/// <summary>
/// 短视频流聚合服务。负责从媒体库中筛选短时长视频，
/// 并维护一个内存中的播放队列，支持随机/顺序。
/// </summary>
public interface IFeedService
{
    /// <summary>获取下一条短视频。如果队列空了会自动重新加载。</summary>
    /// <param name="userId">用户 ID，用于推荐加权（收藏优先）。为空则不加权。</param>
    ShortVideoItem? Next(Guid? userId = null);

    /// <summary>获取接下来 N 条（用于前端预取），N 由配置 PrefetchCount 决定。</summary>
    /// <param name="userId">用户 ID，用于推荐加权。</param>
    IReadOnlyList<ShortVideoItem> NextBatch(Guid? userId = null);

    /// <summary>强制刷新候选池（可由定时任务调用）。</summary>
    void Reload();
}
