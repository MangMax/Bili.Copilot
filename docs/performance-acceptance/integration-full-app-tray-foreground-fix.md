# 完整应用托盘前台修复：独立集成复核

2026-10-31。固定父起点 `1384779e798aba1a368bae2c566b976ff101a935`，独立复核 docs-only `f66cc2a735180d17d4d7d050b21c12bc2d8f17dd`，无阻断后以 `--no-ff` 合入 `6f6b4a8af6642514422a9b2c6471c5189dc36a53`。本报告与 status 是后续文档提交；没有新增生产改动。

**仅解除已测完整应用托盘前台路径的未验状态。完整应用仍 PARTIAL、perf HOLD、#49 pause FAIL、AOT BLOCKED、历史 net9 crash 未归因、#56 预算/任意大重排限制全部保留。**

## 审查范围和方法

读取根 AGENTS、docs/agents 三份规则及 code-review、winui-code-review、verification-before-completion 技能；依显式要求独立完成 Standards/Spec 两轴，不启动子代理。不重跑 487、build、UI 或部署，不启动应用、不作桌面输入，不操作 PID9608/Dev、登录数据或注册。不读取旧原始媒体日志或认证存储。

Standards：交付仅 docs 下的验收报告、辅助脚本和证据；`git diff --check` 通过，无阻断。WinUI 审查集中于证据保真度、安全和发布边界，不新增 WinUIAnalyzer/AOT 通过宣称。

Spec：逐项复算来源、布局、加载 hash、前台与输入顺序、退出窗口、原失败保留和截图；以下有限结论通过。此处是既有运行证据的独立只读复核，不是新一轮桌面重播。

## 来源、文件与注册

- `334d47c78e04bebe836f6a8191b16859e6343843` 与父 `1384779e` 的非 docs 正式树一致；候选与父的非 docs 树也一致。
- 独立重算冻结 Release 的 **1887** 文件双向集合、大小、SHA256，以及冻结 files.json 与原集成 summary 的 hash；全部一致。
- activation、layout-v3、新旧 provenance 各 **670** 文件复算通过；旧 layout-v2 保留且全量 hash 一致。activation 与 v3 只有 AppxManifest.xml 不同，XML 比较除 Identity 的 Name/Publisher/Version 外相等。
- **1275** 源码输入 hash/双向集合、**134** 包的冻结解析图 hash、输出到 activation 的闭包及 recipe DLL 集合复核通过，无额外 DLL。
- 首轮播放 **162**、重开 **148** 包内模块路径与 SHA256 逐项重算一致，包括 UI.dll `67f0c0d0de1da249964bb11c79a506d50abf9b6120f7108fa9281bdbf5bde341` 和 coreclr `46a09d97ad8dd30d400a86c90af707bee7593b585d3ccb8586c4fffca404d1e2`；对应 RC3 `11.0.0-rc.3.26522.148`。这是已记录模块集合，不是全生命周期模块跟踪。
- 本次只读 `Get-AppxPackage` 确认仍注册 **2.2610.0.3**，路径为 `t3-233185ea/artifacts/full-app-tray-foreground-fix/layout-v3`，精确身份与 registration-after 一致。没有重新注册或覆盖旧 v2。
- kernel 为 `0ae5e4e000a41a07f6696746706df11c06e8835b`，工作树 clean，仅声明 local-only。

## 恢复、键盘与历史中断

交叉核对 case、actions、states、timeline 与 helper 实现。自建竞争窗为 PID27828/HWND790566，主窗为 PID24552/HWND14748754。四条路径均有恢复前主窗 hidden、自建竞争窗 foreground 的准备记录；正常打开系统托盘面板后，Shell/溢出面板会短暂成为前台。因此不将该准备采样误写成“实际托盘点击瞬间仍是竞争窗前台”。实际恢复均经正常托盘入口，无额外目标 focus/强抢前台。

历史播放器标题栏 UIA 关闭后，竞争窗第一次点击被遮挡拒绝，后续旧 overflow helper 多次因 HWND/几何与实际命中不一致而拒绝。四份 `case-History*.json` 失败保留。修正准备、打开真实面板后，第一次实际托盘左键恢复成立：`history-first-actual-restore.json` 在输入工具前已采到目标前台、可见且非最小化；随后真实 send-input 的 `trayhistory` 被 get-value 读回，再清空。**不称原历史全过程无间断重播，也不抹去多次准备中断。**

Mouse（主窗 UIA 关闭→左键）、Menu（主窗 UIA 关闭→右键“显示主窗口”）、Physical（真实鼠标标题栏关闭→左键）三个完整 case 均一次 PASS。各 case 的独立恢复采样早于键盘工具；send-keys 的 before 状态再次确认目标前台。逐条解析 get-value 原输出，确认 `traymouse`、`traymenu`、`trayphysical` 实际进入 TextBox，随后为空；没有提交搜索。

## 退出与失败保留

PID24552 与 PID16612 两轮 ready/observation 的 EXE 路径、creation FILETIME、startUtc、package 精确相等，保留句柄且 exitCode=0。独立调用既有 exit_gate 的纯 verdict，结合全部 query.ok、最后 query.startedSecondsAfterExit>=30 和 lastQueryUtc 判据，两轮均 **PASS_BOUNDED**；lastQuery 距退出分别 **30.066646s / 30.067722s**。Application1000、linked/unresolved WER 依精确身份判断，事件安全集合为空；未来精确错误仍可撤销有界结论。

`exit-observation-original.json` 与补充版仅 managedLogScan/managedUnhandled 不同，退出码、身份和查询事实一致。原日志 helper 属性赋值失败、verifier 拒绝、启动/播放器发现失败、竞争窗首次启动失败、遮挡/面板准备拒绝、裁图工具失败均保留。只读已交付日志摘要：首轮 4 条 search-suggestion 白名单异常、重开空集合，managedUnhandled 均 0；没有重读原日志，不能由摘要推断所有网络成功或弹幕完整。

## 截图及敏感交付检查

逐张打开全部 8 张 PNG，并核对动作记录及 evidence-ledger：

- `player-initial.png`：公开课程初始画面及密集弹幕。
- `player-hover.png`：后续课程画面，覆盖层未完整显示。
- `player-caption-hover.png`：课程标题、窗口按钮及播放控件可见。
- `player-short-p.png`：短 P 练习画面及公开课程信息侧栏。
- `player-short-playing.png`：后续练习演示画面及变化的弹幕；不据此编造 UIA 时间值。
- `main-restored.png`：已裁去顶部个人区域，6 张首页卡片，其余大片空白保留。
- `tray-exit-menu.png`：“显示主窗口/退出应用”精确菜单。
- `logged2/reopen.png`：裁顶后的重开首页，多排卡片、底部截断可见。

截图与有限叙述一致，不替代前台 API，也不扩大为完整视觉认证。源工作树逐项重算 evidence-ledger 的大小/hash 通过；压缩快照解压后 **57** 项文本与交付原件逐项相等。

合入后重新检查发现 `core.autocrlf=true` 的换行边界：`logged1/timeline.jsonl` 的 Git blob 匹配原 ledger，父签出转为 CRLF；`prepare.py`、`verify-source.py`、`verify.py` 的源文件为混合换行，Git 入库规范化为 LF，父签出转为 CRLF，因此这三份 helper 的 Git blob 与父签出均不匹配原始字节 ledger。独立确认四份文件差异仅为 CRLF/LF，规范化后与源原件完全一致；其余父签出文件大小/hash 匹配。原 ledger 不改写，不宣称所有 Git/签出字节均匹配原始 ledger；截图、运行 JSON 事实及冻结产物不受此差异影响。

扫描候选 Git 交付文本、嵌套 JSON、转义/URL 编码和解压快照，未发现媒体查询 URL、签名或凭据值；逐张视觉检查未发现这些内容。PNG 无文本/EXIF 元数据块或尾随数据；二进制压缩像素中的短模式匹配不能认定为凭据。检查仅输出路径/类别，不打印疑似命中值。

原报告承认“一次探索性工具输出误含媒体查询数据”，该事实及 failures 记录原样保留；交付扫描未发现不等于历史从未发生误输出。

## 状态边界

原证据记载驻留 Off、搜索清空、owned 应用/观察器/竞争窗归零、竞争窗 retained exit0、yield released:true。本轮不重新驱动桌面验证这些终态；保持注册、应用数据及新旧 layout。完整应用仍 PARTIAL；仅上述已测托盘前台恢复路径解除未验，历史无间断时序、其他完整应用覆盖、性能收益、AOT、#49、#56 限制继续保留。无 push、PR 或 issue 操作。
