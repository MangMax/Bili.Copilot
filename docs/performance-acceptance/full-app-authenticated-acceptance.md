# 已登录完整应用验收：有限通过，#48 未完成

2026-10-30，约 12:45–13:07（UTC+08）。本轮确实进入已登录首页、搜索结果和真实播放器；不再是 BLOCKED LOGIN。**不能签署 #48 或整应用全部通过**：多 P/多集播放、部分切换和加载中关闭有实际证据，但严格的在途切换交错、同源连续重载、底层取消及计数完整性仍缺证。没有修改生产代码或新增遥测。

## 身份与来源

- 本验收工作区：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-f8558b57`，分支 `t3/authenticated-full-app-acceptance`。
- **部署源码为 `c9ac0822defd7804a0ef92e9b4bf316d0d1ddfe0`**，不是本轮文档 HEAD。构建来源为前一轮交接；本轮核对构建工作区相对该 SHA 的 `src` / `externals` 无差异，并核对已加载模块及 layout 六项哈希全部与交接一致。
- UI DLL SHA-256：`8CFB9B382CFED51F9A7CCFE28A40627807FC40D4B849910D59AD8D1D7A80DD2B`。见 [部署六项核验](evidence-full-app-authenticated/deployment-hashes.json)、[实际进程加载模块](evidence-full-app-authenticated/loaded-module-hashes.json)。Debug x64 JIT，不是 AOT。
- kernel：`2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`；本工作区与构建工作区嵌套 checkout 均干净。
- 唯一操作身份：`Mang.BiliCopilot.PerfAcceptance.Caa6a7be`；AUMID `Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg!App`；full name `Mang.BiliCopilot.PerfAcceptance.Caa6a7be_2.2610.0.1_x64__y1mf40g4r9srg`。
- InstallLocation 保持 `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-caa6a7be/artifacts/full-app-acceptance/layout`。每次激活后均按该目录的精确 EXE 路径发现 PID，再 scoped `list-windows` 获取 HWND，没有绑定同名其他窗口。
- 交接 `a4a0042365cfb56662a6a6e6c63505dae5e16081` 经核对仅 docs 后 cherry-pick 为 `ab94f67`。保留原报告，未回写历史失败。
- 已读 AGENTS、docs/agents、交接全文及相关验收/status；应用 winui-ui-testing、winui-dev-workflow、winui-code-review、verification-before-completion，异常调查使用 systematic-debugging。没有子代理；未安装、重建、重装、卸载、push、PR/issue 写操作或收费 AI 调用。WinUIAnalyzer 未运行；未重跑 403 或已知缺 MSVC 的 AOT。

## 实际入口与内容

先读 `winapp --help` / `run --help` / `ui` 各实际使用 verb 的 help。现有 CLI 的 run 可能注册/部署，因此采用系统 AUMID 激活现有注册包：

```powershell
Start-Process explorer.exe -ArgumentList 'shell:AppsFolder\Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg!App'
```

首次新 PID48296 / HWND8265322 已有搜索框、流行页和真实卡片，未进入登录页，未索取登录或读取认证存储。首次试输 `BV1GJ411x7h7` 的实际结果成为 `BVGJ1型号`，作为输入路径偏差保留，未冒称正确输入；精确 SetValue + get-value 核对后才提交 BV。该 BV 是 Rick Astley 单视频，只作基本解码/真实弹幕观察，**不算多 P 矩阵**。

正式内容：

- 普通视频 **BV1qW4y1a7fU / AV941747210**，黑马程序员 Python 课程；P1「Python导学视频」、P2「第一阶段-第一章-01-初识Python」、P3「02-什么是编程语言」、P4「03-Python环境安装(Windows)」、P5「04-【拓展】-Python环境安装(MacOS)」。标识取自实际播放器信息面板。
- PGC **SS24588《工作细胞》**：第1集「肺炎链球菌」、第2集「擦伤」**EP232466**、第3集「流行性感冒」**EP232467**、第4集「食物中毒」**EP232468**。标识取自实际信息面板。本账号可直接播放，未出现购买操作，没有付费、关注、点赞、投币、收藏、评论、发送弹幕、分享或下载。

## Requirement | Evidence

| Requirement / 场景名 | 结果与实际证据 |
| --- | --- |
| `AuthenticatedHome`：完整已登录入口 | **PASS（有限）**。AUMID 启动后首页内容、搜索、真实视频/PGC 可达；多次重新激活仍进入首页。没有验证账号详情。 |
| `VideoParts`：普通多 P 切换 | **PARTIAL**。真实搜索 Python 教程后选择黑马课程；P1→P2，后续 P3→P4→P5；P2 可见课程对应画面和“已加载 2,639 条弹幕”。[计数图](evidence-full-app-authenticated/multip-count-visible.png)。P4 过渡捕获“连接中”，旧画面/旧计数仍可见，[加载图](evidence-full-app-authenticated/multip-p4-loading.png)。没有证明每次切换都发生在旧弹幕请求仍在途时，未把 UIA 命令成功等同媒体完成。 |
| `VideoLoadCloseReopen`：普通视频加载中关闭并重开 | **PASS（可见生命周期）/ PARTIAL（弹幕取消）**。**12:57:12.980** 选择 P5、随后只读树仍有“连接中，请稍候…”，**12:57:13.618** invoke owned Close；PID24888 于 **12:57:14.031** 退出码0。后续同包重新打开同 BV，画面/弹幕可播放。加载提示是媒体连接状态，不等同弹幕 HTTP 在途证明。 |
| `PgcEpisodes`：PGC 多集切换、再次加载成功 | **PASS（有限 UI）**。第1→2集，后续重开并选第3→4集；EP232468 的画面、标题“工作细胞 - 食物中毒”、计数 **69,901** 一致可见，[最终图](evidence-full-app-authenticated/pgc-ep4-count.png)。[三次短时采样](evidence-full-app-authenticated/pgc-stability.json) 13:03:39.869–13:03:41.754 未见旧标题/计数回写；不是任意迟到响应的证明。 |
| `PgcLoadCloseReopen`：PGC 加载中关闭、再开 | **PASS（可见生命周期）/ PARTIAL（弹幕取消）**。选择第3集，先等到“连接中”，截图时已变“加载中”，信息面板 EP232467；[实际截图](evidence-full-app-authenticated/pgc-ep3-loading.png)。关闭 owned HWND2360396，PID15668 于12:59:22.035退出码0。再开后能选第3、第4集并播放。 |
| `OverlapSwitch`：加载期间连续换 P/集 | **未充分验证**。一次 Next 后 DanmakuOptionButton=false；紧接的 Next 因不可调用失败。分集面板重建会使旧 selector 失效；保留失败，并按新面板重新定位。部分 ItemContainer 的 UIA ancestor 报 isEnabled=false 而 SelectionItemPattern 仍成功，故不将这类程序化选择作为真实可点击的严格并发交错证明。未注入延迟或调用私有方法。 |
| `ContinuousReload`：同源连续重载 | **NOT TESTED（产品入口缺口）**。当前源码的 ReloadDanmakuCommand 没有找到 XAML/代码调用入口；PlayerOverlay 中 ReloadCurrentSourceCommand 的错误页按钮位于注释中。没有将重新打开播放器、重复点当前选集或切源冒充同一实例连续重载，也没有反射注入。 |
| `CountsAndCancellation`：计数/内容及底层取消 | **PARTIAL，不能签全通过**。显示计数与当前 PGC 标题/画面配对；未独立取得内存 items.Count、服务返回总数或认证网络 trace。日志有大量分片失败，计数可能为成功分片的部分结果。既有15测试仅补足受控会话/HTTP边界，见下节。 |
| `SearchAndRegion`（#51）：真实输入、选择、区域导航 | **有限 PASS**。13:02 左右 `send-keys --via send-input --verbatim 工作细胞`，get-value准确为“工作细胞”；Enter 后等待“搜索 工作细胞”，选择动漫类别，再选择《工作细胞》结果进入播放器。视频导航→科技分区后出现科技标题及数码/软件应用/计算机技术栏目和真实卡片，再回流行页。未完成搜索建议鼠标选择、防抖请求计数和完整 IME 矩阵；初次 BV 输入偏差保留。 |
| `HomeSwitchBottomRefresh`（#50/#53） | **有限 PASS**。推荐→热门→全区排行榜→滚动到底部，真实末尾卡片与空余末行可见；刷新后可见首行恢复，[刷新图](evidence-full-app-authenticated/ranking-refresh-visible.png)，再切回推荐可见卡片。最初仅等 RootCard 会命中 offscreen旧节点，后续改要求 IsOffscreen=False。刷新瞬间 scroll 报不可滚动，保留失败，待可见卡片后重新检查。没跑无限推荐流的“最终页”、失败 API 刷新恢复或后台请求静止计数。 |
| `StandaloneModes`（#54） | **有限 PASS**。临时关闭“播放时隐藏主窗口”后主窗口与播放器同时存在；真实小窗→Esc恢复→全屏→Esc恢复，截图已逐张看过，小窗 [证据](evidence-full-app-authenticated/mode-compact.png)。置顶从 false→true→false 由只读 GetWindowLong(GWL_EXSTYLE) 核对，[结果](evidence-full-app-authenticated/topmost.json)。关闭播放器后主窗口继续存活。操作为秒级批次，不声称覆盖500ms内部竞争；小窗 CompactOverlayButton不暴露，改走实际Esc恢复，原失败保留。 |
| `IntegratedClose`（#54） | **有限 PASS**。原设置 HideMain=On、Integration=On；设置文案明确“关闭播放窗口视作关闭应用，点击返回按钮才显示主窗口”。两次受控集成播放器关闭均正常退出0。未完成集成模式的全套快速切换/返回导航矩阵、跨进程设置组合、托盘或多屏。 |
| `PausedSeek49`（#49） | **既有 FAIL 保持**。本轮没有新做暂停seek/双引擎暂停切换；播放成功不撤销既有阻断。 |

## 取消证据与异常边界

[安全业务摘要](evidence-full-app-authenticated/business-error-summary.json) 仅读取这个包 `LocalState/Logger/log-2026-10-30.txt` 的已有日志，以共享只读方式在内存筛选，再保存时间/分类/异常类型；没有保存原始日志、媒体地址、请求 headers、账户字段或认证数据。时间窗从12:45到本轮结束，涵盖多个播放进程，未建立每个请求的关联标识。

采集限制：早期 UIA 工具输出曾意外包含 mpv 原生子窗标题中的媒体地址；随后改为白名单提取控件/标签，未沿该地址请求、抓包或写入交付文件。上述“没有保存”指本轮文件证据，不能理解为工具输出从未出现过该标题。提交材料另做敏感字段模式扫描，未检出这些地址参数或认证字段。

- **1,953 条“获取弹幕分片失败”**（12:49:34.546–13:01:30.303），类型均为 `RichasyKernel.KernelException`；[类型摘要](evidence-full-app-authenticated/danmaku-error-types.json)。没有安全取得可判定根因的结构化错误码，不能将所有失败统称正常取消，也不能凭此确定是 #48 新回归。
- 另有6条搜索建议失败、9条其他错误消息（正文未留存）。错误段中能识别的 System 类型包括 IOException、SocketException、TaskCanceledException；统计不是请求数，也不是网络取消完成证明。
- 时间窗内没有 `Unhandled exception occurred.` marker；查询本轮系统 Application 1000/1001/1026 未得到关联记录。不能据此保证所有 native 异常不存在。
- 早期 PID27348、11816 在后续操作时已不存在，未提前持有退出句柄，**退出原因未定**，不认定确定崩溃、不洗成通过。之后 PID24888/15668/45048 均持有只读进程句柄观察，明确关闭后退出0；第一轮 PID48296也是显式Close后消失，但没有退出码。
- 另有 UIA foreground_not_target（弹层存在）、旧selector失效、错误就绪判据、unsupported VerticalScrollPercent 等工具/脚本失败，均保留在操作记录或本报告，不当作产品缺陷。没有绕过前台输入保护、用系统按键抢其他应用或无限重试。

既有 [#48报告](issue-48.md) 的15项测试覆盖：旧会话失效、六并发/排队取消、迟到结果/错误不发布、连续同源重载、关闭、HTTP取消保持、Web/gRPC取消不重试与普通失败仍重试。那些是历史受控测试，本轮**没有重跑**，更没有把它们改称真实账号网络实测。UI 所见“连接中/加载中”、最终计数和进程正常退出，只补足真实接线的有限观察。

## 复放与证据读取

1. 保留原包及 layout，按上述 AUMID 启动；以精确EXE路径发现进程，再 `winapp ui list-windows --on local -a <PID> --json`。旧PID/HWND均不可复用。
2. 普通视频在搜索框精确输入 BV1qW4y1a7fU，核对 get-value 后Enter。移动到标题栏使控件显示，WidgetButton→分集→相应课程条目。面板可能重建并重置至信息页，必须重新取当前selector，不能缓存旧条目。
3. 观察 DanmakuOptionButton 的禁用→启用，以及媒体连接提示与画面；打开弹幕设置，滚动其内部面板到底部读取标题/计数。首个启用采样可能发生在新初始化开始前，不能仅凭瞬时true判完成。本轮PGC初期截图出现新标题配旧计数，后续新实例加载完成后才取得69,901终态。
4. PGC 从“工作细胞”搜索→动漫→精确同名结果进入，分集选第2/3/4集；信息面板可核对 SS/EP。出现媒体加载提示后关闭owned播放器；重新启动/打开验证恢复。
5. 独立窗口仅临时关闭“播放时隐藏主窗口”，记录原值。CompactOverlayButton→Esc、FullScreenButton→Esc；标题栏Pin开关两次，最后关闭播放器。恢复 HideMain=On、Integration=On。

[操作JSONL](evidence-full-app-authenticated/actions.jsonl) 从受控批次开始保存每条实际指令、时间、HWND、CLI退出码；早期探索在上文记录，不伪造为完整原始事件。配套 [helpers.ps1](evidence-full-app-authenticated/helpers.ps1) 是调用/检查辅助，不是能无条件重跑的完整测试套件；selector必须新发现。inspect原树不落盘；结果断言与图片观察分开。

23张实际PNG均逐张查看，见 [视觉账本](evidence-full-app-authenticated/visual-review.json)。仅提交6张必要图片；推荐页含“已关注”等个性化信息的图片只留本地ignored，未提交。其余本地图片也未充当已发布证据。完整屏幕/账户页/二维码/登录挑战均未截图。播放器图片含公开课程/番剧和公开弹幕；弹幕密集遮盖属于此设置下的实际内容，不据此声称全视觉矩阵通过。

## 恢复、收尾与同步

- 设置原值 [before](evidence-full-app-authenticated/settings-before.json) 与 [restored](evidence-full-app-authenticated/settings-restored.json) 均 HideMain=On、Integration=On；仅HideMain临时Off，未改全局DPI或系统设置。置顶最终false；普通窗口恢复到原1742×1021。
- 最后关闭本轮owned播放器，并通过主窗口Close正常退出。最后PID45048退出码0，13:07:08.496；其主窗口原HWND9377802。**最终精确路径进程0、owned播放器窗口0、主窗口0**。三个受控句柄观察已结束，无待运行UI批次。
- 同workflow `bili-auth-f8558b57` 调用 `winapp ui yield --on local --json` 实际返回 **released:true**；之后没有桌面输入。见 [最终状态](evidence-full-app-authenticated/final-state.json)。桌面已释放。
- 注册包和layout保留，登录数据未读取/复制/删除，后续可以按AUMID重开。不执行旧交接中可能导致卸载的清理步骤。未触碰PID9608、Dev/生产包。
- `git merge --no-edit feat/performance-45` 返回 Already up to date；同步核对父分支仍为 `c9ac0822defd7804a0ef92e9b4bf316d0d1ddfe0`。最终文档提交SHA与clean状态在交付消息报告。
- PR27/43不在构建中，其兼容性未验；真实性能、Release/AOT、长时稳定、所有迟到网络交错、搜索建议/IME完整矩阵和#49缺陷仍开放。没有因本轮有限结果关闭任何工单。
- WinUI代码审查范围仅实际操作入口、显示/关闭逻辑和本次文档辅助脚本；没有生产代码diff，不声明全应用静态审查通过。交付一致性核验见 [verification.json](evidence-full-app-authenticated/verification.json)，它不是新增产品测试通过数。
