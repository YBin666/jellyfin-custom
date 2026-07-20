using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HubBar.Infrastructure.ScriptHost;

public static class SelfInjector
{
    private const string InjectMarker = "<!-- Jellyfin.HubBar injected -->";

    private const string ScriptTag = InjectMarker + "\n" +
        "<script src=\"/HubBar/Inject.js\"></script>\n";

    public static bool TryInject(IApplicationPaths appPaths, ILogger logger)
    {
        try
        {
            logger.LogInformation("HubBar Injector: 开始解析 jellyfin-web 路径...");
            var webPath = ResolveWebPath(appPaths, logger);
            if (webPath == null)
            {
                logger.LogWarning(
                    "HubBar Injector: 无法找到 jellyfin-web 目录，跳过 JS 注入。" +
                    "插件仍可通过直接 URL /HubBar/Inject.js 访问");
                return false;
            }

            logger.LogInformation("HubBar Injector: 找到 jellyfin-web 路径 = {Path}", webPath);
            var indexPath = Path.Combine(webPath, "index.html");
            logger.LogInformation("HubBar Injector: index.html 完整路径 = {Path}", indexPath);
            return TryInjectFile(indexPath, logger);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex,
                "HubBar Injector: 没有 index.html 的写权限。" +
                "Docker 用户需将 index.html 文件映射为卷。" +
                "插件仍可通过直接 URL /HubBar/Inject.js 访问");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HubBar Injector: TryInject 发生异常");
            return false;
        }
    }

    private static string? ResolveWebPath(IApplicationPaths appPaths, ILogger logger)
    {
        var webPath = GetWebPathByReflection(appPaths);
        logger.LogInformation("HubBar Injector: 反射获取 WebPath = {Path}", webPath ?? "(null)");
        if (!string.IsNullOrEmpty(webPath))
        {
            var dirExists = Directory.Exists(webPath);
            var indexExists = dirExists && File.Exists(Path.Combine(webPath, "index.html"));
            logger.LogInformation("HubBar Injector: WebPath 目录存在={DirExists}, index.html 存在={IndexExists}", dirExists, indexExists);
            if (dirExists && indexExists)
            {
                return webPath;
            }
        }

        var probeDir = AppContext.BaseDirectory;
        logger.LogInformation("HubBar Injector: 开始从程序集目录向上探测: {Dir}", probeDir);
        for (var i = 0; i < 4; i++)
        {
            var candidate = Path.Combine(probeDir, "jellyfin-web");
            var candidateExists = Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "index.html"));
            logger.LogDebug("HubBar Injector: 探测 [{I}] {Path} => 存在={Exists}", i, candidate, candidateExists);
            if (candidateExists)
            {
                logger.LogInformation("HubBar Injector: 在程序集上级目录找到 jellyfin-web: {Path}", candidate);
                return candidate;
            }

            candidate = Path.Combine(probeDir, "web");
            candidateExists = Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "index.html"));
            logger.LogDebug("HubBar Injector: 探测 [{I}] {Path} => 存在={Exists}", i, candidate, candidateExists);
            if (candidateExists)
            {
                logger.LogInformation("HubBar Injector: 在程序集上级目录找到 web: {Path}", candidate);
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

        logger.LogInformation("HubBar Injector: 开始探测常见安装路径 ({Count} 个候选)...", candidates.Length);
        foreach (var c in candidates)
        {
            var exists = Directory.Exists(c) && File.Exists(Path.Combine(c, "index.html"));
            logger.LogDebug("HubBar Injector: 探测 {Path} => 存在={Exists}", c, exists);
            if (exists)
            {
                logger.LogInformation("HubBar Injector: 在常见路径找到 jellyfin-web: {Path}", c);
                return c;
            }
        }

        logger.LogWarning("HubBar Injector: 所有路径探测均失败，未找到 jellyfin-web 目录");
        return null;
    }

    private static string? GetWebPathByReflection(object appPaths)
    {
        var prop = appPaths.GetType().GetProperty("WebPath",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        return prop?.GetValue(appPaths) as string;
    }

    private static bool TryInjectFile(string indexPath, ILogger logger)
    {
        if (!File.Exists(indexPath))
        {
            logger.LogWarning("HubBar Injector: index.html 不存在: {Path}", indexPath);
            return false;
        }

        logger.LogInformation("HubBar Injector: 读取 index.html: {Path}", indexPath);
        var html = File.ReadAllText(indexPath, Encoding.UTF8);
        logger.LogInformation("HubBar Injector: index.html 大小 = {Bytes} 字节", html.Length);

        var modified = html;
        bool hasChanges = false;

        if (!modified.Contains(InjectMarker, StringComparison.Ordinal))
        {
            logger.LogInformation("HubBar Injector: index.html 未注入 script，准备写入...");
            var bodyTag = Regex.Match(modified, "</body>", RegexOptions.IgnoreCase);
            if (bodyTag.Success)
            {
                logger.LogInformation("HubBar Injector: 找到 </body> 标签，位置 = {Index}", bodyTag.Index);
                modified = modified.Insert(bodyTag.Index, ScriptTag);
                hasChanges = true;
            }
            else
            {
                logger.LogWarning("HubBar Injector: 未找到 </body> 标签，追加到文件末尾");
                modified = modified + ScriptTag;
                hasChanges = true;
            }
        }
        else
        {
            logger.LogInformation("HubBar Injector: index.html 已包含注入标记，跳过");
        }

        if (!hasChanges)
        {
            logger.LogInformation("HubBar Injector: 无需修改，index.html 已是最新");
            return true;
        }

        var tmpPath = indexPath + ".hubbar.tmp";
        logger.LogInformation("HubBar Injector: 写入临时文件: {Path}", tmpPath);
        File.WriteAllText(tmpPath, modified, Encoding.UTF8);
        logger.LogInformation("HubBar Injector: 覆盖 index.html: {Path}", indexPath);
        File.Copy(tmpPath, indexPath, overwrite: true);
        try { File.Delete(tmpPath); } catch { }
    
        logger.LogInformation("HubBar Injector: 注入成功! 已写入 {Path}", indexPath);
        return true;
    }

    public static bool TryUninject(IApplicationPaths appPaths, ILogger logger)
    {
        try
        {
            logger.LogInformation("HubBar Injector: 开始卸载清理...");
            var webPath = ResolveWebPath(appPaths, logger);
            if (webPath == null)
            {
                logger.LogInformation("HubBar Injector: 未找到 jellyfin-web 目录，无需清理。");
                return true;
            }

            var indexPath = Path.Combine(webPath, "index.html");
            if (!File.Exists(indexPath))
            {
                logger.LogInformation("HubBar Injector: index.html 不存在，无需清理。");
                return true;
            }

            logger.LogInformation("HubBar Injector: 读取 index.html: {Path}", indexPath);
            var html = File.ReadAllText(indexPath, Encoding.UTF8);

            if (!html.Contains(InjectMarker, StringComparison.Ordinal))
            {
                logger.LogInformation("HubBar Injector: index.html 未包含注入标记，无需清理。");
                return true;
            }

            var cleaned = html;
            var pattern = Regex.Escape(InjectMarker) + @"\s*\n?\s*" +
                Regex.Escape("<script src=\"/HubBar/Inject.js\"></script>") + @"\s*\n?";
            cleaned = Regex.Replace(cleaned, pattern, string.Empty);

            if (cleaned == html)
            {
                logger.LogInformation("HubBar Injector: 正则未匹配到注入内容，尝试简单字符串替换...");
                var lines = html.Split('\n');
                var newLines = new List<string>();
                var skipNext = false;
                foreach (var line in lines)
                {
                    if (skipNext)
                    {
                        skipNext = false;
                        if (line.Trim().StartsWith("<script")) continue;
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
                logger.LogWarning("HubBar Injector: 未能清理注入内容，跳过。");
                return false;
            }

            var tmpPath = indexPath + ".hubbar.tmp";
            File.WriteAllText(tmpPath, cleaned, Encoding.UTF8);
            File.Copy(tmpPath, indexPath, overwrite: true);
            try { File.Delete(tmpPath); } catch { }

            logger.LogInformation("HubBar Injector: 卸载清理完成，已从 index.html 移除注入。");
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "HubBar Injector: 没有 index.html 的写权限，无法清理注入。");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HubBar Injector: TryUninject 发生异常");
            return false;
        }
    }
}