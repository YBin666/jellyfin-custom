# Jellyfin ShortVideo Plugin

基于React框架的Jellyfin短视频插件，提供类似TikTok/抖音的短视频浏览体验。

## 目录结构

```
Jellyfin.Plugin.ShortVideo/
├── Configuration/
│   └── PluginConfiguration.cs     # 插件配置类
├── Controllers/
│   ├── ShortVideoController.cs    # 短视频业务接口（/ShortVideo/NextBatch、Reload）
│   └── DiyController.cs           # DIY模块业务接口（/Diy/*，预留）
├── Infrastructure/
│   └── ScriptHost/                # 脚本托管基础设施（通用，与业务解耦）
│       ├── ScriptHostController.cs # 脚本资源控制器（/ScriptHost/*）
│       └── SelfInjector.cs        # JS注入器（修改index.html）
├── Services/
│   ├── FeedService.cs             # 视频流服务
│   ├── IFeedService.cs            # 服务接口
│   └── ShortVideoItem.cs          # 数据模型
├── Plugin.cs                      # 插件入口类
├── ServiceRegistrator.cs          # 服务注册
├── Jellyfin.Plugin.ShortVideo.csproj
├── Web/
│   └── react-app/                 # React前端项目
│       ├── src/
│       │   ├── common/            # 公共基础设施
│       │   │   ├── auth.js        # 认证Token获取
│       │   │   └── infrastructure.js # 路由注册、抽屉菜单、悬浮按钮
│       │   ├── shorts/            # 短视频模块
│       │   │   ├── ShortsPage.jsx # 短视频主页面
│       │   │   ├── VideoCard.jsx  # 视频卡片组件
│       │   │   ├── FavoritesPanel.jsx # 收藏面板
│       │   │   └── shorts.css     # 短视频样式
│       │   ├── diy/               # DIY模块
│       │   │   ├── DiyPage.jsx    # DIY页面
│       │   │   └── diy.css        # DIY样式
│       │   ├── utils/             # 工具函数
│       │   │   └── videoUtils.js  # 视频工具函数（直链/HLS回退）
│       │   ├── main-prod.jsx      # 生产环境入口（IIFE）
│       │   └── main-dev.jsx       # 开发环境入口（带凭据管理）
│       ├── dist/                  # 构建产物（打包进DLL）
│       │   └── inject.js
│       ├── index.html             # 开发环境HTML
│       ├── vite.config.js         # Vite配置
│       ├── package.json           # 前端依赖
│       ├── DEV.md                 # 开发指南
│       └── pnpm-lock.yaml
└── README.md                      # 本文档
```

## 设计思路

### 1. ScriptHost基础设施

ScriptHost 是**独立的前端脚本基础设施**，与业务代码互不侵入。只做两件事：修改 index.html 注入 script 标签，提供 inject.js 资源服务。

```
Jellyfin启动
  → Plugin构造函数执行
    → SelfInjector.TryInject() 修改 index.html
      → 在 </body> 前插入 <script src="/ScriptHost/inject.js"></script>
  → 浏览器加载 index.html
  → 请求 /ScriptHost/inject.js
    → ScriptHostController 直接从程序集嵌入资源读取 inject.js
    → 返回JS（IIFE格式单文件）
  → React代码执行，注册路由和基础设施
  → 用户访问 #/shorts 时挂载React组件
```

**职责分离**：

| 组件 | 职责 | 依赖 |
|------|------|------|
| SelfInjector | 修改 index.html，插入 script 标签 | 无（纯静态方法） |
| ScriptHostController | 提供 /ScriptHost/inject.js 资源 | 无（直接读嵌入资源） |
| ShortVideoController | 短视频业务接口 | IFeedService |
| DiyController | DIY模块业务接口（预留） | 无 |

**ScriptHost API**：

| 路由 | 方法 | 说明 |
|------|------|------|
| `/ScriptHost/inject.js` | GET | 返回前端引导脚本（从嵌入资源直接读取） |
| `/ScriptHost/Status` | GET | 健康检查，返回资源可用状态 |

**关键设计决策**：
- **零依赖**：ScriptHost 不依赖任何服务接口，直接从程序集嵌入资源读取，与业务代码完全解耦
- **通用命名**：`ScriptHost` 与 `ShortVideo` 业务名称无关，可被任何插件复用
- **幂等注入**：通过 `<!-- Jellyfin.ScriptHost injected -->` 标记避免重复注入
- **原子写入**：先写临时文件再覆盖，避免写入中途崩溃损坏index.html
- **优雅降级**：注入失败时静默跳过，插件仍可通过直接URL访问

**关键代码**：
- [ScriptHostController.cs](file:///c:/CodeFiles/IdeaProject/jellyfin-custom/Jellyfin.Plugin.ShortVideo/Infrastructure/ScriptHost/ScriptHostController.cs)：脚本资源控制器
- [SelfInjector.cs](file:///c:/CodeFiles/IdeaProject/jellyfin-custom/Jellyfin.Plugin.ShortVideo/Infrastructure/ScriptHost/SelfInjector.cs)：注入器核心逻辑
- [Plugin.cs](file:///c:/CodeFiles/IdeaProject/jellyfin-custom/Jellyfin.Plugin.ShortVideo/Plugin.cs)：插件入口触发注入

### 2. React构建策略

采用**单文件IIFE构建**，将所有依赖打包成一个自执行函数：

```javascript
(function(){"use strict";
    // React核心代码（约40KB）
    // ReactDOM核心代码
    // hls.js代码
    // lucide-react图标库
    // 业务组件代码（ShortsPage、VideoCard、FavoritesPanel等）
    // CSS样式（通过createElement注入到<style>标签）
})();
```

**为什么选择IIFE而非ESM**：
| 方案 | 优点 | 缺点 |
|------|------|------|
| ESM | 模块清晰，按需加载 | 需要`type="module"`标签，与Jellyfin原生环境不兼容 |
| IIFE | 可直接在普通`<script>`标签执行，零依赖 | 单文件体积较大，无法按需加载 |

**关键配置**：[vite.config.js](file:///c:/CodeFiles/IdeaProject/jellyfin-custom/Jellyfin.Plugin.ShortVideo/Web/react-app/vite.config.js)

```javascript
build: {
  rollupOptions: {
    input: {
      main: './src/main-prod.jsx'
    },
    output: {
      entryFileNames: 'inject.js',
      format: 'iife'
    }
  }
}
```

### 3. 路由管理

自定义hash路由与Jellyfin原生路由共存，互不干扰：

| 路由 | 处理方式 | 说明 |
|------|----------|------|
| `#/shorts` | React组件挂载 | 短视频页面 |
| `#/diy` | React组件挂载 | DIY页面 |
| 其他 | Jellyfin原生处理 | 首页、列表、详情等 |

**路由生命周期**：

```
进入 #/shorts
  → 隐藏Jellyfin的 reactRoot（display: none）
  → 创建全屏React容器（position:fixed; z-index:9998）
  → mountShorts() 挂载 ShortsPage 组件
  → 修改 document.title 为 "短视频 - Jellyfin"

离开 #/shorts
  → root.unmount() 卸载React组件（防止内存泄漏）
  → 移除React容器DOM
  → 恢复 reactRoot 显示
  → 恢复 document.title
```

**关键代码**：[infrastructure.js](file:///c:/CodeFiles/IdeaProject/jellyfin-custom/Jellyfin.Plugin.ShortVideo/Web/react-app/src/common/infrastructure.js)

### 4. 认证机制

Token获取采用**双模式策略**：

```
开发模式（pnpm dev）
  → localStorage.jellyfin_dev_credentials（工具栏输入）

生产模式（Jellyfin内嵌）
  → localStorage.jellyfin_credentials.Servers[0].AccessToken
```

**API请求自动附加Token**：

```javascript
export function apiUrl(path) {
  const token = getToken();
  let url = BASE_URL + path;
  if (token) {
    const sep = path.indexOf('?') >= 0 ? '&' : '?';
    url += sep + 'api_key=' + encodeURIComponent(token);
  }
  return url;
}
```

**关键代码**：[auth.js](file:///c:/CodeFiles/IdeaProject/jellyfin-custom/Jellyfin.Plugin.ShortVideo/Web/react-app/src/common/auth.js)

### 5. 视频播放策略

**两阶段播放回退机制**，确保最大兼容性：

```
视频加载
  → 尝试静态直链 /Videos/{id}/stream?static=true&api_key=xxx
  → loadedmetadata 后检查 videoWidth/videoHeight
  → 如果不支持（HEVC/H.265、VP9、AV1），回退到HLS转码
  → HLS URL: /Videos/{id}/main.m3u8?VideoCodec=h264&AudioCodec=aac&api_key=xxx
```

**HLS转码参数说明**：
- `VideoCodec=h264`：确保浏览器兼容（H.264是最广泛支持的编码）
- `AudioCodec=aac`：确保音频兼容性
- 保留所有原始query参数（包括api_key）

**关键代码**：[videoUtils.js](file:///c:/CodeFiles/IdeaProject/jellyfin-custom/Jellyfin.Plugin.ShortVideo/Web/react-app/src/utils/videoUtils.js)

### 6. 模块隔离

短视频和DIY功能在前端层面通过目录结构和组件隔离，后端通过独立的Controller提供API：

| 模块 | 业务Controller | 路由前缀 | React组件 |
|------|---------------|----------|-----------|
| 短视频 | ShortVideoController | `/ShortVideo` | ShortsPage |
| DIY | DiyController | `/Diy` | DiyPage |
| 脚本资源 | ScriptHostController | `/ScriptHost` | inject.js（统一引导脚本） |

前端代码打包在同一个 `inject.js` 中，通过 `main-prod.jsx` 统一注册路由，短视频和DIY各挂载独立的React组件。

**通信方式**：通过`window.__svRegisterRoute`、`window.__svGoBack`等全局接口进行模块间通信。

## 遇到的问题及解决方案

### 问题1：ESM模块无法在普通script中运行

**现象**：Vite默认构建为ESM模块，浏览器报`SyntaxError: Unexpected token 'import'`

**原因**：Jellyfin的index.html中插入的是普通`<script>`标签，不支持ES Module语法

**解决方案**：修改Vite配置为IIFE格式

```javascript
output: {
  format: 'iife'
}
```

### 问题2：多chunk导致资源找不到

**现象**：Vite生成多个chunk文件（vendor.js、infrastructure-CyVEq3Ob.js等），Controller无法识别带哈希的文件名

**原因**：Vite的代码分割会生成带哈希的chunk文件名，而Controller使用固定路径匹配

**解决方案**：合并为单一入口文件

```javascript
input: {
  main: './src/main-prod.jsx'
},
output: {
  entryFileNames: 'inject.js'
}
```

### 问题3：index.html写入权限不足

**现象**：SelfInjector无法写入`C:\Program Files\Jellyfin\Server\jellyfin-web\index.html`

**原因**：Windows默认保护Program Files目录，普通用户无写权限

**解决方案**：通过管理员权限设置文件权限

```powershell
icacls "C:\Program Files\Jellyfin\Server\jellyfin-web\index.html" /grant Everyone:F
```

### 问题4：嵌入资源命名不匹配

**现象**：Controller查找资源名`Jellyfin.Plugin.ShortVideo.Web.react-app.dist.inject.js`，但实际嵌入资源名为`Jellyfin.Plugin.ShortVideo.Web.react_app.dist.inject.js`（短横线变成了下划线）

**原因**：.NET嵌入资源命名规则将目录名中的短横线替换为下划线

**解决方案**：使用`EndsWith()`而非完全匹配

```csharp
var bundleResource = resourceNames.FirstOrDefault(r => r.EndsWith("inject.js"));
```

### 问题5：开发模式与生产模式环境差异

**现象**：开发模式使用Vite dev server（localhost:5173），生产模式通过DLL嵌入资源（同源），两者环境差异大

**解决方案**：
1. 使用`import.meta.env.DEV`区分环境
2. 开发模式跳过Jellyfin DOM操作（抽屉菜单、悬浮按钮）
3. 通过Vite proxy转发API请求到Jellyfin

```javascript
const IS_DEV = typeof import.meta !== 'undefined' && import.meta.env && import.meta.env.DEV;
if (IS_DEV) {
  console.log('[ShortVideo] 开发模式，跳过Jellyfin DOM注入');
  return;
}
```

### 问题6：SQLite数据库I/O错误

**现象**：Jellyfin启动时报`SQLite Error 10: 'disk I/O error'`

**原因**：Jellyfin进程未完全停止，数据库文件被占用

**解决方案**：确保Jellyfin进程完全停止后再启动，以管理员身份运行

### 问题7：视频播放兼容性问题

**现象**：某些视频（HEVC/H.265、VP9、AV1编码）在浏览器中无法播放，或只有音频没有画面

**原因**：浏览器对视频编码支持有限，尤其是移动端Safari

**解决方案**：实现两阶段回退机制，检测到不支持的编码时自动切换到HLS转码

### 问题8：React组件卸载时内存泄漏

**现象**：快速切换路由时，视频播放器继续占用资源

**解决方案**：在路由离开时调用`root.unmount()`，并在VideoCard组件中清理hls.js实例

```javascript
state: {
  destroy: () => {
    root.unmount();
  }
}
```

### 问题9：JS注入功能与业务耦合

**现象**：注入器和资源服务都以`ShortVideo`命名，与业务代码互相侵入

**原因**：最初设计时未考虑职责分离

**解决方案**：重构为通用的`ScriptHost`基础设施，只保留两个文件：`SelfInjector`（注入script标签）和`ScriptHostController`（提供JS资源），直接从程序集嵌入资源读取，不依赖任何服务接口，与业务代码完全解耦

## 开发流程

### 1. 环境准备

```bash
cd Web/react-app
pnpm install
```

### 2. 开发模式

```bash
cd Web/react-app
pnpm dev
# 访问 http://localhost:5173/?dev=1
# 在顶部工具栏输入Token和UserId（从Jellyfin的localStorage获取）
```

**开发模式特性**：
- Vite热更新，修改代码立即生效
- 通过Vite proxy转发API请求到Jellyfin
- 顶部工具栏管理开发凭据
- 跳过Jellyfin DOM操作（无抽屉菜单、悬浮按钮）

### 3. 构建生产版本

```bash
cd Web/react-app
pnpm build
cd ..
dotnet build -c Release
```

### 4. 安装插件

```bash
Get-Process -Name jellyfin | Stop-Process -Force
mkdir "C:\Users\Yangb\AppData\Local\jellyfin\plugins\Jellyfin.Plugin.ShortVideo" -Force
Copy-Item "bin/Release/net9.0/Jellyfin.Plugin.ShortVideo.dll" `
    "C:\Users\Yangb\AppData\Local\jellyfin\plugins\Jellyfin.Plugin.ShortVideo\" -Force
Remove-Item "C:\Users\Yangb\AppData\Local\jellyfin\plugins\Jellyfin.Plugin.ShortVideo\meta.json" -ErrorAction SilentlyContinue
Start-Process "C:\Program Files\Jellyfin\Server\jellyfin.exe" -Verb RunAs
```

## 功能特性

| 功能 | 状态 | 说明 |
|------|------|------|
| 短视频流 | ✅ | 无限滚动加载，自动播放，循环播放 |
| 视频播放 | ✅ | 静态直链播放 + HLS转码回退 |
| 进度条 | ✅ | 拖拽、时间显示、缓冲进度，hover时展开 |
| 全局静音 | ✅ | 一键静音所有视频，同步控制 |
| 收藏功能 | ✅ | 双击爱心收藏，右侧弹出收藏列表 |
| 抽屉菜单 | ✅ | 首页下方插入"短视频"和"DIY"项 |
| 悬浮按钮 | ✅ | 右下角短视频入口，仅在#/home和#/list显示 |
| DIY页面 | ✅ | 独立空白页面，用于功能测试 |
| 双击刷新 | ✅ | 双击HUB首页按钮强制刷新短视频流 |

## 技术栈

### 后端
| 技术 | 版本 | 说明 |
|------|------|------|
| .NET | 9.0 | 插件运行时 |
| Jellyfin.Controller | 10.11.3 | API控制器基类 |
| Jellyfin.Model | 10.11.3 | 数据模型 |

### 前端
| 技术 | 版本 | 说明 |
|------|------|------|
| React | 18.3.1 | UI框架 |
| ReactDOM | 18.3.1 | DOM渲染 |
| Vite | 5.4.10 | 构建工具 |
| hls.js | 1.5.13 | HLS视频播放 |
| lucide-react | 0.454.0 | 图标库 |

## 关键约束

1. **插件目录**：DLL必须放在`C:\Users\<用户>\AppData\Local\jellyfin\plugins\Jellyfin.Plugin.ShortVideo\`
2. **Token来源**：必须从`localStorage.jellyfin_credentials.Servers[0].AccessToken`获取
3. **路由格式**：使用hash路由（`#/shorts`、`#/diy`）与Jellyfin保持一致
4. **HLS转码**：必须包含`VideoCodec=h264&AudioCodec=aac`参数确保兼容性
5. **React卸载**：离开路由时必须调用`root.unmount()`防止内存泄漏
6. **注入标记**：使用`<!-- Jellyfin.ScriptHost injected -->`确保幂等注入
7. **模块隔离**：短视频和DIY模块完全独立，互不依赖
8. **ScriptHost命名**：注入脚本路径为`/ScriptHost/inject.js`，与业务解耦

## 日志排查

```bash
Get-Content "$env:LOCALAPPDATA\jellyfin\log\log_*.log" | Select-String "ShortVideo|ScriptHost"
```

**常见日志关键词**：
- `ShortVideo Plugin`：插件启动日志
- `ScriptHost Injector`：JS注入日志
- `ScriptHost`：脚本资源服务日志（inject.js加载）
- `ShortVideo Controller`：业务API请求日志

## 常见问题

### Q1：插件安装后功能未生效？

**排查步骤**：
1. 检查DLL是否在正确路径：`C:\Users\<用户>\AppData\Local\jellyfin\plugins\Jellyfin.Plugin.ShortVideo\`
2. 删除`meta.json`缓存文件
3. 检查Jellyfin日志中是否有`ShortVideo`或`ScriptHost`相关错误
4. 检查index.html是否包含注入标记`<!-- Jellyfin.ScriptHost injected -->`

### Q2：视频无法播放？

**排查步骤**：
1. 检查Token是否有效（从localStorage获取）
2. 检查浏览器控制台是否有401错误
3. 检查视频编码是否为H.264（非H.264需HLS转码）

### Q3：开发模式下无法访问Jellyfin API？

**排查步骤**：
1. 确保Jellyfin运行在`http://localhost:8096`
2. 在顶部工具栏输入正确的Token和UserId
3. 检查Network面板确认API请求是否正确代理

## License

MIT
