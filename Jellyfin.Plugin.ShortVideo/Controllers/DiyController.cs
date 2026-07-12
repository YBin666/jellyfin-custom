using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShortVideo.Controllers;

/// <summary>
/// DIY 模块业务接口控制器。
/// 仅提供 DIY 相关的 API，不负责 Web 资源注入（由 WebAssetController 处理）。
/// </summary>
[ApiController]
[Route("Diy")]
public class DiyController : ControllerBase
{
    private readonly ILogger<DiyController> _logger;

    public DiyController(ILogger<DiyController> logger)
    {
        _logger = logger;
    }
}
