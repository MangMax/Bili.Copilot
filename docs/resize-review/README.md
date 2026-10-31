# 固定提交独立审查

裁决：**可合入 `2eee5b8e8453d142438cbecdd095549d65d21bbc`**。Standards 无阻断项；Spec 无阻断项。审查基础为父 `3f607587cdda285f0331068427ec8ad433f9b0e3`；本区快进集成候选，仅增加审查脚本和证据，不修改生产实现。父 `feat/performance-45` 复查仍为该父提交。

本次按 code-review、winui-code-review、winui-ui-testing、verification-before-completion、implement-spec 执行；遵照用户要求不再派子代理，不创建 PR/issue，不发布或安装用户试用版。补测按 code-testing 的 focused 路径执行，使用实际 WinUI/WIC/Win2D 控件，未以 timer adapter 替代生产代码。未运行 WinUIAnalyzer。

## Standards

没有新增依赖、XAML 或同步 UI 等待。两槽在 `WaitAsync(token)` 成功后才进入 `try/finally`；等待取消不误释放，取得后的取消、WIC 异常、surface 创建异常均经过释放。预算覆盖到 UI surface 提交，并非仅覆盖 decoder 创建。

两次原生 WIC await 都保持无 token；`stream/random` 的 using 生命周期跨过原生完成，取消只拒绝结果，不提前关闭原生仍在使用的流。请求 identity、token 和 `IsLoaded` 阻挡旧结果。此结构保留了父版本的 AV 修复。

timer 首次布局有效时创建，非重复；相同物理尺寸不重启正在运行的 timer，回到已请求尺寸时停止。卸载停止 timer 并清空请求，重挂立即 Reload。源变化先停止 timer 再读取当前 Source/尺寸；Tick 本身也读当前状态，没有捕获旧 source。XamlRoot 通知只调度尺寸，按当前 RasterizationScale 计算。device recovery 原有节流与订阅路径保持，恢复不在持有槽位期间等待另一请求。

## Spec 和证据口径

`audit.py` 验证归档 161 个文件的 SHA256/字节数，重新从全部 heartbeat/resize 数组计算 max 和 p95；三档 120 步、条目数不变、最终轨迹列表更新为零均核对通过。重新执行原 verifier：red-final exit 1，green-final/hot-final exit 0。green1 的失败保留，不归入成功样本。复算输出见证据 `audit.json`。

同源 instrument.py 对本候选重新生成的封面文件与 green-final 原件字节哈希相同。Decoders 在每次真实 CreateAsync 前递增；Native 在同一 UI 上下文进入原生流水线时加一，外层 finally 减一，覆盖提交和取消结束。该峰值是流水线在途峰值，不等于 CPU 并行线程数。此单 UI 宿主计数可靠；不能据此推断多个 UI 线程的性能。

SetWindowPos 来自后台 Task.Run；每次调用返回后才延迟 16 ms，红绿墙钟时长不同，是固定步长而非固定墙钟吞吐比较。进程采样来自外部 PowerShell，UI 只做 dispatcher 心跳和计数。性能改善证据支持受控轨迹内的严重阻塞缓解，不支持 60fps、所有机器或 AOT 发布性能承诺。

`VerticalCacheLength=20` 与 realized=76 解释较多离屏封面参与；200→500 条仍为 76，不支持静态认定整个列表虚拟化失效。推荐模板使用 RecommendationCoverImage；热门、搜索、分区仍有 BasicCoverImage 路径。热门的原始通过记录只代表对照测试，不能给所有类似页签标记“已修复”。

原始 targeted/full TRX 的 testName 集合逐项比较：98 是 572 的子集，两者均零失败；属于历史验证，不相加，也不冒充此次新运行。Release x64 JIT 全量 Rebuild 日志末尾 exit=0、1 个既有 protobuf 警告、0 错误、143.74 秒；前次资产门失败日志仍保留。本次不重跑完整应用或 native 性能矩阵。

## 新增原生缺口验证

命令：`python docs/resize-review/prepare.py`，`dotnet build artifacts/resize-review/native/Harness.csproj -c Release -p:Platform=x64 -p:NuGetAudit=false`，`./docs/resize-review/run.ps1 -Stage run-verified`。准备脚本要求先解压候选 evidence.zip 到 `artifacts/resize-review/original`。宿主直接链接本区真实生产文件；仅日志服务与网络为离线适配，未读写 kernel。

六项 PASS：

- `InitialLoadSameSizeNotificationsAndFinalPixels`：实际 timer 运行期间反复调用生产尺寸调度路径，不延期至通知结束；检查真实 CanvasImageSource 物理尺寸。
- `SourceReplacementStopsTimerAndRemountUsesCurrentSource`：有效新 source 立即停 timer，旧 timer 不覆盖新 surface；卸载清空、重挂加载。
- `QueuedUnloadDoesNotLeakOrOverReleaseSlots`：测试持有两个真实 semaphore 槽，已加载控件排队后卸载，未取得的槽不被误释放。
- `InvalidWicImageReleasesSlotAndNewSourceRecovers`：真实无效图片使 WIC 失败，槽位归还，随后有效 source 恢复。
- `TwoWindowsDrainSharedBudgetAndClosedWindowReleases`：同一 UI 线程两个真实窗口、24 个控件共享预算，全部完成；尺寸变化中关闭第二窗后排空。
- `RapidNativeUnloadAndSurfaceRecovery`：40 次快速加载/卸载没有重现 AV；真实 surface-loss 回调重建 surface，最终槽位归还。

限制：上述不是任意长时间公平性证明，不覆盖多个独立 dispatcher 停摆或强制 GPU device removal。持有原生操作的请求必须等原生完成，不能承诺取消立即归还槽位；同 UI 线程失活窗口本身不停止 dispatcher。硬件 device-loss 和跨显示器 DPI/AOT 发布未新增实测。已有错误恢复路径的代码审查未发现由本改动引入的死锁。

原始像素截图人工查看有非空封面；此次最终截图查看保留的真实封面。新宿主最初构建沿用归档 nullable=disable，因此有 CS8632 测试项目警告，未抑制；不是生产 Release 构建警告。

## 失败保留与安全

首轮五项 PASS。加第六项的 run-final 出现按钮动作重入，记录中有重复测试名、互相占槽和额外窗口，退出观察为 absent=false；明确判失败。只修复测试入口一次性保护后重跑 run-verified，未修改生产代码。并发旧轮清理尚未结束时的新启动被 identity guard 拒绝，也保留日志。不能用失败轮冒充成功或归咎生产缺陷。

最终独立身份 `BiliCopilot.ResizeReviewB5d1.Owned`，精确 PID、exe path、关闭后 >=31 秒观察及 Application 1000/1001 路径 WER 查询见 `run-verified/exit.json` 与 `wer.json`。runner finally 注销自己的布局并 yield；所有动作只针对 owned PID。

kernel 初始 f02c33f6 和 474 项异常状态先保存；结束时 HEAD、porcelain 状态、完整 binary diff 哈希逐项一致，未 restore/reset/clean。父、来源、AotDev/正式/Caa6a7be/Dev、凭据与其 layout 未写入；未使用 dump、外部内存或逆向。开发试用版更新留给父任务，本审查不部署。
