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
/// 按时长过滤得到候选池，内存维护一个游标，支持随机/顺序。
/// </summary>
public class FeedService : IFeedService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<FeedService> _logger;
    private readonly IMediaSourceManager _mediaSourceManager;
    private PluginConfiguration _config;

    private readonly object _lock = new();
    private List<ShortVideoItem> _pool = new();
    private int _cursor;

    public FeedService(
        ILibraryManager libraryManager,
        ILogger<FeedService> logger,
        IMediaSourceManager mediaSourceManager)
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _mediaSourceManager = mediaSourceManager;
        // 延迟取配置：插件可能在构造时还没完全加载完
        _config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        logger.LogInformation("==== ShortVideo FeedService: 构造完成 ====");
        logger.LogInformation("ShortVideo FeedService: Plugin.Instance = {Instance}", Plugin.Instance != null ? "已加载" : "null");
        logger.LogInformation("ShortVideo FeedService: 配置 => MinDuration={Min}s, MaxDuration={Max}s, Shuffle={Shuffle}, Prefetch={Prefetch}",
            _config.MinDurationSeconds, _config.MaxDurationSeconds, _config.Shuffle, _config.PrefetchCount);
    }

    /// <inheritdoc />
    public ShortVideoItem? Next()
    {
        lock (_lock)
        {
            if (_pool.Count == 0 || _cursor >= _pool.Count)
            {
                ReloadInternal();
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
    public IReadOnlyList<ShortVideoItem> NextBatch()
    {
        var count = Math.Max(1, _config.PrefetchCount);
        var result = new List<ShortVideoItem>(count);
        for (var i = 0; i < count; i++)
        {
            var item = Next();
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
            ReloadInternal();
        }
    }

    private void ReloadInternal()
    {
        // 每次刷新时重新读配置，使仪表盘修改即时生效
        _config = Plugin.Instance?.Configuration ?? _config;
        _logger.LogInformation("==== ShortVideo FeedService: 开始加载候选池 ====");
        _logger.LogInformation("ShortVideo FeedService: 配置 => MinDuration={Min}s, MaxDuration={Max}s, Shuffle={Shuffle}",
            _config.MinDurationSeconds, _config.MaxDurationSeconds, _config.Shuffle);

        // 时长以 tick 为单位：1 秒 = 10_000_000 ticks
        var minTicks = (long)_config.MinDurationSeconds * TimeSpan.TicksPerSecond;
        var maxTicks = (long)_config.MaxDurationSeconds * TimeSpan.TicksPerSecond;
        _logger.LogInformation("ShortVideo FeedService: 时长过滤 => minTicks={Min}, maxTicks={Max}", minTicks, maxTicks);

        var query = new InternalItemsQuery
        {
            // 只取视频类媒体
            IncludeItemTypes = new[] { BaseItemKind.Video },
            // 一次取够候选池
            Limit = 500,
            // 不分用户
            User = null
        };

        _logger.LogInformation("ShortVideo FeedService: 查询媒体库 (Limit=500)...");
        var items = _libraryManager.GetItemsResult(query);
        _logger.LogInformation("ShortVideo FeedService: 查询返回 TotalRecordCount={Total}, Items.Count={Count}",
            items.TotalRecordCount, items.Items.Count);

        var pool = new List<ShortVideoItem>(items.TotalRecordCount);
        var skippedNoPath = 0;
        var skippedDuration = 0;

        foreach (var item in items.Items)
        {
            // 跳过没有真实文件路径的项目（如虚拟合集、文件夹、集合）
            if (string.IsNullOrEmpty(item.Path))
            {
                skippedNoPath++;
                continue;
            }

            // 按时长过滤
            var runTime = item.RunTimeTicks ?? 0;
            if (runTime < minTicks || runTime > maxTicks)
            {
                skippedDuration++;
                continue;
            }

            var seconds = runTime / (double)TimeSpan.TicksPerSecond;

            var streamUrl = BuildStreamUrl(item.Id);

            // 提取视频/音频编码信息，供前端判断是否需要转码
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

            pool.Add(new ShortVideoItem
            {
                Id = item.Id,
                Name = item.Name,
                DurationSeconds = seconds,
                StreamUrl = streamUrl,
                VideoCodec = videoCodec,
                AudioCodec = audioCodec,
                Container = container,
                PrimaryImageTag = item.Id.ToString("N")
            });
        }

        _logger.LogInformation("ShortVideo FeedService: 过滤结果 => 入选={In}, 跳过(无路径)={NoPath}, 跳过(时长不符)={Dur}",
            pool.Count, skippedNoPath, skippedDuration);

        // 打印编码分布统计，方便排查兼容性问题
        var videoCodecStats = pool.GroupBy(p => p.VideoCodec ?? "(unknown)")
            .Select(g => $"{g.Key}:{g.Count()}");
        var containerStats = pool.GroupBy(p => p.Container ?? "(unknown)")
            .Select(g => $"{g.Key}:{g.Count()}");
        _logger.LogInformation("ShortVideo FeedService: 视频编码分布 => {Stats}", string.Join(", ", videoCodecStats));
        _logger.LogInformation("ShortVideo FeedService: 容器格式分布 => {Stats}", string.Join(", ", containerStats));

        if (_config.Shuffle)
        {
            // 简单洗牌，避免每次启动顺序相同
            var rng = Random.Shared;
            for (var i = pool.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            _logger.LogInformation("ShortVideo FeedService: 已随机洗牌");
        }

        _pool = pool;
        _cursor = 0;
        _logger.LogInformation("==== ShortVideo FeedService: 候选池加载完成, 共 {Count} 条 ====", pool.Count);
    }

    /// <summary>
    /// 拼接 Jellyfin 原生播放流地址：/Videos/{id}/stream?static=true
    /// 优先直接返回原始文件（零转码开销）。
    /// 浏览器不支持的格式由前端检测错误后自动回退到转码 URL。
    /// 注意：用相对路径，前端会拼上 BASE；api_key 由 Controller 注入替换占位符。
    /// </summary>
    private static string BuildStreamUrl(Guid itemId)
    {
        return $"/Videos/{itemId}/stream?static=true&api_key=__APIKEY__";
    }
}
