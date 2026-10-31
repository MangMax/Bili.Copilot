# Overlay 同实例重挂局部修正

基线 e6f6970b。仅修改 OverlayTransportControls 的本地生命周期订阅；不合入 seek/pointer 候选。

原构造器订阅 Tick，首次卸载退订，后续 Loaded 无重订阅，且基类首次卸载后自身生命周期接线不再执行。改为本地 Loaded/Unloaded 接线，使用活动标志使基类首次回调与自身回调幂等；每次挂载恢复两个 Tick 和 ProgressChanged，卸载清理队列/订阅。恢复已创建的字幕 Repeater 数据源。卸载后的迟到速度变化不安排请求。

复放：`python docs/engine-61-design-draft/overlay-remount-fix/probe.py verification`。
探针提取实际构造器及生命周期方法，用适配器表达基类首次卸载自注销行为，检查三轮重复 Loaded/Unload 的订阅计数。原代码第2/3轮失败，修复后 failures=0。它没有执行真实 WinUI Timer、完整绑定或 native 播放，不能替代实机。
初次 green 提取器不支持表达式方法而重复捕获后续方法，编译失败；已修复提取器，green-final 成功，原输出留在 artifacts/overlay-remount/green。

WinUI 静态自审：UI线程事件接线，首次重复回调幂等，未增加架构层或修改依赖。完整应用编译及真实同实例 Overlay 重挂、Playing 下调速关闭待独立验证。历史 AV、Legacy seek、AOT 等未由本片解除。
