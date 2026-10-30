# #56 已登录完整应用观测：独立集成复核

2026-10-30，父工作区 `feat/performance-45`。结论：**有限观测证据复核通过，#56 未完成、未关闭。** 本轮只读既有证据，不签真实业务50页、缓存预算、泄漏、长期稳定或优化收益。

## 合入范围

起点 `c49f738bdea215c5584ddebefbdc017d34d7b0bc`，工作区 clean。仅有一个待合入提交 `5ec26d431a0e0a66f61783d91f6e850d8378b6e9`，以 `git merge --no-ff` 合入，merge 为 `f83bf9cde40a055380023c4bb7b44ef3d067f223`，两个父提交依次为上述起点和指定提交。随后仅修改文档归因、status 与本复核报告。

已读 AGENTS、docs/agents、winui-code-review、verification-before-completion、[静态调查](issue-56.md)、[既有离线集成](integration-ui-56-cache.md)、[本轮报告](ui-56-authenticated-observation.md)及其证据。独立执行，不启动子代理。原报告“按用户指令自行工作”已改为“按父级任务约束自行工作”，不将父任务约束冒充原用户指令。

没有启动应用、诊断采集或桌面操作，没有读取凭据、账号状态文件或原始业务日志；未改包注册、layout 或 app data。没有 push、PR 或 issue 操作。正式源码、测试、依赖及 kernel 无变更；未重跑403/build，也未运行 WinUIAnalyzer，不声称全应用代码或运行验收通过。

## 新执行的只读复核

- Python 显式 UTF-8 `ast.parse` 通过。通过 `runpy.run_path` 执行 [analyze.py](auth56-observer/analyze.py)，用 `unittest.mock.patch` 拦截 `Path.write_text` 到内存；完整 summary JSON 与 [原摘要](evidence-56-authenticated/summary.json)相同，生成 HTML 与原 memory.html 全文相同，没有改写证据。
- 独立检查 [samples](evidence-56-authenticated/samples.jsonl)共53行、标签唯一、目标PID一致；actualItems、realizedCards、businessBatches 均保留 null。十轮30点按 B/C/A 顺序出现且时间严格递增；每栏10点的序列散列和可见文本散列均与首次回访相同。UIA数 A/B/C 为181/28/88。这些是记录一致性，不是缓存命中或网络静默证明。
- 采样时间为14:34:38.6281954至14:52:06.5070933（UTC+08）。首6行确无 viewport 字段，未回填。早期 browse-6 位于 browse-3/4 之间，重叠批次原样保留；不按标签推导请求批次。[actions](evidence-56-authenticated/actions.jsonl)46条均记录exit 0，但动作文件不覆盖最早滚动，也不独立证明所有动作串行完成。
- [两份PowerShell脚本](auth56-observer/ui-observe.ps1)均经 `System.Management.Automation.Language.Parser.ParseFile` 解析，零语法错误；未执行 UI 脚本或 [日志读取脚本](auth56-observer/list-error-summary.ps1)。后者固定包/日期与三类错误，ReadToEnd 后内存分块，不能称有界流式读取。已有 [错误摘要](evidence-56-authenticated/list-errors.json) count=0，仅复核记录与分类口径；未重读日志核实计数。
- 审阅 HTTP 观察器源码：精确EXE/PID guard、端点白名单、输出类别/时间/ActivityID/状态；不保存URL/query/headers/body或原始trace。原三次无事件试验仍为失败采数，不把普通编译成功当运行功能通过。C#仅静态审阅及来源hash核对，本轮未编译。

## 来源与保留状态

[历史加载记录](evidence-56-authenticated/modules.json)4个模块与现存原路径文件的SHA256全部一致。按[新部署清单](evidence-full-app-duration-fix/deployment-hashes.json)复核新layout六项文件，按[旧保留清单](evidence-48-duration-fix/retained-binary-before.json)复核旧layout六项文件，全部匹配。未重新观察运行进程模块，也未查询或更改实时包注册。

当前部署来源的既有证据为 `c3b32c527cf7f80f2c0aaea81e92a822480efb56`，新layout为 `t3-1630ecee/artifacts/full-app-duration-fix/layout`。该工作区及父工作区的src/tests/externals相对c3b32c5均无diff；[构建来源](evidence-full-app-duration-fix/provenance.json)的build/layout UI DLL hash与本次重算均为 `BE5A365A577756B29B7E3EDDF3C4EF3A7F1D12350DD92269522C1E19EF265D68`。源码到二进制关联依赖历史构建provenance，本次没有重现构建。旧layout仍在 `t3-caa6a7be/artifacts/full-app-acceptance/layout`。

实际新layout runtimeconfig hash与[provenance](evidence-56-authenticated/provenance.json)一致，EventSource.IsSupported及StartupHookProvider.IsSupported均为false。因此HTTP事件无法提供页数，零事件/零lost不代表零请求、缓存命中或全请求成功。[verification](evidence-56-authenticated/verification.json)所列原观察器两份源码及Observer.dll的3个hash也全部匹配。

[私人截图清单](evidence-56-authenticated/visual-review.json)10项原PNG重新hash均匹配；本次没有打开图片、输出私人内容或提交图片。原逐张视觉判断由采集任务负责，本次不追加“独立视觉通过”。登录状态同样仅引用原观察记录，未读取认证文件或重新鉴权；本次不触碰登录数据。[cleanup](evidence-56-authenticated/cleanup.json)保留历史exitCode=0、ownedRemaining=0、observerRemaining=0、yield released=false，不改写为true或当前实时进程检查。

## 口径、问题与未验项

UIA RootCard只是限定可访问树代理；viewport交集含边缘卡片，不验证遮挡/像素。文本hash不是业务Id。不能代表Videos.Count、VM缓存槽/Capacity、全可视树、隐藏或回收元素、堆存活数。private/working set是完整进程瞬时样本，不能相减推导native、独占缓存bytes或真实峰值；固定等待3–8秒和10秒尾窗没有网络/布局稳态oracle。

导航前后的序列/可见文本/首卡Y代理一致、刷新后变化及尾窗保持已由原分析复算支持；不等于稳定业务Id锚点、旧对象释放或缓存根关系。PopularPageViewModel的ToList保存、Clear/Add恢复及刷新移除缓存/重置offset静态路径与报告相符，但UIA不能证明该路径全部副作用。

本轮未发现新的可确认生产缺陷，不扩修生产。已修正文档归因。既有静态调查的分区缓存归属、迟到结果风险仍留待独立复现；此次未测子栏目/排序，不能撤销这些历史Warning。

真实50页、请求数/成功完整性、全部VM缓存/存活对象/根与retained bytes、具体条目/MB/TTL预算、冷/热各三轮、长期稳定、GPU/native归因、性能收益、PR27撤销/快照/diff峰值、完整导航/视觉矩阵及Release/AOT均未验。三类固定错误为0不等于全请求成功、无限流或全错误覆盖。原离线证据及视觉分歧保留，与本次认证观测分账。

## 提交门禁

本轮修改和全部合并范围 `git diff --check c49f738 HEAD` 通过；本报告、status与原报告的相对文件链接核验存在。全部新增/修改路径限 `docs/performance-acceptance/`，正式源码/依赖/kernel无diff。kernel HEAD保持 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d` 且clean。最终提交SHA及提交后clean由交付消息记录，避免自引用。
