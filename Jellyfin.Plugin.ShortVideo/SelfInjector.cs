using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShortVideo;

/// <summary>
/// 自实现 JS 注入：启动时修改 jellyfin-web 的 index.html，
/// 在 &lt;/body&gt; 前插入一个 &lt;script&gt; 标签指向 /ShortVideo/Web/bootstrap.js，
/// 由 WebAssetController 动态返回 React 引导脚本（IIFE 格式单文件 bundle）。
///
/// 优点：
/// - 零外部依赖，不需要装 JavaScript Injector
/// - 用户只需装本插件一个
///
/// 注意：
/// - 需要 jellyfin-web 目录的写权限（Docker 用户需映射 index.html）
/// - Jellyfin 升级会覆盖 index.html，本方法会在每次启动时重新检测注入
/// </summary>
public static class SelfInjector
{
    /// <summary>注入标记注释，用于幂等判断。</summary>
    private const string InjectMarker = "<!-- Jellyfin.Plugin.ShortVideo injected -->";

    /// <summary>注入的 script 标签：引导脚本由 WebAssetController 提供。</summary>
    private const string ScriptTag = InjectMarker + "\n" +
        "<script src=\"/ShortVideo/Web/bootstrap.js\"></script>\n";

    /// <summary>
    /// 尝试找到并修改 index.html，注入 script 标签。
    /// 失败时打日志静默返回，不阻断插件加载。
    /// </summary>
    public static bool TryInject(IApplicationPaths appPaths, ILogger logger)
    {
        try
        {
            logger.LogInformation("ShortVideo Injector: 开始解析 jellyfin-web 路径...");
            var webPath = ResolveWebPath(appPaths, logger);
            if (webPath == null)
            {
                logger.LogWarning(
                    "ShortVideo Injector: 无法找到 jellyfin-web 目录，跳过 JS 注入。" +
                    "插件仍可通过直接 URL /ShortVideo/Page 访问");
                return false;
            }

            logger.LogInformation("ShortVideo Injector: 找到 jellyfin-web 路径 = {Path}", webPath);
            var indexPath = Path.Combine(webPath, "index.html");
            logger.LogInformation("ShortVideo Injector: index.html 完整路径 = {Path}", indexPath);
            return TryInjectFile(indexPath, logger);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex,
                "ShortVideo Injector: 没有 index.html 的写权限。" +
                "Docker 用户需将 index.html 文件映射为卷。" +
                "插件仍可通过直接 URL /ShortVideo/Page 访问");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ShortVideo Injector: TryInject 发生异常");
            return false;
        }
    }

    /// <summary>
    /// 解析 jellyfin-web 根目录。
    /// 优先从 IApplicationPaths 的 WebPath 属性拿（IServerApplicationPaths 继承自 IApplicationPaths，
    /// 所以即使传进来的是 IApplicationPaths，只要实际类型是 IServerApplicationPaths，
    /// 就能通过反射拿到 WebPath）。拿不到就遍历常见路径兜底。
    /// </summary>
    private static string? ResolveWebPath(IApplicationPaths appPaths, ILogger logger)
    {
        // 1. 反射拿 WebPath（兼容 IServerApplicationPaths 和其他实现）
        var webPath = GetWebPathByReflection(appPaths);
        logger.LogInformation("ShortVideo Injector: 反射获取 WebPath = {Path}", webPath ?? "(null)");
        if (!string.IsNullOrEmpty(webPath))
        {
            var dirExists = Directory.Exists(webPath);
            var indexExists = dirExists && File.Exists(Path.Combine(webPath, "index.html"));
            logger.LogInformation("ShortVideo Injector: WebPath 目录存在={DirExists}, index.html 存在={IndexExists}", dirExists, indexExists);
            if (dirExists && indexExists)
            {
                return webPath;
            }
        }

        // 2. 兜底：从程序集同级目录往上找 jellyfin-web
        var probeDir = AppContext.BaseDirectory;
        logger.LogInformation("ShortVideo Injector: 开始从程序集目录向上探测: {Dir}", probeDir);
        for (var i = 0; i < 4; i++)
        {
            var candidate = Path.Combine(probeDir, "jellyfin-web");
            var candidateExists = Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "index.html"));
            logger.LogDebug("ShortVideo Injector: 探测 [{I}] {Path} => 存在={Exists}", i, candidate, candidateExists);
            if (candidateExists)
            {
                logger.LogInformation("ShortVideo Injector: 在程序集上级目录找到 jellyfin-web: {Path}", candidate);
                return candidate;
            }

            // 也试试直接叫 web
            candidate = Path.Combine(probeDir, "web");
            candidateExists = Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "index.html"));
            logger.LogDebug("ShortVideo Injector: 探测 [{I}] {Path} => 存在={Exists}", i, candidate, candidateExists);
            if (candidateExists)
            {
                logger.LogInformation("ShortVideo Injector: 在程序集上级目录找到 web: {Path}", candidate);
                return candidate;
            }

            var parent = Directory.GetParent(probeDir);
            if (parent == null) break;
            probeDir = parent.FullName;
        }

        // 3. 常见安装路径兜底
        var candidates = new[]
        {
            // Windows tray install
            @"C:\ProgramData\Jellyfin\Server\jellyfin-web",
            // Windows portable
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "jellyfin", "web"),
            // Linux
            "/usr/share/jellyfin/web",
            "/usr/lib/jellyfin-web",
            "/opt/jellyfin/jellyfin-web",
            // macOS
            "/Applications/Jellyfin.app/Contents/Resources/jellyfin-web",
            // FreeBSD / 其他
            "/usr/local/share/jellyfin/web"
        };

        logger.LogInformation("ShortVideo Injector: 开始探测常见安装路径 ({Count} 个候选)...", candidates.Length);
        foreach (var c in candidates)
        {
            var exists = Directory.Exists(c) && File.Exists(Path.Combine(c, "index.html"));
            logger.LogDebug("ShortVideo Injector: 探测 {Path} => 存在={Exists}", c, exists);
            if (exists)
            {
                logger.LogInformation("ShortVideo Injector: 在常见路径找到 jellyfin-web: {Path}", c);
                return c;
            }
        }

        logger.LogWarning("ShortVideo Injector: 所有路径探测均失败，未找到 jellyfin-web 目录");
        return null;
    }

    /// <summary>
    /// 反射获取 WebPath 属性，避免强引用 IServerApplicationPaths。
    /// </summary>
    private static string? GetWebPathByReflection(object appPaths)
    {
        var prop = appPaths.GetType().GetProperty("WebPath",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        return prop?.GetValue(appPaths) as string;
    }

    /// <summary>
    /// 执行注入。用正则匹配 </body>（大小写不敏感），在前面插入脚本。
    /// 没有 body 就追加到文件末尾。
    /// </summary>
    private static bool TryInjectFile(string indexPath, ILogger logger)
    {
        if (!File.Exists(indexPath))
        {
            logger.LogWarning("ShortVideo Injector: index.html 不存在: {Path}", indexPath);
            return false;
        }

        logger.LogInformation("ShortVideo Injector: 读取 index.html: {Path}", indexPath);
        var html = File.ReadAllText(indexPath, Encoding.UTF8);
        logger.LogInformation("ShortVideo Injector: index.html 大小 = {Bytes} 字节", html.Length);

        // 已注入则跳过
        if (html.Contains(InjectMarker, StringComparison.Ordinal))
        {
            logger.LogInformation("ShortVideo Injector: index.html 已包含注入标记，跳过");
            return true;
        }

        logger.LogInformation("ShortVideo Injector: index.html 未注入，准备写入 script 标签...");
        string modified;
        var bodyTag = Regex.Match(html, "</body>", RegexOptions.IgnoreCase);
        if (bodyTag.Success)
        {
            logger.LogInformation("ShortVideo Injector: 找到 </body> 标签，位置 = {Index}", bodyTag.Index);
            // 在 </body> 前插入
            modified = html.Insert(bodyTag.Index, ScriptTag);
        }
        else
        {
            logger.LogWarning("ShortVideo Injector: 未找到 </body> 标签，追加到文件末尾");
            modified = html + ScriptTag;
        }

        // 原子写：先写临时文件再覆盖，避免写入中途崩溃损坏 index.html
        var tmpPath = indexPath + ".shortvideo.tmp";
        logger.LogInformation("ShortVideo Injector: 写入临时文件: {Path}", tmpPath);
        File.WriteAllText(tmpPath, modified, Encoding.UTF8);
        logger.LogInformation("ShortVideo Injector: 覆盖 index.html: {Path}", indexPath);
        File.Copy(tmpPath, indexPath, overwrite: true);
        try { File.Delete(tmpPath); } catch { /* 忽略临时文件清理失败 */ }

        logger.LogInformation("ShortVideo Injector: 注入成功! script 标签已写入 {Path}", indexPath);
        return true;
    }
}
