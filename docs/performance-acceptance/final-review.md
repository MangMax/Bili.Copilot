# 整分支两轴审核及修正

固定审核范围：`4344ae756344f3ed6f1c11bd3216320b9143996f...bcce5f3106088469db93196515bc233799f8934c`，kernel `6501985..2ca79bd`。两个独立只读审查均读取仓库规则；Spec 轴通过 gh 读取 #45–#58。以下保留两轴各自结论，不以历史 393 项测试代替验收。

## Standards

未发现可证实的文档规则违规。Note：AIViewModel.Chat.cs 六个入口重复当前代次清理和异常归属判断，属于维护性判断，未证实当前运行错误，不据此扩大重构。Warning：不接受取消令牌的 SvgToImage 若长期不返回，会持续占用共享四槽；卸载能拒绝迟到发布，不能结束底层渲染。实际 renderer 风险仍需 #52 实机验证，不能宣称泄漏。

## Spec

P2：#53 要求末页无持续空转，但 PopularPage 在空结果或内部捕获失败后仍发送 VideoListUpdated，触发下一次延迟触底检查，可能形成重复请求。此为既有行为缺口，非新增回归。源码通知链已确认，未实机测量频率。

Warning：#45 的真实 WinUI 和 Release 发布采样仍不完整；#56 的条目/存活对象/可视卡片/内存采样缺失。未确认额外 scope creep。#56–58 的未来优化不属于当前必须实现的生产代码。

## #53 最小修正与回归

只在当前请求实际增加列表条目时发布 VideoListUpdated。版本检查仍在通知之前，加载状态仍正常释放；空页或失败不自驱动下一次检查，显式加载/刷新仍允许恢复。没有增加永久终止标记，也不凭空推断服务端游标语义。真实滚动/缩放事件仍可能触发独立检查；本修复切断的是无进展通知的反馈链。

`EmptyOrFailedPageDoesNotScheduleMoreLoadingAndManualRetryRecovers` 覆盖 Recommend、Hot、全站 Rank、分区 Rank 各自的空页与失败（8 例）。修复前全部因更新次数 1 而非 0 失败；修复后验证单次请求、无更新通知、loading 释放，随后手动加载成功、内容和通知恢复。实际链接生产 ViewModel，沿用现有 headless 服务/队列适配器，不是实机请求频率测量。已有非空分页/排行榜和滚动控件测试一起通过。

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --filter FullyQualifiedName~EmptyOrFailedPageDoesNotScheduleMoreLoadingAndManualRetryRecovers --logger 'trx;LogFileName=empty-page-red.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=empty-page-green.trx'
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
```

全套 401 passed / 0 failed / 0 skipped，已独立解析 TRX；Debug x64 构建退出 0、零警告/错误，耗时 26.84 秒。TRX 与构建日志在 ignored TestResults 下。该修正不改 XAML、布局和线程调度。独立后续复核及真实 UI 尚待完成，不能据此将 PR 转 Ready。

两轴计数：Standards 为 0 个硬违规、1 个维护性 Note、1 个待验证资源 Warning；Spec 为 1 个 P2 行为缺口（本地修正）、1 类缺失验收 Warning。各自最重要的剩余项分别为 SVG 资源等待风险与真实验收缺口。
