using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ShortVideo.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShortVideo.Services;

/// <summary>
/// FeedService 实现：随机分页抽取短视频。
/// 每次 NextBatch 随机选择一个起始页，让所有视频都有均等机会进入候选池，
/// 避免硬编码 Limit=500 导致后面的视频永远抽不到。
/// </summary>
public class FeedService : IFeedService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<FeedService> _logger;
    private readonly IMediaSourceManager _mediaSourceManager;
    private PluginConfiguration _config;

    private readonly object _lock = new();
    private int _totalCount = -1;
    private readonly HashSet<Guid> _recentIds = new();
    private const int RecentCacheSize = 100;

    public FeedService(
        ILibraryManager libraryManager,
        ILogger<FeedService> logger,
        IMediaSourceManager mediaSourceManager)
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _mediaSourceManager = mediaSourceManager;
        _config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        logger.LogInformation("==== ShortVideo FeedService: 构造完成 ====");
        logger.LogInformation("ShortVideo FeedService: 配置 => MinDuration={Min}s, MaxDuration={Max}s, Prefetch={Prefetch}",
            _config.MinDurationSeconds, _config.MaxDurationSeconds, _config.PrefetchCount);
    }

    /// <inheritdoc />
    public ShortVideoItem? Next()
    {
        var batch = NextBatch(1);
        return batch.Count > 0 ? batch[0] : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<ShortVideoItem> NextBatch(int? count = null)
    {
        lock (_lock)
        {
            _config = Plugin.Instance?.Configuration ?? _config;
            var batchSize = Math.Max(1, count ?? _config.PrefetchCount);
            var minTicks = (long)_config.MinDurationSeconds * TimeSpan.TicksPerSecond;
            var maxTicks = (long)_config.MaxDurationSeconds * TimeSpan.TicksPerSecond;

            // 首次或刷新后获取总数
            if (_totalCount < 0)
            {
                _totalCount = GetTotalVideoCount();
                _logger.LogInformation("ShortVideo FeedService: 媒体库视频总数={Total}", _totalCount);
            }

            if (_totalCount == 0)
            {
                return Array.Empty<ShortVideoItem>();
            }

            var result = new List<ShortVideoItem>(batchSize);
            var pageSize = Math.Max(batchSize * 3, 50); // 每页多取一些，提高过滤后凑够的概率
            var randomStart = Random.Shared.Next(0, Math.Max(0, _totalCount - pageSize) + 1);

            _logger.LogDebug("ShortVideo FeedService: 随机起始位置 startIndex={Start}, total={Total}",
                randomStart, _totalCount);

            var startIndex = randomStart;
            var maxIterations = 10; // 最多翻 10 页，防止死循环
            var iteration = 0;

            while (result.Count < batchSize && iteration < maxIterations)
            {
                iteration++;
                if (startIndex >= _totalCount)
                {
                    startIndex = 0; // 翻到底了就从头继续
                }

                var query = new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Video },
                    StartIndex = startIndex,
                    Limit = pageSize,
                    User = null
                };

                var pageResult = _libraryManager.GetItemsResult(query);
                if (pageResult.Items == null || pageResult.Items.Count == 0)
                {
                    break;
                }

                // 更新总数（可能库变化了）
                if (pageResult.TotalRecordCount > 0)
                {
                    _totalCount = pageResult.TotalRecordCount;
                }

                foreach (var item in pageResult.Items)
                {
                    if (result.Count >= batchSize)
                    {
                        break;
                    }

                    if (string.IsNullOrEmpty(item.Path))
                    {
                        continue;
                    }

                    var runTime = item.RunTimeTicks ?? 0;
                    if (runTime < minTicks || runTime > maxTicks)
                    {
                        continue;
                    }

                    // 去重：最近返回过的跳过
                    if (_recentIds.Contains(item.Id))
                    {
                        continue;
                    }

                    var svItem = BuildShortVideoItem(item, runTime);
                    if (svItem != null)
                    {
                        result.Add(svItem);
                        _recentIds.Add(item.Id);
                        if (_recentIds.Count > RecentCacheSize)
                        {
                            _recentIds.Clear(); // 简单策略：满了就清空
                        }
                    }
                }

                startIndex += pageResult.Items.Count;
            }

            _logger.LogInformation("ShortVideo FeedService: NextBatch 返回 {Count} 条 (遍历 {Pages} 页)",
                result.Count, iteration);

            return result;
        }
    }

    /// <inheritdoc />
    public void Reload()
    {
        lock (_lock)
        {
            _totalCount = -1;
            _recentIds.Clear();
            _logger.LogInformation("ShortVideo FeedService: 已重置缓存");
        }
    }

    /// <summary>
    /// 获取媒体库中视频总数。
    /// </summary>
    private int GetTotalVideoCount()
    {
        try
        {
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Video },
                Limit = 0,
                User = null
            };
            var result = _libraryManager.GetItemsResult(query);
            return result.TotalRecordCount;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ShortVideo FeedService: 获取视频总数失败");
            return 0;
        }
    }

    /// <summary>
    /// 将 BaseItem 转为 ShortVideoItem，提取编码信息。
    /// </summary>
    private ShortVideoItem? BuildShortVideoItem(BaseItem item, long runTimeTicks)
    {
        try
        {
            var seconds = runTimeTicks / (double)TimeSpan.TicksPerSecond;
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
                _logger.LogDebug(ex, "ShortVideo FeedService: 获取 {Name} 的媒体流信息失败", item.Name);
            }

            return new ShortVideoItem
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
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ShortVideo FeedService: 构建 {Name} 失败", item.Name);
            return null;
        }
    }

    private static string BuildStreamUrl(Guid itemId)
    {
        return $"/Videos/{itemId}/stream?static=true&api_key=__APIKEY__";
    }
}
