# RX2-R2：场景控制回执身份

2026-10-31。父级直接修正独立审查 `8c705e7aad9cde4e7d548c00bf5656e808f439da` 的三个反例。
仅隔离引擎候选；IX 尚未准入，#49 仍 FAIL。正式应用源码、依赖和 kernel gitlink 未修改。

## 修正

基线引擎 `abfe2641813905eeb5e0374cb4f983f4ce2ff6af`。修复提交完整 SHA 及53文件 hash 见
[source.json](source.json)，可恢复 bundle、ZIP 与二文件 [patch](candidate.patch) 同时封存。

StopTracked 返回 callback 之前分配的 scene 和 PauseTracked 分配的 intent；SetTracked/ClearTracked
返回 BeginCollection 的原 scene。RenderAsync 每次准备前核验请求身份，Closed/Faulted 仍优先。
被重入的新场景或恢复意图取代时返回 Superseded，而不是借新帧的 Applied。
Set/Clear 不绑定 intent，保留同场景 Pause/Resume 下仍可准备内容的语义。

公开 void 签名、同步事件调用顺序、原异常传播、outer-Set-wins 集合赋值和原 Stop 分段 admission
保持不变；没有把本片扩大为 Stop 原子性重设计。owner、borrow、资源释放和UI生命周期未改。
两个文件共25行增加、12行删除；修正原理来自审查的临时因果对照，本轮在自己的隔离源中实施并重测。

## 实际验证

沿用 receiving-code-review、run-tests、code-testing focused、winui-code-review。
执行：`python -X utf8 -B docs/engine-61-design-draft/rx2-r2/fix-and-test.py --name rx2-r2-parent-fix`。
脚本从R1的校验bundle新clone，使用固定RC3 SDK、原console fixture及真实owner/CoreRenderSurface链；
测试friend只加入副本，未提交引擎。没有把旧审查的对照结果作为本轮运行。

| Requirement | Evidence |
|---|---|
| Stop callback恢复后旧Stop不可报Applied | `R1_StopCallbackResumes_ReceiptMustBeSuperseded`，红→绿，验证下一真实帧100ms |
| Set callback替换场景后旧回执不可报Applied | `R1_SetCallbackReplacesScene_ReceiptMustBeSuperseded`，红→绿，同时保持outer-Set-wins |
| Clear callback建立新内容后旧回执不可报Applied | `R1_ClearCallbackReplacesScene_ReceiptMustBeSuperseded`，红→绿，同时保持replacement列表 |
| 同场景准备重试保持 | `R1_Selection_QueuedIntentChange_RetriesOnce`、`R1_Realtime_QueuedIntentChange_RetriesOnce` |
| 既有Pause/Resume身份、异常、关闭与RX2行为保持 | 原25项及其余R1边界，原始唯一用例结果见evidence |

基线 **33 passed / 3 failed / 0 skipped，exit1**；修正后 **36 passed / 0 failed / 0 skipped，exit0**。
两次构建0警告/0错误；JSON强制36个唯一名称及三个精确红例，全部原字节日志/JSON gzip保存在[evidence](evidence)。
`seal.py`要求引擎Git clean；bundle verify成功，未修改源交付或审查工作区。

## 限制与交接

本轮未重跑完整QX/OX/C1a/R1套件、mutants、clean无friend DLL/PDB封存，也未启动新桌面宿主或做WER观察。
不是实际public包消费或完整UI通过。独立复核需补上述适当回归及来源，并检查Set/Clear同场景意图改变、
Stop关闭/异常等边界；不得仅凭36项认定整个API已闭合。

WinUI静态审查未见新增UI等待、XAML/ABI更改或资源所有权路径；未运行WinUIAnalyzer/AOT。
保留全部历史FAIL/UNKNOWN、Present hang未证、cache预算、AOT、IX/PX、Legacy与#49限制。
未push、PR/issue修改、部署或操作认证数据。
