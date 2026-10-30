# #48：离线真实 PlayerViewModel 验收

2026-10-30。**时长边界 red 保留，生产代码未修复。最终干净目录复放：Passed: 8, Failed: 1, Skipped: 0，runner退出1。** [实际结果](evidence-48-player-viewmodel/results.json)、[关键观测](evidence-48-player-viewmodel/observations.json)、[源码/DLL来源](evidence-48-player-viewmodel/provenance.json)。本报告不把离线结果外推为全部 1,953 条真实错误的原因。

## 范围与保真度

- 基线 `c9ac0822defd7804a0ef92e9b4bf316d0d1ddfe0`，先快进合入 `4205f3a71780c8f0d8f5d0f954868a0deeb63377` 及其仅文档祖先；已读 diagnosis-48-live-errors.md 全文、AGENTS/docs/agents 和指定技能。没有子代理、生产/kernel 修改、认证网络、付费服务、工具安装、push、PR/issue 写操作或 AOT 发布。
- [prepare.ps1](harness-48-vm/prepare.ps1) 将完整受版本控制 UI 源码复制到全新诊断目录。仅替换 App 启动入口、在副本 GlobalDependencies 的 Build 前追加离线注册、修改副本包身份/项目路径/JIT 配置；PlayerViewModel 的所有 partial、Video/Pgc connectors、selection commands、DanmakuRenderViewModel、DanmakuLoadSession、PlayerWindow/PlayerOverlay 原样编译。不是只编译 Danmaku partial 的简化 VM，没有反射修改私有字段。
- 真正的 `InitializeCommand.ExecuteAsync(MediaSnapshot)` 创建原生 PlayerWindow、MpvClient、MpvPlayer，运行实际媒体 resolver，再由 `IsPlaybackInitialized` 事件自动触发弹幕初始化。构造障碍已经实际跨越，没有退回 coordinator fixture。
- 真实 DispatcherQueue、真实 WinUI 窗口与公开命令；IPlayerService 注入实际 VideoPlayerView/VideoPart/EpisodeInformation 等模型，本地生成 120 秒静音 WAV 供真实 mpv 初始化。取消 fixture 的元数据为 7,200 秒以生成 20 个计划段，不声称本地 WAV 长达 7,200 秒。
- 所有 Bili 公共服务接口在 Build 前替换为离线代理，settings 为内存字典，合成账户、禁用 SponsorBlock/进度上报。DispatchProxy 只用于公共服务边界。没有解析、读取或复用真实账户身份。封面使用本地 ms-appx URI；产品图像 HTTP loader 不支持该 scheme，出现已记录的无网络封面加载警告，封面显示不在 PASS 范围。
- 唯一新包：`Mang.BiliCopilot.Offline48.C6355575b`。已登录 `Mang.BiliCopilot.PerfAcceptance.Caa6a7be` 的注册、layout、状态完整保留；不启动它，不操作 PID9608 或 Dev/生产包。

## 实际结果

| Requirement | Evidence |
| --- | --- |
| 当前 P 分片上界 | **FAIL** `VideoPartDurationBounds`：稿件7200秒、P120秒；真实服务收到1–20段，严格预期只有1段。成功合成内容20条，CountText“已加载 20 条弹幕”。保留失败断言，runner退出1。 |
| 普通换 P 的在途隔离 | **PASS** `VideoSwitchLateSuccessAndError`：真实 part section 的 SelectPartCommand，旧6请求进入后切P；旧token全部取消，旧队列7–20不启动。当前内容全部为新代次/新cid，20条、CountText匹配、Loading=false；旧成功/异常逆序释放后无污染。取消用例稿件与P均7200秒，不依赖时长缺陷。 |
| PGC 换集及单集时长 | **PASS** `PgcSwitchLateSuccessAndError`：实际 EpisodeItemViewModel.PlayCommand → section → connector NewMediaRequest → PlayerViewModel.Initialize；旧集7200秒进入6请求后换到120秒新集，只请求新集第1段。旧成功/异常不发布、旧token取消、旧排队不启动；新集1条、CountText匹配、Loading=false。 |
| 同源连续重载 | **PASS** `RepeatedReloadLateSuccessAndError`：初始6请求在途，ReloadDanmakuCommand再进入6请求，再次Reload完成20条；前两代token取消，迟到成功/异常隔离，前两代各仅6个请求，新内容全部第三代。**这是公开生成命令级验收，产品没有Reload按钮。** |
| 直接 Dispose | **PASS** `DirectDisposeInFlight`：6请求已进入才DisposeAsync，旧token取消、队列不启动、缓存为空；迟到结果/错误不能改变关闭后的CountText/Loading；再次调用弹幕初始化/重载命令无服务请求。 |
| 真实窗口 Destroy | **PASS** `WindowDestroyInFlight`：6请求已进入才PlayerViewModel.Close → 实际PlayerWindow.Close/AppWindow.Destroy → 实际VM OnWindowDestroying → DisposeAsync；核对与直接Dispose相同的隔离属性。 |
| VM 取消到实际 transport | **PASS** `VmCancellationReachesHttpHandler`：IDanmakuService边界转发到真实DanmakuService → DanmakuClient → BiliHttpClient → 受控HttpMessageHandler。等handler六个SendAsync进入后关闭真实VM；handler观察6次取消，最终尝试数6，整个初始化命令已完成，无重试/后续排队发送、无弹幕错误发布。 |
| 非取消空体重试与错误归属 | **PASS** `EmptyBodyRetriesThreeTimes`：真实VM单段、真实transport，合成HTTP 200/application-grpc/0字节；3次尝试，当前VM恰好发布一次分片错误，最终缓存0/CountText“无弹幕”/Loading=false。这是受控HTTP状态，不是对真实日志状态的推断。 |
| 合法空帧与实际parser | **PASS** `FiveByteEmptyFrameParsesOnce`：相同链路返回5个零字节：未压缩标志0、big-endian消息长度0；BiliHttpClient校验通过，实际DmSegMobileReply.Parser解析空protobuf，1次尝试、0分片错误、0条/“无弹幕”/Loading=false。 |

旧请求使用 TaskCompletionSource 屏障，故意忽略取消。新结果完成后逆序释放旧请求，交替返回成功和抛出 `SYNTHETIC-OLD-SEGMENT-ERROR`；等待旧初始化/重载任务完成并 drain 真正的 Dispatcher 后断言。没有以延时猜测代替“服务已进入”。等待状态的20秒上限仅用于失败退出。

关闭终态的精确限制：这两个 fixture 开始时 CountText=null、Loading=true；关闭后仍为 null/true，IsClosed/IsDisposed=true。PASS 指取消/迟到隔离，**不是关闭后Loading被重置为false**。直接Dispose会先解除VM的Destroying订阅，再Dispose窗口；Close路径则实际经过已接线的Destroying回调。宿主关闭了集成主窗口模式，集成模式下联动关闭主窗口未执行。

## 已确认缺陷与最小建议

`PlayerViewModel.Danmaku.cs` 的普通视频分支使用 `_part.Identifier.Id` 作为cid，却用 `_view.Information.Duration` 计算段数。实际 VideoAdapter 分别保留稿件和page时长，模型并未丢失P时长。7200/120 fixture的真实公开初始化证据将静态候选升级为**确定的客户端规划缺陷**：120秒P按6分钟分片只需1段，实际计划20段，额外19段。

最小建议：普通视频分支的计划时长取当前 `_part.Duration`；对缺失/零时长的策略另外明确，不能默认回退到多P总时长并把其视为当前P上界。受影响入口包括普通视频首次初始化、换P和同源Reload；共享同一分片规划。该取值不是#48新引入，本任务不修生产，也不删除red。

PGC采用 `_episode.Duration`。实际 `Services.Media/Core/Adapters/PgcAdapter.cs` 把 `PgcEpisodeDetail.Duration / 1000` 写入EpisodeInformation，模型文档明确秒；`EpisodeItemViewModel`亦用FromSeconds展示。运行用例从7200秒旧集切到120秒新集，验证规划归属当前集，而非season/旧集。adapter单位核对为源码证据，不冒充这次执行了真实PGC接口的反序列化。

1,953个真实最终失败块仍是已确定的空响应校验失败，不是正常取消；上游为何为空仍未证。离线空体实验说明错误类别/重试行为，不证明真实服务在越界段必然返回空body。没有新增真实网络采样，真实网络全部请求取消、全部弹幕完整性、真实视频视觉质量仍为 **NOT RUN / 未证明**。

## 截图与执行过程

第一轮普通PrintWindow截图遗漏XAML island，只看到白底，逐张审查后拒绝将其作为视觉通过证据。最终用 `winapp ui screenshot -w <owned HWND> --capture-screen`，逐张检查duration、video-switch、pgc-switch、reload-command、before-dispose、before-destroy及宿主results。仅捕获目标窗口矩形；记录PID/HWND和尺寸。最终逐张观察见 `visual-review.json`。

播放器截图展示实际标题、播放器控件、120秒本地音频进度以及部分滚动合成弹幕；白色音频画布和无封面是fixture限制，不是完整视频画面验收。滚动文字在右边缘进入/离开，不用截图计数判断数据完整性；CountText与完整缓存通过真实VM断言验证。results图是宿主截屏交接状态，最终PASS/FAIL以JSON为准，不冒充产品界面有测试按钮。

构建/启动诊断保留在ignored artifacts：首次复制工程缺少LangVersion/ImplicitUsings，后续缺ffmpeg运行依赖；补齐均仅作用于宿主。一次JIT构建未出现窗口，runtimeconfig中DynamicCodeSupported和JsonSerializer反射开关为false；宿主显式PublishAot=false后运行成功。首次没有保存到异常栈，因此“反射开关导致启动失败”是基于开关和修正结果的诊断推断，不计为用例执行。UI脚本首次将窗口数组的空error属性集合误当truthy，修正为逐项检查后在同一个屏障恢复；没有改生产或放宽断言。最终从全新目录重新prepare/build/run，不使用这些失败尝试作为最终成功证据。

## 复放与交付

安装已有.NET/WinApp和本机原生资产即可复放；脚本不下载或安装工具。prepare的NativeAssets源为主checkout的ignored libmpv/ffmpeg，不是已登录应用layout。可按机器调整这两条资产路径。

```powershell
./docs/performance-acceptance/harness-48-vm/launch.ps1 -Destination "$PWD/artifacts/48-replay"
# 取 build-launch.log 最后JSON的ProcessId，且核对EXE路径。
./docs/performance-acceptance/harness-48-vm/run-ui.ps1 -AppPid <owned-pid> `
  -Layout "$PWD/artifacts/48-replay/layout" -Evidence "$PWD/artifacts/48-replay/evidence" `
  *> "$PWD/artifacts/48-replay/ui.log"
# 基线存在red时，预期runner退出1；必须读取九项具名结果，不能将exit1洗成通过。
./docs/performance-acceptance/harness-48-vm/provenance.ps1 -AppPid <owned-pid> `
  -Destination "$PWD/artifacts/48-replay" -Evidence "$PWD/artifacts/48-replay/evidence"
./docs/performance-acceptance/harness-48-vm/cleanup.ps1 `
  -Layout "$PWD/artifacts/48-replay/layout" -Evidence "$PWD/artifacts/48-replay/evidence"
```

本宿主是自带具名断言/JSON结果的WinUI进程，不是VSTest程序集；未以既有15测试或403全套替代新用例。未安装专用WinUIAnalyzer；最终Debug x64 JIT构建退出0，1522个编译器/现有分析器警告、0错误，用时1:15.63，不宣称全应用零警告。839个生产C#/XAML副本逐文件相等；实际build/layout/loaded `BiliCopilot.UI.dll` SHA256均为 `68A4987815CEB8E43E09F7E71AF3048F2367A6476E92F48801AC5E338C9A8638`。宿主源码相等、主仓src和kernel无diff、脚本语法及git diff --check、准确九项结果与截图已核验。

提交前仅移除宿主App/Fixture文件末尾额外空行以通过diff whitespace检查，没有改可执行语句；实际编译输入原始bytes以base64保留在 `compiled-host-sources.json`（避免Git换行转换），编译输入与交付版本各自哈希及TrimEnd后逐字相等校验见 `host-source-finalization.json`。生产839文件的逐字节相等不受此整理影响。

已合并本地最新 `feat/performance-45`（仍为c9ac082，Already up to date）；最终SHA由交付消息给出。最终owned PID14056与精确独立包已清理，已登录完整包仍注册在原layout。[cleanup](evidence-48-player-viewmodel/cleanup.json)与[yield](evidence-48-player-viewmodel/final-yield.json)原样保存：UI批次yield released=true，最终清理后再次yield退出0、released=false（已没有待释放turn），不将false改写为true。桌面独占槽已交回父级。
