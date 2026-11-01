# Legacy seek 并发/worker 修复候选

基线父 `0d3a22f61575550e23f935028ec5f6376cac22ce`；纳入固定候选 `f0e7629923e1aaa6ed5d71a9b9b8fdbd09690ba5` 的面板门禁及原交付，并纳入 `c00a4c2ba2825f544b73bd5aad61a5124fa5e053` 的 review。新版本 **`0.0.1-seekd1fc.20261101.2`**，不覆盖 `.5`，未接入正式依赖。早期 `.1` 是提交源码前的本地构建尝试，未启动或交付；最终六包仅 `.2`。

## 修复

- **P1**：worker 在短 `_updateTimeQueue` 临界区一起取得列表引用、目标、版本与 seek 标记，再等待列表 monitor。Set/Clear/AddList 使用同一退休/发布协议；播放锁仅用于清理及单条原生准备，没有全程播放大锁。Set 后的首个时钟请求也会重建新场景。
- **P2**：新增只读 `WorkerFailure` 终态。原生调用逃逸异常在释放播放锁前保存原始原因、退休版本并清空队列；worker 外层 catch 同样发布终态。后续 Seek/UpdateTime/Resume/换列表/资源设置显式抛出带原始原因的 `InvalidOperationException`。Start 成功后才提交 ready 时钟。Close 仍允许显式最终清理，清理异常照常传播；不重启 worker、不自动重试或重用故障资源。没有修改底层 owner/borrow 协议。
- 可以明确判断安全的参数错误（实时弹幕 null、非法 layer）在任何资源操作前抛出参数异常，不使 worker 终止。未知原生异常不猜测可恢复。正常过期版本是取消，不是失败；暂停、隐藏和关闭意图保留。

## 验证与复放

原 review 两个反例先用未修改 `221bbb019` 原样复放，见 `evidence.zip/tests/replay-red.txt`。之后同一新增断言集对旧源码红、对新源码绿；最终红为 9 项失败，绿为 **13 通过 / 0 失败**。较早的 10 项红绿记录亦保留，退休后的追加请求交错可能导致旧版不同额外失败数，两个核心反例均固定复现。

| Requirement | Evidence |
|---|---|
| 最新请求绑定最新列表 | `ListReplacementLatestSeek` |
| Stop/Pause/逐条准备/Start 故障后明确拒绝 | `FailureThenLatestSeek_Stop`, `FailureThenLatestSeek_Pause`, `FailureThenLatestSeek_Item`, `FailureThenLatestSeek_Start` |
| 故障退休此前已入队的新请求 | `FailureRetiresAlreadyQueuedLatestSeek` |
| 安全参数错误不杀 worker | `InvalidRequestDoesNotFaultWorker` |
| 尚未就绪时替换场景 | `ReplacementBeforeResourcesReady` |
| 捕获后 Clear/Stop/Close 退休 | `RetirementWhileSnapshotBlocked_Clear`, `RetirementWhileSnapshotBlocked_Stop`, `RetirementWhileSnapshotBlocked_Close` |
| 暂停意图、逐项取消、提交不等资源准备 | `PauseLatestSeek`, `NewSeekCancelsBetweenItems` |
| 原相关 44 项回归 | `evidence.zip/tests/replay44.trx`，与原 review 44 个名称逐一相同；Passed: 44, Failed: 0, Skipped: 0 |
| 新包实际原生验收 | 下述有界 owned 运行、`summary.json`、完整原始事件 |

在仓库根复放确定性红绿（新目录，不启动 UI）：

```powershell
python -X utf8 docs/seek-responsive-fix/replay.py artifacts/replay-seek-fix
python -X utf8 docs/seek-responsive-fix/verify.py
```

`replay.py` 已实际从新 bundle clone 并成功复放。探针只替代原生表面，直接编译原始 master；不冒充 GPU/WinUI 故障验证。相关 44 项测试使用候选源码私有归档与只读复制的离线 kernel 源码，未修复或恢复正式 kernel。

## 本轮原生证据

仅本区 `Bili.Seekd1fc`、本地媒体和离线服务；Legacy 普通 300 / 密集 12000 条、快速最新 seek、隐藏取消、寿命/暂停恢复、3600 秒时间轴上的列表替换和 pending Close。未重复历史 Core/native 全矩阵。Core 源码精确保留原提交；原 `.5` 的 Core 短密集样本没有提速，原证据及限制在 `../seek-responsive/README.md` 保留，不以这次重新打包宣称 Core 性能收益。

最终运行的实际加载 DLL SHA 与交付三包逐一一致；原生 ready（内部时钟/活动资源）与 UI 提交、媒体确认→进度通知、Dispatcher 心跳分别记录，不把 ready 当作光学显示。最终密集 12000 条样本：提交 3.30 ms，媒体确认→进度 5.42 ms，最大心跳间隔 40.67 ms，原生 ready 2355.00 ms；pending Close 151.87 ms。15 项原生断言全部通过。完整数值见 `summary.json`。长时间轴是弹幕数据，实际本地媒体仍为 60 秒。非完整 AOT 发布验证；后续独立聚焦 review 和正式接入仍交父任务。

第一轮原生断言通过，但观察器在 Legacy 挂载前停止，缺完整运行时加载哈希；其证据保留在 `evidence.zip/native-initial/`，不用于最终加载证明。第二轮仅调整测试宿主观察点、重跑同一有界集合。两次均保留退出句柄、正常退出、退出后至少 31 秒精确 PID/path WER 查询、独立包清理及 yield。没有杀用户进程、更新用户包或访问凭据。

最初宿主构建失败来自旧测量注入文件的双换行破坏 XML 注释，以及缩减夹具后未赋值字段告警；只修正宿主注入。最终宿主构建 0 警告 / 0 错误。完整过程日志保留。

## 源码/包闭包与恢复

- Legacy/Share：`15434adfee3c80032ca12ab4840bc36f54858f65`，相对审查源码 `221bbb01917b71f2d305506572c0526138950e28` 只修改 `DanmakuFrostMaster.cs`。
- Core：`e094d330a69dbedeba9b0260b6bb499391d643c4`，未修改。两源码仓库 Share 的生产 `.cs` 完全一致；Core/Legacy 的包依赖统一指向同版 Share。
- `legacy.bundle` / `core.bundle` 包含完整 Git 恢复链；对应 `.patch` 相对原冻结基线，`legacy-review-fix.patch` 仅本次增量。可 `git clone <bundle> <新目录>`。`sources.json` 记录基线/头。
- `source-snapshot.zip` 包含精确生产源码、DLL/PDB CodeView/MVID 及全部 PDB 文档（含嵌入的生成源码）。独立 `audit-tool` 校验三 DLL/PDB 配对和每个源码 SHA；`packages.json` / `manifest.json` 锁定包与证据。
- `pack.py` 记录本机打包命令，重建使用新输出目录/版本，勿覆盖交付版本。`prepare-native.py` / `build-owned.py` / `launch-retain.ps1` / `cleanup.ps1` 用于本机原生复现；依赖 README 所述原候选的只读离线资产目录 `../t3-39df52c6/artifacts/combined-60cd/full`，不是跨机器自包含安装器。准备要求本区 `artifacts/seek-fix/full` 不存在。仅在独立身份与 owned 桌面可用时启动。

kernel 初态 HEAD `f02c33f639addd5d421700c43fa66382beafe323` 及原有 474 项 staged deletion 保持不变，未写正式 gitlink。正式 `src/Directory.Packages.props`、冻结包、父区和来源区均不变。用户 AotDev 与另一验收实例 PID/path/创建时间和已安装包快照前后相同。本区提交交父任务安排一次针对 P1/P2 的独立复核；本实现任务未启动任何子代理、push、PR 或 issue 操作。
