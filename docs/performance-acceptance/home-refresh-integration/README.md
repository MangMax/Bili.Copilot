# 首页/分区修复父级集成

2026-11-01，`feat/performance-45` / `t3-1d19cc80`；无子代理。

## 固定范围与合并

父起点 `8c797bfdd3854f00171b18cd8f121a210298ff1e`，kernel
`0ae5e4e000a41a07f6696746706df11c06e8835b`，开始均 clean。
生产 `cd915f2d81bab1a78bd165177a8851d477b12c78` 与审查
`b5b1b55b18625d311b8bd471231007d242c84999` 均直接基于父起点，彼此非祖先。
核查生产范围与 docs-only review 后，分别按固定 SHA `--no-ff` 合并，无冲突：

- 生产 merge：`219b95a810b23f3e8ae5b147888f6797415b31d1`。
- 审查 merge / fresh 验证 HEAD：`472db6c3e33bd8e5e13beb724628d452598ee11f`。

合后 src/tests 与固定生产候选完全一致。仅两个生产方法：
`SelectPartition` 解包两边领域对象比较，避免侧栏同项回显误作废首次响应；
`RefreshRecommendVideosAsync` 允许显式刷新接管分页，旧代次不得追加、覆写游标或清理新刷新状态。
新增五个测试用例解释全套 572→577；review 的两项额外边界测试仍保留在原审查归档。

Popular 外层 `RefreshVideoAsync` 原已推进 `_sectionVersion` 并清 `IsVideoLoading`，
分区原缺少这一接管；Popular 也不共用分区父选中回显链。本次没有修改 Popular，
不证明所有开关问题均已解决。开关在下一次刷新读取，不追溯改变在途请求。
WinUI 集成自审确认同项早退、真正换区隔离、刷新防重入及 finally 代次保护仍在；
未增加 XAML、订阅、集合替换或线程切换，未发现新增集成阻断。

## Fresh 验证

实际命令、时间、源码 hash、输出 hash、TRX 和 PDB 核对保存在 [evidence.zip](evidence.zip)。
本地大产物/binlog 保留在 `artifacts/home-refresh-integration/`。
依次执行 `python docs/performance-acceptance/home-refresh-integration/verify.py Tests`、
同脚本 `Release`、`RuntimeGate`，再执行同目录 `audit.py`。
使用已有 `C:/Program Files/dotnet` 的固定 RC3 SDK，`--no-restore`，不安装或升级依赖。

全套 Release：**Passed: 577, Failed: 0, Skipped: 0**。
解析 TRX counters 和每条结果，并对照历史 all-green 的577个测试名称。
headless 测试验证生产 VM 逻辑，不代称真实 UI/认证验收。

Release x64 JIT 全新输出目录 Rebuild **exit0、1个既有 protobuf unused-import
warning、0 errors**，耗时3分6.90秒；关闭签名/打包，PublishAot=false，保留既有分析设置。
测试 DLL/PDB 身份匹配，168份文档中128份可读取源码全匹配；应用 DLL/PDB 身份匹配，
2566份文档中1297份可读取源码全匹配，包含两个生产变更文件。
不可读取文档不宣称逐字节核对。所有记录的源码与输出 hash 在构建后重新核对通过。

Release `ValidateRuntimeDependencies` **exit0**；输出 libmpv、ffmpeg、BBDown
与本区既有输入逐字节 SHA256 一致，resources.pri 非空。这是资源文件门禁，
不是实际加载模块或媒体运行验收。`git diff --check` 通过，kernel HEAD/index/工作树 clean。

## 历史证据与边界

[原修复报告](../home-refresh-navigation/README.md) 与
[固定独立审查](../home-refresh-navigation-review/README.md) 原样保留。
本轮运行原 `validate.py` 只读校验归档 SHA、历史 red/green、native 标识与有限退出证据；
不是重新执行历史测试或 native/AOT 矩阵。历史 WinUI 24/24、AOT 27/27、
独立审查 related 37/37、red 0/3、边界 green 7/7 均按原轮次解读。

原 `SymbolIcon.UseSegoeMetrics` ILC 缺失 API 和既有分析器告警保留；
未新增或运行专用 WinUIAnalyzer，不修改 NoWarn。首页离线 host 未进入播放器，
不能外推完整认证启动、线上接口、播放器、全部样式/机器或全应用性能/AOT通过。
旧驱动未完成构建、早期回收池 oracle 误判、保护快照缺口及有界退出结论均保留。

正式依赖、gitlink、播放器/弹幕源码保持不变。未合其他分支，未操作独立 child 工作区，
无桌面、部署、安装、包升级、认证、UI、新用户包、push、PR 或 issue 操作。
最终提交仅增加集成报告/验证材料并更新 status；完整最终 SHA 在提交后交付，避免自引用。
