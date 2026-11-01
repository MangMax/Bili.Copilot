# 固定提交独立审查

结论：**可合入，无实际阻断项。** Standards 0 项，Spec 0 项。本结论只覆盖此次首页/分区修复，不批准播放器或完整用户包验收。

- 固定候选：`cd915f2d81bab1a78bd165177a8851d477b12c78`。
- 父提交：`8c797bfdd3854f00171b18cd8f121a210298ff1e`；交回前 `feat/performance-45` 仍指向该父。
- 来源工作区 `t3-d85120ab` 只读；自身 `t3-fedf120e` 仅提交此审查小证据。双方原有 `externals/bili-kernel` 脏状态未操作，未改 gitlink、依赖、用户包、实例或登录。
- 读过 AGENTS、domain 指引、code-review、winui-code-review、run-tests；补边界用例时应用 code-testing，交回前应用 verification-before-completion。按任务禁止子代理，两个审查维度均独立完成。

## Standards

生产差异确实只有两个方法；没有新增 XAML、事件订阅、集合替换、线程切换、动态绑定或依赖。`git diff --check 8c797bfd cd915f2` 通过。领域 `Partition.Equals` 比较 Id/Name，候选把右侧也解包到 Partition，符合现有缓存/同项比较语义。没有需要引入新设计的代码异味。未另外运行 WinUI analyzer 或重建完整 native 矩阵。

## Spec 与根因范围

首次分区加载：恢复父分区先发异步请求，ItemsView 回显同一选中项；旧代码将领域对象与包装 VM 比较，必不相等，清快照并推进代次，首个响应因过期被丢弃。候选同项早退保留首次请求和 undo；真正换项仍清理旧代次。可解释从别页进入**分区视频页**空白，不能泛称所有页面、线上错误或完整认证启动已修复。

增量开关：设置 TwoWay → 设置 VM → LocalSettings 写入和重新读取链无丢失。每次推荐刷新读一次开关，false 替换、true 在非空列表上增量；变更在下一次刷新读取，不追溯修改进行中的请求。旧分区代码在分页中因 `IsVideoLoading` 返回，之后分页追加，造成两个开关值都像增量。候选让显式刷新接管分页，推进代次。

**Popular 正常并不矛盾**：它的外层 `RefreshVideoAsync` 原已推进 `_sectionVersion`、清 `IsVideoLoading`，再进入读取开关的 `Requests.cs`；分区外层原来没有此接管。两个页面也不共用分区父选中回显逻辑。本次无需修改 Popular。

旧分页写游标、追加条目之前及加载完成清标记之前均核对代次。刷新自身还有 `IsRecommendRefreshing` 防重入，成功/空结果/异常与 finally 的状态恢复受代次保护。同分区 echo 保留撤销快照，真正换区不能接受旧刷新结果。

## 独立复放

在自身 `artifacts/hr/source` 导出固定候选及精确 kernel `0ae5e4e000a41a07f6696746706df11c06e8835b`，没有采用脏 kernel 工作文件。对象来自现有 `t3-041ea9a9` 模块对象库，只读 archive。首次全仓导出遇路径长度限制，缩小导出范围后完成；来源工作区没有构建写入。

测试用 .NET 11 固定 SDK、xUnit 2/VSTest。共同命令：

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release -p:Platform=x64 -p:NuGetAudit=false --filter <下面的过滤器> --logger "trx;LogFileName=<结果名>.trx" --results-directory ../results
```

首次 `related` 过滤器为 `FullyQualifiedName~PartitionLoadingRegressionTests|FullyQualifiedName~PopularPageLoadingTests`，Passed: 37, Failed: 0, Skipped: 0，退出 0。包含原五项分区测试、两项审查测试以及 Popular 相关测试；此范围与作者归档的 33 项过滤器不同，不混称同一轮。

随后只在导出副本把两个生产文件替为父版本，过滤 `FullyQualifiedName~SidebarSelectionEchoPreservesFirstPendingResponseAndRepeatedInitialization|FullyQualifiedName~PersistedRefreshModeWinsOverPendingLoadMoreAndNextSettingChange`，加 `--no-restore`，结果名 `red`：Passed: 0, Failed: 3, Skipped: 0，退出 1。确实分别失败于空列表和缺少第三次刷新请求，未以构建失败冒充 red。

恢复候选两个文件，过滤 `FullyQualifiedName~PartitionLoadingRegressionTests`，加 `--no-restore`，结果名 `green-boundaries`：Passed: 7, Failed: 0, Skipped: 0，退出 0。TRX 与两项额外方法在 `review-evidence.zip`；方法插入候选 `PartitionLoadingRegressionTests` 内、`Parent()` 前即可重放，没有提交为生产测试改动。

| Requirement | Evidence |
| --- | --- |
| 首次请求及重复初始化 | `SidebarSelectionEchoPreservesFirstPendingResponseAndRepeatedInitialization` |
| 两种开关、下一次生效、undo 引用恢复 | `PersistedRefreshModeWinsOverPendingLoadMoreAndNextSettingChange(false/true)` |
| 旧分页不清刷新状态/覆写 cursor | `OldPaginationCannotClearPendingRefreshOrOverwriteItsCursor` |
| 同分区回显保留 undo | `SamePartitionEchoPreservesUndoSnapshot` |
| 刷新防重入、失败保留列表、拒绝旧分页、原 cursor 可重试 | `ReviewRefreshFailureRecoversWithoutAcceptingSupersededPage` |
| 真正换区、旧刷新不能写回或清新页 loading | `ReviewTruePartitionSwitchRejectsOldRefreshAndPreservesNewLoading` |

## 原验收证据核验

候选 `validate.py` 独立执行通过；zip 全部条目 SHA 校验通过。额外核对 `final-production-inputs.json` 指向的源码逐项匹配固定提交（计入 checkout CRLF），不只相信 equal 字段。

- 原 TRX：baseline 572/572、regression-red 0/3、related-green 33/33、all-green 577/577，均无跳过。新增 5 个用例解释 572→577；本审查没有重跑整套 577。
- WinUI `pendingred2` 21 项中 5 失败，准确包括首次显示、初始可见卡片、返回内容及两个分页刷新开关。`green` 24/24。其卡片 oracle 是可见索引、视口、容器 Content/DataContext 与卡片 VM；不冒称该轮已核对实际标题。
- `aotgreen` 27/27；11 个 Visible 行均含 `title=True`，包含分区和 Popular 实际 TrimTextBlock.Text 与对应 VM 标题相等。最终驱动只枚举当前索引的视口内元素，规避回收池假阳性。
- native EXE 文件 SHA 与 native-pe、运行 loaded-modules 记录一致；CLR RVA/size 均为 0。只读检查 fixture assets：Mpv 四包均 `0.0.2-preview2`，WinUI 包 metadata 源为 nuget.org，内容 hash 匹配 assets；不是 `aot-local.1`。
- publish 日志确有 `SymbolIcon.UseSegoeMetrics` getter/setter 的 ILC 缺失 API 告警及既有分析器告警。首页 host 不进入播放器，不能外推播放器可用，也不能以首页绿灯覆盖告警。
- PID 34208/54160/15820 与各 owned layout 路径、启动/退出记录对应；退出码均 0、保留句柄、非强杀、无 managedUnhandled。最后 WER 查询分别在退出后 31.119/31.084/31.134 秒开始，事件 XML 空，退出门禁均 `PASS_BOUNDED`，不等于无限期无崩溃保证。
- 三轮 cleanup 均 processAbsent/registrationAbsent。yield 调用存在，回执是 `released:false`，不包装成成功释放了某个已有 lease。
- 保留报告中历史事实：`pendingred` 是未完成构建的旧驱动，不作 red；早期 AOT 广泛扫可视树误把回收池当当前卡片，两项失败不是绑定故障。原材料仍在来源 artifacts，未删除或替换。Caa6a7be 未入最初保护快照，不能补造完整前后证明。

交回父安排集成与新的 AOT 修复包。本审查未启动 native/用户实例，未注册或卸载包，未 push、创建 PR 或操作 issue。
