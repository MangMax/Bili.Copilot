# 固定候选 711660de 独立审查

审查范围：`90e215f0d303c7ae6487f1e1cbf261f677b3d1ef..711660de06a9749d871c6a4fbd682bb8ed01f496`，含 `1797c94a`、前次 review `f81fa979`。来源工作区只读。遵守 AGENTS、code-review 的 Standards/Spec 两轴、WinUI code-review/UI-testing、run-tests、code-testing 与 verification 技能；按用户要求不启动子代理。

**结论：阻断 711660de 整体合入。** cache2→0.5 的 resize 停顿改善有真实证据，原 200ms 门槛未放宽；但独立快速滚动发现持续空用户卡，原图像检查会漏报。不能以 resize 全绿替代滚动正确性。Basic/Blur 重挂与通知 OneWay 未发现新的独立阻断，可保留/拆分采用；本报告没有修改生产实现。

## Spec：阻断项

### P1 — 缩小缓存后快速往返留下未绑定用户卡，图像验收排除了这些卡

变更定位：`src/Desktop/BiliCopilot.UI/Styles/Repeater.xaml:11` 的共享 cache0.5；漏检定位：`docs/list-resize/Matrix.cs` 的 `pendingVisibleImages` 和 `ScrolledImagesPresent`（都先筛 `Source != null`），以及只检查存在 command 的卡片集合。候选复用的是生产 User 搜索模板和 UserCardControl。

独立 owned 原生补验：本地固定 96×96 JPEG、50/500 项、1.5 DPI、同一 1400×900 物理窗口。缓存和初始图像稳定后，执行 12 次向下及 12 次反向的 1/3 视口步进，再做 6 次底部/顶部跳转。每次等待匹配 correlation ID 的 ScrollCompleted，之后仅等待 16ms；结束后另等100ms及1秒。这里的16ms是请求的等待量，实际调度间隔见原数组，不称逐帧采样。

* `rapid-half50`、`rapid-half500` 的原生窗口截图均显示头像、姓名、描述、ID一起空白的用户卡，心形按钮和背景仍在。原 `pending=0` 不能排除这种状态。
* 加强后的 `binding-half50`：在100ms和额外1秒检查，12张与视口相交的卡中均有3张 `ViewModel=null`，其 BasicCoverImage 为 `Source=null`、`IsLoaded=true`、无图像。截图有2张完整可见空卡，另1张计入底部部分可见范围。不是仅凭 RenderTargetBitmap 判断，owned 窗口截图也确认内容缺失。
* 同一 clean build、同一生命周期修复、同一模板/头像/轨迹，新进程 `binding-cache20` 只把内部 cache 改回20：12张卡全部有 ViewModel，100ms/1秒均0张空卡。整个50项保持 realized，无新增绘制。该对照隔离缓存影响，不冒称它就是未经修改的父版本。
* `check-native.py` 的 `EveryVisibleUserCardBoundAfterRapidScroll` 返回 **exit=2 / FAIL**，candidate=3、control=0。两次进程 runner exit=0只说明执行和清理成功，不能覆盖功能红。

数据定位到 **回收后的绑定/数据恢复**，而非单纯等待头像解码。`GetElementIndex` 对采样的内层 UserCardControl 返回-1，不能拿它当外层 repeater 数据索引；报告只使用有效的可见范围、ViewModel/Source状态与截图。补验使用本地 stub 用户数据，不等于真实认证页面；尚未定位具体包内部/控件回调根因，不将某一行声明为唯一原因。早期 cache2 对照截图同样有空卡，因此这是整个减少缓存候选尚未闭合的功能风险，不能说0.5独有，也不能用“cache2已有”豁免本候选。

最小下一步：保持当前局部结构，确认 UserCardControl 在回收/重挂后的 ViewModel 与当前外层数据项一致，连同头像Source、文字和主命令一起恢复。将断言改为检查每个可见预期用户卡（包括null ViewModel/Source），不要只检查非空图片或非空命令子集。用这条快速往返小复现做红→绿，并补一个真实唯一用户ID/不同头像的回收样本以排除stub特性；不需要新增全局图片缓存、调度器或重跑历史全矩阵。

### 重复加载与预取成本

快速补验的 User50：cache20新增draw=0；cache2=74；cache0.5首次独立运行=114，加强记录运行=116。User500 cache0.5=116。计数覆盖30次滚动及最后settle，不能与原27次慢检查点的计数直接比较，也不能把不同进程的细小计数差异视为统计结论。缩小缓存确实增加相对全保留短列表的重新绘制；包路径每次FetchImage返回CanvasBitmap后才Draw，但draw不是精确decode提交/取消计数，更不是HTTP请求数。

cache0.5 User50曾在一个 ScrollCompleted 当下有8张非空Source头像未就绪，16ms请求等待之后恢复；这些瞬时图像等待和上面的null ViewModel持续空卡是不同问题。原 `ImageDiskCacheReusedAcrossRecycledInstances` 证明虚拟HTTP适配的同URL第二实例不重复下载，不证明所有真实头像/网络条件下不重复下载。最终格式批次与本次快速测试使用本地文件，不用于公网请求成本结论。

## resize 证据：认可改善，不夸大闭合范围

从原始数组独立复算max及原floor-index p95，确认120宽度步、27匹配滚动检查点、最终cache0.5。归档PNG结果：

| 阶段 | User50 max heartbeat ms | User500 max heartbeat ms | 初始realized 50/500 | resize draw 50/500 |
| --- | ---: | ---: | --- | --- |
| baseline cache20 | 75.54 | 1395.43 | 50/103 | 0/79 |
| reviewed cache2 | 170.62 | 222.16 | 22/22 | 20/20 |
| clean half-user-png cache0.5 | 136.23 | 123.62 | 13/13 | 8/8 |

因此在固定轨迹/机器的记录中，0.5通过了原User50/500停顿线并降低创建峰值；短User仍慢于全保留20，不能称“完全没有回退”。此前84.79→267.47和独立79.27→209.59、cache1 WebP 202.66/206.20等红样本均保留。最终JPEG/WebP/AVIF批次各50/500均低于200ms，不能反向抹去历史红。

最终22代表模板场景应取 `half-main` 的14个非User、`half-extra` 的6个、`half-user-png` 的2个。`half-main` 编辑发生在编译中，其captured P1Formats源码不能证明已编译头像输入；查看截图确为JPEG，只作JPEG补充。`build-half-final` clean Rebuild exit0与随后PNG截图/来源分开核对，不拿half-main冒充同PNG对照。未发现升高阈值、排除User的最终数字处理。

计时包含resize后800ms settle，16ms dispatcher heartbeat不是composition帧时长；200ms门槛通过不等于60fps。背景用户进程仍在，非隔离机器、非随机交错/置信区间。本审查新增原生测试集中于滚动缺口，没有重跑全部历史或重复572项测试。

## Standards / WinUI 与保留语义

生产差异共7文件；`f81fa979..711660de` 的生产差异仅隐式cache从2到0.5及注释。没有新增decoder、源格式切换、依赖升级或全局调度框架。独立XML枚举40个AppRepeater使用，当前没有显式/内联Style遮蔽；未来显式Style仍需BasedOn或显式策略，不能说隐式样式强制覆盖一切。策略在Loaded写包内部名为Repeater的对象；保留固定包结构依赖，不修改共享layout。

核对 ReloadableCoverImage 的活动防重入、再次挂载时重取brush/补surface，以及固定公开包源的request ID/URI/取消检查：未发现新确定性缺陷。Basic与Blur的裁剪/Offset/ShouldAppendOffsetHeight和96 DPI保持原语义，默认Ignore及调整纵横比后的Premultiplied分支得到保留。包卸载置空surface引用不等于立即释放所有GPU内存；未将全尺寸解码伪称缩略图解码。通知ActiveCommand的OneWay保留，未无差别改其它绑定。

| Requirement | Evidence |
| --- | --- |
| Basic/Blur多次重挂与换源 | 归档 `BasicRepeatedUnloadSourceReplacementAndRemount`、`BlurRepeatedUnloadSourceReplacementAndRemount`，另用System.Drawing独立比较解码像素：同源相同、替换不同 |
| 非列表原尺寸/圆角 | `NonListOriginalSizeAndRoundedAvatarPreserved`；检查1200×675原图截图，无此前底部裁切；不外推所有alpha/DPI/device-loss |
| 精确undo | `UndoExactReferenceSequence`、`UndoExactViewportAnchor`；实际Popular 70/220/520项保持count及0集合更新，cache=[0.5] |
| 推荐封面尺寸/取消 | `FinalPhysicalPixelDecode`、`PhysicalCoverageWithoutUpsampling`、`SourceClearedDuringResizeRejectsOldResult`、`UnloadCancelsPendingResize`、`RemountLoadsCurrentSize` |
| 快速用户列表每卡绑定 | 新 `EveryVisibleUserCardBoundAfterRapidScroll` **FAIL**；原非空Source筛选不是充分验收 |

前四行是独立审计归档，不冒称本审查又执行了一轮这些原生场景。WinUI analyzer未单独执行；独立host clean build的161条warning不能混同归档生产Rebuild的一条protobuf warning。未出具AOT发布验证结论。

## 双 pane 与整体集成边界

原同源baseline/candidate双pane均未通过挂载阶段的COM E_FAIL，未到独立滚动断言；本次未扩修。它不构成“仅候选新增”的证据，也没有证明fixture错误或共享layout安全。共享资源可在两个同时打开页面中使用，因此全页面/多pane集成仍不能签收。

本次初始 `rapid` 多场景单进程批次在完成cache20/cache2 User50后也发生未处理E_FAIL，runner进程exit1；保留partial结果、截图和无匹配WER的32秒观察。它发生在0.5结果产生之前，不能当0.5性能样本，不确定与原双pane是否同因。随后新进程的cache0.5及绑定对照完成，不抹去这一失败。最小后续仍为固定template/尺寸/数据和生命周期，一次只变共享/独立layout或单/双pane，记录异常发生阶段；在明确根因前不得宣布多pane安全。本片阻断主要由可复现空卡支持，不靠猜测E_FAIL归因。

11代表模板×50/500涵盖视频/直播/文章/用户/动态/季番/通知等代表路径，不能等同全部真实页面、粉丝/关注分页、富文本动态、历史/收藏及直接ItemsRepeater完成。AVIF只证明本机现有CanvasBitmap路径能读固定96×96样本；没有生产格式切换，也不外推所有Windows安装。

## 原始证据与执行边界

* 固定提交中的P1归档1922个条目、前次独立归档383个条目的长度/SHA256全部校验。max/p95复算、最终source snapshot SHA、退出/清理记录独立核验。manifest证明归档一致性，不等于运行时DLL来源全认证；早期retained-layout hashes是事后hash，不补称启动时hash。
* 归档目标TRX **Passed 113, Failed 0, Skipped 0**，全量 **Passed 572, Failed 0, Skipped 0**；逐测试名集合确认113是572子集。生产首次build缺libmpv exit1仍在；准备既有runtime资产后retry exit0，不是测试失败被删除。build/test有时间记录的区间与owned运行不重叠，早期缺绝对时间记录的构建不能据此证明全系统无构建。
* 本次在 `artifacts/list-p1-review/source` 从固定git对象展开只读构建输入，kernel来自来源本地git对象 `0ae5e4e`；没有写父/来源工作区。审查区原kernel HEAD=`f02c33f6`且大量index删除已记录并保留patch，没有恢复/覆盖这个异常区。
* 新身份仅 `BiliCopilot.ListReviewD50f.Owned`。每次保存精确PID/layout路径、源码和启动二进制hash、退出码、至少32秒退出观察、精确layout Application WER、owned包注销数量0及yield。没有操作AotDev、Caa6a7be/Dev/官方包、认证数据或现存用户实例，没有更新试用包。所有本次build区间与本次owned运行区间独立核对无重叠。
* 初次补验host编译因缺using失败并保留，修正后clean Rebuild才运行。像素脚本首次缺PIL、结果汇总首次缺异常进程的runner退出文件也保留；未升级依赖。前者改用系统System.Drawing；后者用工具会话实际exit1的明确外部收据，未伪造runner生成文件。最后功能检查exit2是预期揭示空卡的红结果。

交付时复核父 `feat/performance-45` 仍为 `90e215f0d303c7ae6487f1e1cbf261f677b3d1ef`。本次只提交本报告与 [p1-independent-evidence.zip](p1-independent-evidence.zip)，没有生产修改、push、PR或issue操作。

证据包解压到repo的 `artifacts`。`list-p1-review/native-summary.json`、`binding-half50/owned.png`、`binding-cache20/owned.png` 为首要复核入口；所有阶段原数组和source snapshots保留。复现采用每阶段的固定sources与附带prepare/run脚本，初始化时从候选归档取host；初始及最终FastReview版本均在对应sources中，不能将初始prepare后的六场景批次误当最后单场景绑定检查。父代理应先修复最小可见卡片绑定反例，再重新审查cache策略合入，而非将此次审查标为性能任务完成。
