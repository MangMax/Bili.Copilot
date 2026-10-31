# Popular 模型接口生成：独立集成审查

2026-10-31。父分支 `feat/performance-45` 初始 `a3453b2fd5a6dba10dfb1c5d16a2004060e69318`，root 与 kernel 均 clean；kernel 为 `0ae5e4e000a41a07f6696746706df11c06e8835b`。固定候选 `2db290a15a128404466f7f40fc4187cce4064cf5`，证据工作区 `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-ee707477`。独立 Standards/Spec 审查均无实质阻断，以 `--no-ff` 合入 `ff339ca73279fa01776951410073205b3ef6660b`。

## 审查结论

生产差异严格只有 `PopularRankPartitionViewModel` / `PopularSectionItemViewModel` 两个顶层声明增加 partial；从 Git 对象去掉关键词后字节一致。初始化、公开成员、init-only 展示属性、相等性/哈希、通知和集合身份保持。没有扩大到其他模型，kernel/gitlink、正式依赖和 NoWarn 均不变。

实际 ABI 路径为 `Sections` → `ItemsView.ItemsSource` → 模板 `ContentControl.Content`；`SelectedSection` 也通过 OneWay 写入 header Content。原生 SelectedItem 取回实例后执行命令，恢复通过 IndexOf/Select；模板依据真实类型，编译绑定读取展示成员。接口继承 INotifyPropertyChanged，生成暴露表具有真实跨 ABI 用途。共享 ChapterItemViewModelWinRTTypeDetails 是生成器去重表，其内容是 WinUI INotifyPropertyChanged 的 IID/vtable，并非业务类型混绑。

## 本轮新执行与历史复核

- 独立读取原始 red/green build.log，与 committed gzip 逐条对照：357 → 355，仅移除目标两条 `(11,21)` CsWinRT1028；303 MVVMTK0045、46 MVVMTK0046、6 CsWinRT1028 全文原样保留，零新增。两次 strict 历史 exit1 仍为 red；本轮未重跑 strict/ILC/publish。
- 46 份历史大产物的长度和 SHA256 全部匹配，包括原始日志、binlog、TRX、最终 DLL/PDB、异常日志。原前后各30和全套535 TRX 均逐结果 Passed。
- 本轮重新执行只读 Metadata 工具，读取候选最终 Debug/Release DLL/PDB：PE/PDB identity 匹配，各2589个源文档全匹配，包括两个模型和生成声明。两个 attribute 指向存在的生成详情类，方法体分别52/47字节；保存的生成片段与实际 PDB 来源相同。另用独立 Python 复算每份源文档散列。
- 本轮重新执行现有隔离 Models DLL 的 Debug/Release probe：构造展示值、Data 引用、区块相等性、读取无通知和非零 WinUI INotifyPropertyChanged vtable 均通过。该模型工程链接真实源文件/接口/枚举及真实依赖；不加载应用模块。没有 COM QueryInterface、真实 WinUI 控件或 Native AOT UI 验收。
- 合并后父级新执行 `dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger "trx;LogFileName=all.trx" --results-directory artifacts/popular-aot-integration/tests`，exit0，**Passed: 535, Failed: 0, Skipped: 0**。535名称与历史完全一致。页面测试仍使用原 headless model adapter，不能代替真实模型 probe 或实机绑定。
- 应用 Debug 0 warnings/0 errors、Release 1既有 protobuf warning/0 errors 是本轮 hash 复核的候选历史构建，**不是父级 fresh app build**。未无理由重复昂贵 ILC。

本轮 Python 首次复算遇到 Windows 超长路径，随后路径前缀修正有一次字符串转义 SyntaxError；修正验证器后全量通过。均非产品失败。候选应用 probe 的 `REGDB_E_CLASSNOTREG` 初始化失败、原模型缺少 generated attribute 的 red 及其他准备失败日志保留，不被最后 probe 通过覆盖。

## 边界与交接

完整 AOT/性能继续 HOLD，真实 AOT 绑定仍待验；剩余诊断、kernel unsafe 审计前提和正式依赖缺口保持。专用 WinUIAnalyzer 未运行。本轮无 UI/部署/登录/安装/UAC/push/PR/issue，无子代理；#49 CoreOX 独占桌面，未操作其环境。

原报告：[aot-popular-models.md](aot-popular-models.md)。本轮结果及产物哈希：[verification.json](evidence-popular-aot-integration/verification.json)。本轮 TRX 压缩副本和测试日志随该目录提交；大 PDB 复读 JSON 留在父 ignored `artifacts/popular-aot-integration/`，原候选大产物保留原路径。
