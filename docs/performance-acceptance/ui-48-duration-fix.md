# #48 当前 P 时长修复：独立复验

2026-10-30。本轮唯一生产修改是普通视频弹幕初始化由稿件 `Information.Duration ?? 0` 改为当前 `VideoPart.Duration`。新独立真实VM原九项9/9及三边界3/3通过，完整Release403/0/0；不把客户端规划缺陷归因为全部 1,953 个真实空响应。

## 基线与范围

- 父 checkout `t3-1d19cc80` / `feat/performance-45` 初始 HEAD `c9ac0822defd7804a0ef92e9b4bf316d0d1ddfe0`，只有父交付的一行未提交修改。确认 docs-only 后用 `git merge --no-ff d896f6dde4325e499410d184fd5fc5002e8d98de` 合入，merge `1e1966b9ba35ff9828cbc9937e899919cd0b56dc`；源码没有覆盖、reset 或 stash。
- [原报告](ui-48-player-viewmodel.md)及原 8 pass/1 fail、1–20 段 red、[1953 诊断](diagnosis-48-live-errors.md)、全部历史错误和证据原样保留。本报告单独记录修复后的执行。
- 应用 winui-code-review、winui-ui-testing、winui-dev-workflow、run-tests、verification-before-completion；边界测试按 code-testing 的 focused 流程直接扩展单个真实 VM fixture。未提供 code-testing-extensions 参考包，沿用原 fixture 与项目 xUnit 约定；不更换模型、测试框架或依赖。不启动子代理，不 push/PR/issue，不安装工具，不重试已知缺 MSVC 的 AOT。
- kernel 保持 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`；最终工作区检查见交接。

## 根因与零时长策略

`VideoAdapter.ToVideoPart` 将同一个 page 的 cid 与 duration 写入 `VideoPart`；模型 Duration 为非 nullable int。普通 VM 原来对当前 cid 使用稿件总时长规划，因此 7200 秒稿件/120 秒 P 会多计划 19 段。此次只让时长与 cid 来自同一个 `_part`；PGC 仍取当前 episode 秒数，未改分片算法、并发、取消或错误处理。

零/缺失时长采取保守策略：当当前 P 时长为零时不请求分片，不回退多 P 稿件总时长。模型没有 nullable Duration；不能构造“null P 时长”而保持正式模型不变。真实 API 缺失值是否统一映射为零、是否存在正时长媒体但 page duration 为零、负值/异常元数据分布仍未知。这种策略可能不加载该 P 的弹幕；本次不添加猜测时长、媒体时钟回退或产品按钮。

## 保真度与范围限制

[新宿主源码](harness-48-duration-fix/Fixture.cs)保留原九项测试及取消用例稿件/P均7200秒，仅追加三个边界，预期索引使用常量而不复制规划算法。完整生产 UI 源码复制到新目录编译，包含所有 PlayerViewModel partial、connectors、DanmakuRenderViewModel、DanmakuLoadSession、真实窗口与 dispatcher。仅宿主 App/离线 DI/包身份/JIT 构建配置不同。服务代理只位于公共边界，没有反射修改 VM 私有状态。

实际媒体为本地120秒静音 WAV；7200/360/361/0 是服务模型元数据，不声称 WAV 具有这些长度。原取消测试通过进入屏障、逆序迟到成功/异常释放及任务完成验证，不以延时推测请求已进入。连续 Reload 为生成命令级测试，生产没有 UI 入口。直接 Dispose 与真实 Window Destroy 的关闭终态仍可保持 Loading=true/CountText=null；通过表示取消及迟到隔离，并非重置该显示状态。

已登录完整包 `Mang.BiliCopilot.PerfAcceptance.Caa6a7be` 本轮不更新、不启动，保留注册/layout/登录数据；它仍是 `c9ac0822` 二进制，待父任务后续更新。真实登录已不再 BLOCKED LOGIN，已有[有限登录验收](full-app-authenticated-acceptance.md)。本次离线绿不证明所有真实网络在途请求均取消，不证明计数完整性，不撤销 #49 暂停 seek/渲染线程/关闭安全等阻断。

## 实际矩阵结果

**Passed: 12, Failed: 0, Skipped: 0；原九项 9/9，新增边界 3/3，runner exit 0。** [原始结果](evidence-48-duration-fix/results.json)、[观测](evidence-48-duration-fix/observations.json)、[独立 verifier](harness-48-duration-fix/verify.ps1)与[核验结果](evidence-48-duration-fix/verification.json)。

| Requirement | Evidence |
| --- | --- |
| 原 7200/120 当前 P 上界 red→green | `VideoPartDurationBounds` PASS：原测试断言未改，实际 indices=[1]、count=1、CountText=已加载 1 条弹幕；旧证据 indices=1–20/FAIL 保留。 |
| 普通换 P，旧成功/错误迟到隔离 | `VideoSwitchLateSuccessAndError` PASS：稿件/P 均7200，旧六请求 token 全取消、旧7–20不启动，新cid810002的20条均属第二代，Loading=false。 |
| PGC 时长及换集 | `PgcSwitchLateSuccessAndError` PASS：7200秒旧集六进入后切120秒新集，新cid850002只请求第1段，旧六取消，迟到成功/异常不发布。 |
| 同源连续 Reload | `RepeatedReloadLateSuccessAndError` PASS：前两代各六进入，第三代完成20条；前两代全取消且各仅六个请求，旧成功/错误不污染。命令级，没有新增产品入口。 |
| 直接 Dispose | `DirectDisposeInFlight` PASS：六进入后 DisposeAsync，六token取消；迟到释放及关闭后再次初始化/Reload均无新增请求、缓存为空、关闭状态不再被旧结果改变。 |
| 真实窗口 Destroy | `WindowDestroyInFlight` PASS：真实 Close→AppWindow.Destroy→OnWindowDestroying→DisposeAsync，相同六请求与关闭后隔离断言；Window.IsClosed/IsDisposed 均 true。 |
| VM→真实 HTTP 链取消 | `VmCancellationReachesHttpHandler` PASS：实际 DanmakuService→DanmakuClient→BiliHttpClient→受控 handler；6次进入/6次取消，命令完成后 Attempts仍6，错误数组为空，无重试/排队续发。 |
| 0 byte 普通错误 | `EmptyBodyRetriesThreeTimes` PASS：合成HTTP200/application-grpc/0 byte，Attempts=3，当前分片错误1，缓存0/无弹幕/Loading=false。 |
| 5 byte 合法空帧 | `FiveByteEmptyFrameParsesOnce` PASS：实际parser解析5零字节空帧，Attempts=1，错误0，缓存0/无弹幕/Loading=false。 |
| 当前P=0，稿件7200，不回退 | `VideoPartZeroDoesNotFallbackToArticle` PASS：真实本地媒体初始化成功，自动 InitializeDanmakuCommand 完成及真实 ReloadDanmakuCommand 后均 indices=[]，count=0/无弹幕/Loading=false；无需降级简化 VM。 |
| 当前P=360，稿件7200 | `VideoPart360RequestsOneSegment` PASS：初始化与 Reload 均 indices=[1]，当前cid正确，count=1/CountText一致/Loading=false。 |
| 当前P=361，稿件7200 | `VideoPart361RequestsTwoSegments` PASS：初始化与 Reload 均 indices=[1,2]，当前cid正确，count=2/CountText一致/Loading=false。 |

以上边界都同时覆盖总时长与 P 不同；新增用例等待真实命令任务结束并 drain dispatcher，不以“暂时没看到请求”判零段。

## 构建、TRX 与来源核验

全部命令实际退出0；SDK 10.0.403、winapp 0.7.1，xUnit2/VSTest 项目。日志随本报告保留。

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false --logger 'trx;LogFileName=release.trx' --results-directory docs/performance-acceptance/evidence-48-duration-fix
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
dotnet msbuild src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:ValidateRuntimeDependencies -p:Configuration=Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false -v:normal
& docs/performance-acceptance/harness-48-duration-fix/launch.ps1 -Destination C:/Users/hp/AppData/Local/Temp/bili48-duration-fix-1d19
& docs/performance-acceptance/harness-48-duration-fix/run-ui.ps1 -AppPid 62308 -Layout C:/Users/hp/AppData/Local/Temp/bili48-duration-fix-1d19/layout -Evidence docs/performance-acceptance/evidence-48-duration-fix
& docs/performance-acceptance/harness-48-duration-fix/provenance.ps1 -AppPid 62308 -Destination C:/Users/hp/AppData/Local/Temp/bili48-duration-fix-1d19 -Evidence docs/performance-acceptance/evidence-48-duration-fix
& docs/performance-acceptance/harness-48-duration-fix/verify.ps1 -Evidence docs/performance-acceptance/evidence-48-duration-fix
```

- Release **Passed: 403, Failed: 0, Skipped: 0**；TRX total/executed/passed/实际结果条目403，所有条目 outcome=Passed。[TRX解析](evidence-48-duration-fix/release-summary.json)；本次增量日志没有 warning 行，不能据此否认历史 protobuf 警告。该suite不替代真实VM矩阵。
- 正式 Debug x64：0 warnings / 0 errors，00:01:26.34；runtime gate：0/0，00:00:00.60，仅验证资产存在。
- 全新诊断宿主：**1522 warnings / 0 errors，00:01:25.27**。保留原宿主的 TreatWarningsAsErrors=false、JIT配置与简化副本 Directory.Build.props，不代表正式构建同配置。日志包含CS86xx nullable与VSTHRD线程规则；[warning行分类](evidence-48-duration-fix/warning-line-summary.json)含最终summary重复，共3044 warning行，不能将它误计为3044个独立警告。专用 WinUIAnalyzer 未安装/未运行；未改依赖/NoWarn，不宣称 AOT 安全。
- 新包 `Mang.BiliCopilot.Offline48.D1d19DurationFix`；[provenance](evidence-48-duration-fix/provenance.json)校验839个生产源码复制一致，两个宿主入口源码hash一致，六个实际加载模块包括libmpv与kernel。UI构建DLL/layout/实际加载模块 SHA256 同为 `C9F3B6088DCE0B5095D437D5534EBFAEC6EEF4C804046AD2587EE473BE1C134B`。取证时HEAD为docs merge且一行修复尚未提交，必须结合源码hash识别。
- [与旧red源码比较](evidence-48-duration-fix/source-comparison.json)：839源文件中仅 `PlayerViewModel.Danmaku.cs` 变化，新hash `FB274DEA10450A0B981A1F136A06E7E24FB9E76021EFCD820F1CF870465CA15B`。新Fixture原九项正文未改，只追加三个用例及边界方法；原源码/原证据均未覆盖。
- [运行日志摘要](evidence-48-duration-fix/runtime-log-summary.json)：10次本地ms-appx封面loader不支持scheme错误；1次由0byte测试预期产生的当前分片错误；没有SYNTHETIC-OLD-SEGMENT-ERROR发布。不能称宿主无错误或封面链路通过。

## UI 后独立 WinUI 审核与截图

本代理在build/UI之后重新核对正式diff、当前part赋值、生命周期/dispatcher/取消接线及测试断言，未发现这一行修复引入的必修问题。aid仍为稿件，cid和duration均在第一个await前取当前part；PGC分支和RelayCommand并发语义未变；未新增同步等待、UI类型、绑定、控件、颜色、资源或凭据读取。这里只审核改动及相关调用链，不把既有全仓警告消除或整应用可访问性列为通过。零时长的功能取舍和异常元数据未知边界见前文。

十张PNG均使用view_image逐张实际打开，逐图hash和观察见[visual-review](evidence-48-duration-fix/visual-review.json)。**完整视觉验收未通过/未覆盖**：duration、video-switch、before-dispose、before-destroy与多个boundary截图捕获控件动画中间状态，文字淡出/底部部分裁切；白色静音WAV画面上的进入弹幕只露右缘，不能凭截图数条目。PGC图的Episode 2和控件清晰，reload图标题/控件可读；没有打开计数面板，因此计数靠真实VM断言而非可见像素。results图是CAPTURE检查点，不是12项结果表。保留原图，不洗成完整视觉PASS；不据这些瞬间截图断言新的布局回归或#49已修复。

## 清理与交接

[cleanup](evidence-48-duration-fix/cleanup.json)：只停止精确owned layout的PID62308，卸载本轮owned包，确认二者均不存在。runner的[yield](evidence-48-duration-fix/yield.json)为 `released:true`；清理脚本再次[final-yield](evidence-48-duration-fix/final-yield.json)为 `released:false`、exit0，表示已无本workflow持有的槽，不能写成第二次仍released:true。

保留完整包注册与六项文件[前后hash一致](evidence-48-duration-fix/retained-package-after.json)，没有读取认证数据、操作PID9608或Dev/生产包。新宿主诊断源/layout留在独立Temp目录供追溯，但已无注册与运行进程。父任务仍需在授权后更新已登录完整包并补真实网络关联/完整性证据；本次没有此部署，1953条真实空响应上游原因不能全部归入当前P规划缺陷。

提交前仅去除 runner-output.txt 的末尾空行以通过 diff-check；原始 stdout 全文及原/归档 SHA256 保存在 runner-output-archive.json，provenance.uiLogSha256 仍指实际运行原日志。没有改写测试结果或生产/宿主源码。
