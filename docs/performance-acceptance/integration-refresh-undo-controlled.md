# 受控刷新撤销独立集成复核

2026-10-31。父起点 `1b425c0dfd86ed333eb88e3504f45d3c52a6e82f` clean；指定候选 `687299d324f07e53fa466b5c5c7a95c68e41b6ca`，本地 `--no-ff` merge `7f7f6f2ec5aa17156c372e54e4364abc29d12b42`。

**本组合受控精确撤销及空/失败批次有限通过；完整应用 PARTIAL、总体性能 HOLD。** 真实公网空页仍未观察到，这是覆盖缺口，不是生产 FAIL。

## Standards / Spec

已读 AGENTS、docs/agents、code-review、winui-code-review、verification-before-completion，按用户要求自行完成两轴审核、不子代理。Standards 无实质阻断；Spec 十二项原始证据与限定结论相符。候选及合并 docs 外 diff 为空，未重跑487/build/UI，也未宣称新的全应用分析器审查。

## 独立核验方法及闭包

既有 verify.py 会覆写其所在目录，故将其与 ControlledRefresh.cs 复制到唯一临时目录，从来源工作树运行 `python <temporary>/verify.py run4`，exit0；原始证据不覆写。另直接解析 gzip JSONL 的493行，自行比较完整ID序列、逐项引用编号、快照、cursor、请求与CollectionChanged累计值，而非只读12个PASS标签。

现存冻结产物逐文件大小/SHA256和双向集合通过：901文件、363 DLL、134 NuGet包；部署manifest仅Identity.Name不同。tracked UI源仅App.xaml.cs、GlobalDependencies.cs、项目文件及两份manifest五个组合文件不同。真实VM/生成命令、展示项、头部/主体、AppRepeater、IncrementVerticalScrollControl及NuGet基类参与；外部接口由固定批次屏障提供，不能冒称公网。

131个历史加载模块对应现存文件hash通过；UI DLL `4bffc1e01b88d61251ebe52b3ac9e4bea57d25b03b42216b24b711b20cf9a3f9`。不是所有阶段模块追踪。父kernel实查 `0ae5e4e000a41a07f6696746706df11c06e8835b` clean/local-only。

## 十二项行为及生产契约

- Popular/Partition精确序列两项：初始31项，旧105出现两次；刷新输入101/900/900/901，实际为101/900/901加原列表去101，共33项，未变化对象逐项引用保持；Undo恢复全部31个原引用及顺序。cursor41→73→41，更新1→2→3，集合通知32→36→40。Popular请求1→3→3，Partition为1→2→2。
- 两页各空/失败刷新四项：终止响应只有一次，保留900加原序列、cursor73及原31项/41快照，更新2/集合通知33；手动恢复902加原序列，更新4/通知35。没有把终止后的PropertyChanged全量计入集合通知。
- 两页旧返回切栏两项：新700先返回，旧999后返回；最终仅700，无undo/refreshing残留。
- Popular旧自动分页一项：cursor41旧请求与刷新屏障交错，旧999不能写回；旧返回时仍31项/41且refreshing=true，刷新后900加原序列/cursor73。
- 空/失败自动分页两项：终止后[100]，请求2/更新1/集合通知2；静默窗口分别2021/2020ms，约四个500ms生产周期。手动请求仍从41出发，恢复[100,200]/42，请求3/更新2/通知3。这只证明有界窗口，不证明永不循环。
- 真实按钮一项：click刷新得到900加原序列，click撤销恢复31原引用/41；随后滚到底，第四个请求offset41追加[100,800]，33项/42，更新4/集合通知36。自动追加允许重复100，不是Undo失败。

**原报告需补充解读：最终33项快照之后，JSONL还有第5个offset42请求，未记录响应。** 请求4是快照时计数，不能解释为整轮最终请求总数。首次独立审查脚本把整轮请求误限为4而触发断言；读取末行后纠正此审查假设，精确序列/引用/游标复算通过。该后续请求符合生产触底续页契约，未改变前面的精确Undo结论，也不属于空/失败窗口。

源码交叉核对：Undo恢复snapshot.Offset并发VideoListUpdated；主体接收后DelayCheckItemsAsync，500ms后按当前触底几何请求下一页。Load只在数量增长后通知；空刷新及失败保留旧快照。Popular刷新入口替换旧请求代次，分区忙碌时拒绝刷新，两者语义不混同。初始化以命令完成、Loaded/几何、观测请求放行与dispatcher drain为屏障；20秒为失败上限，不以固定等待替代就绪。引用字典强持有对象，不能用于存活/收益推论。

## 输入、退出及失败历史

逐张查看run4三张原PNG，与15条动作及selector核对：真实click RefreshRecommendButton/UndoRefreshButton，截图显示100/101→900/100且有撤销→100/101且撤销消失。截图只证明可见部分，全Items由VM记录证明；无公网封面/DPI验收主张。

独立比对ready与observation的PID38552、路径及creation FILETIME；retained handle、非强杀exit0、managed unhandled0。以安全事件重算exact PID/path/creation及WER门禁PASS_BOUNDED，安全事件为空，59次查询全部成功。末次查询启动距退出30.5187332秒；lastQuery距termination实际30.536100秒，独立于collectionEnd；满足退出后至少30秒查询要求。未来精确异常仍可撤销结论。

run1未知退出码/强制清理BLOCKED；run2保存栏目污染导致runner场景失败；run3分区挂载自动请求未排空导致刷新被loading guard拒绝，11 PASS/1 FAIL。保留原始失败及旧源码，不作为生产修复或生产缺陷结论。run2/3退出通过不覆盖行为失败。最终build3原日志1567 warnings/0 errors保留，未重建、未添加NoWarn。

## 状态与下一最小步骤

owned清理与yield记录保留，released:false不能写成true。本轮只读取证据/源码及现存宿主文件，未操作桌面、Caa6a7be layout-v2、PID9608、Dev或认证数据；未启动应用、部署、push、PR或issue。

#49既有FAIL、AOT BLOCKED、托盘foreground未通过且根因未归因、#56预算/任意大重排限制和perf HOLD全部保留。建议下一最小步骤为托盘foreground定向诊断：固定前台竞争窗口，记录目标HWND/实际foreground及输入可用性，先区分Windows策略、测试工具与产品原因；本轮不启动诊断或新任务。

依据：[原报告](refresh-undo-controlled-acceptance.md)、[原始run4](refresh-undo-controlled/run4/controlled.jsonl.gz)、[退出观测](refresh-undo-controlled/run4/exit-observation.json)。
