# 当前 P 时长修复：完整应用独立集成复核

2026-10-30，父工作区 feat/performance-45。**受控真实 VM 12 项与真实登录普通多 P 有限行为均通过；真实网络完整性未证，全应用总验收仍为 PARTIAL，#49 保持 FAIL。**

## 合入与审查范围

起点 c3b32c527cf7f80f2c0aaea81e92a822480efb56，工作区 clean，确认其为 9569c251b731f9c7195ce3e5127e2bd95002fe56 的祖先后，以 --no-ff 合入；merge 为 9f120b56ff145e8bc86ae47de5d1e7e82cfc1f92。差异仅 docs 下的报告、证据及安全分析脚本。已全文读取 AGENTS、docs/agents、winui-code-review、verification-before-completion 与[原验收报告](full-app-duration-fix-acceptance.md)。父级任务要求独立复核、不启动子代理；已更正报告的归因措辞。

本轮不启动应用、不做桌面输入、不改包注册或 app data、不读取账号状态/认证文件。登录保留结论来自原报告记录的两次可见首页入口、公开 BV 播放和重开行为；没有重新鉴权，也未读取 auth-state/reopen-auth 文件。没有 push、PR、issue 操作。正式 src/tests/externals 及依赖相对起点无 diff，kernel 为 2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d 且 clean。未重跑 403/build、未运行 WinUIAnalyzer；不宣称全应用静态审核或 AOT 通过。

## 独立只读核验

- 实时 Get-AppxPackage 精确身份/Publisher/InstallLocation 与[注册记录](evidence-full-app-duration-fix/registration.json)相同。新 layout 六项文件重新 SHA256；两份实际加载记录共 4+4 个模块逐项与现存文件重算一致。进程已退出，因此核对的是历史实际加载记录与当前文件，未重新观察进程模块。
- [provenance](evidence-full-app-duration-fix/provenance.json)的源码为 c3b32c5；来源工作区 src/tests/externals 相对该提交无差异，当前 PlayerViewModel.Danmaku.cs hash 与记录相符。build、layout 及两次加载记录的 UI DLL hash 均为 BE5A365A577756B29B7E3EDDF3C4EF3A7F1D12350DD92269522C1E19EF265D68。源码关联依赖原构建 provenance，不声称本轮重现构建。
- 旧 t3-caa6a7be/artifacts/full-app-acceptance/layout 六项文件重新 hash，与[旧基线](evidence-48-duration-fix/retained-binary-before.json)一致，仍保留原位。
- [受控真实 VM 12项](ui-48-duration-fix.md)及其 results.json 保持12 PASS。历史原8/1、1–20段 red、1953空响应及视觉限制均保留。

逐张打开四张已提交公开播放器 PNG，未查看私人页面；均与邻近状态及稳定计数复读一致。公开列表与播放器总时钟相差1秒，按不同来源记录，不混用。

| 场景 | 公开时长 / 播放器总时钟 | 选择、终态与截图核对 |
| --- | --- | --- |
| P1 | 06:54 / 06:53 | 首次分集观察关联；14:16:01.943计数3119，14:16:17.859仍3119；原图为Python应用场景，进度02:12 |
| P2 | 06:56 / 06:55 | 14:16:22.355选择，14:16:44.363计数2639，14:17:03.211仍2639；原图Python起源，进度00:40 |
| P8 | 01:46 / 01:45 | 14:17:07.352选择，14:17:21.860计数662，14:17:34.313仍662；原图命令提示符练习，进度00:14（状态00:13） |
| 重开P1 | 06:54 / 06:53 | 14:18:35.147计数3119，14:19:00.358仍3119；原图导学，进度00:23（状态00:22） |

依据[公开列表](evidence-full-app-duration-fix/public-parts.json)、[选择记录](evidence-full-app-duration-fix/operations.jsonl)、[动作时间](evidence-full-app-duration-fix/actions.jsonl)、[稳定计数](evidence-full-app-duration-fix/stable-counts.jsonl)和原报告四张图片链接核对。P1/P2/P8计划2/2/1段只是 ceil(Duration/360) 的规划值，绝非网络观测。P8过渡记录未开计数面板，不能证明切换时无旧计数残留。标题是整稿标题，不能据标题独立证明当前P。

[首次关闭](evidence-full-app-duration-fix/first-close.json)14:17:36.050及[重开后关闭](evidence-full-app-duration-fix/final-state.json)14:19:02.765均 exited=true、exitCode=0；末次 ownedRemaining=0、yield released=true。此为记录复核，不等于本轮再次关闭或长期生命周期安全证明。

## 安全脚本审查与复算

[脚本](analyze-48-live-errors.ps1)只打开固定身份包的固定日期日志，FileAccess.Read/FileShare.ReadWrite；输出只含固定分类、白名单异常类型/方法、明确标注的SocketException数值码、计数、时间及hash。不输出异常Message、原始块、URL、headers或账号数据。实现为 ReadToEnd 后内存分块，已修正文档，不声称流式读取或有界内存。

发现并修正参数仅匹配两位数字但未限制合法时钟范围的问题：改为ASCII小时00–23、分秒00–59、三位毫秒、严格首尾匹配。时间语义固定为2026-10-30 UTC+08日志时钟，开始包含/结束排除，同日非空，拒绝相等、逆序和跨午夜；不做时区转换。分类与白名单未改。参数拒绝与PowerShell语法检查通过。

实际重跑 StartTime=14:11:43.752、EndTime=14:19:02.885，完整JSON与[原安全摘要](evidence-full-app-duration-fix/business-error-summary.json)一致：0分片失败、0固定kernel-empty-response，2搜索建议取消块（14:13:51.303、14:18:15.607，TaskCanceledException→TaskCanceledException→IOException→SocketException，995）。搜索取消块单独归类，不能混成分片问题。默认旧窗口重跑的完整groups（包括hash）与[历史摘要](diagnosis-48-live-errors-summary.json)相同，分片分类总计仍1953。

零分片错误不是全部HTTP成功、完整性或取消证明；无requestID、成功分片观测、服务端总数或在途取消关联。不能将历史1953全部归因为当前P时长，也不能声称严格性能A/B。PGC本轮未测；既有受控与有限真实PGC证据不升级为本轮通过。关闭重开不替代无产品入口的Reload命令测试。#49暂停seek、线程所有权和关闭安全阻断原样保留。

## 后续绑定

后续唯一当前入口为 C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1630ecee/artifacts/full-app-duration-fix/layout。先核验上述注册位置，再由获授权任务按 AUMID Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg!App 激活，以该目录 BiliCopilot.UI.exe 路径重新发现PID/HWND。不要复用已退出句柄或旧layout EXE；保留新旧layout、现有包注册及登录app data。本轮未执行激活或回滚。

新增/修改文档相对链接与 git diff --check 已检查；仅修正上述小问题及当前状态入口，没有覆写历史原始red或证据文件。
