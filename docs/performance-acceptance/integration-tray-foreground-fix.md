# 托盘前台请求修复：独立本地集成审查

2026-10-31。固定候选 `78cbbcd5304fba7a286654e2afe82e583c19cf16` 独立审查无阻断，
以 `--no-ff` 合入 `feat/performance-45`，merge **`334d47c78e04bebe836f6a8191b16859e6343843`**。
第一父 `cfc02534a3b71b9bec12ef307eca542a4c17b89c`，第二父为固定候选；初始工作树 clean，合并无冲突。
正式源码/config/tests 与候选一致。读取 AGENTS、docs/agents 和指定四项技能；未再委派。

结论：**独立宿主受控修复通过，修复后完整应用尚未验**。已登录 Caa6a7be 的
`layout-v2` 仍为旧 binary；没有部署本次修复。完整应用 PARTIAL，#49 pause FAIL、
AOT BLOCKED、历史 net9 crash 未归因、#56 预算及任意大重排限制、总体性能 HOLD 均保留。

## Standards / Spec / WinUI

Standards：无本次范围内阻断。生产唯一变化是在 `App.RestoreMainWindow` 的 Activate 后
调用一次既有 WinUIEx `SetForegroundWindow`，其余三行为注释。没有新依赖、P/Invoke、置顶、
输入注入或重试循环。Spec：与既有恢复意图一致，无扩大修复。

检查全部恢复调用：托盘左键和菜单的 XamlUICommand、第二实例经 `_dispatcherQueue.TryEnqueue`、
关闭时托盘失败、禁用驻留及托盘健康恢复；均沿原有 UI 线程协调。`TrayLifecycle.Restore` 的
_disposed/IsExiting guards、主窗存在及 IsClosed guard 保留；Show → minimized presenter.Restore →
Activate → 一次请求的顺序正确。没有改变关闭、退出、播放器生命周期或命令绑定。

核对实际 production 构建 `project.assets.json`：WinUIEx 2.9.3，NuGet XML 中存在
`WindowExtensions.SetForegroundWindow(Microsoft.UI.Xaml.Window)`，返回 bool。
固定版本 [WinUIEx 源码](https://github.com/dotMorten/WinUIEx/blob/72f2975d2a237c0d7ad1113fe617d5894e66feb6/src/WinUIEx/WindowExtensions.cs)
与 [微软策略说明](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)
确认这是可被 Windows 拒绝的普通前台请求。忽略 false 保留窗口显示，不承诺强抢前台。
未运行独立 WinUIAnalyzer；不能把编译检查称为全应用分析器审查。

## 原证据独立复核

先检查 verifier 无写证据操作，再在原 `t3-ee493f5e` cwd 以 `python -B -X utf8 .../verify.py --live`
运行并保存 stdout；原工作树和证据保持不变。实际901文件布局、源码/生产输出、134包闭包、
六个关键运行模块及原已登录670文件布局哈希核验通过。原25项及487项TRX逐项计数通过。
见 [复算输出](integration-tray-foreground-fix/review-evidence/candidate-verify.json.gz)。

red/green 宿主源码唯一差异是 App 中上述四行；payload 唯一差异为 UI.dll/PDB。
宿主 App 与生产 App 仍有既有的隔离实例键及 Offline48 启动替换；不是完整生产启动链。
独立比较确认除此无额外 App 差异。首次直接要求两者 raw bytes 相等过严，失败及更正记录在
[review-correction](integration-tray-foreground-fix/review-evidence/review-correction.txt)，未改原始证据。

逐项核查 case/ui 脚本及原始状态：恢复前主窗隐藏，竞争 HWND 确为前台；恢复后
GetForegroundWindow 的主 HWND 相等判据在键盘工具前采集。UIA 标题栏关闭后原版左键、Invoke、
AUMID 红，修复宿主对应入口绿，原版菜单绿。后续 send-keys 工具自行激活并收到文本不能反证
先前 foreground 红。真实鼠标标题栏关闭的原版和修复版均 PASS，未把 UIA 条件泛化为普通鼠标路径。
历史完整播放器关闭组合仍未逐事件重播，不把相关性称为完整根因证明。

五组 run2/logged1/green1/physical-red/physical-green 的 retained handle exitcode 均0，
exact PID/path/creation-time 与 ready 相符，Application1000及 linked/unresolved WER 门禁
PASS_BOUNDED。末次查询启动及 lastQuery 均满30秒，lastQuery 分别为
30.050865 / 30.079264 / 30.250949 / 30.101042 / 30.135611秒。
每组58次查询。logged1未单独归档 exit-initial，最终 retained exit-observation 存在；不补造缺失文件。
首次 run1 缺少 persisted exit 仍 **BLOCKED**，不从进程消失推断退出码。
未来精确错误证据仍可撤销有界通过。

仅审阅 logged1 安全日志摘要及脚本：限精确包、当日运行窗口，输出白名单类别/计数及类型，
managedUnhandled=0；未读取原日志或认证内容。逐张查看5张已有PNG：三张离线宿主、原失败黑图、
自建原生竞争窗；哈希一致，无账号区域截图。黑图、physical-green 准备守卫拒绝、首次 manifest
比较错误以及所有历史红例继续保留；截图不代替前台API判据。

## 合并后验证与交付

**Fresh Passed: 487, Failed: 0, Skipped: 0**；DebugClean / Release JIT 均 exit0，
各1条既有 protobuf unused-import warning、0 errors；ValidateRuntimeDependencies exit0。
冻结 Tests347、DebugClean1890、Release1887、RuntimeGate26文件；activation分别671/670文件。
全量哈希/双向集合、recipe/DLL闭包、RC3 coreclr固定哈希、候选包图均PASS，122份小证据归档。

门禁结果及可复算闭包见 [summary](integration-tray-foreground-fix/summary.json)。
执行器 [validate.py](integration-tray-foreground-fix/validate.py) 使用既有固定
`11.0.100-rc.3.26522.148` SDK、NuGet/native资源，只设置进程局部环境。
Tests → DebugClean → Release JIT → RuntimeGate 串行；每步完成立即复制、哈希冻结。
两个 app Rebuild 使用不同的新 BuildOutDir；Release 显式 PublishAot=false。
原 output recipe 保留，独立 activation 按 recipe 补齐缺失内容，仅移除副本中的 recipe，
不替换身份、不注册、不启动，可供后续另行授权同身份认证回归准备。
冻结 manifest 保持项目默认值：Debug 为 Mang.BiliCopilot.Dev 2.2610.0.1，Release 为
Mang.BiliCopilot 2.2610.0.0；尚未制备/部署 Caa6a7be 同身份包，不能直接把原始产物称为已登录候选。
Tests 冻结包含既有其他 TFM 子目录，只有 net11.0 程序集/runtimeconfig 与本轮 TRX 为 fresh 证据。

冻结根：`artifacts/integration-tray-foreground-fix/334d47c78e04bebe836f6a8191b16859e6343843/`。
小证据按原字节 gzip；大文件 ignored 保留。runtime gate 只是媒体依赖文件存在检查，
不是媒体运行验收；测试集是 headless，不替代本次 native 受控证据。

Kernel `0ae5e4e000a41a07f6696746706df11c06e8835b` clean；已有 kernel.bundle verify 成功，
保留其 prerequisite `6501985d`，local-only，不宣称远端可达。
本轮无 push/PR/issue、部署/注册变更、桌面/UI复跑、认证读取或 PID9608/Dev 操作。
