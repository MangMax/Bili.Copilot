# 当前 P 时长修复：已登录完整应用复验

2026-10-30（UTC+08）。**本轮规定的普通视频 P1/P2/短 P、关闭重开及保留登录状态通过可见行为验收；真实网络完整性及全应用总验收仍为 PARTIAL。** 完整生产应用 Debug x64/JIT，非离线替身宿主，非 Native AOT。没有生产代码修改。

## 来源、构建和安全更新

- 工作区 `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1630ecee`，分支 `t3/full-app-duration-acceptance`。构建源码 **`c3b32c527cf7f80f2c0aaea81e92a822480efb56`**；kernel **`2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`**。文档提交不改变实际被测源码。
- 先读 AGENTS、docs/agents 和五份交接/诊断报告；使用 winui-packaging、winui-dev-workflow、winui-ui-testing、code-review、verification-before-completion；对资产检查和启动延迟按 systematic-debugging 核对。遵守父级任务不启动子代理的要求，审查由本代理完成。
- 精确核对旧注册包 Identity/Publisher/family/InstallLocation，更新脚本内再次确认 owned EXE 进程为零。没有操作 PID9608、Dev/生产包，没有卸载、清理 app data、读取/复制认证文件或向用户请求登录。
- `winapp run --help` 明确默认重新部署保留 app data，只有 `--clean` 才清理。本轮使用 `--no-launch`，未使用 `--clean`、`--unregister-on-exit`，没有执行卸载命令。工具没有要求先卸载或删除数据。同版本成功更新注册，无需提高版本。
- 全新 ignored `artifacts/full-app-duration-fix/build` 和 `layout`；未覆盖旧 layout。kernel 项目使用本工作区自己的 bin/obj。运行时三项资产已存在于本工作区，逐项与原主 checkout `C:/WorkFile/Project/Bili.Copilot` 哈希相等，无需复制。初次普通 `check-ignore` 因主 checkout 跟踪状态未返回匹配，随后用 `--no-index` 核对忽略规则；该检查失败没有更改资产。详见 [资产来源](evidence-full-app-duration-fix/runtime-assets.json)。
- 临时修改本工作区 `Package.Dev.appxmanifest` 的 Identity Name 和显示名，Publisher/版本不变；`finally` 按原 bytes 恢复。前后 SHA256 均 `5EDADABCC576061C73B8FDDAB3FBDF8CD61C6B8E89253301405AEC5835CBDEA3`，逐字节相等且生产源码 git diff 为空。[恢复记录](evidence-full-app-duration-fix/manifest-restoration.json)。

实际命令（临时清单生效期间）：

```powershell
$root='C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1630ecee'
$art="$root/artifacts/full-app-duration-fix"
winapp run "$root/src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj" `
  -c Debug --arch x64 --no-launch --json `
  --output-appx-directory "$art/layout" `
  -p Platform=x64 -p "SolutionDir=$root/src/" -p "BuildOutDir=$art/build/"
```

完整构建退出 **0**，**1 警告、0 错误，00:02:06.27**；警告为已有 protobuf unused import。原始构建日志保留于 ignored `artifacts/full-app-duration-fix/build-update.log`，哈希见 [provenance](evidence-full-app-duration-fix/provenance.json)。未安装 WinUIAnalyzer/其他工具，未重跑已绿的403测试，未尝试已知缺MSVC的AOT。没有声称该JIT构建验证了AOT。

## 当前注册与实际加载绑定

- Identity：`Mang.BiliCopilot.PerfAcceptance.Caa6a7be`
- Publisher：`CN=BiliCopilotDev`
- family：`Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg`
- 版本：`2.2610.0.1`；full name：`Mang.BiliCopilot.PerfAcceptance.Caa6a7be_2.2610.0.1_x64__y1mf40g4r9srg`
- **新 InstallLocation**：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1630ecee/artifacts/full-app-duration-fix/layout`
- AUMID：`Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg!App`
- 首次 PID59592，主页 HWND1575346，播放器 HWND2031992；重开 PID30484，主页 HWND2492122，播放器 HWND1775562。全部按精确新 EXE 路径发现，再 scoped 枚举窗口；不可复用这些已经退出的句柄。
- 修复版 `BiliCopilot.UI.dll` SHA256 **`BE5A365A577756B29B7E3EDDF3C4EF3A7F1D12350DD92269522C1E19EF265D68`**，build/layout/实际加载相等。两次进程加载的 UI、Models、kernel Core/Media 路径与哈希核对一致，见 [首次模块](evidence-full-app-duration-fix/loaded-dlls.json)、[重开模块](evidence-full-app-duration-fix/reopen-loaded-dlls.json)、[部署六项](evidence-full-app-duration-fix/deployment-hashes.json)。首次早期检查只有 apphost，待初始化完成才采集完整模块，不把早期结果算通过。
- 旧 `t3-caa6a7be/artifacts/full-app-acceptance/layout` 保留原位，其交接六项哈希未变，[旧 layout 核验](evidence-full-app-duration-fix/old-layout-preserved.json)。没有回滚或覆盖旧文件。

两次均通过系统 AUMID 激活：

```powershell
Start-Process explorer.exe -ArgumentList 'shell:AppsFolder\Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg!App'
```

首次初始化约到14:13:43才取得首页树，不能用早期没有窗口判登录丢失。两次均进入有搜索及内容的首页，登录页标记为false，随后真实视频可播放。这里只验证保留登录后的可见入口，不读取账户资料或认证存储。

## 实际播放矩阵

唯一测试内容 **BV1qW4y1a7fU 黑马 Python 多 P 课程**。搜索框使用 SetValue，核对 get-value 精确相等再 Enter；BV直接进入播放器。分集通过 WidgetButton→分集→可见启用条目选择。没有点赞、评论、投币、收藏、关注、发送弹幕、分享、下载、购买或真实AI操作。

| 场景 | 公开分集时长 / 播放器总时长 | 预计分片数 | 稳定计数与时间 |
| --- | --- | --- | --- |
| P1「Python导学视频」 | 06:54（414秒）/ 06:53 | 2 | 14:16:01.943：3,119；14:16:17.859复读仍3,119 |
| P2「第一阶段-第一章-01-初识Python」 | 06:56（416秒）/ 06:55 | 2 | 14:16:22.355选择；14:16:44.363：2,639；14:17:03.211复读仍2,639 |
| P8「第一阶段-第一章-07-第一个Python程序-练习讲解」 | 01:46（106秒）/ 01:45 | 1 | 14:17:07.352选择；14:17:21.860：662；14:17:34.313复读仍662 |
| 同包关闭→AUMID重开→同BV的P1 | 06:54 / 06:53 | 2 | 14:18:35.147：3,119；14:19:00.358复读仍3,119 |

预计段数仅依据公开分集列表和源码 `ceil(currentPart.Duration / 360)`，**不是观测到的HTTP数/成功段数**。分集列表直接绑定 `VideoPart.Duration`；播放器时钟显示分别少1秒，本轮按列表规划值记录，不混用两个来源，也未将显示差异确认为新缺陷。公开列表前8项保存在 [public-parts](evidence-full-app-duration-fix/public-parts.json)。首次 P1 选择状态已从分集面板观察；P2/P8由明确条目选择关联，标题栏/弹幕设置显示的是整稿标题，不声称产品显示了当前P副标题。

P1画面为Python应用场景讲解，P2为Python起源，P8为命令提示符练习；均可见实际弹幕、播放进度及启用的弹幕设置。逐张查看原始PNG，只提交目标播放器矩形的四张必要公开内容图：[P1](evidence-full-app-duration-fix/p1-final.png)、[P2](evidence-full-app-duration-fix/p2-final.png)、[P8](evidence-full-app-duration-fix/p8-final.png)、[重开P1](evidence-full-app-duration-fix/reopen-final.png)。状态JSON紧邻截图采集，计数一致，未跨P复用图；期间视频仍在播放，进度可有1秒差。密集公开弹幕遮盖部分画面是实际截图状态，不声称整个视觉矩阵通过。早期分集探索图因悬停Maximize出现系统布局弹层，仅留ignored，未提交为终态证据。

加载与终态：P8选择后14:17:08.659的过渡采样显示00:00:00/00:01:45及启用按钮，当时没有打开计数面板，**不能声称已经取得最终计数或证明无旧计数残留**。终态依据后续662的两次一致观察、对应画面和进度。本轮未捕获可量化的旧计数残留窗口；历史加载中旧计数观察保持，不用新画面配旧计数签通过。没有绕过禁用控件；一次尝试因面板遮挡使控件不可见，辅助脚本在调用invoke之前拒绝，随后按公开UI点击遮罩关闭面板再操作。

关闭重开是可见生命周期验收，**不是 Reload 命令验收**。产品没有Reload入口，既有真实VM9+3受控结果保持。本轮未追加PGC两集实测；生产修复仅普通视频时长，PGC的既有真实UI有限证据和受控VM证据仍有效，但不写成此次真实PGC回归通过。未证明旧网络仍在途时切换、全部请求取消、长时稳定或 #49 暂停seek问题已消失。

## 本轮白名单日志

仅允许读取该精确包 `LocalState/Logger/log-2026-10-30.txt`，共享只读、整文件读入内存后按时间头分块。开始前记录安全边界；最终窗口 **[14:11:43.752,14:19:02.885)**。对已提交 [analyze-48-live-errors.ps1](analyze-48-live-errors.ps1) 只增加可选StartTime/EndTime参数，默认历史窗口不变，分类白名单不变。

```powershell
./docs/performance-acceptance/analyze-48-live-errors.ps1 `
  -StartTime '14:11:43.752' -EndTime '14:19:02.885'
```

[安全摘要](evidence-full-app-duration-fix/business-error-summary.json)：本轮 **0 个分片失败块、0 个固定 kernel-empty-response 分类块**；2个搜索建议异常块，均为 `TaskCanceledException → TaskCanceledException → IOException → SocketException`，明确数值码995，时间14:13:51.303和14:18:15.607。没有任意异常Message、原始日志、媒体URL、headers、token或账户字段。UI树只在内存白名单过滤，mpv原生标题不输出、不落盘。公开操作时间另见 [actions](evidence-full-app-duration-fix/actions.jsonl)。

历史1,953个空响应失败的归类不变。本轮相同课程未观察到此类日志，与当前P规划修复的预期一致；**不能归因历史全部1,953个错误，也不是严格性能A/B**。播放时段、选P、停留时长和服务状态不同，没有requestID、成功段数或服务返回总数，故零失败日志不证明所有HTTP成功、所有取消完成或弹幕完整。搜索建议取消链不属于分片失败，也不据此宣称新增已确定产品缺陷。

## 收尾与后续绑定

14:17:36首次关闭 PID59592，14:19:02最终关闭 PID30484；均持有精确进程句柄观察退出，退出码 **0**。最终精确路径 owned 进程 **0**，窗口随进程退出；同 workflow `bili-duration-1630ecee` 的 `winapp ui yield --on local --json` 退出0、实际 **released:true**，此后无桌面输入。[最终状态](evidence-full-app-duration-fix/final-state.json)。桌面独占槽已释放。

后续必须保留新旧layout，按上方新InstallLocation验证注册，再AUMID激活，以新目录的 `BiliCopilot.UI.exe` 路径重新发现PID/HWND；不要使用旧layout的EXE或旧PID。登录包/app data保留，未卸载。旧layout可供另行授权的同身份安全回滚，本轮未验证回滚。

审查范围为相对c3b32c5的文档/只读分析参数和部署清单恢复，不声明全应用静态审查通过。生产源码与kernel未改。PowerShell语法、日志重复白名单重算、部署/加载哈希、敏感模式扫描、git diff --check均以 [verification](evidence-full-app-duration-fix/verification.json) 记录。提交前同步本地最新 `feat/performance-45`；最终提交SHA、clean和kernel状态由交付消息报告。无push、发布、PR或issue操作。
