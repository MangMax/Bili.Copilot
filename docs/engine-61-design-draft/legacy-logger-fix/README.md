# Legacy logger 重入局部修复

基于 d954aed5 与 ef9ed399 审查。原 StopLoggerCallsSeek 候选死锁反例由审查原记录保留。

只将 master Stop/Close 所调用 renderer 的状态更新与日志回调分开：增加内部带 log 参数重载；master 在 playback 锁内完成原修改，在锁外报告原日志。公开签名不变，Seek 的原日志语义不变，不建立日志队列/owner 框架。日志异常依然传播；Close 状态已完成后才报告，不以 logger 不重入作为使用条件。

实际构建 exit0；审查的原 Boundary.dll 方法体在新 Legacy DLL 上执行：StopLoggerCallsSeek completed=true，QueuedTailAndLatestIntent PASS，CloseRejectsLateCommit PASS；DequeuedThenStop 仍显示原有 stopped=false 限制，不算修复。回归是 managed 同步状态 seam，不是 native UI 验收。source.json 记录新来源/hash。replay.py 依赖当前机器审查产物路径，后续独立复核须重编 Boundary.cs 验证来源，不当通用安装脚本。

正式应用 src/依赖/gitlink 未变。完整源码覆盖文件在 source/，原候选/原审查证据均未改。下一步一次独立复核加真实应用隔离接线验收；检查 Close logger 重入、日志异常及原 native 暂停案例。历史 AV/#49/AOT 仍未解除。
