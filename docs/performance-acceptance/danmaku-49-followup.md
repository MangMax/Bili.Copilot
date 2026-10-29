# #49 暂停阻断：danmaku-kernel 最小后续范围

2026-10-30，只读调查；未修改引擎仓库、发布 NuGet 或改包引用。此前 bili-kernel 的发布授权不覆盖本仓库。下述为具体实现建议，尚未实现或验证。

## 已核实契约

实际 Core/Legacy/Share 为 `0.0.1-preview6`。nuspec 指向 `Richasy/danmaku-kernel` 提交 `4d0f8691f08f1e143294402477e72306a1b38b6f`；本轮从该提交读取源码，部署 DLL 哈希另存。不是按当前 main 推断。

- [DanmakuRenderer](https://github.com/Richasy/danmaku-kernel/blob/4d0f8691f08f1e143294402477e72306a1b38b6f/src/Danmaku.Core/DanmakuRenderer.cs) 的公开 `UpdateTime(uint)`、`Seek(uint)` 返回 void，无公开状态/完成事件。
- [DanmakuScheduler](https://github.com/Richasy/danmaku-kernel/blob/4d0f8691f08f1e143294402477e72306a1b38b6f/src/Danmaku.Core/DanmakuScheduler.cs) 的 UpdateTime 仅入队并 Set event；worker 在后退或跨度 >5000ms 时 Seek。Seek 内部 Stop、重设游标、Resume。Pause 不保存用户播放意图，只发 PauseRequested。
- [DanmakuFrostMaster](https://github.com/Richasy/danmaku-kernel/blob/4d0f8691f08f1e143294402477e72306a1b38b6f/src/Danmaku.Legacy/DanmakuFrostMaster.cs) 具备同类队列和隐式 Seek → Resume 行为，也无完成接口。Close 置 closing 并释放 render，worker 在 WaitOne 后没有立即统一退出检查，Seek 路径需一起审计。
- [DirectX render](https://github.com/Richasy/danmaku-kernel/blob/4d0f8691f08f1e143294402477e72306a1b38b6f/src/Danmaku.Core/DanmakuDirectXRender.cs) 的 Start 清除暂停并启动帧时钟，Pause 停表；[Legacy render](https://github.com/Richasy/danmaku-kernel/blob/4d0f8691f08f1e143294402477e72306a1b38b6f/src/Danmaku.Legacy/DanmakuRender.cs) 的 Start 清除暂停，Pause 设标志并 Invalidate。只修上层 VM 的 IsPaused 无法约束这些写入。

UpdateTime 返回后 Pause 可能先于 worker 的 Resume；显式 Seek 再 Pause 也不能控制已出队的旧进度。外部读列表不能作为跨引擎的完成屏障（Core 返回副本，Legacy 返回内部列表）。固定等待、生产反射或每个 tick 重建 renderer 均不作为方案。

## 必要修复与可选 API 分开

**必要：引擎内部持有播放意图，并保证旧操作不能覆盖它。** 在两个调度器中区分调用者 Pause/Resume 与 Seek 内部暂时 Stop。Seek 完成时应用执行当时的最新播放意图；不能恢复开始时捕获的旧布尔值，也不能只写一个 volatile 后进行无序 render 调用。用统一状态序列/同步边界使 Pause、Resume、内部 seek 收尾和 Close 的 render 状态写入有确定顺序。锁顺序需核查现有 list/queue 锁，不能在 UI 线程等待渲染线程或持锁调用可能回到 UI 的代码。

**必要：内部操作归属与关闭保护。** Clear、SetDanmakuList、Stop/Seek、Close/Dispose 在适当边界推进代次，队列工作携带代次，worker 在修改游标、发渲染事件和恢复状态前验证所属代次及 closing；包括已经出队的工作。关闭必须阻止所有新提交，并使已接收工作进入终态。需明确 Clear/Stop 是清空画面还是改变用户暂停意图；先用旧行为测试锁定 playing 调用方的兼容预期。

**新增可等待 API 不是修复暂停不变量的必要条件。** 若以上内部状态语义正确，现有 void UpdateTime 调用不需要靠等待再补 Pause。可等待完成适合精确测试、外部 seek 编排或需要区分提交失败的调用者；不能把“新增 Task”作为必须升级整个应用的先决条件。

若确有调用方需求，再增加兼容的 `UpdateTimeAsync`/明确的 seek 操作，保留现有签名。完成语义应是“当前代次的调度状态已应用/被替代/已取消/关闭”，不是 GPU 首帧呈现。CancellationToken 只有在待处理队列以及应用状态前检查才有意义；`Task.WaitAsync(token)` 仅取消等待，不取消引擎工作，不能用它宣称取消。已应用操作不能回滚；合并替代的请求也必须完成，不能留下永不结束的 Task。内部代次屏障仍必需，不能仅靠调用方 token。

**可见暂停帧另有要求。** 状态修复后，要验证目标弹幕实际渲染一帧且之后不移动。当前 Seek 会清空旧画面，Core 自动 Seek 分支还会 continue，跳过本次目标调度；暂停阻止 render loop 运行。因此“_isPaused=true”不足以证明暂停 seek 可见。必须覆盖目标弹幕消费和静止首帧，必要时添加内部单帧 invalidation；不得为出图临时无约束 Resume。Legacy Draw 与 Update 的暂停处理也需验证，原验收曾观察普通暂停内容消失。

## 有界实现及测试交付

1. 在独立 danmaku-kernel 本地分支从实际发布来源建立 red：公共 Pause → UpdateTime 前跳/后退，播放中 UpdateTime → 立即 Pause，双向 renderer 重建后暂停。优先保存现有调用签名，不先增加异步 API。
2. 用受控队列/同步屏障在调度器边界覆盖：旧任务已出队时 Pause/Resume、连续 A→B seek、Clear/SetList 后旧进度、Close/Dispose 后迟到任务；不得用固定睡眠控制正确性。表驱动共享行为规范，但两个实现均须执行。
3. 修两个调度器的播放意图、内部 seek 收尾、代次和关闭顺序；按可见首帧测试结果限定渲染层改动。验证普通 playing tick 连续运动、空列表、0ms、列表末尾、恰好5000ms和5001ms、向后跳转、重复时间、切换数据集。保留 playing 的既有恢复行为并明确 Stop/Restart 契约。
4. 独立离线 WinUI 宿主运行两种真实引擎：合成时钟、已知 top/rolling 弹幕；分别记录结构、引擎暂停/时钟、两帧可见像素，关闭后无异常/回写。AOT 与真实视频时钟仍需单列验证。
5. 先交付本地源码提交、测试、打包候选及来源/哈希，不 push、不发布。父级据具体补丁决定是否需要新增 API、申请 danmaku-kernel/NuGet 发布授权。获授权发布后再单独更新 Bili.Copilot 包引用，复验 #49 原始六个失败及上述竞态，不夹带 bili-kernel gitlink 变更。

最小候选涉及 Core scheduler、Legacy master；只有静止首帧测试证明需要时才改两个 render。无全局框架、替换引擎、窗口预取或无关性能重构。本报告不构成发布授权。
