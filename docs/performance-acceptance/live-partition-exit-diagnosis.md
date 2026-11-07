# 直播分区退出诊断（#170，来源 #168，母票 #169）

调查性文档。本轮不包含生产修复；结论以本机 2.2611.7.8 安装包、WER 报告、崩溃转储与应用日志为限。

## 结论摘要

- **进程退出可稳定复现**：已安装开发包 2.2611.7.8 上，从直播页「推荐」信息流进入分区「网游」时进程以
  `0xc000027b`（stowed exception）退出。4 次崩溃（10:40:11、11:18:29、11:21:15、11:21:49）签名完全一致：
  WER `EventType=MoAppCrash`、`IsFatal=1`、故障模块 `combase.dll`、异常代码 `0x80004003`（E_POINTER）、
  偏移 `0xc4063`、桶 `783bfb263944e27d699a93e8b912c34e`；事件日志 1000 的模块 `CoreMessagingXP.dll+0xadf5e` 是
  随后的 fail-fast 现场，不是根因。
- **首个可证的失败**是 `0x80004003`（E_POINTER）经 WinRT stowed exception → `RoFailFastWithErrorContextInternal2`
  → `RaiseFailFastException` 终止进程。**具体抛出语句未证实**：转储未捕获 stowed 记录（见「证据缺口」），因此
  「哪一行 C# 抛出 E_POINTER」仍属未证实的假设。
- **触发条件是「直播分区列表加载失败后的收尾路径」，不是任意直播页访问**：4 次崩溃前日志最后一条可读事件都是
  `尝试加载 全部网游 分区视频时出错` + `RichasyKernel.KernelException: 无法获取到有效的直播间列表`；
  对照用例「直播 → 单机游戏」（加载成功）与「直播 → 单机游戏 → 网游」（详情页已挂载）都不退出。
- **TemplateRoot not found 是既有、独立故障**，被 `App.OnUnhandledException` 的 `e.Handled = true` 吞掉，
  与退出无因果关系（同一进程内可发生数十次而不退出）。
- 分支对比：`8bceaed7`（#166 之前）与 `d6fcd68d`（含 #166）在直播模型、`ReloadableCoverImage`、
  `LivePartitionDetailViewModel`、`LivePartitionPageViewModel`、`LiveCardControl` 上**逐字节相同**；
  该区间这两条路径的唯一差异是 `LiveSubPartitionSelector` 从裸 `SelectorBar` 迁到 `SlidingTabBar`。
  「本轮接线回归」与「既有故障」**尚未区分**（缺旧基线安装包实测），但可证：崩溃现场位于选择器/详情页首次
  挂载的收尾路径上，而该路径正是本轮唯一被改动的 UI 接缝。

## 证据

### 1. 应用程序日志 1000（Application Error）

```powershell
Get-WinEvent -FilterHashtable @{LogName='Application'; Id=1000; StartTime=(Get-Date).AddDays(-4)} |
  Where-Object { $_.Message -match 'BiliCopilot' } |
  Select-Object -First 6 | ForEach-Object { $_.TimeCreated; $_.Message }
```

| 时间 | PID | 模块 | 版本 | 异常代码 | 偏移 |
| --- | --- | --- | --- | --- | --- |
| 2026-11-07 10:40:11 | 0x8E98 (36504) | CoreMessagingXP.dll | 12.6.5.0 | 0xc000027b | 0xadf5e |
| 2026-11-07 11:18:29 | 0x7C6C (31852) | CoreMessagingXP.dll | 12.6.5.0 | 0xc000027b | 0xadf5e |
| 2026-11-07 11:21:15 | 16072 | CoreMessagingXP.dll | 12.6.5.0 | 0xc000027b | 0xadf5e |
| 2026-11-07 11:21:49 | 40576 | CoreMessagingXP.dll | 12.6.5.0 | 0xc000027b | 0xadf5e |

应用时间戳均为 `0x6aee8e5e`，包 `Mang.BiliCopilot.Dev_2.2611.7.8_x64__y1mf40g4r9srg`。

### 2. WER 报告（比事件日志更权威，含 stowed 签名）

```
C:\ProgramData\Microsoft\Windows\WER\ReportArchive\AppCrash_Mang.BiliCopilot_104957cbb55d8d926741dbb9da76d6f9b70315b_6f0e1a2d_bc99342d-4e27-4cb5-8be7-9a12ab9b27c2\Report.wer
```

- `EventType=MoAppCrash`、`IsFatal=1`、`IntegratorReportIdentifier=17a2f87d-3015-47a5-a6d0-1a5745c6b94c`
  （与事件 1000 的 Report ID 完全相同 → 同一次崩溃）。
- `Sig[4]=combase.dll`、`Sig[5]=10.0.26100.9711`、`Sig[7]=80004003`、`Sig[8]=0xc4063`。
- 复现得到的报告（11:18:35，`AppCrash_..._cb35e58d-fb75-42e3-8925-e3931af99833`）签名逐字段相同。

对 `0xc000027b` 而言 WER 的「故障模块/偏移」取自 stowed 记录，**不等于**异常记录里的
`CoreMessagingXP+0xadf5e`；两者分别是「错误被记账处」和「fail-fast 触发处」。因此
`combase.dll` / `CoreMessagingXP.dll` 都不是应用侧根因。

### 3. 转储解析

转储：`%LOCALAPPDATA%\CrashDumps\BiliCopilot.UI.exe.36504.dmp`
（18,482,016 字节，sha256 `1e0aa4f7859a16b88c7c9d075fa22c355284b3321d2d9aff95439635c80e46a8`）。

```json
{"thread": 41636, "code": "0xc000027b", "address": "0x7ff86dd0df5e",
 "location": "CoreMessagingXP.dll+0xadf5e", "parameters": ["0x200498f6030", "0x1"], "count": 2}
```

只含 `MemoryListStream`（9503 个区间，地址跨度 `0x73d46000`–`0x7ffff44bb180`），**没有** `Memory64ListStream`。
参数 0 指向的 stowed 数组（`0x200498f6030`）与记录本身都在堆上，未被捕获 —— 这是本轮的硬证据缺口，
复现转储 31852 / 40576 同样如此。不要用「模块名 + 错误码」代替缺失的 stowed 记录。

### 4. 崩溃线程的符号化调用链

`walk.py`（DbgHelp `StackWalk64` + 用本机 PE 按 TimeDateStamp/SizeOfImage 兜底未捕获代码页）得到 31 帧，
自内向外（`36504-stacks.json`）：

```
KERNELBASE.dll              RaiseFailFastException                          +0x188
combase.dll                 RoFailFastWithErrorContextInternal2             +0x4a9
CoreMessagingXP.dll         WinUICreateDispatcherQueueController            +0x614e   (= +0xadf5e)
CoreMessagingXP.dll         ...                                             (+0x2f482 …)
user32.dll                  CallWindowProcW                                 +0x6a6
user32.dll                  GetFocus                                        +0x1e3c
user32.dll                  GetWindowDpiAwarenessContext                    +0x4d3
ntdll.dll                   KiUserCallbackDispatcher                        +0x24
win32u.dll                  NtUserGetMessage                                +0x14
user32.dll                  GetMessageW                                     +0x22
Microsoft.UI.Xaml.dll       (nearest export XamlCheckProcessRequirements)   +0x3c43e8
Microsoft.UI.Xaml.dll       ...                                             +0x3c498d
Microsoft.UI.Xaml.dll       ...                                             +0x1edb6b
BiliCopilot.UI.exe          (Native AOT, 无符号)                            +0xdce219
BiliCopilot.UI.exe          ...                                             +0xf0a9db
BiliCopilot.UI.exe          ...                                             +0x6367a1
BiliCopilot.UI.exe          ...                                             +0x52ef222
BiliCopilot.UI.exe          ...                                             +0x605e23f
kernel32.dll                BaseThreadInitThunk                             +0x17
ntdll.dll                   RtlUserThreadStart                              +0x2c
```

读法（已证实）：

1. 这是应用自己的 UI 线程：入口帧是 `BiliCopilot.UI.exe`（Native AOT），经 XAML 消息循环
   （`Microsoft.UI.Xaml` → `GetMessageW`/`DispatchMessageW` → `KiUserCallbackDispatcher`）。
2. 分派的窗口过程在 `CoreMessagingXP` 内，由它调用 `RoFailFastWithErrorContextInternal2` →
   `RaiseFailFastException`；**分派点与 fail-fast 之间没有应用回调帧**。
3. 因此「抛出者」不是这条栈上的应用语句：异常是在某个回调/ABI 边界被捕获后，通过
   `RoFailFastWithErrorContext` 记账并 fail-fast。真正的抛出点只存在于 stowed 记录里（未捕获）。

### 5. 应用日志时间线（`log-2026-11-07.txt`）

| 行号 | 时间 | 事件 |
| --- | --- | --- |
| 31400 起 | 09:07:21 | 首次 `System.InvalidOperationException: TemplateRoot not found`（栈：`ImageExBase.OnApplyTemplate` ← `ReloadableCoverImage.OnControlLoaded` ← `LayoutControlBase.OnLoaded`） |
| 65150–65344 | 10:39:34 | 同一条 TemplateRoot 密集爆发（本轮 46 次），全部经 `App.OnUnhandledException`（`e.Handled = true`）记录后吞掉 |
| 65345 | 10:40:10.682 | `尝试加载 全部网游 分区视频时出错` / `RichasyKernel.KernelException: 无法获取到有效的直播间列表`（在 `LivePartitionDetailViewModel.InitializeAsync` 的 catch 里记录） |
| 65350 | 10:41:15 | 应用重启 |
| 146135 | 11:18:29.212 | 复现：同一条「全部网游」KernelException → 11:18:29 进程退出 |
| 146502 | 11:21:48.785 | 复现：同一条 KernelException → 11:21:49 进程退出 |

崩溃前**没有任何** `Unhandled exception occurred.`：致命的那一个没有经过应用级 XAML 未处理异常处理器。
TemplateRoot 每日计数（`Select-String -Pattern 'TemplateRoot not found'`）：11-03 = 0、11-04 = 30、
11-05 = 111、11-06 = 185、11-07 = 146 —— 长期存在，且可在不崩溃的进程里大量出现（复现时的 11:20:12、
11:21:36 两次爆发后应用继续存活）。

### 6. 真机复现矩阵（2.2611.7.8，x64，150% DPI，2560×1440）

| # | 导航链 | 结果 | 最后一条日志 |
| --- | --- | --- | --- |
| 1 | 启动 → 直播 → 网游（推荐流直接进分区） | **进程退出** 11:18:29 | 全部网游 KernelException |
| 2 | 启动 → 视频 → 动画 → MAD·AMV → 直播 → 网游 | **进程退出** 11:21:15 | 同上 |
| 3 | 启动 → 直播 → 网游（最小复现） | **进程退出** 11:21:49 | 同上 |
| 4 | 启动 → 直播 → 单机游戏（加载成功） | 存活 | 无错误 |
| 5 | 启动 → 直播 → 单机游戏 → 网游（详情页已挂载） | 存活 | 同一条 KernelException |

复现脚本：`docs/performance-acceptance/live-partition-exit/repro-nav.ps1`
（`-Partition 网游` / `-Partition 单机游戏` / `-Fresh`）。每轮先确认 `Get-Process BiliCopilot.UI` 为空。

## 判定三分

### 已证实

1. 进程退出是 WinRT stowed exception → `RoFailFastWithErrorContextInternal2` → `RaiseFailFastException`
   的 fail-fast，HRESULT 为 `0x80004003`（E_POINTER），由 UI 线程的分派器触发；`0xc000027b` 与
   `CoreMessagingXP.dll+0xadf5e` 是现场而非根因。
2. 触发条件：进入「网游」分区时 `ILiveDiscoveryService.GetPartitionLiveListAsync` 抛
   `RichasyKernel.KernelException`（`无法获取到有效的直播间列表`），失败收尾后进程退出；
   同页面的成功路径与「详情页已挂载后再切到失败分区」都不退出。
3. TemplateRoot 异常被 `e.Handled = true` 吞掉，崩溃前日志无 `Unhandled exception occurred.`，
   因此它不是致命异常。

### 独立故障（既有，另行修复）

- `TemplateRoot not found`：`ImageExBase.OnApplyTemplate` 在模板未就绪时被
  `ReloadableCoverImage.OnControlLoaded` 无条件调用。跨 5 天、跨多个版本存在，且不导致退出。
  建议单开票据（封面控件生命周期），不要并入 #171。
- 同签名（`combase+0xc4063` / `0x80004003`）在 11-01、11-04 的其他构建上也出现过（例如
  `Mang.BiliCopilot.AotDev_2.2611.1.5`、`Mang.BiliCopilot.Search125`），说明「stowed E_POINTER →
  CoreMessagingXP fail-fast」这一失败**模式**在本仓库早已存在；本次只是找到了稳定的触发路径。

### 未证实（需要 full dump / 符号）

- E_POINTER 究竟由哪一条语句抛出（`LivePartitionDetailViewModel.InitializeAsync` 的空标签收尾、
  `LiveSubPartitionSelector.InitializeTags` 的 `SetItems(空, null)`、`SlidingTabBar` 的空列表选择器，
  还是页面切换时封面控件卸载/重挂载）。
- 「本轮接线回归」还是「既有故障」：需要同一路径在 `8bceaed7`（`SelectorBar` 版）安装包上实测。
- TemplateRoot 与封面 surface 重建是否会让上述收尾路径更易失败（相关性存在，因果未证）。

## 分支对比

```powershell
git diff --stat 8bceaed7 d6fcd68d -- `
  src/Desktop/BiliCopilot.UI/Controls/ReloadableCoverImage.cs `
  src/Desktop/BiliCopilot.UI/ViewModels/Components/LivePartitionDetailViewModel `
  src/Desktop/BiliCopilot.UI/ViewModels/View/LivePartitionPageViewModel `
  src/Desktop/BiliCopilot.UI/Controls/Components/LiveCardControl `
  src/Desktop/BiliCopilot.UI/Controls/LivePartition
```

结果只有两个文件：

| 文件 | 变化 |
| --- | --- |
| `Controls/LivePartition/LiveSubPartitionSelector.xaml` | 裸 `SelectorBar`（包在 `ScrollViewer` 里）→ `components:SlidingTabBar` |
| `Controls/LivePartition/LiveSubPartitionSelector.xaml.cs` | `OnSelectorChanged(SelectorBar, …)` → `OnSelectorChanged(object?, EventArgs)`；`InitializeTags` 先建列表再 `Selector.SetItems(items, selected?.Tag)`（旧版是 `Items.Clear()` + 直接赋 `SelectedItem`） |

直播模型、`LivePartitionDetailViewModel`、`LivePartitionPageViewModel`、`LiveCardControl`、
`ReloadableCoverImage` 在该区间**逐字节相同**。2.2611.7.8 对应的 `19a4f509` 也包含 #166（`ffd2e891`），
其直播/封面文件同样未改。

需要分别核查的三项：

1. 接口失败/空数据路径：两版都在 `LivePartitionDetailViewModel.InitializeAsync` 里
   `catch` 后继续走 `Initialized?.Invoke`，`Children` 保持 `null`、`CurrentTag` 保持 `null`
   （`tags.First()` 在空标签列表时还会抛 `InvalidOperationException`，但本次是服务端抛异常，走不到那行）。
   这条**两版相同**，属于既有缺陷。
2. 封面模板加载/卸载/重挂载：`ReloadableCoverImage` 两版相同，TemplateRoot 也是既有问题。
3. 快速导航卸载时序：**本轮唯一变动点**是 `LiveSubPartitionSelector` 及其宿主 `SlidingTabBar`
   （`OnLoaded/OnUnloaded` 重挂载、`LayoutUpdated`、`Indicator` 动画、`SetItems` 空列表）。
   崩溃恰好发生在该控件首次挂载后的收尾路径上，但缺少旧基线实测，不能断言回归。

## 最小复现步骤

1. 确认无其它实例：`Get-Process BiliCopilot.UI`（应为空）。**不要**重置/删除用户数据。
2. 记录环境：`Get-AppxPackage Mang.BiliCopilot.Dev`（2.2611.7.8 / x64 / y1mf40g4r9srg），
   显示缩放 150%，窗口 2560×1440。
3. `powershell -NoProfile -File docs/performance-acceptance/live-partition-exit/repro-nav.ps1 -Fresh -Partition 网游`
   （等价手工步骤：启动 → 侧栏「直播」→ 主分区「网游」）。
4. 期望（修复前）：约 0.2–1 s 内进程退出；`Get-WinEvent` 出现 1000/0xc000027b/CoreMessagingXP+0xadf5e；
   日志最后一条为「全部网游 分区视频时出错」。
5. 对照：`-Partition 单机游戏` 不退出；先「单机游戏」再「网游」也不退出。

## 修复边界与最小修复建议（供 #171）

崩溃发生在「列表加载失败后的收尾路径」：服务失败 → `catch` 只写日志 → `IsLiveLoading = false` →
`Initialized?.Invoke` → 选择器用**空标签列表**重建（首次挂载）→ 进程退出。修复应把这条路径变成
**可恢复的失败状态**，而不是加 try/catch 吞掉异常：

1. `src/Desktop/BiliCopilot.UI/ViewModels/Components/LivePartitionDetailViewModel/LivePartitionDetailViewModel.cs`
   `InitializeAsync`：把成功/失败显式分开——只有拿到非空 `tags` 才设置 `Children`/`CurrentTag` 并写
   `_childPartitionOffsetCache`；空列表不要调用 `tags.First()`（当前会抛
   `InvalidOperationException: Sequence contains no elements`）；失败时置一个可绑定的失败状态
   （如 `IsLiveLoading = false` + `HasLoadFailed`/`EmptyText`），保持 `Children`、`CurrentTag`、`Rooms`
   彼此一致（例如 `Children = []`、`CurrentTag = null`、`Rooms` 清空），并允许 `Refresh` 重试。
2. `src/Desktop/BiliCopilot.UI/Controls/LivePartition/LiveSubPartitionSelector.xaml.cs`
   `InitializeTags`：`ViewModel` 为 null、`Children` 为 null/空时不要向 `SlidingTabBar` 传入空列表，
   直接把选择器 `Visibility` 收起；有标签后再展开并同步选中项。避免「空列表 + 失效选中项」触发
   选择器重建。
3. `src/Desktop/BiliCopilot.UI/Controls/Components/SlidingTabBar.xaml.cs`
   `SetItems`/`Select`：`items` 为空时先 `HideIndicator()`、`_target = null`，并在
   `Selector.Items.Count == 0` 时跳过 `Selector.SelectedItem` 赋值/读取，保证空列表是显式的空状态。
4. 页面/导航（`LivePartitionPageViewModel.SelectSubSection`）：从失败分区离开或切到其它分区时不复用
   半初始化的 `LivePartitionDetailViewModel`（当前 `_partitionCache` 会缓存失败实例），并保证重启后
   「恢复上次直播页 → 同一分区」不会形成稳定崩溃环。

以上都是状态一致性修复，不改 `ILiveDiscoveryService` 契约、不改模型、不吞异常。若实施时证实根因只在
`SlidingTabBar` 的空列表分支，可把 1、2 收敛为「失败态不重建选择器」，把 3 单独成票。

## #171 的「修复前失败 / 修复后通过」断言

用假 `ILiveDiscoveryService` 控制成功/空/异常三种返回（复用 `LivePartitionDetailViewModel` 现有构造）：

1. **失败不退出**：`GetPartitionLiveListAsync` 抛 `KernelException` 时，执行 `InitializeAsync` 命令后
   （a）不向调用方抛出异常，（b）`Initialized` 仍触发一次，（c）`IsLiveLoading` 回到 `false`。
   修复前：在视图宿主中这条路径会让选择器以空列表重建并终止进程（端到端以「进程存活 + 无新
   1000/0xc000027b 事件」判定）；修复后：进程存活。
2. **空列表不炸**：服务返回 `tags = []`（`lives` 正常）时，不得出现
   `InvalidOperationException`（当前 `tags.First()` 会抛）；`Children` 与标签选择器保持一致
   （空/隐藏），`CurrentTag` 为 `null` 且后续 `LoadRoomsAsync` 不抛 `ArgumentNullException`。
3. **空状态可恢复**：失败态下调用 `RefreshCommand`（或再次进入该分区），当服务下一次返回非空标签
   与房间时，选择器展开、首标签被选中、`Rooms` 有内容，`_childPartitionOffsetCache` 记录新 offset。
4. **正向对照不回退**：成功返回 `tags=[t1,t2]` 时，首标签被选中、`Rooms` 按 `t1` 填充、offset 缓存正确，
   且不额外触发 `ChangeChildPartitionCommand`（选中同步不回发用户事件）。
5. **离开页面安全**：在失败收尾后立即切到其它分区/页面（`OnControlUnloaded` 路径）不抛异常，再次进入
   该分区仍可重试。
6. **真实宿主**：在安装包上执行第 3 节的导航链，修复后要求进程存活 60 s、日志出现失败提示而**没有**
   新的 `Application Error 1000`，且能正常切到其它可加载分区。

断言必须落在外部可观察行为（进程存活、日志、选择器可见性、`Rooms` 内容），不要断言私有字段或
「捕获了某个异常类型」。TemplateRoot 生命周期断言留给独立票据。

## 证据缺口

1. **stowed 记录未捕获**：三个转储都只有 `MemoryListStream`，参数 0 指向的数组在堆上。需要
   `LocalDumps\DumpType=2`（full dump，正在由主代理用 procdump 采集）才能读出 HRESULT 上下文、
   nested language exception 与 origin 栈，届时可把「未证实」第一条收敛。
2. **应用 AOT 帧无符号**：`BiliCopilot.UI.exe` 的 PDB 标识为 `BiliCopilot.UI.pdb` /
   `857DE865E95D49F3B62CCDED7A2069F4` / age 9，安装包不带 PDB。要映射
   `+0xdce219 / +0xf0a9db / +0x6367a1 / +0x52ef222 / +0x605e23f` 需按 `19a4f509` 重建并核对 PDB 标识
   （当前这些帧是线程入口与 XAML 消息循环，即使解析也大概率不含抛出点）。
3. **旧基线未实测**：`8bceaed7`（`SelectorBar` 版选择器）没有同版本安装包，无法判定回归/既有。
4. **服务端失败是否为唯一触发**：只在「网游」上观察到 `无法获取到有效的直播间列表`；若其它分区也会
   失败，应能同样复现（未逐一验证）。
5. 本机 `CoreMessagingXP.dll` / `Microsoft.UI.Xaml.dll` 无公共符号，帧名只能显示最近导出
   （`WinUICreateDispatcherQueueController+0x614e` 等），内部函数名不可得。
