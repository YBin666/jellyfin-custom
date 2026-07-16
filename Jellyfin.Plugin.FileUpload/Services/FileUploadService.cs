using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.FileUpload.Configuration;
using Jellyfin.Plugin.FileUpload.Models;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileUpload.Services;

/// <summary>
/// 文件上传业务服务实现。
/// 路径校验基于 Jellyfin 媒体库的真实物理路径，不再需要手动配置白名单。
/// </summary>
public class FileUploadService : IFileUploadService
{
    private const string TempRootFolderName = "file-upload";
    private const string MetaFileName = ".meta.json";
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly IApplicationPaths _appPaths;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(
        IApplicationPaths appPaths,
        ILibraryManager libraryManager,
        ILogger<FileUploadService> logger)
    {
        _appPaths = appPaths;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<MediaLibraryInfo> GetMediaLibraries()
    {
        // 方式 1：GetVirtualFolders(true)
        var libs = TryGetFromVirtualFolders(true);
        if (libs.Count > 0)
        {
            _logger.LogInformation("FileUpload: 通过 GetVirtualFolders(true) 找到 {Count} 个媒体库", libs.Count);
            return libs;
        }

        // 方式 2：GetVirtualFolders(false)
        libs = TryGetFromVirtualFolders(false);
        if (libs.Count > 0)
        {
            _logger.LogInformation("FileUpload: 通过 GetVirtualFolders(false) 找到 {Count} 个媒体库", libs.Count);
            return libs;
        }

        // 方式 3：从用户根目录递归查找 CollectionFolder
        libs = TryGetFromUserRoot();
        if (libs.Count > 0)
        {
            _logger.LogInformation("FileUpload: 通过 UserRootFolder 找到 {Count} 个媒体库", libs.Count);
            return libs;
        }

        _logger.LogWarning("FileUpload: 三种方式均未找到任何媒体库");
        return [];
    }

    private List<MediaLibraryInfo> TryGetFromVirtualFolders(bool includeRefreshState)
    {
        try
        {
            var folders = _libraryManager.GetVirtualFolders(includeRefreshState);
            _logger.LogDebug("FileUpload: GetVirtualFolders({Refresh}) 返回 {Count} 条", includeRefreshState, folders?.Count ?? 0);
            if (folders == null || folders.Count == 0) return [];

            var locGetter = BuildLocationsGetter(folders[0]);
            var libs = new List<MediaLibraryInfo>();
            foreach (var vf in folders)
            {
                var paths = locGetter(vf)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (paths.Count == 0) continue;

                var collectionType = vf.GetType().GetProperty("CollectionType")?.GetValue(vf)?.ToString();
                libs.Add(new MediaLibraryInfo
                {
                    Name = vf.Name,
                    Paths = paths,
                    CollectionType = collectionType
                });
                _logger.LogDebug("FileUpload: 媒体库 {Name} 路径: {Paths}", vf.Name, string.Join(";", paths));
            }
            return libs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FileUpload: GetVirtualFolders({Refresh}) 异常", includeRefreshState);
            return [];
        }
    }

    private List<MediaLibraryInfo> TryGetFromUserRoot()
    {
        try
        {
            var root = _libraryManager.GetUserRootFolder();
            if (root == null) return [];

            var children = GetChildren(root);
            _logger.LogDebug("FileUpload: UserRootFolder Name={Name}, Children={Count}", root.Name, children?.Count ?? 0);
            if (children == null || children.Count == 0) return [];

            var libs = new List<MediaLibraryInfo>();
            foreach (var child in children)
            {
                if (child == null) continue;
                var paths = new List<string>();

                // 尝试拿 PhysicalLocations（如果是 CollectionFolder）
                var physLocProp = child.GetType().GetProperty("PhysicalLocations", BindingFlags.Instance | BindingFlags.Public);
                if (physLocProp != null && physLocProp.GetValue(child) is IEnumerable<string> physLocs)
                {
                    paths.AddRange(physLocs.Where(p => !string.IsNullOrWhiteSpace(p)));
                }

                // 尝试拿 Path 属性
                var pathProp = child.GetType().GetProperty("Path", BindingFlags.Instance | BindingFlags.Public);
                if (pathProp != null && pathProp.GetValue(child) is string childPath && !string.IsNullOrWhiteSpace(childPath))
                {
                    if (!paths.Contains(childPath, StringComparer.OrdinalIgnoreCase))
                    {
                        paths.Add(childPath);
                    }
                }

                paths = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (paths.Count == 0) continue;

                var collTypeProp = child.GetType().GetProperty("CollectionType", BindingFlags.Instance | BindingFlags.Public);
                var collType = collTypeProp?.GetValue(child)?.ToString();

                var nameProp = child.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
                var name = nameProp?.GetValue(child)?.ToString() ?? "未知";

                libs.Add(new MediaLibraryInfo
                {
                    Name = name,
                    Paths = paths,
                    CollectionType = collType
                });
            }
            return libs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FileUpload: TryGetFromUserRoot 异常");
            return [];
        }
    }

    private static List<BaseItem>? GetChildren(BaseItem folder)
    {
        // 先试 Children 属性
        var prop = folder.GetType().GetProperty("Children", BindingFlags.Instance | BindingFlags.Public);
        if (prop != null && typeof(IEnumerable<BaseItem>).IsAssignableFrom(prop.PropertyType))
        {
            return prop.GetValue(folder) as List<BaseItem> ?? (prop.GetValue(folder) as IEnumerable<BaseItem>)?.ToList();
        }

        // 再试 GetChildren() 方法（无参）
        var method = folder.GetType().GetMethod("GetChildren", Type.EmptyTypes);
        if (method != null && typeof(IEnumerable<BaseItem>).IsAssignableFrom(method.ReturnType))
        {
            return method.Invoke(folder, null) as List<BaseItem> ?? (method.Invoke(folder, null) as IEnumerable<BaseItem>)?.ToList();
        }

        return null;
    }

    private static Func<object, List<string>> BuildLocationsGetter(object? sample)
    {
        if (sample == null)
        {
            return _ => [];
        }

        var type = sample.GetType();

        // 先试属性 Locations
        var prop = type.GetProperty("Locations", BindingFlags.Instance | BindingFlags.Public);
        if (prop != null && typeof(IEnumerable<string>).IsAssignableFrom(prop.PropertyType))
        {
            return obj =>
            {
                var val = prop.GetValue(obj);
                return val is IEnumerable<string> en ? en.ToList() : [];
            };
        }

        // 再试方法 Locations()
        var method = type.GetMethod("Locations", Type.EmptyTypes);
        if (method != null && typeof(IEnumerable<string>).IsAssignableFrom(method.ReturnType))
        {
            return obj =>
            {
                var val = method.Invoke(obj, null);
                return val is IEnumerable<string> en ? en.ToList() : [];
            };
        }

        // 兜底：返回空
        return _ => [];
    }

    /// <inheritdoc />
    public BrowseResult BrowseDirectories(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("路径不能为空。");
        }

        var normalized = NormalizeDirPath(path);
        EnsureTargetDirectoryAllowed(normalized);

        var result = new BrowseResult { Path = normalized };

        // 计算父目录，父目录也必须在媒体库下才允许返回
        var parent = GetParentDirectory(normalized);
        if (parent != null && IsPathInLibrary(parent))
        {
            result.ParentPath = parent;
        }

        try
        {
            var dirs = Directory.GetDirectories(normalized);
            result.Directories = dirs
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .Select(d => new DirectoryEntry
                {
                    Name = Path.GetFileName(d),
                    Path = NormalizeDirPath(d)
                }).ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "FileUpload: 无权限浏览目录 {Path}", normalized);
            throw;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogWarning(ex, "FileUpload: 目录不存在 {Path}", normalized);
            throw;
        }

        return result;
    }

    /// <inheritdoc />
    public string EnsureTargetDirectoryAllowed(string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new UnauthorizedAccessException("目标目录为空。");
        }

        var normalized = NormalizeDirPath(targetDirectory);

        if (!IsPathInLibrary(normalized))
        {
            throw new UnauthorizedAccessException($"目标目录不在任何媒体库路径下：{normalized}");
        }

        Directory.CreateDirectory(normalized);
        return normalized;
    }

    /// <summary>
    /// 判断路径是否属于任何媒体库。
    /// 优先使用媒体库物理路径前缀匹配（更可靠），FindByPath 作为兜底。
    /// 支持大小写不敏感比较（兼容不同文件系统差异）。
    /// </summary>
    private bool IsPathInLibrary(string normalizedPath)
    {
        var trimmed = normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(trimmed)) return false;

        var libs = GetMediaLibraries();
        if (libs.Count > 0)
        {
            foreach (var lib in libs)
            {
                foreach (var libPath in lib.Paths)
                {
                    var normalizedLibPath = NormalizeDirPath(libPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (string.IsNullOrEmpty(normalizedLibPath)) continue;

                    if (trimmed.Equals(normalizedLibPath, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("FileUpload: 路径 {Path} 精确匹配媒体库路径 {LibPath}", normalizedPath, libPath);
                        return true;
                    }

                    if (trimmed.StartsWith(normalizedLibPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        || trimmed.StartsWith(normalizedLibPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("FileUpload: 路径 {Path} 前缀匹配媒体库路径 {LibPath}", normalizedPath, libPath);
                        return true;
                    }
                }
            }

            _logger.LogInformation("FileUpload: 路径 {Path} 不匹配任何媒体库路径（共 {Count} 个媒体库）",
                normalizedPath, libs.Count);
            foreach (var lib in libs)
            {
                _logger.LogInformation("FileUpload:   媒体库 {Name}: {Paths}", lib.Name, string.Join(";", lib.Paths));
            }
        }

        var current = trimmed;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (!string.IsNullOrEmpty(current))
        {
            if (visited.Contains(current)) break;
            visited.Add(current);

            try
            {
                var item = _libraryManager.FindByPath(current, true);
                if (item != null)
                {
                    _logger.LogDebug("FileUpload: 路径 {Path} 通过 FindByPath 匹配媒体库项 {Name} ({ItemType})",
                        normalizedPath, item.Name, item.GetType().Name);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "FileUpload: FindByPath({Path}) 异常", current);
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || parent == current)
            {
                break;
            }
            current = parent;
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<InitUploadResponse> InitUploadAsync(InitUploadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ArgumentException("文件名不能为空。", nameof(request.FileName));
        }

        if (request.TotalChunks <= 0)
        {
            throw new ArgumentException("分片数必须大于 0。", nameof(request.TotalChunks));
        }

        var targetDir = EnsureTargetDirectoryAllowed(request.TargetDirectory);

        var cfg = Plugin.Instance?.Configuration;
        if (cfg != null && cfg.MaxFileSizeMb > 0 && request.TotalSize > 0)
        {
            var limitBytes = (long)cfg.MaxFileSizeMb * 1024 * 1024;
            if (request.TotalSize > limitBytes)
            {
                throw new InvalidOperationException($"文件大小 {request.TotalSize} 字节超过配置上限 {cfg.MaxFileSizeMb} MB。");
            }
        }

        var uploadId = Guid.NewGuid();
        var tempDir = GetUploadTempDir(uploadId);
        Directory.CreateDirectory(tempDir);

        var meta = new UploadMeta
        {
            FileName = request.FileName,
            TotalChunks = request.TotalChunks,
            TotalSize = request.TotalSize,
            TargetDirectory = targetDir,
            CreatedAt = DateTime.UtcNow
        };
        await WriteMetaAsync(tempDir, meta).ConfigureAwait(false);

        _logger.LogInformation("FileUpload: 初始化上传 uploadId={UploadId}, file={FileName}, chunks={Chunks}, size={Size}",
            uploadId, request.FileName, request.TotalChunks, request.TotalSize);

        return new InitUploadResponse
        {
            UploadId = uploadId,
            TempPath = tempDir
        };
    }

    /// <inheritdoc />
    public async Task SaveChunkAsync(Guid uploadId, int chunkIndex, Stream chunkStream)
    {
        var tempDir = GetUploadTempDir(uploadId);
        if (!Directory.Exists(tempDir))
        {
            throw new InvalidOperationException($"上传会话不存在或已过期：{uploadId}");
        }

        var meta = await ReadMetaAsync(tempDir).ConfigureAwait(false);
        if (meta == null)
        {
            throw new InvalidOperationException($"上传元数据丢失：{uploadId}");
        }

        if (chunkIndex < 0 || chunkIndex >= meta.TotalChunks)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkIndex), $"分片索引越界：{chunkIndex}（共 {meta.TotalChunks} 片）");
        }

        var chunkPath = Path.Combine(tempDir, $"chunk_{chunkIndex:D8}.part");
        await using (var fs = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        {
            await chunkStream.CopyToAsync(fs).ConfigureAwait(false);
        }

        _logger.LogDebug("FileUpload: 保存分片 uploadId={UploadId}, index={Index}", uploadId, chunkIndex);
    }

    /// <inheritdoc />
    public async Task<CompleteUploadResponse> CompleteUploadAsync(Guid uploadId)
    {
        var tempDir = GetUploadTempDir(uploadId);
        if (!Directory.Exists(tempDir))
        {
            throw new InvalidOperationException($"上传会话不存在或已过期：{uploadId}");
        }

        var meta = await ReadMetaAsync(tempDir).ConfigureAwait(false);
        if (meta == null)
        {
            throw new InvalidOperationException($"上传元数据丢失：{uploadId}");
        }

        var chunkFiles = Enumerable.Range(0, meta.TotalChunks)
            .Select(i => Path.Combine(tempDir, $"chunk_{i:D8}.part"))
            .ToList();
        var missing = chunkFiles.FirstOrDefault(p => !File.Exists(p));
        if (missing != null)
        {
            throw new InvalidOperationException($"缺少分片：{Path.GetFileName(missing)}");
        }

        var safeFileName = GetSafeFileName(meta.FileName);
        var finalPath = Path.Combine(meta.TargetDirectory, safeFileName);
        var finalDir = Path.GetDirectoryName(finalPath) ?? meta.TargetDirectory;
        Directory.CreateDirectory(finalDir);

        var stagingPath = finalPath + ".uploading";
        await using (var dest = new FileStream(stagingPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        {
            foreach (var chunk in chunkFiles)
            {
                await using var src = new FileStream(chunk, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
                await src.CopyToAsync(dest).ConfigureAwait(false);
            }
        }

        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }

        File.Move(stagingPath, finalPath);

        long finalSize = new FileInfo(finalPath).Length;

        TryCleanupDirectory(tempDir);

        _logger.LogInformation("FileUpload: 合并完成 uploadId={UploadId}, finalPath={Path}, size={Size}",
            uploadId, finalPath, finalSize);

        var scanTriggered = false;
        string? scanTarget = null;
        if (Plugin.Instance?.Configuration.AutoScanAfterUpload ?? true)
        {
            try
            {
                var scanResult = await TriggerScanAsync(meta.TargetDirectory).ConfigureAwait(false);
                scanTriggered = scanResult.Triggered;
                scanTarget = scanResult.LibraryName ?? scanResult.LibraryPath;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FileUpload: 触发媒体库扫描失败 dir={Dir}", meta.TargetDirectory);
            }
        }

        return new CompleteUploadResponse
        {
            FilePath = finalPath,
            Size = finalSize,
            ScanTriggered = scanTriggered,
            ScanTarget = scanTarget
        };
    }

    /// <inheritdoc />
    public Task CancelUploadAsync(Guid uploadId)
    {
        var tempDir = GetUploadTempDir(uploadId);
        if (Directory.Exists(tempDir))
        {
            TryCleanupDirectory(tempDir);
            _logger.LogInformation("FileUpload: 取消上传并清理临时目录 uploadId={UploadId}", uploadId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ScanResult> TriggerScanAsync(string directory)
    {
        var normalized = NormalizeDirPath(directory);
        var result = new ScanResult();

        var folder = _libraryManager.FindByPath(normalized, true) as Folder;
        if (folder == null)
        {
            var current = normalized;
            while (!string.IsNullOrEmpty(current))
            {
                var candidate = _libraryManager.FindByPath(current, true) as Folder;
                if (candidate != null)
                {
                    folder = candidate;
                    break;
                }

                current = GetParentDirectory(current) ?? string.Empty;
                if (string.IsNullOrEmpty(current))
                {
                    break;
                }
            }
        }

        if (folder == null)
        {
            result.Message = $"未找到目录 {normalized} 对应的媒体库，跳过扫描。";
            _logger.LogWarning("FileUpload: {Message}", result.Message);
            return Task.FromResult(result);
        }

        result.LibraryName = folder.Name;
        result.LibraryPath = folder.Path;
        result.Triggered = true;

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("FileUpload: 开始扫描媒体库 {Name} ({Path})", folder.Name, folder.Path);
                var progress = new Progress<double>();
                await folder.ValidateChildren(progress, CancellationToken.None).ConfigureAwait(false);
                _logger.LogInformation("FileUpload: 媒体库扫描完成 {Name}", folder.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FileUpload: 媒体库扫描异常 {Name}", folder.Name);
            }
        });

        result.Message = $"已触发媒体库 {folder.Name} 的扫描。";
        return Task.FromResult(result);
    }

    // ========== 私有方法 ==========

    private static string NormalizeDirPath(string path)
    {
        var full = Path.GetFullPath(path.Trim());
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }

    private static string? GetParentDirectory(string normalizedPath)
    {
        var trimmed = normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var parent = Path.GetDirectoryName(trimmed);
        if (string.IsNullOrEmpty(parent) || parent == trimmed)
        {
            return null;
        }

        return NormalizeDirPath(parent);
    }

    private string GetUploadTempDir(Guid uploadId)
    {
        var root = ResolveTempRoot();
        return Path.Combine(root, TempRootFolderName, uploadId.ToString("N"));
    }

    private string ResolveTempRoot()
    {
        var configured = Plugin.Instance?.Configuration.TempDirectory;
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return configured;
        }

        return _appPaths.CachePath;
    }

    private static async Task WriteMetaAsync(string tempDir, UploadMeta meta)
    {
        var metaPath = Path.Combine(tempDir, MetaFileName);
        await using var fs = new FileStream(metaPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await JsonSerializer.SerializeAsync(fs, meta, JsonOpts).ConfigureAwait(false);
    }

    private static async Task<UploadMeta?> ReadMetaAsync(string tempDir)
    {
        var metaPath = Path.Combine(tempDir, MetaFileName);
        if (!File.Exists(metaPath))
        {
            return null;
        }

        await using var fs = new FileStream(metaPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        return await JsonSerializer.DeserializeAsync<UploadMeta>(fs, JsonOpts).ConfigureAwait(false);
    }

    private static void TryCleanupDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // 清理失败不影响主流程
        }
    }

    private static string GetSafeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new StringBuilder(fileName.Length);
        foreach (var c in fileName)
        {
            safe.Append(invalid.Contains(c) ? '_' : c);
        }

        return safe.ToString();
    }
}
