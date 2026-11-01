# 快速滚动连续显示：共享缓存候选未采用，留白目标未完成

基线父：`6133ae590daf474d606e7b0afbdbf21e857b8ed1`。kernel：
`0ae5e4e000a41a07f6696746706df11c06e8835b`。最终只有一个生产 XAML 改动：

* `PartitionArticlePresenter` 的头像 Source 加 `Mode=OneWay`，修复卡片回收后仍显示前一项头像。

**不能按“所有列表快速滚动留白已修复”合入。** 视频、直播和部分短列表有收益，
但 500 项 User、Season 仍有大量整片背景；文章头像修复后，缓存 2 的 500 项表现更差。
因此撤回共享缓存 2 候选，生产仍为 0.5。这是一次有明确反证的部分交付，不是全场景完成声明。
未修改图片解码/流生命周期、推荐封面 resize 合并与 WIC 并发 2、此前 33 处整对象 Binding、
Basic/Blur 重挂及通知 OneWay；没有动态调度、纹理缓存或关闭虚拟化。

## 复现与判定

使用归档恢复的原生 WinUI host，实际生产控件/模板与离线适配 VM；50/500 数据预先全部装入。
窗口 1400×900 physical，DPI 1.5。每组缓存比较使用同一 Harness.dll，差异仅运行参数。
不是登录中的产品实例、网络分页或完整 AOT 产品测试。

先前动画关闭、半 viewport 的尝试没有复现整片留白。随后用 ScrollView 原生惯性：
64 次 ±4000 velocity、请求间隔 50ms、每 8 次换向。实际请求间隔和 offset 保存在原始记录；
UI 阻塞时请求本身会延后，这是一条高强度压力轨迹，不能称为普通鼠标速度。
另测已 warmup 4.5 viewport 内往返：32 次 animated ScrollTo，请求间隔 150ms；实际位移
约 1.64–2.13 viewport。它覆盖返回刚离屏区域，但不等于惯性压力轨迹。

所有 visible outer item 均检查索引、VM 引用、Source、ready 与几何位置；不丢弃空 VM/Source。
同时用独立 `winapp ui record` 录制 owned 窗口，30fps，在 motion 全时段采样，
按 viewport 内 RGB 均 <6 的背景比例 >90% 判定整片留白。持续时间为采样估计，不能声称 60fps。
全部 RGB 网格、crop、时戳及 JPEG hash 可重算；归档只保留最长留白附近等声明过的 JPEG 子集。

定位结论分开看：

1. 文章确有绑定缺失。原 `real-article05/2` 的 VM 与封面正确，头像仍来自旧 index；
   50/500 两种数量均为红。均一图片 fixture 曾掩盖它。加入 OneWay 后用唯一图片重测，
   不允许用旧的低工作量性能结果验收。
2. 其他家族在能收到回调的帧中未发现整卡 VM/Source 丢失。原生惯性继续运行时，外部录像
   捕获了 UI 回调没有覆盖到的整片背景；所以“回调几何 coverage 正常”不等于连续显示正常。
   这是 realization/rendering 跟不上移动视口的可见表现；图片 pending/反复 draw 与之并存。
   当前证据不足以把每一个被阻塞期间的像素唯一归因于创建卡片或图片解码，不能冒称已完全分离。
3. 缓存增大明显减少部分反向重新准备/绘制，但不能覆盖密集小图的全部成本。因此没有继续叠加
   未经验证的图片生命周期修改，也不把缓存调大视作通用解法。

## 测量结果

以下表格由归档中的原始 JSON 汇总；0.5→2 为同 fixture 同二进制配对。
`resize` 是滚动后立即发起 resize 的最大 heartbeat，**不是**原 settled PNG 的 200ms 门槛。
内存是 case 结束时 private bytes，非峰值或纹理缓存大小。

<!-- MEASUREMENTS -->

| 家族/数量 | 整片背景总 ms | 最长连续 ms | realized 最大 | private MB | resize heartbeat ms |
|---|---:|---:|---:|---:|---:|
| 视频/50 | 270 → 54 | 177 → 54 | 11 → 17 | 254 → 300 | 80 → 80 |
| 视频/500 | 2349 → 78 | 339 → 78 | 11 → 17 | 458 → 427 | 184 → 225 |
| User/50 | 152 → 0 | 92 → 0 | 20 → 32 | 164 → 175 | 135 → 129 |
| User/500 | 5312 → 5292 | 799 → 767 | 20 → 32 | 195 → 202 | 350 → 451 |
| 直播/50 | 0 → 0 | 0 → 0 | 11 → 20 | 301 → 333 | 58 → 104 |
| 直播/500 | 758 → 79 | 200 → 79 | 11 → 20 | 449 → 437 | 123 → 120 |
| 文章/修复后唯一源/50 | 702 → 661 | 375 → 566 | 11 → 16 | 181 → 181 | 62 → 99 |
| 文章/修复后唯一源/500 | 4929 → 8142 | 698 → 1359 | 11 → 17 | 187 → 196 | 479 → 528 |
| Season/唯一源/50 | 313 → 64 | 124 → 64 | 17 → 27 | 231 → 238 | 170 → 98 |
| Season/唯一源/500 | 4341 → 4475 | 611 → 636 | 17 → 27 | 269 → 293 | 249 → 230 |
| 文章/1600×900 均一源压力/50 | 142 → 0 | 80 → 0 | 11 → 17 | 300 → 311 | 67 → 92 |
| 文章/1600×900 均一源压力/500 | 341 → 2528 | 124 → 537 | 11 → 17 | 440 → 558 | 77 → 272 |

已经 warmup 区域的 Return 轨迹，六组数量/家族在两档均为 **0** 个整片背景样本。
因此不能称这条轨迹的留白被修复；可确认的是反向工作量减少：

| Return 500 项 | 反向 prepared | 反向 Basic draw | private MB |
|---|---:|---:|---:|
| user | 269 → 187 | 137 → 56 | 180 → 201 |
| season | 334 → 45 | 0 → 0 | 379 → 463 |
| article | 349 → 13 | 7 → 5 | 339 → 382 |

Season 的计数器只统计 Basic，不统计 Blur，0 不能解释为没有图片工作。
Return Article 使用旧均一源，不能代替修复后唯一头像的连续性能验收。

<!-- END MEASUREMENTS -->

cache 1 也保留完整结果：它不是稳定折中，例如 Live500 的整片背景时间比 0.5 更差。
原 oversized Article/Season 使用同一 1600×900 PNG（文章头像也是此图），不代表通常源尺寸。
`Real` fixture 使用 kernel adapter 的文章 400×220、头像 48×48、Season 320×426，
每项唯一的本地合成 PNG。其尺寸有源码依据，但不代表真实 JPEG/网络/所有 codec。

### 原 resize 门槛与功能回归

在 mount **之前**应用 cache 的 `settled-png-05/2`，保留原 PNG User50/500 轨迹与 200ms 门槛：

| cache | User50 最大 heartbeat ms | User500 最大 heartbeat ms | 初始/峰值 realized | 检查 |
|---|---:|---:|---:|---|
| 0.5 | 140.76 | 119.75 | 13 / 17 | 81 通过 |
| 2 | 154.10 | 172.99 | 22 / 32 | 81 通过 |

代价可见：2 的 resize 更慢，但这两次仍低于原门槛。早期 `original-png-*` 在 mount 后才覆盖 cache，
存在预填充混杂，保留但不拿它作最终比较。未重新测 20，不能用历史 20 的数值冒充本次对照。

* `final-binding500`：8 项检查及 12 个唯一头像像素检查通过。
* `undo-final`：13 项通过，包含引用序列、undo/锚点及推荐图片相关检查。
* `covers-final`：10 项 Basic/Blur 生命周期检查通过。
* `pane-final`：13 项 Season 双 pane 检查通过。

这四组在头像补丁之前的 build6、缓存 2 下执行；最后补丁只改文章头像绑定，另用 build7 的文章双档覆盖。
最终撤回共享缓存后，生产默认为 0.5；`fixed-article05` 已在 mount 前显式设置此值，
不要求重新编译相同模板来覆盖同一运行策略。归档实验 host 的共享资源仍是 2，不能误称与最终资源逐字一致；
新 checkout 的 prepare.py 从最终提交读取 0.5，再由运行参数设置比较档位。
未把它们宣称为完整产品测试套件。最终 harness 构建 0 errors、161 warnings（归档适配器等）；
首次实验构建曾失败，日志保留，没有把后续成功覆盖成“全程零失败”。

## 证据、安全边界与交接

[可重放步骤](scroll-continuity/README.md) · [紧凑证据包](scroll-continuity-evidence.zip)。
`receipts/summary.json` 是全量汇总；`runs/*/continuity-checks.json` 是身份/时序安全检查，
`visual-check-output.txt` 是严格零整片背景 oracle，红色结果故意保留，不能将 audit passed 当作视觉通过。
两组旧 Article Source 红由 audit 精确白名单保留，只有这两个明确失败名称允许出现；新修复组必须全绿。

初态 kernel 为异常 HEAD/删除状态，先备份本区 git 元数据与状态，再从仓库 kernel.bundle 恢复精确提交；
未触碰父或其他工作区。部分旧 Temp 证据已不存在，仅使用提交内 zip 恢复源/资产并新跑，
未假称找回或核验丢失历史证据。

37 次运行仅使用 `BiliCopilot.Recycleba62b02144c9.Owned`。每次正常退出保留 handle/exit0，
>=32 秒后核验 exact PID/path/time-window WER、卸载本次 owned package、yield。
初始保护的 AotDev 主进程 PID 47980、visor PID 46644 的 PID/path 在最终收据仍相同。
没有读凭据、启动/关闭/升级其他实例、全机/Temp/其他 worktree 清理。

独立 review 尚未发生。按“不要启动子代理”的范围，交父安排一次独立 review；本报告、审计脚本和
实现者自查不冒充独立批准。review 只应评估文章 OneWay 修复与证据有效性，不应把被否定的缓存 2
作为已实现成果合入。User/Season/Article500 压力轨迹和完整连续显示目标仍未关闭。
未合父、未 push/PR/issue。
