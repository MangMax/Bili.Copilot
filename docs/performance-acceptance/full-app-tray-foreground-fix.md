# 完整应用托盘前台修复：定向回归

2026-10-31（UTC+08）。**真实已登录 Release JIT 的四条实际恢复路径通过；两轮退出 PASS_BOUNDED。完整性能验收仍 HOLD。**
父 tip `1384779e798aba1a368bae2c566b976ff101a935`，生产 `334d47c78e04bebe836f6a8191b16859e6343843`。
本轮不重建、不重跑487、不改生产代码/manifest，不替代 AOT、#49、#56 或历史 net9 结论。

## 来源与部署

先核对旧注册及旧670文件全量SHA256：`t3-4d615e72/artifacts/full-app-net11/layout-v2`、2.2610.0.2、
`Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg`、Publisher `CN=BiliCopilotDev`，无目标进程运行。
父冻结 Release 的1887文件大小/SHA256/双向集合通过，再复制670文件 activation 到本工作树独立 layout-v3。
仅生成 manifest Identity Name/Publisher/Version 改为授权身份及2.2610.0.3，其他payload逐字节相等。
`Add-AppxPackage -Register`一次成功，无卸载、clean、数据迁移、凭据读取/复制或覆盖旧layout。

最终保留注册：

```text
C:\Users\hp\.t3\worktrees\Bili.Copilot\t3-233185ea\artifacts\full-app-tray-foreground-fix\layout-v3
Mang.BiliCopilot.PerfAcceptance.Caa6a7be_2.2610.0.3_x64__y1mf40g4r9srg
shell:AppsFolder\Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg!App
```

[provenance](full-app-tray-foreground-fix/provenance.json)保留新旧全量hash与原路径；
[source-graph-verification](full-app-tray-foreground-fix/source-graph-verification.json)独立复算1275源码输入双向集合、
134包解析图、recipe闭包及无额外DLL。与父源码一致，kernel `0ae5e4e` local-only。
新UI.dll SHA256 `67f0c0d0de1da249964bb11c79a506d50abf9b6120f7108fa9281bdbf5bde341`；
RC3 coreclr `46a09d97ad8dd30d400a86c90af707bee7593b585d3ccb8586c4fffca404d1e2`，
runtime `11.0.0-rc.3.26522.148`。实际进程模块路径及hash复核：首轮播放阶段162个包内模块，重开148个；不称全部播放模块跟踪。

## 已测路径

| Requirement | Evidence |
| --- | --- |
| 同身份更新保留登录 | `logged1/home-safe.json`：6卡片、登录入口0；`login-visible.json`：账户菜单可见。个人资料只在内存检查。重开`logged2/home-safe.json`为30卡片、登录入口0。 |
| 指定公开视频短P真实播放 | 公开搜索进入BV1qW4y1a7fU，真实鼠标选“第一阶段-第一章-07-第一个Python程序-练习讲解”（既有P8），`player-short-p.png`及`player-short-playing.png`显示实际画面推进。未取得本轮UIA时间值，相关inspect尝试记录保留；不编造时长/进度数值。 |
| 历史播放器UIA标题栏关闭→托盘左键恢复 | `history-result.json`与`history-first-actual-restore.json`：同一主HWND14748754可见、非最小化，在输入工具前已为前台，真实`trayhistory`进入TextBox并清空。准备阶段曾中断，详见下文；不是原历史全过程无间断逐事件重播。 |
| 主窗UIA标题栏关闭→真实托盘左键 | `case-Mouse.json` PASS：hidden+竞争前台→同主窗前台→真实文本与清空。 |
| 主窗UIA标题栏关闭→真实右键菜单“显示主窗口” | `case-Menu.json` PASS，同样三个独立判据。 |
| 普通鼠标标题栏关闭→真实托盘左键 | `case-Physical.json` PASS，同样三个判据。 |
| 显式托盘退出、重开再关闭 | 首轮真实右键菜单“退出应用”；重开后公开设置恢复驻留Off，再主窗UIA标题栏关闭。两轮保留句柄exit0及有界事件门禁。 |

每条实际恢复触发仅一次，通过即停。竞争窗固定为本任务创建的PID27828/HWND790566。
恢复前`IsWindowVisible(main)=false`且`GetForegroundWindow=competitor`；恢复后独立API采样在键盘工具前。
前三条主窗案例在临近键盘前再采样。失败时脚本不允许调用键盘工具制造前台绿例。
真实键盘读取实际TextBox文本，测试词清空且未提交。公开视频BV导航是明确授权的正常播放入口。
没有AttachThreadInput、前台锁调整、置顶或强抢。任务栏面板使用正常公开click/Invoke，不将其当目标应用恢复成功。

## 所有失败及边界

[failures.json](full-app-tray-foreground-fix/failures.json)、case失败文件、actions/states/timeline原记录保留。
首次主窗发现早于启动完成；播放器初次筛选误用了主窗class；竞争窗首次启动路径错误；PIL不可用后改用System.Drawing裁剪。
这些均是工具准备问题，未改生产应用。

历史组合的播放器关闭成功后，第一次竞争窗点击被遮挡守卫拒绝。正常focus自建竞争窗后，
其前台/主窗hidden已成立，但旧OpenTray helper把仍可枚举的溢出HWND/几何误当可点击面板，
几次`WindowFromPoint`返回329928而拒绝发送；其间没有目标托盘点击，也没有键盘激活主窗。
`case-History*.json`全部失败仍在，不能被最终绿记录覆盖。
修正测试准备为公开任务栏按钮打开实际面板后，第一次真实托盘左键才执行并通过。
因此本轮证明“播放器关闭后仍隐藏的主窗可从竞争前台恢复”，不证明零准备间断的历史时序。
后续Mouse/Menu/Physical三个完整case均一次PASS，无产品修补。

退出准备另有`foreground_not_target`拒绝和面板toggle后图标缺失，保留；最终重新发现精确图标后真实右键退出。
日志摘要helper初次给不存在的`managedLogScan`属性赋值失败，verifier拒绝；改为Add-Member，
原观察器输出另存`exit-observation-original.json`，未修改exitcode/事件事实。

一次探索性播放器树输出误打印了mpv原生标题中的媒体查询数据。未保存到交付树/日志、未读取认证存储；
之后只输出白名单字段，所有应用inspect的actions输出均为withheld。此操作失误明确保留，不宣称全过程没有媒体地址输出。

8张PNG逐张打开审查，见[screenshots-reviewed](full-app-tray-foreground-fix/screenshots-reviewed.json)。
主窗/重开图裁去顶部个人区域，原临时图已删除；其余是公开课程/弹幕或精确托盘菜单。
播放器自动隐藏覆盖层导致初次UIA只见原生外壳；悬停标题栏后取得WidgetButton/分集。
截图可确认画面、中文和恢复内容，不替代前台API；首页部分空白、卡片截断与密集弹幕不在本次视觉全面认证范围。

## 退出及日志

| Run | exact PID | retained code | last query after exit | verdict |
| --- | --- | --- | --- | --- |
| logged1 | 24552 | 0 | 30.066646s | PASS_BOUNDED |
| logged2 | 16612 | 0 | 30.067722s | PASS_BOUNDED |

[verification](full-app-tray-foreground-fix/verification.json)复核PID/EXE/creation FILETIME、保留句柄、
Application1000与linked/unresolved WER、末次查询开始也满30秒；两轮安全事件集合空，查询完整。
未来精确错误可撤销有界结论，无“进程消失即exit0”推断。
只读精确family当日Logger、限本轮本地时间窗，输出固定类别/类型/计数，无原Message、URL或auth数据写入证据。
首轮4条search-suggestion（白名单TaskCanceledException/IOException/SocketException）；重开类别空；managedUnhandled均0。
不把它们解释成所有网络成功或弹幕完整。

## 交付状态

[final-state](full-app-tray-foreground-fix/final-state.json)：owned应用0、观察器/竞争窗0；竞争窗保留句柄exit0。
最终`winapp ui yield --on local --json` exit0、**released:true**，此后没有桌面输入。
驻留Off恢复原值，搜索空；保留新旧layout、2.2610.0.3注册及登录。无社交/付费/下载/真实AI、无PID9608/Dev操作。

读取AGENTS/docs及winui-ui-testing/packaging/code-review/verification技能，遵从显式禁止子代理。
未新增全应用静态分析或单测通过宣称。kernel初始仅.git且旧HEAD/index删除已存ignored；
从父本机kernel取固定对象展开当前工作树gitlink，clean；无远端可达宣称。
父tip交付前仍1384779e，当前分支已包含；无父工作树改动、push、PR、issue。

**#49 pause FAIL、AOT BLOCKED、历史net9 crash未归因、#56预算/任意大重排限制、总体perf HOLD全部保留。**
本轮仅解除上述已测托盘前台路径的未验状态，不扩大为全部完整应用或性能通过。
