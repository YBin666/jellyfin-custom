using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ShortVideo.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShortVideo.Services;

/// <summary>
/// FeedService 实现：用 ILibraryManager.GetItemsResult 查询所有视频项，
/// 按时长过滤得到候选池。当提供 userId 时，收藏视频获得更高权重（3x），
/// 实现个性化推荐。
/// </summary>
public class FeedService : IFeedService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<FeedService> _logger;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly IUserManager _userManager;
    private PluginConfiguration _config;

    private readonly object _lock = new();
    private List<ShortVideoItem> _pool = new();
    private int _cursor;
    private Guid? _currentUserId;

    public FeedService(
        ILibraryManager libraryManager,
        ILogger<FeedService> logger,
        IMediaSourceManager mediaSourceManager,
        IUserManager userManager)
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _mediaSourceManager = mediaSourceManager;
        _userManager = userManager;
        // 延迟取配置：插件可能在构造时还没完全加载完
        _config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        logger.LogInformation("==== ShortVideo FeedService: 构造完成 ====");
        logger.LogInformation("ShortVideo FeedService: 配置 => MinDuration={Min}s, MaxDuration={Max}s, Shuffle={Shuffle}, Prefetch={Prefetch}",
            _config.MinDurationSeconds, _config.MaxDurationSeconds, _config.Shuffle, _config.PrefetchCount);
    }

    /// <inheritdoc />
    public ShortVideoItem? Next(Guid? userId = null)
    {
        lock (_lock)
        {
            // userId 变化时重建候选池（切换用户或从无用户切换到有用户）
            if (userId != _currentUserId || _pool.Count == 0 || _cursor >= _pool.Count)
            {
                ReloadInternal(userId);
            }

            if (_pool.Count == 0)
            {
                return null;
            }

            if (_cursor >= _pool.Count)
            {
                _cursor = 0;
            }

            return _pool[_cursor++];
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ShortVideoItem> NextBatch(Guid? userId = null)
    {
        var count = Math.Max(1, _config.PrefetchCount);
        var result = new List<ShortVideoItem>(count);
        for (var i = 0; i < count; i++)
        {
            var item = Next(userId);
            if (item == null)
            {
                break;
            }

            result.Add(item);
        }

        return result;
    }

    /// <inheritdoc />
    public void Reload()
    {
        lock (_lock)
        {
            ReloadInternal(_currentUserId);
        }
    }

    private void ReloadInternal(Guid? userId = null)
    {
        // 每次刷新时重新读配置，使仪表盘修改即时生效
        _config = Plugin.Instance?.Configuration ?? _config;
        _currentUserId = userId;
        _logger.LogInformation("==== ShortVideo FeedService: 开始加载候选池 (userId={UserId}) ====",
            userId?.ToString() ?? "(none)");

        // 时长以 tick 为单位：1 秒 = 10_000_000 ticks
        var minTicks = (long)_config.MinDurationSeconds * TimeSpan.TicksPerSecond;
        var maxTicks = (long)_config.MaxDurationSeconds * TimeSpan.TicksPerSecond;

        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Video },
            Limit = 500,
            User = null
        };

        _logger.LogInformation("ShortVideo FeedService: 查询媒体库 (Limit=500)...");
        var items = _libraryManager.GetItemsResult(query);
        _logger.LogInformation("ShortVideo FeedService: 查询返回 TotalRecordCount={Total}, Items.Count={Count}",
            items.TotalRecordCount, items.Items.Count);

        // 如果提供了 userId，查询该用户的收藏视频 ID 集合
        HashSet<Guid>? favoriteIds = null;
        if (userId.HasValue)
        {
            favoriteIds = GetUserFavoriteIds(userId.Value);
            _logger.LogInformation("ShortVideo FeedService: 用户收藏视频数={Count}", favoriteIds.Count);
        }

        var pool = new List<ShortVideoItem>(items.TotalRecordCount);
        var skippedNoPath = 0;
        var skippedDuration = 0;

        foreach (var item in items.Items)
        {
            if (string.IsNullOrEmpty(item.Path))
            {
                skippedNoPath++;
                continue;
            }

            var runTime = item.RunTimeTicks ?? 0;
            if (runTime < minTicks || runTime > maxTicks)
            {
                skippedDuration++;
                continue;
            }

            var seconds = runTime / (double)TimeSpan.TicksPerSecond;
            var streamUrl = BuildStreamUrl(item.Id);

            string? videoCodec = null;
            string? audioCodec = null;
            string? container = null;

            if (item is Video video)
            {
                container = video.Container?.ToLowerInvariant();
            }

            try
            {
                var mediaStreams = _mediaSourceManager.GetMediaStreams(item.Id);
                if (mediaStreams != null)
                {
                    foreach (var ms in mediaStreams)
                    {
                        if (ms.Type == MediaStreamType.Video && string.IsNullOrEmpty(videoCodec))
                        {
                            videoCodec = ms.Codec?.ToLowerInvariant();
                        }
                        else if (ms.Type == MediaStreamType.Audio && string.IsNullOrEmpty(audioCodec))
                        {
                            audioCodec = ms.Codec?.ToLowerInvariant();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ShortVideo FeedService: 获取 {Name} 的媒体流信息失败", item.Name);
            }

            var svItem = new ShortVideoItem
            {
                Id = item.Id,
                Name = item.Name,
                DurationSeconds = seconds,
                StreamUrl = streamUrl,
                VideoCodec = videoCodec,
                AudioCodec = audioCodec,
                Container = container,
                PrimaryImageTag = item.Id.ToString("N")
            };

            pool.Add(svItem);

            // 收藏视频额外添加 1 份副本，实现 2x 权重
            if (favoriteIds != null && favoriteIds.Contains(item.Id))
            {
                pool.Add(svItem with { });
            }
        }

        _logger.LogInformation("ShortVideo FeedService: 过滤结果 => 入选={In} (含加权副本), 跳过(无路径)={NoPath}, 跳过(时长不符)={Dur}",
            pool.Count, skippedNoPath, skippedDuration);

        if (_config.Shuffle)
        {
            var rng = Random.Shared;
            for (var i = pool.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            _logger.LogInformation("ShortVideo FeedService: 已随机洗牌 (加权池)");
        }

        _pool = pool;
        _cursor = 0;
        _logger.LogInformation("==== ShortVideo FeedService: 候选池加载完成, 共 {Count} 条 ====", pool.Count);
    }

    /// <summary>
    /// 查询用户收藏的视频 ID 集合。
    /// </summary>
    private HashSet<Guid> GetUserFavoriteIds(Guid userId)
    {
        var result = new HashSet<Guid>();
        try
        {
            var user = _userManager.GetUserById(userId);
            if (user == null)
            {
                _logger.LogWarning("ShortVideo FeedService: 未找到用户 {UserId}", userId);
                return result;
            }

            var favQuery = new InternalItemsQuery
            {
                User = user,
                IncludeItemTypes = new[] { BaseItemKind.Video },
                IsFavorite = true,
                Recursive = true,
                Limit = 500
            };
            var favResult = _libraryManager.GetItemsResult(favQuery);
            foreach (var item in favResult.Items)
            {
                result.Add(item.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ShortVideo FeedService: 查询用户收藏失败");
        }

        return result;
    }

    private static string BuildStreamUrl(Guid itemId)
    {
        return $"/Videos/{itemId}/stream?static=true&api_key=__APIKEY__";
    }
}
