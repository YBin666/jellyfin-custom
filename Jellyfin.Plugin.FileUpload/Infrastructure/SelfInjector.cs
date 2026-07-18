using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileUpload.Infrastructure;

/// <summary>
/// 历史版本会在 index.html 注入 overlay.js 浮动按钮。
/// 新版本已移除该功能，本类仅保留清理逻辑用于：
/// 1. 升级时清理旧版本注入的残留
/// 2. 卸载时清理注入痕迹
/// </summary>
public static class SelfInjector
{
    private const string InjectMarker = "<!-- FileUpload Plugin injected -->";

    public static bool TryUninject(IApplicationPaths appPaths, ILogger logger)
    {
        try
        {
            logger.LogInformation("FileUpload Injector: 开始清理 index.html 注入...");
            var webPath = ResolveWebPath(appPaths, logger);
            if (webPath == null)
            {
                logger.LogInformation("FileUpload Injector: 未找到 jellyfin-web 目录，无需清理。");
                return true;
            }

            var indexPath = Path.Combine(webPath, "index.html");
            if (!File.Exists(indexPath))
            {
                logger.LogInformation("FileUpload Injector: index.html 不存在，无需清理。");
                return true;
            }

            logger.LogInformation("FileUpload Injector: 读取 index.html: {Path}", indexPath);
            var html = File.ReadAllText(indexPath, Encoding.UTF8);

            if (!html.Contains(InjectMarker, StringComparison.Ordinal))
            {
                logger.LogInformation("FileUpload Injector: index.html 未包含注入标记，无需清理。");
                return true;
            }

            var cleaned = html;
            // 匹配 marker + script 标签（兼容历史 /FileUpload/overlay.js 和 /web/fileupload-overlay.js?v=ts 两种 src）
            var pattern = Regex.Escape(InjectMarker) + @"\s*\n?\s*" +
                @"<script\s+src=""(?:/FileUpload/overlay\.js|/web/fileupload-overlay\.js)(?:\?v=\d+)?""\s*></script>\s*\n?";
            cleaned = Regex.Replace(cleaned, pattern, string.Empty);

            if (cleaned == html)
            {
                logger.LogInformation("FileUpload Injector: 正则未匹配到注入内容，尝试简单字符串替换...");
                var lines = html.Split('\n');
                var newLines = new List<string>();
                var skipNext = false;
                foreach (var line in lines)
                {
                    if (skipNext)
                    {
                        skipNext = false;
                        if (line.Trim().StartsWith("<script") &&
                            (line.Contains("/FileUpload/overlay.js", StringComparison.Ordinal) ||
                             line.Contains("/web/fileupload-overlay.js", StringComparison.Ordinal)))
                        {
                            continue;
                        }
                    }
                    if (line.Contains(InjectMarker, StringComparison.Ordinal))
                    {
                        skipNext = true;
                        continue;
                    }
                    newLines.Add(line);
                }
                cleaned = string.Join("\n", newLines);
            }

            if (cleaned == html)
            {
                logger.LogWarning("FileUpload Injector: 未能清理注入内容，跳过。");
                return false;
            }

            var tmpPath = indexPath + ".fileupload.tmp";
            File.WriteAllText(tmpPath, cleaned, Encoding.UTF8);
            File.Copy(tmpPath, indexPath, overwrite: true);
            try { File.Delete(tmpPath); } catch { }

            logger.LogInformation("FileUpload Injector: 清理完成，已从 index.html 移除注入。");

            // 清理物理文件方式的 overlay.js（apply-overlay.ps1 历史部署的物理文件）
            var overlayPhysicalPath = Path.Combine(webPath, "fileupload-overlay.js");
            if (File.Exists(overlayPhysicalPath))
            {
                try
                {
                    File.Delete(overlayPhysicalPath);
                    logger.LogInformation("FileUpload Injector: 已删除物理文件 {Path}", overlayPhysicalPath);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "FileUpload Injector: 删除物理文件失败 {Path}", overlayPhysicalPath);
                }
            }

            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "FileUpload Injector: 没有 index.html 的写权限，无法清理注入。");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FileUpload Injector: TryUninject 发生异常");
            return false;
        }
    }

    private static string? ResolveWebPath(IApplicationPaths appPaths, ILogger logger)
    {
        var webPath = GetWebPathByReflection(appPaths);
        if (!string.IsNullOrEmpty(webPath))
        {
            var dirExists = Directory.Exists(webPath);
            var indexExists = dirExists && File.Exists(Path.Combine(webPath, "index.html"));
            logger.LogDebug("FileUpload Injector: WebPath={Path}, 目录存在={DirExists}, index.html 存在={IndexExists}", webPath, dirExists, indexExists);
            if (dirExists && indexExists)
            {
                return webPath;
            }
        }

        var probeDir = AppContext.BaseDirectory;
        for (var i = 0; i < 4; i++)
        {
            var candidate = Path.Combine(probeDir, "jellyfin-web");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "index.html")))
            {
                return candidate;
            }

            candidate = Path.Combine(probeDir, "web");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "index.html")))
            {
                return candidate;
            }

            var parent = Directory.GetParent(probeDir);
            if (parent == null) break;
            probeDir = parent.FullName;
        }

        var candidates = new[]
        {
            @"C:\Program Files\Jellyfin\Server\jellyfin-web",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "jellyfin", "web"),
            "/usr/share/jellyfin/web",
            "/usr/lib/jellyfin-web",
            "/opt/jellyfin/jellyfin-web",
            "/Applications/Jellyfin.app/Contents/Resources/jellyfin-web",
            "/usr/local/share/jellyfin/web"
        };

        foreach (var c in candidates)
        {
            if (Directory.Exists(c) && File.Exists(Path.Combine(c, "index.html")))
            {
                return c;
            }
        }

        logger.LogWarning("FileUpload Injector: 所有路径探测均失败，未找到 jellyfin-web 目录");
        return null;
    }

    private static string? GetWebPathByReflection(object appPaths)
    {
        var prop = appPaths.GetType().GetProperty("WebPath",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        return prop?.GetValue(appPaths) as string;
    }
}
