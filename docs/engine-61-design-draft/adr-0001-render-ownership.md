# ADR draft 0001：有序场景提交与单所有者资源退役

状态：**proposed**。日期：2026-10-30。范围：#61（L）；关联 [Spec](spec.md) 与 [验收切片](acceptance.md)。此编号仅在本草案目录内有效，不占用仓库正式ADR编号。不表示独立审查已完成。

## Context

Spec F1–F6 证明播放意图、场景准备、native队列生命周期混在一起：隐式seek恢复播放；paused阻止初始化；Core有两个执行线程、UI resize及引用快照寿命窗口；Legacy自有paused不停止动画TotalTime。仅扩stateGate会让UI等待昂贵render操作，移出锁又会允许旧seek覆盖新Pause。

应用F10释放旧实例后立即清面板/创建新实例，F11重绘按SetList、时间、样式、意图多步发布。需要隔离实例生命周期，而不是要求调用者了解线程、GPU或在卸载事件同步等待。

## Decision proposed

推荐纯数据调度module产生不可变场景；Core一个真实render owner，Legacy一个经实机验证的动画owner。公开调用只发布有序命令；render执行点重新验证lifetime/content/scene/resource版本，最新播放意图独立读取。每条命令有明确完成或拒绝终态。

Close/Dispose返回标记逻辑关闭和UI脱离；资源由retirement协调器保活至owner清理、调度退出和native队列shutdown完成。该完成不等于GPU已显示最后一帧，UI也不得等待它。若外部使用者需要同步Dispose语义，该兼容问题必须先解决，不能以签名未变跳过。

暂停仍允许Prepared场景的零时间步布局与Draw。先交付等时Top首帧，再按已裁决的active-set语义重建年龄、轨迹、寿命、槽位。正常播放的逻辑年龄排除暂停墙钟时间；不以短暂Resume借一帧。

## Alternatives

| 选项 | 拒绝/保留理由 |
|---|---|
| 状态锁覆盖整个render与同步drain | 保留顺序但UI等待worker；超时也不能证明资源安全 |
| 把所有操作交给UI Dispatcher | 不能消除Legacy动画线程，并把文本/native/Present成本移到UI |
| 新公开async API作为唯一修复 | 可提供外部完成观察，但不能自动解决内部两个owner、资源保活、首帧与旧意图；只作为D1的兼容扩展选项 |
| 全部复用失败candidate并增加draw开关 | 已有失败首帧/关闭实验，无安全所有权证明；不采纳 |
| 保存完整历史配置重放seek | 更接近过去轨迹，但需历史存储、新引擎交接和预算；与目标配置重建一起交D2裁决，不能暗中实现 |

## Consequences and review gates

- native复杂性局限在两个render adapter，应用不增加线程协调框架；额外代价是版本、终态和异步退役状态。
- 构造/关闭不再同步等待ready/drain，行为契约需要D1审查。旧实例必须自持清理责任，不能让panel置null变成泄漏。
- 内部纯数据命令不够证明安全：必须分别证明Core唯一执行线程、Legacy真实动画线程、UI-only操作、shutdown失败终态及资源未就绪时的关闭。
- D2决定seek的目标配置/历史配置、媒体时间与动画年龄换算、密度/槽位及模式范围；D3决定别名/增量兼容；D4决定Clear/Stop/Restart；D5决定实时插入。未决项阻塞对应实施切片。
- 原26调度测试是回归底线；需新增render阶段屏障、UI heartbeat、资源完成观察、真实WinUI首帧及包来源证据。
- 本ADR不批准修改正式包引用、kernel或外部仓库，不授权发布。每个切片在独立分支可审可测，但直到最终矩阵完成前不替换正式依赖。

## Status of evidence

静态路径和行号见Spec第2节，fresh源码hash见source-verification.json；历史来源纠正以[集成报告](../performance-acceptance/integration-engine-49-diagnostics.md)为准。Win2D历史两图不证明白字消失，故不以该解释支撑本决策。此轮没有运行/性能结论，后续由新独立审查处理。
