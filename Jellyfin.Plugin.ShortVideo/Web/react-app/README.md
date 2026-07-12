# 本地开发环境

## 快速启动

### 1. 启动 Jellyfin 服务
确保 Jellyfin 运行在 `http://localhost:8096`。

### 2. 启动 Vite 开发服务器

```bash
cd Web/react-app
pnpm dev
```

浏览器会自动打开 `http://localhost:5173/?dev=1`。

### 3. 配置开发凭据

在页面顶部工具栏输入：
- **Token**: 从 Jellyfin 的 localStorage 获取
  - 浏览器登录 Jellyfin → F12 → Application → Local Storage → `jellyfin_credentials` → `Servers[0].AccessToken`
- **UserId**: 同上位置 → `Servers[0].UserId`

点击"保存"后即可访问 Jellyfin API。

## 功能说明

### 工具栏

- **页面切换**: 短视频 / DIY
- **Token**: Jellyfin Access Token
- **UserId**: Jellyfin 用户 ID
- **保存**: 保存凭据并重载页面
- **重载**: 重新挂载当前页面组件
- **状态**: 显示连接状态

### API 代理

Vite 开发服务器会自动将以下路径代理到 Jellyfin (`http://localhost:8096`)：

- `/ShortVideo/*` - 短视频 API
- `/Diy/*` - DIY API
- `/Users/*` - 用户数据（收藏、播放状态等）
- `/Videos/*` - 视频流
- `/Items/*` - 媒体项（封面图等）
- `/web/*` - Jellyfin web 资源

### 自定义 Jellyfin 地址

如果 Jellyfin 不在默认地址，可通过环境变量指定：

```bash
# Windows PowerShell
$env:JELLYFIN_URL="http://192.168.1.100:8096"; pnpm dev

# Linux/Mac
JELLYFIN_URL=http://192.168.1.100:8096 pnpm dev
```

## 开发模式 vs 生产模式

| 特性 | 开发模式 (`pnpm dev`) | 生产模式 (`pnpm build`) |
|------|----------------------|------------------------|
| 入口 | `index.html` + `main-dev.jsx` | `shortvideo-entry.jsx` / `diy-entry.jsx` |
| 运行环境 | Vite dev server (5173) | Jellyfin 内嵌 |
| API 访问 | 通过 Vite proxy | 同源直连 |
| Token 来源 | 顶部工具栏输入 | localStorage.jellyfin_credentials |
| DOM 注入 | 跳过（无抽屉菜单、悬浮按钮） | 完整执行 |
| 热更新 | ✅ 支持 | ❌ 需重新编译 |
| React DevTools | ✅ 可用 | ✅ 可用 |

## 调试技巧

1. **React DevTools**: 安装浏览器扩展，可查看组件树和状态
2. **Network 面板**: 查看API请求是否正确代理到Jellyfin
3. **Console**: 查看日志输出，开发模式有更详细的日志
4. **快速重载**: 点击工具栏"重载"按钮，重新挂载组件

## 构建发布

开发完成后，构建生产版本：

```bash
pnpm build
```

产物输出到 `dist/` 目录，会被打包进插件 DLL。
