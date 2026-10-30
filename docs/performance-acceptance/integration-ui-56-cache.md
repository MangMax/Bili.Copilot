# #56 离线缓存证据集成复核

2026-10-30。独立 merger/reviewer，未启动子代理。结论：有限离线实际控件、缓存与资源观测已取得，允许证据集成；真实账户/API、真实业务50页、对象根/独占bytes、预算和完整视觉仍未验。没有泄漏结论或具体生产预算。

## 集成与范围

父 `feat/performance-45` 起始 HEAD `6b9ebef52d2ec007112fd765648eea825c5c7214`，初始 `git status --short` 为空，merge-base 等于父 HEAD。以 `--no-ff` 合入 `a6a0037bf5d61782cc1419326acdafda75eece99`（t3-f57790f9），merge 为 `a471130a7b46bc3745e9cad0cf6962fcccf66f4a`。本报告提交 SHA 见交付消息。

已完整阅读 [issue-56](issue-56.md)、[离线实验](ui-56-cache-observation.md)、AGENTS/docs/agents 及 winui-code-review、verification-before-completion。审核采样、引用持有、绑定/可视树口径及适配边界；没有运行专用 WinUIAnalyzer，不宣称全应用代码或 UI 验收通过。无生产/测试/依赖修改，无 push、PR、issue 写操作，无桌面操作、应用启动、403 suite 或 build 重跑。

## 新执行的离线验证

```powershell
python docs/performance-acceptance/harness-56/analyze.py docs/performance-acceptance/evidence-56 run-4 run-5
python docs/performance-acceptance/harness-56/verify.py docs/performance-acceptance/evidence-56 run-4 run-5
```

analyze exit 0：两轮各83个scalar检查点、11阶段和30次B/C/A切换检查；重生成 summary/gc-events 与已提交文件无差异。请求数循环固定23，刷新后24；缓存Count及Capacity均为A450/B150/C60，身份660、业务Id640、当前A加缓存总引用槽1110。这些只证明固定刺激下集合、游标和服务请求稳定，不能推断进程内存趋稳。

**原 verify 在父 checkout exit 1，AssertionError: Adapters.cs，保留此事实。** 逐文件比对实际 build 与父 checkout：App.xaml.cs、App.xaml、Harness.csproj 原字节相同；Adapters.cs（58行）和Fixture.cs（39行）仅 LF→CRLF，规范化后完全相同。没有改写原文件/hash/verifier。随后仅在内存中将 verifier 的五个 harness 文本比较改成 CRLF→LF 比较并执行其余原检查，exit 0；源、DLL、依赖、trace 的 hash 检查保持原始字节精度。故不声称父 checkout 原版 verify 无条件通过。

[来源清单](evidence-56/linked-source-hashes.json)的152个文件在父 checkout 原字节SHA256全部匹配；该集合来自 csproj Compile链接及kernel Abstractions源枚举，不能理解成152个UI控件。实际XAML复制/模板提取另由production/fixture hashes核验。两轮build/layout/实际启动模块中的Harness.dll均为 `AC365C611DCE032594CD52701A409689656A34BC2BA47B11A41AEF31DC052CA4`。run-5加载依赖清单与原路径文件、layout一致；run-4只有三个实际模块加layout依赖，不能补称同等完整运行加载清单。校验依赖原工作区ignored产物仍在，不是跨机器自包含证据包。

使用已存在的 `t3-f57790f9/docs/performance-acceptance/harness-47-gc/Trace/bin/Release/net10.0/Trace.dll parse <原nettrace> <父artifacts/review56/run-N.json>` 离线重解析两份trace，没有collect或启动UI。新解析完整JSON与各轮raw-events-hash所指原解析相同（11239/11620事件）；原JSON和nettrace hash均匹配。GC/Start、Stop子集逐事件相同：663/679完整配对，零报告丢事件；sample内658/674与gen0差一致。原因计数与原报告一致，刷新后4/5次GC也由analyze重算。包含BackgroundGC，collection duration不是pause；没有从本次复核计算暂停预算。

## 采样口径与保留限制

[Snapshot/TrackOld](harness-56/App.xaml.cs)按ReferenceEqualityComparer去重当前Videos和cache中的VM，另按Identifier.Id去重业务Id。cacheSlots是List.Count之和，Capacity单列，不是对象数或堆字节；Distinct Id也不代替对象身份。局部HashSet/数组在采样期间暂时强持对象，方法返回后不保存它们。

realizedCards来自实际主栏VisualTreeHelper遍历，包含保留/回收元素；visibleCards只做自身Visibility、正尺寸和ScrollView矩形相交，含边缘卡片，不是像素遮挡检测；Collapsed强制可见0。两者都不是全堆存活数或ItemsRepeater有效index数。

WeakReference列表累计保留包装器，ConditionalWeakTable记录见过的卡片但不强持key；日志/JSON/快照及采样本身有分配成本。循环见过卡片增至2566，存活区间223–296/297与树固定223可以并存。旧450 VM在刷新和尾窗均223仍活/227不再活；这与树内223卡片数相等仅为线索，没有逐卡片VM身份映射和根路径，不能认定卡片是这些旧VM的根或证明泄漏。自然GC的代次、BackgroundGC及WinRT生命周期限制仍在，不能认为每个对象都经历了适当回收。managed/private/working set含宿主和观察器，不以相减估算native或独占bytes。

数据仍为Synthetic，每次服务创建真实kernel模型/生产VideoItemViewModel，重复Id为刻意输入。只用两种URI/同一小Logo，不能代表线上图片资源。组合根、设置和未调用播放/导航等为适配器；直接VM命令令侧栏高亮留Recommend，显式回顶部不验证隐式锚点；同页Collapsed不等于MainFrame导航。PR27头 `863a941aa98b164778f862935e0c632c4cecd829` 不是当前祖先（exit 1）；撤销、增量diff峰值、有效快照pin及分区排序均未测。不将原业务50页协议改为通过。

## 六张原图的独立复核与历史分歧

逐张打开原始 run-4/state-1、state-11 及 run-5/state-1、state-7、state-10、state-11 PNG，SHA256均与[原视觉记录](evidence-56/visual-review.json)一致；没有新截图或使用后续绿图覆盖。六图当前均可看到三行四列粉色Logo，第三行底部受viewport裁剪。因此历史“部分/全部封面缺失”不能作为已独立复证结论；原记录原样保留，此处追加分歧，不据此签完整视觉通过。

额外使用System.Drawing读取同图像素：12个封面区域左上角x=268/572/876/1180、y=160/472/784，宽282、高180并裁到图边界。筛选R>180、B>100、R-G>35、B-G>15，各区域均3850或3851个粉色像素。此检查只支持每个区域存在Logo颜色，不证明封面完整/精确渲染、头像、所有视觉状态或捕获可靠性。最初Python像素脚本因PIL未安装失败，未安装依赖，改用现有System.Drawing完成。结构sample的decodedImages非空是不同证据层，不能代替视觉判断。

## 最终校验与未验项

新文档、原报告追加补记及status账本 `git diff --check` 通过；相对文件链接逐一解析存在。全合并范围 `git diff --check 6b9ebef HEAD` 的历史例外准确为：

- `evidence-56/attempt-b/failure-capture-error.txt:5` trailing whitespace；`:6` new blank line at EOF。
- `evidence-56/run-1/failure-capture-error.txt:5` trailing whitespace；`:6` new blank line at EOF。

以上raw输出未重写，不声称全范围whitespace clean。正式 `src/tests/externals` 相对父基线无diff；所有变更路径限docs/performance-acceptance，正式依赖未变。kernel HEAD保持 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d` 且工作区clean。最终父SHA/clean由提交后新命令确认并在交付消息给出。

原[静态调查](issue-56.md)按历史范围保留；后续需要真实授权账户/API、业务长列表/50页、对象根与共享/独占retained bytes、代表性图片/native/GPU及长期稳定/峰值数据、产品预算、PR27集成和完整导航/视觉验收。本次不关闭#56，不实施缓存淘汰，不宣布泄漏或具体预算。
