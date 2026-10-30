# NET11 完整应用验收独立集成复核

2026-10-31。指定候选 `91ef66b9fcf4ac937c65c1c64b19f47aa021c0a6`，父起点 `043d1e8a87f67e0e56967a2bfc536818c2bd4282`（复核前 clean）。本地 `--no-ff` merge：`698ff8b80426969298670619a92f61b1c27d5cab`。源证据原样保留，审查只新增本文、更新 status。

**完整生产 RC3 Release x64 JIT 三次退出有限通过；完整应用 PARTIAL，总体性能 HOLD。** 多P、可访问免费/限免 PGC 换集、IME、模式切换、字体首次打开有限通过，不是 AOT、长期稳定或收益证明。

## Standards 与 Spec

已读 AGENTS、docs/agents 三份规则及 code-review、winui-code-review、verification-before-completion 技能。按用户要求自行完成两轴审查，无子代理。Standards 无阻止本次 docs-only 合入的问题；Spec 按下列明确边界接收证据，不宣称整票完成。没有生产修复、全应用静态审查或新 WinUIAnalyzer；生产树未变，未重跑 487/build/UI。历史构建测试不能描述为本轮 fresh 结果。

## 来源、注册与哈希

独立 `git diff be7105b 043d1e8a -- src tests global.json externals` 为空；043d1e8a 到候选的 docs 外差异为空。候选仅新增原报告与 full-app-net11 证据目录。读取实际文件重算父冻结 Release 全部 **1887** 文件的双向集合、大小和 SHA256，一致；activation **670** 文件与新 layout 双向集合一致，唯一字节差异为 AppxManifest.xml。解析 XML 后只移除 Identity 的 Name/Publisher/Version，剩余 XML 完全一致，非任意 manifest 变化。

只读 Get-AppxPackage 核实实际注册位置为 `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-4d615e72/artifacts/full-app-net11/layout-v2`，family `Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg`，版本 `2.2610.0.2`，Publisher `CN=BiliCopilotDev`。最终 manifest SHA256 `ead860442444bbe3c1e34897640991cdd466787778eddacadc95741f24ad37ce`。provenance 的 layout/manifestAfter 是第一次复制位置/版本，最终位置以 registration-after 和本次实际注册核验为准。

运行时记录中的包内模块路径与实际文件 SHA256 全部一致：run1 播放阶段163项，run2/3 启动阶段各146项；均含 RC3 coreclr `46a09d97ad8dd30d400a86c90af707bee7593b585d3ccb8586c4fffca404d1e2`，runtime `11.0.0-rc.3.26522.148`。这核验历史加载记录对应的现存文件，不是本轮启动或所有阶段完整模块跟踪。

父 `externals/bili-kernel` HEAD `0ae5e4e000a41a07f6696746706df11c06e8835b`、status clean。`git bundle verify` 核实 dependency-upgrade/kernel.bundle 有对应 integration/dependency-upgrade-d48 ref，前置对象6501985d存在；仍 local-only，不声称远端可达。

## 退出与安全摘要

独立调用既有 exit_gate.verdict，以每组 ready/exit-observation 的 exact PID、路径、creation FILETIME、保留句柄、非强杀 exit0、查询记录及 events-safe 重算，三组均 PASS_BOUNDED。时间字符串小数位格式不同按时间值比较，不按字符串认定身份不一致。

| 组 | PID | 查询次数（全部ok） | collectionEnd距退出秒 | 最后查询距退出秒 | 结论 |
| --- | --- | --- | --- | --- | --- |
| run1 | 32244 | 58 | 30.520623 | 30.008956 | PASS_BOUNDED |
| run2 | 37344 | 57 | 30.015904 | 29.502982 | PASS_BOUNDED |
| run3 | 20236 | 57 | 30.030766 | 29.515067 | PASS_BOUNDED |

查询从启动前 Application bookmark 开始，覆盖运行期间与退出后累计事件；不是仅查询最后30秒。每组安全事件集合为空。**collectionEnd含最后一次查询后的等待，run2/3没有在退出满30秒后再查询；末尾约0.51秒不能称已查询覆盖。** 因此接受“约30秒有限观察”，不接受“严格满30秒完整覆盖”或未来绝不崩溃。未来 exact 事件可撤销结论。

历史 net11-exit-reproduction/evidence.json 安全事件重算仍 BLOCKED（fault6525、WER6526，非零退出及exact AppError），没有读取原始XML日志/dump。另在内存做拒绝语义检查：缺句柄/退出码/查询/观察、非零、强杀、managed unhandled、精确AppError及未解析同身份WER均拒绝；不同creation-time与其关联WER不误归本次PID。这些合成输入仅核验门禁，不充当应用证据。历史 accepted=true 不覆盖红例。

只读正确的三个 log-summary.json：UTC+08窗口逐一对应 startUtc/collectionEndUtc；run1搜索建议取消2，run2搜索建议取消1及 other-exception 68（含TaskCanceledException），run3空组，managedUnhandled均0。invalid-time-window旧JSON未参与计算或结论。**现存汇总没有逐块安全明细，不能独立从汇总反推原始分类计数**；本轮仅复核窗口、汇总一致性及门禁计算，按要求未读原始日志或凭据，未补造缺字段。68块尚未归因，不能宣称都是正常网络取消；零分片错误不证明HTTP/弹幕完整。

## 图像、动作与恢复

13张原PNG逐张独立查看，全部SHA256与screenshots-reviewed一致。首页两图显示已解码封面与可读中文，但内容不同，不证明撤销精确恢复；UIA的6/32/13不是实际Items全序列。P1分集选择、P2普通/全屏/小窗、P8字体面板与 actions 顺序相符；P8图可见00:13/01:45，字体Segoe UI。字体下拉30项来自JSON，不是截图中的展开列表或枚举性能证据。

PGC截图与免费/限免换集动作一致；pgc-first图是00:01/20:03，pgc-second-playing图是00:25/17:55，报告00:03/00:26来自相邻UIA时间JSON，不能当该截图精确读数。tray-menu只显示“显示主窗口/退出应用”。p1-normal未有独立screenshot动作行，已有分集状态/时间邻接支持场景，但不补造缺失捕获动作。其余截图按记录核对，截图本身不证明全程或精确列表。

IME安全记录提交“中”成功，错误shift文本探索不算产品失败。设置原增量/驻留Off，末次记录均Off，字体前后Segoe UI；三次首页有内容且无登录标记，支持会话保留的有限观察，不证明认证存储逐字节不变。未读取登录数据。托盘同HWND恢复但未取得前台，UIA Invoke后续可用不能替代前台通过。

final-state记录owned应用/观察器均0；yield实际 **released:false**，不写为成功释放。本轮没有桌面输入、应用启动、部署、注册修改、强杀或登录数据操作，未操作PID9608/Dev。旧新layout均保留。

## 剩余缺口与最小后续

| 分类 | 缺口 | 最小后续与停止条件 |
| --- | --- | --- |
| 可用受控测试补证 | 空页NOT OBSERVED、失败批次反馈；undo精确恢复未证 | 用固定ID序列的空/失败批次及并发自动加载屏障，记录实际Items顺序与请求次数，断言无自激循环和撤销恢复；公网搜索返回40个UIA项不能制造空页，不无限重试公网。 |
| 可用受控测试补证 | 托盘恢复未前台，原因未定位；严格退出窗口末端缺口 | 固定前台竞争窗口，仅记录目标HWND/foreground和输入可用性做最小复现；观察器末端补一次查询、分别记录lastQuery与collectionEnd。当前不擅自修生产或改原证据。 |
| 需真实业务观测 | 68取消块归因、HTTP取消/弹幕完整、长期稳定及#56预算/收益 | 后续明确授权的短场景，采集脱敏操作关联ID/取消来源/请求终态，先归因再判缺陷；固定工作负载与稳态窗口做对照，不能用UIA数量/固定等待代替内存存活和性能。 |
| 需真实业务观测及工具链前置 | AOT发布运行、硬件/device-loss/DPI、多输入法；历史net9崩溃未归因 | 先解除已知AOT工具链阻塞，再验真实发布产物；net9保留独立诊断，不由本次RC绿例推断已修。 |
| 已证实缺陷 | 历史#49暂停seek/切换首帧FAIL | 维持FAIL与不可发布边界，沿既有架构切片解决后定向重验；本次播放/模式通过不能覆盖。 |

托盘未前台是已观察到的行为失败，但本证据不能确定是产品、Windows前台策略还是工具限制；不擅自把它定为已归因生产缺陷。AOT BLOCKED是工具链限制；历史net9 crash是确定的异常事实但根因未归因。#56预算与任意大重排复杂度限制保留。最小优先顺序：受控空/失败/undo → 托盘前台复现 → 取消归因；不以重复公网空页探索作为完成条件。

原始依据：[完整应用报告](full-app-net11-acceptance.md)、[安全证据](full-app-net11/verification.json)。本轮没有push、PR或issue操作。
