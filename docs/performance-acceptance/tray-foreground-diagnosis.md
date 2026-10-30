# 托盘恢复前台：定向诊断与有界修复

2026-10-31，父 `cfc02534a3b71b9bec12ef307eca542a4c17b89c`。父生产源码与 `be7105b` 相同。没有子代理、push、PR、issue 或父分支修改。

**确认并修复了 UIA 标题栏关闭所触发的恢复激活缺口；不能扩大为普通鼠标关闭路径普遍失败。** 未修改版本的真实鼠标关闭→真实托盘左键也通过。生产变化仅 `App.RestoreMainWindow()` 在既有 Show/Restore/Activate 后，使用已引用的 WinUIEx 请求一次 `SetForegroundWindow`。没有 AttachThreadInput、前台锁修改、全局设置、重试或置顶。

已登录完整应用原版本三种触发复现了“可见但未前台”。修复的 red/green 在独立 identity、全 UI 源码离线宿主完成；**没有更新或替换已登录包，不能声称修复后完整应用已重验**。总体性能仍 HOLD，#49 FAIL、AOT BLOCKED、历史 net9 crash 和 #56 限制独立保留。

## 判据与最小循环

固定自建原生竞争窗口，目标进程由精确 EXE/PID/creation FILETIME 校验。每次先断言主 HWND 隐藏、竞争 HWND 为实际前台。随后分别检查：

1. `IsWindowVisible(main)` 为真，`IsIconic(main)` 为假，同一主 HWND；
2. 在三秒上限内 `GetForegroundWindow() == main`，此判据在任何后续键盘工具激活之前取值；
3. `send-input` 真实键盘输入进入指定 TextBox，读取实际文本。离线另记录 KeyDown/TextChanged，不用 UIA Invoke 成功或发送数量替代收到输入。

命令：`pwsh -NoProfile -File docs/performance-acceptance/tray-foreground/case.ps1 -Run <fresh-run> -Case Mouse -NativeClose`。它已对未改生产的 run2 返回 exit1（visible=true/foreground=false/keyboard=true），对 green1 返回 exit0。`-CaptionMouse` 将关闭动作从 UIA Invoke 改为真实鼠标；不带 `-NativeClose` 则走测试按钮直接调用 MainWindow.Close，不能覆盖标题栏状态。所有案例拒绝覆盖已有结果。

`states.jsonl` 保留判据采样；独立竞争窗以约20ms定时器记录前台/可见/最小化变化；`actions.jsonl` 记录工具调用起止、前后 HWND、退出码。此采样不是每条 Win32 消息或每个 API 返回值的 trace。交互动画的短等待不作为性能/稳态证据。

真实鼠标通过最新 UIA 几何定位、`WindowFromPoint` 校验精确根 HWND 后使用 SendInput；检查在调用线程临时采用物理像素 DPI 上下文并立即恢复，不改变机器或应用 DPI 设置。只定位任务自建窗、目标应用和唯一同名托盘图标，不保存整桌面树。WinApp 对竞争窗也曾返回 `foreground_not_target`；改用这种可核验的真实鼠标操作，不用强制激活来制造通过。

## 结果与归因

| 产物/关闭入口 | 恢复触发 | 可见且未最小化 | 输入工具之前为前台 | 键盘证据 |
| --- | --- | --- | --- | --- |
| run1 未改生产 / 直接 Close | 左键、Invoke、AUMID、右键菜单 | 是 | 是 | 实际文本；该轮退出证据不完整 |
| logged1 完整原包 / UIA 标题栏关闭 | 真实托盘左键 | 是 | **否** | 首次未指定控件，工具随后激活窗口但空文本，不能判为产品键盘损坏 |
| logged1 完整原包 / UIA 标题栏关闭 | 托盘 Invoke、AUMID | 是 | **否** | 工具随后激活并定向输入成功；不覆盖之前的前台 FAIL |
| run2 未改生产 / UIA 标题栏关闭 | 真实托盘左键 | 是 | **否** | 工具随后激活并输入成功 |
| run2 未改生产 / UIA 标题栏关闭 | 真实右键菜单→显示主窗口 | 是 | 是 | 成功 |
| green1 修复 / UIA 标题栏关闭 | 左键、Invoke、AUMID、右键菜单 | 是 | 是 | 四项均成功 |
| physical-red 未改生产 / 真实鼠标标题栏关闭 | 真实托盘左键 | 是 | 是 | 成功，收窄缺陷范围 |
| physical-green 修复 / 真实鼠标标题栏关闭 | 真实托盘左键 | 是 | 是 | 成功，保留普通鼠标路径 |

最小化过程没有改生产 window/tray 行为：最初离线直接 Close 全绿；改成 UIA 调用实际标题栏 Close 即复现完整应用红例；仅加一次普通前台请求后同一回归转绿。右键菜单在原版就绿，支持“恢复入口缺少显式前台请求”的解释，不能解释成机器全局禁止应用获得前台。真实鼠标关闭对照说明 UIA 引入的激活状态是重要条件，**没有把测试工具失败直接定为普遍生产 bug**。

具体时间：logged1 左键恢复后，主 HWND2165358 从19:32:14.712Z起已可见，foreground 仍为 Explorer 的1969332；至19:32:17.488Z最后验收采样仍不等。19:32:17.712Z才由后续输入工具激活主窗。AUMID 同样保持自建竞争 HWND9374044，之后工具才取得前台。run2 标题栏关闭时出现 HWND3150484；完整应用相同阶段出现 HWND6230686，后者实查类名为 `ReunionWindowingCaptionControls`。没有凭此断言所有隐藏窗口均为此类或已证明其内部线程实现。

历史 full-app 的18:34:31动作为播放器标题栏 UIA Invoke，18:35:22真实点击托盘，之后 click 被拒。与本次 UIA 标题栏状态相关，但本轮未重播那一段播放器完整生命周期，历史证据不改写，不能声称已逐事件证明同一根因。

## 实现与版本证据

实际 H.NotifyIcon/WinUI 2.4.1、WinUIEx 2.9.3、App SDK 2.6.4、RC3 coreclr 均按加载路径和 SHA256冻结。run2/green1六个关键模块一致；901文件payload差异仅 UI.dll及其PDB。两个宿主源码输入差异只有 App.xaml.cs，134个包的解析闭包一致。源码输入清单中 freeze 工具自包含的两个生成 manifest 单独排除于源码差异比较，原始清单保留。

- H.NotifyIcon nuspec 指向 [bcaaabec 的鼠标处理](https://github.com/HavenDV/H.NotifyIcon/blob/bcaaabecf16566dd910a3798fe874014e251b28d/src/libs/H.NotifyIcon.Shared/TaskbarIcon.MouseEvents.cs)：即使 NoLeftClickDelay=true，左键仍经 Timer/dispatcher 执行命令。其 [PopupMenu 路径](https://github.com/HavenDV/H.NotifyIcon/blob/bcaaabecf16566dd910a3798fe874014e251b28d/src/libs/H.NotifyIcon.Shared/TaskbarIcon.ContextMenu.WinRT.PopupMenu.cs)先对托盘消息窗口调用 SetForegroundWindow，再显示原生菜单、执行选中项。
- 加载的 Microsoft.UI.Xaml.dll 为 `3.2.0.2511`，文件版本指向 [693f472a 的 DesktopWindowImpl](https://github.com/microsoft/microsoft-ui-xaml/blob/693f472aeaf89c6dff66256ee6fcd453e5d553b8/dxaml/xcp/dxaml/lib/DesktopWindowImpl.cpp)：ActivateImpl 是 ShowWindow、UpdateWindow、SetActiveWindow。没有把网上 main 分支充当安装二进制版本。
- [WinUIEx 2.9.3 对应源码](https://github.com/dotMorten/WinUIEx/blob/72f2975d2a237c0d7ad1113fe617d5894e66feb6/src/WinUIEx/WindowExtensions.cs)确认所用扩展是普通 Win32 请求。微软明确区分 [SetActiveWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setactivewindow) 的线程活动窗口与 [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow) 的前台请求；后者依然可能被 Windows 策略拒绝。本修复不保证任意后台调用强制抢前台。

这属于已存在“显示主窗口”意图的激活健壮性修复，不增添置顶或强制输入策略。WinUI自查：保持 UI线程、原关闭/退出 guards、最小化恢复、资源生命周期和命令绑定；无新P/Invoke或依赖。未安装 WinUIAnalyzer，不能声称全应用分析器审查零问题。

## fresh 构建、测试与退出

固定 SDK `11.0.100-rc.3.26522.148`，只设置进程内 DOTNET_ROOT。独立生产源码树与当前 tracked src/tests/global.json 的1113文件一致：Release x64 JIT build exit0，1条既有 protobuf unused-import warning，0 errors。离线 red/green build 分别1570/1569 warnings、0 errors；不隐藏既有夹具警告，不与生产构建混称。

按 run-tests 的 VSTest/xUnit v2 入口新构建并运行 TrayLifecycleTests：**Passed25 / Failed0 / Skipped0**；同一新测试程序集 `--no-build` 全套 **Passed487 / Failed0 / Skipped0**。没有添加镜像式单测：App.Activate 不在该 headless seam，修复断言来自真实 native case。

| Run | retained PID | exitcode | lastQuery 距退出 | collectionEnd 距退出 |
| --- | --- | --- | --- | --- |
| run2 | 38392 | 0 | 30.050865s | 30.055156s |
| logged1 | 23988 | 0 | 30.079264s | 30.080665s |
| green1 | 39832 | 0 | 30.250949s | 30.253937s |
| physical-red | 28264 | 0 | 30.101042s | 30.104001s |
| physical-green | 10552 | 0 | 30.135611s | 30.138446s |

以上 exact PID/path/creation-time 与 Application1000、linked/unresolved WER 门禁 PASS_BOUNDED，安全事件空，最后一次查询启动也在退出满30秒之后。logged1仅内存解析精确包当日Logger运行时间窗，输出白名单分类/计数，managed unhandled0；不保存原Message、URL或凭据。未来精确事件仍可撤销有界结论。

run1 初次观察器在最终复制探针前被提前卸包，导致报告缺失，明确 **BLOCKED/unknown persisted exit**；未根据进程消失补造exit0。修正观察器先立即保存句柄退出码及探针，再查询满30秒。run2起保留其结果；原失败仍在证据目录。竞争窗第一次 WinForms 受隐藏启动影响没有绘制，黑图保留为夹具失败；最终采用原生自建窗，正常退出清理。

physical-green 首次准备时目标被其他窗口遮挡，WindowFromPoint守卫拒绝在错误HWND329928上点击；尚未执行关闭/恢复。原结果保留为 `case-Mouse-precondition.json`，公开 focus 完成准备后才运行一次有效对照。首次验收脚本误把freeze生成的两份manifest当源码差异，断言失败；修正比较口径后actual payload和源码分别校验通过，不改变任何应用结果。

## 重放与交付边界

脚本和压缩安全证据在 [tray-foreground](tray-foreground/)。`verify.py`只读复算结果；`--live`另外重哈希现存901文件布局、加载模块、生产源码/构建输出和已登录670文件布局。新建目录的prepare/freeze/observe与case调用见同目录 README。所有5张截图逐张查看，均为离线目标或自建竞争窗，没有完整应用账号区域/整桌面截图。

已登录原驻留Off，经公开设置临时On后恢复Off，测试搜索文字清空且未提交；无登录数据读取/复制、安装或注册更新。Caa6a7be仍为2.2610.0.2和既有layout-v2，670文件及manifest哈希与冻结版本一致。旧、新布局均保留。Dev和PID9608未操作。

新工作树kernel起初只有.git、旧HEAD f02c33f且index全删除，无实际文件；初始index/status保留ignored。仅向本工作树独立gitdir取本机已有0ae5e4e对象并展开正确gitlink，clean，仍local-only，不声称远端可达。父tip仍cfc02534，已包含，未修改父工作树。

本次不关闭完整性能验收，不覆盖历史red，不宣称修改后的已登录生产包已经部署。后续若要验证原播放器关闭组合，应在明确部署候选后做一次同身份有界回归，而不是重复当前未改包。

最终 [状态](tray-foreground/evidence/final-state-after-physical.json.gz)：owned应用/竞争窗/观察器0、临时注册0，旧新layout保留。最终yield exit0、**released:false**，没有伪称释放了不存在的租约；其后无桌面输入。[只读复算结果](tray-foreground/verification.json)和压缩TRX/原始日志随提交交付。
