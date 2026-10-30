# #47 GC 验收资料独立集成复核

2026-10-30。结论：**已有有限受控总暂停收益证据，#47 不关闭**。完整阅读 [原报告](ui-47-gc-comparison.md)，独立执行已提交分析与来源核验，并从原 nettrace 重新离线解析；未发现影响暂停计算的实质错误。另发现历史截图文字记录与本次读取原图不一致，详见下文，不能隐藏或覆盖历史。

## 集成范围

- 父分支 `feat/performance-45` 初始 HEAD `be555ee3e33073d56789e9da0a3c4ebca00a03a7`，status 无输出；merge-base 与父 HEAD 相同。
- `git merge --no-ff 7d927b0d9d74853e5fde94a553074d17bbd6ecdd` 成功，merge 为 `e7640ed37f85f9aca200d53cbe5e79a7df56360b`。
- 遵守 AGENTS / docs/agents；已读 winui-code-review、verification-before-completion、dotnet-trace-collect。未启动子代理、UI 应用或桌面操作，无 push / PR / issue 操作。仅资料变更，不重跑生产 403 测试或 build，不将历史通过算作 fresh。

## 独立核验与可复现边界

在父 checkout 执行以下已提交脚本，均退出 0：

```powershell
python docs/performance-acceptance/harness-47-gc/analyze.py docs/performance-acceptance/evidence-47-gc
python docs/performance-acceptance/harness-47-gc/verify.py docs/performance-acceptance/evidence-47-gc
```

分析器包括旧 `c1-current`，因此机器 summary 有九条；正式比较严格只取 final1…6，final7/8 无采集器，不把旧宿主 c1 或无采集轮混进暂停比较。九份重写 JSON 经解析后与 HEAD 完全相等，仅换行序列变化，已恢复原文件，保留原 report/raw 不改写。

`verify.py` **依赖原机器本地产物，并非 checkout 自足**。本次直接读取仍存在的 `C:/Users/hp/AppData/Local/Temp/gc47-d0e5-current-e`、`gc47-d0e5-historical-e` 及八轮 `gc47-d0e5-layout-final*`，未创建替代工程、DLL 或来源文件。脚本核验的 cleanup 为历史 result.json 记录，不是本次重新检查进程/包，也没有重新取得桌面租约。

在脚本外补查了每轮 57 项 copied-source-hashes 清单：路径集合与正式 Markdown 目录完整一致，实际复制文件 hash 与清单一致，逐字节与当前源码比较。historical 唯一差异确实是在 `Clean()` 的 `_renderer?.Clear();` 后增加 `        GC.Collect();\r\n`；其他字节与 UTF-8 BOM 完全一致。八轮宿主源码与已提交 Experiment.cs 一致。build DLL、layout DLL 及 modules.json 所记模块路径上的实际 DLL hash 均对应：

- current：`686EF504EB5F085FB666CE14E4708F044309656BC3DAF36BD408B4504706D70B`
- historical：`8051E02E01846652481D15DBB59EA83D4D0B1DBFED98CC7D07FE428AD62F6797`

这把当前留存产物、源码清单、历史加载记录互相对应；不是在本次重新观察已结束进程的加载行为，也不是重新构建二进制可复现性证明。

六轮原始 nettrace 仍位于原工作区 `t3-d0e5de5d/artifacts/gc47/final1.nettrace` 至 `final6.nettrace`，逐一 SHA256 匹配 trace-hash.json。使用原缓存采集器的 **离线 parse** 模式逐个解析到 `%TEMP%/gc47-review-1d19cc80/finalN.json`；未 collect / probe / build / 启动宿主。按 compact-events.py 的七类事件筛选后，与提交 events.json 的所有字段、顺序完全相同；EventsLost 均为 0。

独立状态机重新核对而非仅信任脚本 exit 0：同一 PID；UTC 与相对时间排序一致；SuspendEEStart → SuspendEEStop → RestartEEStart → RestartEEStop 顺序完整；Start/Stop 按 ClrInstanceID + Count 唯一配对，无重复 Count、未闭合区间或负时长。measure-start、measure-end、observe-10 边界无跨界区间，10 秒观察窗无事件。测量每个暂停含一个完整 NonConcurrentGC；每代累计差与 Depth 推导计数一致。结果记录于 [独立机器复核](integration-ui-47-gc-audit.json)。

analyze.py 的通用防御仍有限：Start 字典可覆盖重复 Count，只断言 gen0 计数，不直接拒绝跨窗口区间；这些问题在本次独立逐事件检查中均未出现。对未来任意 trace 不能仅凭该脚本退出 0 宣称配对正确。

## 度量与结论限度

暂停是 SuspendEEStart → RestartEEStop，包含协调/恢复；不等于所有线程全程停止。完全挂起另算 SuspendEEStop → RestartEEStart；GC/Start → GC/Stop 是 collection duration，不能代替暂停。本次三种总量分别独立重算并与 summary 一致。GCMemoryInfo 只是最近一次 GC 的采样，不能累加补算事件。

三组 C,H / H,C / C,H、每轮 50 周期、Release JIT 自包含 .NET 9.0.22 workstation GC、离线固定小图片和真实 Markdown 控件的有限范围内：current 总暂停 **8.2502–16.1482 ms**，historical **93.5269–109.4341 ms**。测量窗 **7.627786–7.868042 秒**，不是精确等长窗口。current 最大单次 **5.0417–10.3865 ms**，historical **1.7310–2.4349 ms**，current 反而更长。小样本支持该协议下总暂停和同步清空耗时减少，不支持全面延迟、尾延迟、吞吐或统计显著性改善。

current 每轮 2 次为 Induced 与 InducedNotForced，**不得标 natural allocation GC**；historical 每轮 101 次 Induced，源码路径每轮有 100 次显式 GC.Collect，但没有调用栈，不能把每个事件逐一归因。无采集器 current/historical 的三代计数分别为 [2,2,2] / [101,101,101]，只提供计数层对照，不能证明逐事件原因或 collector 零扰动。小探针 AllocSmall 只证明采集能力。

Experiment.cs 的长期容器仅保存非泛型 WeakReference；rows 只保存标量/字符串/复制数组，不保存被观察控件、RichTextBlock 或 Block。shown 在测量前清空；临时 Loaded/LayoutUpdated/Rendering 订阅有 finally 解绑，Cycle 清空局部引用。仍有异步状态机、JIT、WinRT 生命周期的不确定性，静态检查不等于 root 分析。current 末尾 16–17 个活对象在零新 GC 的观察窗中不消失，既不能判泄漏，也不能证明全部可回收；historical 残留 1 个也相同。部分弱引用已死亡仅证明那部分对象未被永久强持有。

WPR 历史策略失败仍保留，但已有可复核 EventPipe pause trace，**WPR 失败不再代表没有暂停证据**。自然分配 GC 后全部对象回收、root、native 图片分配、长期无泄漏、完整应用/认证/provider、AOT、广泛图片/视觉和性能预算均未验。专用 WinUIAnalyzer 未运行。

## 失败历史与截图分歧

原报告与 visual-review.json 的四项“不完整”记录原样保留：final1/state-1、final4/state-4、final5/state-4、final8/state-4。本次直接读取这四张已提交 PNG，final1 可见完整标题、ALPHA、链接、logo 和 FINAL-OMEGA；后三张均可见 Next/Close 和 Ready 4，正文为空与清理后状态一致。**本次像素观察与历史“只剩左端/连工具栏也缺失”文字不一致**。24 张文件的 SHA256 均匹配原清单，且逐字节匹配目标提交；其中 final1/state-1 与被旧记录标 PASS 的 c1/state-1 hash 相同。

因此不能继续把旧描述当作已复证的 PNG 缺失事实，也不删除旧 FAIL、不把本次观察反写成当时捕获成功。该分歧的原因未定位，未对其余 20 图重新逐张做视觉验收，不据此宣称全部捕获可靠性通过。历史 r1-current 的 EmptyRemount=false 与异常、h2-historical 的 `Current image lost readiness after presentation` 已直接从原 records 核对，h1 timeout、早期构建/BOM 失败和宿主修订仍保留；最终通过不能消除它们。

## 最终门禁

相对父起点，正式 `src/`、`tests/`、`externals/` 与所有 docs 外路径无 diff；无正式依赖配置变化。kernel HEAD 为 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`，其工作区 clean。提交前运行 `git diff --check`，原证据目录与目标提交无 diff。最终提交 SHA 和提交后 clean 状态在交接回复记录。
