# 哔哩助理（独立维护版本）

本项目是 [Richasy/Bili.Copilot](https://github.com/Richasy/Bili.Copilot) 的独立维护版本。

- 与原项目相互独立：不向原项目提交改动，也不代表原项目
- 功能取舍与版本节奏由本仓库自行维护
- 原项目的设计、实现与文档版权归原作者所有

## 安装

前往 [Releases](https://github.com/MangMax/Bili.Copilot/releases) 下载最新的 `BiliCopilot_*.zip`，解压后右键 `Install.ps1`，选择 **使用 PowerShell 运行**。

脚本会自动导入随包证书并安装。若提示需要开发人员模式，请在 `设置 → 系统 → 开发者选项` 中开启。

## 从源码构建

```shell
git clone --recurse-submodules https://github.com/MangMax/Bili.Copilot.git
```

[bili-kernel](https://github.com/MangMax/bili-kernel) 以 git submodule 形式位于 `externals/bili-kernel`，通过 `ProjectReference` 直接引用，不需要本地 NuGet 源。

构建前需准备播放与混流依赖：

| 文件 | 目录 | 说明 |
| --- | --- | --- |
| `libmpv-2.dll` | `src\Desktop\BiliCopilot.UI\Assets\libmpv\x64`（或 `arm64`） | [mpv-winbuild](https://github.com/zhongfly/mpv-winbuild) 的 dev 构建，x64 取 `x86_x64`，arm64 取 `aarch64` |
| `ffmpeg.exe` | `src\Desktop\BiliCopilot.UI\Assets\ffmpeg\x64` | 同上仓库的 ffmpeg x64 构建 |

### 包身份

| 配置 | 包名 | 清单文件 |
| --- | --- | --- |
| Debug（开发调试） | `Mang.BiliCopilot.Dev` | `Package.Dev.appxmanifest` |
| Release（正式发布） | `Mang.BiliCopilot` | `Package.appxmanifest` |

两者包身份不同，可与原项目发布的包同时安装、互不覆盖。

## 许可

[GPL-3.0](LICENSE)，与原项目一致。

## 致谢

- 上游项目：[Richasy/Bili.Copilot](https://github.com/Richasy/Bili.Copilot)
- [Windows App SDK](https://github.com/microsoft/windowsappsdk)
- [WinUI](https://github.com/microsoft/microsoft-ui-xaml)
- [BBDown](https://github.com/nilaoda/BBDown)
- [哔哩哔哩-API 收集整理](https://github.com/SocialSisterYi/bilibili-API-collect)
