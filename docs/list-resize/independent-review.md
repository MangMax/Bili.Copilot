# 固定候选 1797c94a 独立审查

审查对象：`90e215f0..1797c94aa7c6fc54226bb4ea8f0efef90a49f5bd`，7 个生产文件。Spec 来源是本次用户明确的「所有类似列表，含直播、文章、用户、动态」要求及 README/results/review；Standards 来源为 AGENTS.md、src/.editorconfig 和 code-review、WinUI review/UI testing、run-tests、code-testing、verification 技能。遵守用户覆盖 implement-spec/code-review 中委派流程的指令：独立审查，没有子代理、PR、issue 或父/来源工作区写入。

**结论：允许作为部分修复采用；不能作为所有同类列表的性能验收完成。** 图像生命周期和通知命令修复有直接证据；缓存减少确实降低长列表布局工作，但短 User 列表存在心跳退化样本，不能宣称性能全面改善。双 pane 的失败已在同源 baseline/candidate 都复现，不是仅凭「diagnostic」命名排除；根因仍未定位。

## Standards / WinUI

没有发现需要父代理立即修改的确定性生产正确性缺陷，也不建议为审查增加通用调度框架。以下是实际边界：

* **样式覆盖**：XML 枚举得到 40 个 AppRepeater 实例，0 个显式 Style、0 个内联 Style，唯一 AppRepeater 隐式样式为新增字典。原生 `StylePrecedence` 反例确认：candidate 隐式实例 cache=2，但 `Style=new Style(typeof(AppRepeater))` 的实例仍为20。当前调用点没有遮蔽；以后新增局部样式应 BasedOn 或显式附加该策略。不能声称隐式样式强制覆盖所有局部样式。
* **模板/name 合同**：核对实际 NuGet 包附带的 `Base/Repeater/AppRepeater.xaml`，它是构造时 InitializeComponent 的 UserControl，内部 AppItemsRepeater 名为 `Repeater`，缓存局部值20。策略写的是内部对象的局部值，因此能覆盖20。它没有修改模板或名字，没有每次 resize 遍历；Loaded 可再次应用。换包后若名字或结构改变会静默不生效，应重跑 AppliedCache；本候选未升级包。
* **生命周期**：LayoutControlBase 先注册的 Loaded/Unloaded 和派生注册的事件都会进入 override，`_active` 使第一次各只调用一次基类；首次 unload 基类解除自己的处理器，派生处理器继续覆盖以后周期。`base.OnApplyTemplate()` 此处重取现存 Root/ImageBrush，不是重新注册加载事件。包卸载把 surface 和 brush 引用置空，composition sequence 却仍当前；补建 surface 是必要修复。基类 BeginNewRequest/卸载取消、request ID/URI 检查仍拦截旧异步结果。
* **surface/alpha**：代码没有重新使用已 Dispose 的 bitmap/surface。包本身卸载丢弃 surface 引用，并不是显式 Dispose；不应把它描述成确定立即回收 GPU 内存。首次默认 Ignore、改变纵横比后 Premultiplied 和96 DPI保持原行为；helper 跟踪该分支用于 remount。Basic/Blur 的绘图、Offset、ShouldAppendOffsetHeight、URI/缓存选择没有更改。不得把未改动的全尺寸 CanvasBitmap 解码说成已缩略图解码。
* **已有边界**：Source=null 无 Holder 时包不会主动清空旧图、Dispose 后再挂载、模板在已加载期间被替换、device loss、跨显示器 DPI、透明图片所有组合、动画、原图下载/保存不在本次新验证范围。不能把它们算成候选新增缺陷；也没有给这些路径出具通过结论。新代码依赖包的 protected 属性及生命周期语义，不访问私有字段或反射；固定包可行，升级需要重验。
* **通知命令**：OneWay 使 recycled ViewModel 的 ActiveCommand 跟随当前项，范围小且有旧失败→新通过证据；没有因其它 once-bound 静态命令就进行无差别修改。

## Spec：实质阻断与覆盖缺口

1. **P1 — User 短列表的性能回退不能当作「仅原问题残留」。** 原始 User/50 最大 heartbeat 从84.79ms到267.47ms，Basic draw 从0到20；User/500 从1705.19ms降到275.59ms，但仍超原200ms线。原结果表只列500项会隐藏短列表的不同方向。resize p95改善不等于 heartbeat 尾部改善。缓存20→2使50项不再全保留，缩放触发新卡片创建/回收/图像绘制，是有代码和计数支持的嫌疑；draw不是 decode 次数，不能直接把267ms全归给解码。独立 User/50 对照结果见下方补充和 `audit.json/focusedUser50`。缓存策略作为全页面性能完成项仍不应签收。
2. **P1 — 用户的所有同类页面范围尚未验收。** 22场景是11代表模板×50/500；真实直播 RecommendLivePresenter、文章 PartitionArticlePresenter 已测，不是仅视频结果外推。其它直播 search/history/followed-horizontal，文章 search/history，综合动态富文本/图库/多类型，用户粉丝/关注真实分页及实际头像尺寸，历史/收藏/稍后看各 presenter，PGC timeline/favorite/search，播放器其它 episode/season，评论/回复、聊天/直接 ItemsRepeater、AI/小列表仍是清单/静态路径覆盖或未测。平台默认缓存没有被改动，不意味着它们达到性能目标。
3. **P2 — 双 pane 安全性仍待证，但暂无候选新增阻断证据。** 两次独立启动使用相同 User 数据、500项、相同 production template、400 DIP左列、两 pane Height=400、同一 UserLayout。baseline/candidate 都在挂载后的布局阶段发生 COM 0x80004005，尚未到独立滚动断言。源文件逐字差异仅 BasicCoverImage、BlurCoverImage、FocusMode（加载候选样式的开关）。这排除了「只有候选才失败」这个假设；没有定位到具体 fixture 错误，故不宣称 fixture 无效，不宣称 shared Layout 安全。原先 independent-layout 等多次失败仍保留。

## 证据审计与判读

`independent-audit.py` 逐数组复算 resize/heartbeat 的 max 与原 floor-index p95，也重算候选 scrollBeats；校验120次 resize、27个滚动检查点、realized范围和峰值。baseline 没有 scrollBeats，且旧 ScrollTo 立即采样；candidate 用匹配 correlation ID 的 ScrollCompleted 后100ms采样。旧空视口失败不能证明持久空白，baseline/candidate 滚动延迟不能作配对性能结论。候选检查点图像齐全也不能证明每帧无占位。

原 verifier 独立运行仍 exit=2，User/50与500仍失败；原 archive 1135个 manifest条目的大小/SHA256验证通过。原32秒退出和精确布局路径 WER证据有效；red-final 的 Microsoft.UI.Xaml.dll 0xc0000005 / PID 0x11AC WER仍保留。该次失败构建后混合/旧输出使其不能进入有效性能比较，但不能据此证明 AV 根因就是旧输出。

原 red-settled 没有逐阶段 sources目录，但42项 source-hashes都能在归档其它文件按SHA256找回；red-extra 42、green-verified 43、green-extra-navigation 43项直接核对通过。`probe-provenance.json` 中 head=90e215f0及旧files字典为继承数据，**不是最终候选编译/DLL认证**。阶段源哈希比它可靠；原归档不含阶段 DLL 哈希，不能补称当时每个加载二进制都逐字认证。本次新原生阶段保留自身源快照和布局 DLL 哈希，Richasy DLL 与恢复的0.0.12-preview4 NuGet DLL一致，nuspec指向公开源码commit `3f1086f6cfa8c1e0c3a64b930a8577dc5496c616`。没有反编译、dump或进程内存读取。

原红阶段早于绿阶段、互不重叠，顺序和 source 中相同稳定缓存等待/120步宽度轨迹/1.5 DPI可核对。没有跨阶段随机交错重复，原 build log缺绝对起止时间，进程采样也非全系统构建活动追踪，因而仅凭归档不能完全独立证明当时无外部构建重叠。本次 User/50采样期间没有启动构建；后续构建仅在已有close记录后进行。计时含resize结束后800ms settle，不能解释为单帧时长。

原 headless TRX验证98项是572项子集、全部 Passed；生产 Rebuild日志exit0、1个protobuf warning，均是**归档审计**，没有冒称本审查又跑了一次572测试或生产全构建。新原生host构建通过；第一次缺 SourceRoot 导致的构建失败保留，补齐本区props后才运行，未启动失败/混合输出。host warning不冒充生产warning；未新增抑制，未单独运行WinUI analyzer。

## 图像与 focused native

本区身份 `BiliCopilot.ListReview3301.Owned`，所有运行均精确PID/path、32秒退出观察、精确layout路径 Application WER、仅自身注册清理和yield。kernel初始异常的status/patch已保护；首次本地对象不存在，随后只从来源本地git库获取所需对象并恢复本区至gitlink `0ae5e4e`，没有写来源或父区。

| Requirement | Evidence |
| --- | --- |
| 隐式/显式样式优先级 | `StylePrecedence`，baseline20/20、candidate2/20 |
| 双pane是否候选新增 | `SameSourceTwoPane`未到完成断言；两端unhandled E_FAIL，均runner exit1，不洗成通过 |
| Basic多次unload/source/remount | 本区 `BasicRepeatedUnloadSourceReplacementAndRemount` PASS |
| Blur原语义/重复挂载 | 本区 `BlurRepeatedUnloadSourceReplacementAndRemount` PASS |
| 原图尺寸及圆头像 | 本区 `NonListOriginalSizeAndRoundedAvatarPreserved` PASS，范围仅fixture尺寸/渲染 |
| 缓存复用 | 本区 `ImageDiskCacheReusedAcrossRecycledInstances` PASS，虚拟HTTP适配，不是公网 |

独立查看原像素 Live/Article/User、Blur和本区owned窗口截图，并查看baseline卸载后空头像反例。对归档 Basic/Blur first/same 还解码PNG逐像素比较，不只比较PNG编码文件；replacement像素不同。蓝底黄色矩形的crop、圆头像、Blur渐变在这些样本成立。RenderTargetBitmap中白底/不可见文字不用于全窗口主题验收；它不完整捕获composition card背景。新owned窗口截图确认实际图像可见。不能从不透明fixture的同图哈希相等外推所有alpha、DPI和动画路径。

## 给父代理的最小下一步

**User/50独立复测补充**：同一已固定的Matrix源码，两边均用ScrollCompleted，只有Basic/Blur版本和是否加载cache样式不同。baseline heartbeat max **79.2673ms**、resize p95 **36.2996ms**、realized **50**、resize draw **0**；candidate为 **209.5865ms / 21.7360ms / 22 / 20**。candidate功能断言全通过；baseline只有 `RemountImagesPresent`失败，截图为空头像。它发生在计时之后，不能解释baseline计时区间没有decode为「图片本来已空」。这是第二对短列表尾部退化样本，与原84.79→267.47同方向；不构成统计置信区间或完整归因，但足以阻止「只是旧User问题」的结论。两个变更未拆开做消融，不能只凭这一对断言cache或surface中的某一行是唯一根因。

优先对 **User/50** 保持同一头像源与宽度轨迹，分离 cache变更和生命周期修复的作用；只增加实际 decode/draw开始结束及新realized卡片的轻量时间关联，或做一次无头像/小源头像对照。先判断全尺寸CanvasBitmap解码、卡片layout还是回收重挂载造成尾部停顿，再决定最小修补，暂不引入全局新框架。不可放宽200ms或把User排除。

图像生命周期与通知OneWay可作为功能修复采用；共享cache=2的长列表改善有据，但短列表性能风险需处理/明确接受后才可完成性能任务。双pane保留待证，补一个真正定位fixture或生产路径的最小反例；不能以换实验名结案。按上述漏项分批补代表性真实页面/数据边界；本审查完成不等于用户总体目标完成。

复现脚本、raw/审计结果和源快照见 `independent-evidence.zip`；解压到repo `artifacts`。不需要重跑全历史矩阵。
