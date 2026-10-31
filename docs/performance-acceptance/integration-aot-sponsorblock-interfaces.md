# SponsorBlock 接口生成：独立集成审查

2026-10-31。父 `feat/performance-45` 初始 `7fcb66a49d0fb43509e96d519e1466c2d5e97731`，root/kernel clean；候选固定 `af6d354b4c7675875b2483ab9f5ac30322b2338c`，证据工作区 `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-4b0da1fe`。Standards/Spec 独立审查均无阻断；以 `--no-ff` 合入 `6c3fdf46952fd1033e5f2b9522d3782c8fcc137d`，无冲突。

## 审查结论

生产差异只有 SponsorBlockService 与 SponsorBlockCategorySettingViewModel 两个顶层声明增加 partial。去掉关键词、归一化换行后与父源码逐字相同；公开成员、默认值、通知、设置持久化、跳过/静音/提示、缓存、取消及 Dispose 代码均不变。没有依赖/gitlink/NoWarn 或 warning level 变化。

设置模型经 ObservableCollection → ItemsControl.ItemsSource → typed DataTemplate，并通过 ComboBox.Tag 与按钮 DataContext 跨 ABI。行为选择仍由 Loaded/SelectionChanged 和 Begin/EndBindingRefresh、ApplySelection 处理，不是 TwoWay XAML。服务只有托管 DI 构造和播放器 GetSegmentsAsync 消费；没有服务实例跨 WinRT ABI 的生产路径证据。服务的 IDisposable/IClosable 生成表满足严格生成要求，不能据此称发现或修复已知运行故障。应用设置模型共用 ChapterItemViewModelWinRTTypeDetails 是 INotifyPropertyChanged 接口表去重。

## 历史证据的独立复核

- 68 份原始产物长度及 SHA256 全匹配，包括 build/binlog/TRX、最终应用 DLL/PDB、probe 原 red。原件未改写。
- 原始 strict 日志与提交的 gzip 全文对应：355 → 353，精确删除 Service `(14,21)` 和设置模型 `(11,21)` 两条 CsWinRT1028；余 303 MVVMTK0045 + 46 MVVMTK0046 + 4 CsWinRT1028 逐项相同、零新增。两次历史 exit1 保留。1543 份 strict 源文件重新校验，只有目标两文件不同。
- 原 baseline-all/all 各535、sponsorblock-before/after 各204条 TRX 逐结果 Passed，并与提交压缩副本逐字一致。
- 应用 Debug/Release 构建 exit0、各一条既有 protobuf unused-import warning 是候选历史结果，不是本轮父应用新构建。历史 ILC/正式依赖缺口沿用前序报告；本片及本轮均未重跑 strict、ILC 或 publish。

## 本轮 fresh 操作

- 只读 Metadata 重新读取候选最终 Debug/Release DLL/PDB：PE/PDB identity 匹配，各2591份文档全部存在且源码散列匹配；另用 Python 独立复算。两目标 attribute 指向存在的详情类，GetExposedInterfaces IL 分别52/47字节。不是沿用旧 DLL 的口头结论。
- 重新运行候选 Debug/Release 隔离接口 probe，均通过；随后在父 checkout 新构建链接父真实 SponsorBlock 源码的 Models 两配置，均0 warnings/0 errors；再次调用同一 probe，IClosable 与 WinUI INotifyPropertyChanged IID/非零 vtable 通过。父 Models 的 DLL/PDB identity 和各38份文档新校验通过，两声明与候选源码逐字匹配。使用候选已核验的真实 MVVM/WinUI 依赖，不加载应用 initializer。
- 父新执行 `dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger "trx;LogFileName=all.trx" --results-directory artifacts/sponsorblock-aot-integration/tests`，exit0：**Passed: 535, Failed: 0, Skipped: 0**。TRX逐结果解析，535名称与候选完全一致。
- Models 命令为 `dotnet build docs/performance-acceptance/evidence-sponsorblock-aot/Models/Models.csproj -c <Debug|Release> -p:SourceRoot=<父路径> -p:AppBin=<候选路径>/src/_build/x64/<配置>/BiliCopilot.UI/bin`。Metadata/Probe 直接使用已核验候选工具 DLL，传入明确的候选应用或父 Models 路径。没有运行 UI、COM QueryInterface 或 Native AOT 绑定测试。

[本轮结果及哈希](evidence-sponsorblock-aot-integration/verification.json)、测试日志/压缩TRX、接口输出与历史复算脚本随报告提交；大 PDB JSON 保留在父 ignored `artifacts/sponsorblock-aot-integration/`。原始大产物仍在候选工作区。

## 保留边界

完整 AOT、真实绑定与性能仍 HOLD，#49 独立推进，CoreOX 独占 desktop。专用 WinUIAnalyzer 未运行；未扩修其他域，未添加镜像测试。kernel `0ae5e4e000a41a07f6696746706df11c06e8835b` 与正式依赖保持 clean。本轮无子代理、UI/部署/登录/安装/UAC/push/PR/issue。准备阶段误从父读取尚未合入的报告及 PowerShell wildcard 搜索失败，后改用候选路径和 rg glob；没有产品失败或证据覆盖。
