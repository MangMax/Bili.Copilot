# 固定提交独立 review

审查对象：`4ca81f88a455616f4a8142e16c2af602e029dbb6`；父：
`6133ae590daf474d606e7b0afbdbf21e857b8ed1`。
读取了 t3-cca140f6 对应提交的 `scroll-continuity-results.md`、全部生产差异、
脚本及提交内证据包。按用户要求自行审查，不派生代理，不写实现者/父工作区。

## Standards

生产补丁未发现阻断问题。唯一生产差异是
`PartitionArticlePresenter.xaml:44` 的头像 Source 使用显式 compiled OneWay。
文章模板用 TemplateBinding 传递 ViewModel，presenter 继承
`LayoutUserControlBase<ArticleItemViewModel>`；已恢复的 0.0.12-preview4 包文档确认
ViewModel 为依赖属性。回收时更换 VM 可触发 OneWay 路径更新。
Avatar 在生产 VM 中为 init 属性，当前修复不依赖它自身发 PropertyChanged。
不引入 runtime Binding、反射属性查找或新的绑定 provider；生产 VM 仍为 partial，
保留 GeneratedBindableCustomProperty。没有修改卸载/重挂、虚拟化、图片生命周期。

未发现新增 AOT 结构性回退；但归档 host 是 Release JIT + adapter，不能证明发布 AOT
运行通过。本次没有构建、运行 WinUI analyzer 或执行 AOT 产品测试；不能作这些通过声明。
该限制不阻断这一行 compiled binding 修复，但禁止升级成完整 AOT 验收结论。

## Spec 与独立复放

允许合入范围：文章头像回收后 Source 陈旧的窄修复，以及明确保留失败的实验文档/证据。
不允许以“全部滚动留白解决”验收；文章内容/点击目标由另一任务处理，此补丁不覆盖。

执行 `python docs/list-resize/scroll-continuity-review-replay.py artifacts/independent-review`，
从固定提交 zip 导出到本审查工作区的忽略目录，只做离线复算：

* 校验归档 manifest 的 9392 个 payload 长度与 SHA256。
* 重跑旧 real-article05/2 和新 fixed-article05/2 的 check.py：每组 18 项；
  旧组恰为两项可见身份失败，新组全部通过（50/500）。
* 另按图像遍历顺序分别检查封面 `article-index.png`、头像 `article-avatar-index.png`，
  不只信任原 expectedUris 集合；新组必须无错，旧组必须存在错误。
* 对全部 9323 个外部像素样本从 RGB 网格重新算黑色比例，逐组复算 blankMs、blankMaxMs、
  样本数、realized、bindingBadFrames 和 resizeMax，与提交 summary 完全一致。

这足以支持“该 fixture 上陈旧 Source 已闭合”，没有用均一图片的旧性能结果代替新结果。
不能据此证明每个中间 compositor 帧的图片正确，也没有新跑 37 次 native。
原始完整录像未收入紧凑包，JPEG 是声明过的子集；RGB 网格可复算统计，
但不是完整录像独立重解码。缺失 Temp 历史没有恢复或核验。

## 剩余问题与下一步边界

整体连续滚动留白未完成：修复后 Article500 黑背景总估计 4929→8142ms、最长
698→1359ms，cache 2 回退已撤回；最终生产资源仍为 0.5。
User500、Season500 也保留明显失败。Return 已 warmup 轨迹两档都零整片背景，
只能说明部分反向工作量减少，不能证明修复了该轨迹的留白。

连续指标足以否定通用 cache 2，并支持下一次最小、单变量离线实验；不足以决定
直接改解码、纹理缓存或调度。黑色阈值、10px 网格、约 30fps、首末采样边界和
UI 阻塞期间回调缺失限制了精度与归因；单次配对也不支持稳定收益/显著性结论。
下一实验应保留唯一头像、生产 0.5 基线和外部 motion 像素检查，并先定位阻塞时间段，
不能只看几何 coverage。本审查不实施后续优化。

审查发现：Standards 无生产阻断项；Spec 无窄补丁阻断项，整体目标仍未完成。
全过程未操作桌面、用户实例、部署、清理、push、PR、issue 或父工作区。
