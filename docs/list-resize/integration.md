# 共享列表优化父级集成

2026-11-01，在 `feat/performance-45` / `t3-1d19cc80` 执行；无子代理。

起点 `90e215f0d303c7ae6487f1e1cbf261f677b3d1ef`，kernel
`0ae5e4e000a41a07f6696746706df11c06e8835b`，开始均 clean。
固定生产候选 `cf85c597590d0050302e07c29e80c18c55803b73` 的祖先含父起点；
固定审查 `5d0e419c3965516710413428628ad0fba0dd6fab` 的直接父是父起点，
仅新增 mount-independent-review.md / mount-independent-evidence.zip。
二者并非祖先关系，因此分别用固定完整 SHA `--no-ff` 合并，无冲突：

- 生产 merge：`fe439b28aee96c8f916657d49bec8da7587c3cb2`。
- 审查 merge / fresh 验证 HEAD：`71bca62a70656c6cdd223d99d9b801554b7b1851`。
- 合后生产树：`ae1755a004a1924c336d33c40e5804f3f902c84c`，与固定候选完全一致。

没有合不相关分支。后续交付提交仅本报告、验证脚本/证据和 status；最终完整 SHA
在提交后写入本地 `artifacts/list-integration/final-receipt.json`，并在交付回复列出，
避免把验证 HEAD 冒充最终文档提交或在提交内制造自引用 SHA。

## 准入范围

采用 [固定独立审查](mount-independent-review.md) 的整体 cache0.5 准入。
父基线到候选生产仅 37 文件；33 处 pathless 整对象交接（32 ViewModel + 1 Content）、
Basic/Blur 的幂等重挂及 surface 恢复、通知 ActiveCommand OneWay、共享 cache 策略均保留。
推荐 WIC semaphore 仍为 2。正式依赖、工程/警告配置、测试树、kernel gitlink、
双引擎、seek/speed 和播放器既有修复均未改变。

WinUI 集成自审复核模板根对象交接、Season 的现有 typed ViewModel DP/命令更新、
TemplateBinding、图片 Loaded/Unloaded 幂等及 cache Loaded 接线；未发现新增集成阻断。
没有添加 analyzer 或宣称专用 WinUIAnalyzer 单独验收。外层 Binding 的类型检查减弱与
真实 AOT 验证缺口继续保留，不把 pathless Binding 等同于新增反射属性路径。

## Fresh 与历史证据

fresh 命令记录及输出见 [integration-evidence.zip](integration-evidence.zip)，
本地大产物和 binlog 保留在 `artifacts/list-integration/`。
串行执行 `python docs/list-resize/integration-verify.py Tests|Debug|Release|RuntimeGate`
（各参数分别调用）；末尾 `python docs/list-resize/integration-audit.py` 核对范围、
源码/输出 hash、PDB 结果及归档 manifest。不触碰桌面，不发布或激活应用。

完整 Release TRX：**Passed: 572, Failed: 0, Skipped: 0**；解析 counters 与572条结果，
与历史 p1-evidence.zip 的572个测试名称集合完全一致。测试 DLL/PDB 身份匹配，151份 PDB 文档中122份可读取源码均匹配；
其余29份未落盘文档不冒称逐字节校验。测试实际使用此前本地隔离 RC3 SDK。

Debug / Release x64 JIT 在各自全新输出目录执行完整 Rebuild，均 **exit0、1个既有
protobuf unused-import warning、0 errors**。禁用签名/打包，PublishAot=false，
没有修改 NoWarn 或现有分析设置。两份应用 DLL/PDB 身份均匹配，各2566文档中1297份
可读取文档全部匹配；37个生产变更文件中36个直接在PDB中匹配，新增 Repeater.xaml
资源字典不在PDB，以本轮源输入hash及独立构建资源产物记录，不冒称PDB覆盖。

ValidateRuntimeDependencies **exit0**；两配置实际输出的 libmpv、ffmpeg、BBDown
逐字节 SHA256 均与本区已有输入匹配，resources.pri 均非空。无需补充本地资产。
这是资源文件门禁，不是实际加载模块、媒体运行或AOT验收。构建日志记录实际输出路径，
来源/输出hash及PDB核验均在下一构建前保存；集成审计重新核验记录中的全部输出hash。
`git diff --check` 通过；kernel HEAD/index/工作树保持预期 clean。

原始失败保留：首次测试命令 `-t:Rebuild` 被 dotnet test 识别为 list-tests 参数，
MSB1008、exit1，未执行测试；去掉该参数后全套 fresh 运行通过。首次 Debug
`--no-restore` Rebuild 使用旧隔离 SDK，6个 kernel 引用报 NETSDK1127：该 SDK 只有
.NET11 pack，而既有 assets 来自有 .NET8 pack 的机器 SDK。随后改用已安装的
`C:/Program Files/dotnet` 同版本 `11.0.100-rc.3.26522.148` 重跑；未安装、联网还原或
升级依赖。失败目录完整保留，不以增量成功改称首次 clean Rebuild 成功。

原生红绿矩阵、像素、双 pane 独立性、退出/WER、历史构建均来自已审归档，
本轮不重复 native 矩阵，也不把其结果标为本轮 fresh。关闭的阻断是回收空卡和
Season/双 pane 挂载适用性；原失败和审查链仍保留。

User50 短列表 cache 降低的创建/重绘与 resize 成本、有限页面/Season样式覆盖、
嵌套 Player/PgcSeasonsSection、认证分页、真实 AOT、所有机器/DPI覆盖继续保留。
heartbeat 不是 composition 帧时，不能签60fps或全应用性能通过。

下一步可基于新集成制作并复验 AOT 开发包；本任务不安装、不运行用户试用包，
不操作认证、桌面、push、PR、issue 或部署。
