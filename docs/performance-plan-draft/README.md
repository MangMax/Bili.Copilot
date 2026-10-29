# 性能开票复核与发布索引

## 状态

已获用户确认并发布 1 张追踪 Spec、10 张独立修复票、3 张限时调查票。已逐张核对远端正文与本地正文一致、ready-for-agent/scope 标签正确、13 张子票均有原生父子关系且无阻塞。Spec：https://github.com/MangMax/Bili.Copilot/issues/45。

## 进一步核对结果

- 重新 fetch 后 origin/master 与工作树都为 4344ae756344f3ed6f1c11bd3216320b9143996f。并非工作树落后默认分支。
- [#37](https://github.com/MangMax/Bili.Copilot/issues/37) 已关闭，但修复位于 [PR #27](https://github.com/MangMax/Bili.Copilot/pull/27) 的集成分支，尚未进入 master。它解决刷新/撤销的全量重建，不覆盖栏目缓存预算，也未改掉首页分区串行等待。本批不重复开该修复。
- [#42](https://github.com/MangMax/Bili.Copilot/issues/42) 已承载托盘性能和资源验收；不重复立托盘票。
- AI 确认完整绑定链：TempResult → Markdown 属性变更 → 全量 Parse/ReloadDocument；取消分支没有 return，旧回调可继续追加，应纳入同一会话修复。
- 弹幕确认完整取消链：UI 未传令牌；HTTP 层包装通用异常；分片客户端通用 catch 重试。只在 UI 加 CTS 不能覆盖全部验收。
- 单引擎重绘使用逻辑或判断，正常情况下仍进入固定等待；条件问题确定，但未测帧率收益。
- 缓存和 HTTP 的开销机制存在，但预算/接口重构决策缺乏实测与依赖能力证据，降为调查票。
- F10 分成窗口与下载两张票，避免跨业务行为混票。F03 拆为可立即修复的生命周期票与预取调查。
- 未运行应用、构建或测试；不把复核升级为“已实机复现”。

## 已确认测试边界

优先现有服务/会话输入输出边界，使用受控异步结果与 HTTP handler；UI 和真实资源表现通过 WinUI 场景验证。只在必要时增加一个会话级协调接缝，不创建通用测试框架。已有 SponsorBlock、音量合并和推荐差异测试作为先例。

## 工单正文

[Spec 正文](spec.md)

1. [[Perf] AI 流式回复合并显示并隔离过期会话](issues/01.md) — scope:M；阻塞：无；交付：长回复生成期间保持输入响应；停止或切换内容后旧回复不再写入，结束后完整呈现 Markdown。
2. [[Perf] Markdown 清理不再主动触发全堆回收](issues/02.md) — scope:S；阻塞：无；交付：清空或关闭 Markdown 面板时释放自身内容，不主动要求整个应用执行全堆 GC。
3. [[Perf] 切换或关闭播放器终止旧弹幕加载](issues/03.md) — scope:M；阻塞：无；交付：切换分 P、重新加载或关闭播放器后，旧弹幕请求停止调度，迟到结果不会污染新视频或已关闭窗口。
4. [[Perf] 弹幕重绘按活动引擎就绪并合并请求](issues/04.md) — scope:M；阻塞：无；交付：活动弹幕引擎已就绪时立即安排重绘；快速设置变更只应用当前有效状态。
5. [[Perf] 首页推荐首屏不等待分区目录](issues/05.md) — scope:M；阻塞：无；交付：默认推荐/热门内容与侧栏分区独立加载，分区接口缓慢或失败不拖住首屏。
6. [[Perf] 搜索联想防抖并仅展示最新输入结果](issues/06.md) — scope:M；阻塞：无；交付：连续输入仅在短暂停顿后获取联想，旧结果不覆盖新输入，正常取消不刷错误日志。
7. [[Perf] Markdown 图片复用连接并限制解码资源](issues/07.md) — scope:M；阻塞：无；交付：含图片的 Markdown 重复打开时复用连接，按显示需求加载图片，关闭后停止无用加载。
8. [[Perf] 合并滚动触底检查并阻止卸载后加载](issues/08.md) — scope:S；阻塞：无；交付：快速滚动和窗口缩放不会重复堆积触底检查，仍能可靠补齐短列表与继续分页。
9. [[Perf] 播放器模式切换采用非阻塞状态收尾](issues/09.md) — scope:S；阻塞：无；交付：全屏和小窗快速切换时状态稳定，延迟收尾不占用等待线程。
10. [[Perf] 下载进程异步等待并确定性释放句柄](issues/10.md) — scope:S；阻塞：无；交付：等待 BBDown 下载完成时不长期占用线程池线程，成功后按设置打开目录并释放进程对象。
11. [[Spike] 确定首页列表缓存预算与撤销兼容策略](issues/11.md) — scope:spike；阻塞：无；交付：产出有测量依据的首页缓存预算方案与可执行后续票，区分正常保留、数据增长和真正泄漏。
12. [[Spike] 验证弹幕按播放位置预取的可行性](issues/12.md) — scope:spike；阻塞：无；交付：明确首段先展示与邻段预取能否在两种弹幕引擎上实现，并形成有边界的实现方案。
13. [[Spike] 量化 bili-kernel 响应重复解析与复制开销](issues/13.md) — scope:spike；阻塞：无；交付：判断 HTTP 重复解析是否值得优化，并给出兼容错误处理的最小方案与后续票。

当前各票均可独立开始。三张调查票产出后续方案；未来实现若依赖弹幕会话取消等基础，再建立原生依赖。没有因“都是性能”而添加总基线票作为所有工作的硬阻塞。

## 发布记录

已使用 gh 发布至 MangMax/Bili.Copilot，标签 ready-for-agent 与 scope:S/M/spike；Spec 使用 scope:L。补齐 scope:S、scope:L、scope:spike 标签。未关闭或编辑任何既有母票，未提交 PR，未发起委派。

用户已确认测试边界、13 张票的粒度和无硬阻塞关系。


## GitHub 工单链接

- [[Perf] AI 流式回复合并显示并隔离过期会话](https://github.com/MangMax/Bili.Copilot/issues/46)
- [[Perf] Markdown 清理不再主动触发全堆回收](https://github.com/MangMax/Bili.Copilot/issues/47)
- [[Perf] 切换或关闭播放器终止旧弹幕加载](https://github.com/MangMax/Bili.Copilot/issues/48)
- [[Perf] 弹幕重绘按活动引擎就绪并合并请求](https://github.com/MangMax/Bili.Copilot/issues/49)
- [[Perf] 首页推荐首屏不等待分区目录](https://github.com/MangMax/Bili.Copilot/issues/50)
- [[Perf] 搜索联想防抖并仅展示最新输入结果](https://github.com/MangMax/Bili.Copilot/issues/51)
- [[Perf] Markdown 图片复用连接并限制解码资源](https://github.com/MangMax/Bili.Copilot/issues/52)
- [[Perf] 合并滚动触底检查并阻止卸载后加载](https://github.com/MangMax/Bili.Copilot/issues/53)
- [[Perf] 播放器模式切换采用非阻塞状态收尾](https://github.com/MangMax/Bili.Copilot/issues/54)
- [[Perf] 下载进程异步等待并确定性释放句柄](https://github.com/MangMax/Bili.Copilot/issues/55)
- [[Spike] 确定首页列表缓存预算与撤销兼容策略](https://github.com/MangMax/Bili.Copilot/issues/56)
- [[Spike] 验证弹幕按播放位置预取的可行性](https://github.com/MangMax/Bili.Copilot/issues/57)
- [[Spike] 量化 bili-kernel 响应重复解析与复制开销](https://github.com/MangMax/Bili.Copilot/issues/58)
