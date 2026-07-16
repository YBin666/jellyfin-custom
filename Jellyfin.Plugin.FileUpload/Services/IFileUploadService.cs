using Jellyfin.Plugin.FileUpload.Models;

namespace Jellyfin.Plugin.FileUpload.Services;

/// <summary>
/// 文件上传业务服务：负责媒体库查询、子目录浏览、分片暂存、合并落盘、触发媒体库扫描。
/// </summary>
public interface IFileUploadService
{
    /// <summary>获取所有 Jellyfin 媒体库及其物理路径。</summary>
    IReadOnlyList<MediaLibraryInfo> GetMediaLibraries();

    /// <summary>浏览指定路径下的子目录（仅允许媒体库路径及其子目录）。</summary>
    /// <exception cref="UnauthorizedAccessException">路径不在任何媒体库下。</exception>
    BrowseResult BrowseDirectories(string path);

    /// <summary>校验目标目录是否在媒体库路径下，返回规范化后的绝对路径。</summary>
    /// <exception cref="UnauthorizedAccessException">目标目录不在媒体库路径下。</exception>
    string EnsureTargetDirectoryAllowed(string targetDirectory);

    /// <summary>初始化一次分片上传，返回 uploadId。</summary>
    Task<InitUploadResponse> InitUploadAsync(InitUploadRequest request);

    /// <summary>保存单个分片到临时目录。</summary>
    Task SaveChunkAsync(Guid uploadId, int chunkIndex, Stream chunkStream);

    /// <summary>合并所有分片到目标目录，返回最终文件路径。可选触发媒体库扫描。</summary>
    Task<CompleteUploadResponse> CompleteUploadAsync(Guid uploadId);

    /// <summary>取消并清理指定上传的临时文件。</summary>
    Task CancelUploadAsync(Guid uploadId);

    /// <summary>手动触发指定目录所属媒体库的扫描。</summary>
    Task<ScanResult> TriggerScanAsync(string directory);
}
