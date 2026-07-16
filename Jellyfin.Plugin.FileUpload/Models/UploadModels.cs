namespace Jellyfin.Plugin.FileUpload.Models;

/// <summary>初始化上传请求。</summary>
public sealed class InitUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public long TotalSize { get; set; }
    public string TargetDirectory { get; set; } = string.Empty;
}

/// <summary>初始化上传响应。</summary>
public sealed class InitUploadResponse
{
    public Guid UploadId { get; set; }
    public string TempPath { get; set; } = string.Empty;
}

/// <summary>完成上传响应。</summary>
public sealed class CompleteUploadResponse
{
    public string FilePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public bool ScanTriggered { get; set; }
    public string? ScanTarget { get; set; }
}

/// <summary>扫描结果。</summary>
public sealed class ScanResult
{
    public bool Triggered { get; set; }
    public string? LibraryName { get; set; }
    public string? LibraryPath { get; set; }
    public string? Message { get; set; }
}

/// <summary>上传元数据，存于临时目录 .meta.json。</summary>
public sealed class UploadMeta
{
    public string FileName { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public long TotalSize { get; set; }
    public string TargetDirectory { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>媒体库信息（前端选择用）。</summary>
public sealed class MediaLibraryInfo
{
    public string Name { get; set; } = string.Empty;
    public List<string> Paths { get; set; } = [];
    public string? CollectionType { get; set; }
}

/// <summary>子目录浏览结果。</summary>
public sealed class BrowseResult
{
    public string Path { get; set; } = string.Empty;
    public string? ParentPath { get; set; }
    public List<DirectoryEntry> Directories { get; set; } = [];
}

/// <summary>单个目录条目。</summary>
public sealed class DirectoryEntry
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}
