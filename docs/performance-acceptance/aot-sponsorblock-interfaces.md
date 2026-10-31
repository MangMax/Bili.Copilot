# SponsorBlock 的 CsWinRT 接口生成

2026-10-31，工作区 `t3-4b0da1fe`。输入与父 `feat/performance-45` 均核验为 `7fcb66a49d0fb43509e96d519e1466c2d5e97731`。本片仅在 `SponsorBlockService`、`SponsorBlockCategorySettingViewModel` 两个顶层声明各增加一个 `partial`，不需要外层声明修改。公开成员、默认值、通知、IDisposable、取消、缓存和跳过实现不变；没有增加生产抽象、迁移 Observable 字段、修改依赖、gitlink、NoWarn 或 warning level。

**完整 AOT 与真实绑定仍 HOLD；#49 独立推进。** 以下仅为本片编译、生成接口及已有纯逻辑回归证据，不表示已经发现或修复了一次实机运行事故。

## 消费与 ABI 边界

- `SettingsPageViewModel.SponsorBlock.cs` 将真实模型加入只读属性持有的 `ObservableCollection<SponsorBlockCategorySettingViewModel>`。`SponsorBlockSettingControl.xaml` 的 `ItemsControl.ItemsSource` OneWay 绑定集合，`DataTemplate.x:DataType` 指向本类型，模板编译绑定读取展示值；颜色相关值使用 OneWay 通知。
- 模型还经 `Tag="{x:Bind}"` 写入原生 ComboBox；`OnBehaviorComboBoxLoaded` 与 `OnBehaviorSelectionChanged` 从 Tag 取回模型，颜色按钮从 DataContext 取回模型。因此实际对象过界有明确依据。行为选中项并非 TwoWay XAML 绑定：Loaded 显式赋值，用 Begin/EndBindingRefresh 包围，SelectionChanged 调用 ApplySelection，继续保留过滤非法选项、抑制重复持久化的逻辑。
- `SponsorBlockService` 唯一生产构造点是 `GlobalDependencies.AddSponsorBlockService` 的托管 `AddSingleton(_ => new SponsorBlockService())`。唯一业务获取点是 `PlayerViewModel.LoadSponsorBlockSegmentsAsync` 中的 `this.Get<SponsorBlockService>().GetSegmentsAsync(bvid, cid, cancellationToken)`，返回的是片段集合；服务实例没有写入 ItemsSource、DataContext、Tag、Content，也没有发现 WinRT 传参/返回路径。DI 持有并管理 IDisposable 生命周期不等于服务跨 WinRT ABI。
- 服务字段为 HttpClient、缓存、TimeProvider、超时、所有权与 disposed 状态；不存在因“服务持有 WinUI 对象”而推定过界的依据。这里给 IDisposable → IClosable 生成接口表是满足严格生成策略，不能把 CsWinRT1028 解释为已有 runtime bug。
- 请求取消继续使用 linked CTS 与默认五秒超时，catch 降级为空；只缓存非空结果，Dispose 仍幂等且仅释放自有 HttpClient。播放器 generation/开关检查、取消及 skip/mute/prompt 路径均无改动。源码对照证明保持，不声称现有测试独立覆盖了全部 Dispose/外部取消分支。

审查依据为 [payload 独立审查](aot-payload-and-compatibility.md) 的“严格诊断：编译阻塞与可达性分开”和 [历史逐文件台账](evidence-aot-review/level2-by-file.json)。当前基线采用已合入的 [Popular 模型审查](integration-aot-popular-models.md)，不把历史364条与当前355条混为同一源码状态。

## 严格 red / 本片差额

使用原 `evidence-aot-payload/export-source.ps1` 建立两个独立、初始无 bin/obj 的源码 export，再以 `audit-build.ps1 -WarningLevel 2 -AllowUnsafeForAudit` 构建。正式依赖保持，副本移除既有 IL/MVVMTK0045/0046 NoWarn，warnings-as-errors、分析器与优化保持。全局 AllowUnsafeBlocks 仅沿用历史审计前提，越过 kernel CsWinRT1030；不是正式修复。

red 为 **355 = 303 MVVMTK0045 + 46 MVVMTK0046 + 6 CsWinRT1028**；修后为 **353 = 303 + 46 + 4**，两次仍 exit 1。按文件、行列、编号和完整消息去除摘要重复，恰好移除 Service `(14,21)` 与设置模型 `(11,21)` 两条1028，零新增；其余353条逐项相同。剩余1028为 TrayIconSession、DanmakuLoadSession、CustomHttpClientHandler、TrayLifecycle。原始 red 与其他域诊断均保留；没有运行 ILC/publish。

见 [复算脚本](evidence-sponsorblock-aot/verify.py)、[结果](evidence-sponsorblock-aot/verification.json)、[red](evidence-sponsorblock-aot/red-diagnostics.json.gz)、[green](evidence-sponsorblock-aot/green-diagnostics.json.gz)。脚本还比较两个副本的全部 tracked src/kernel 文件，仅允许这两个源码声明不同，并保存 [源码哈希](evidence-sponsorblock-aot/strict-source-hashes.json.gz)；两个副本的审计 csproj 保持相同。green 明确复制未提交的目标源码，避免 HEAD 导出漏掉本片。

## 验证与局限

普通应用 Debug/Release 均构建 exit0，各有一条既有 protobuf unused-import warning、零错误。最终 DLL/PDB 校验与隔离 probe 的具体结果由上述 verification.json 和 [大产物哈希](evidence-sponsorblock-aot/large-artifacts.json) 记录。Metadata 工具只读 PE，不加载应用：检查两个 WinRTExposedType attribute 指向实际存在且含 GetExposedInterfaces IL 的详情类型、CodeView 与 portable PDB identity，以及所有 PDB 文档与当前磁盘源码的哈希；缺失文件同样失败。两个配置各2591份文档全部匹配，接口方法 IL 为 Debug 52字节 / Release 47字节。strict 两副本的1543份 tracked src/kernel文件比对通过。

服务生成 IClosable/IDisposable 表；设置模型在应用中复用 `ChapterItemViewModelWinRTTypeDetails` 的 INotifyPropertyChanged 表，这是 CsWinRT 去重，并非模型混绑。[Debug XAML 生成摘录](evidence-sponsorblock-aot/debug-binding-excerpts.txt) 还显示模板 SetDataRoot 的类型转换、Tag 写入及 PropertyChanged 订阅，但这仍是编译证据。[版本及生成器 hash](evidence-sponsorblock-aot/packages.json) 固定 CsWinRT 2.3.1、MVVM 8.4.2、WinUI 2.6.4。

隔离 Models 工程直接链接真实 SponsorBlock 源码与设置模型，使用应用输出的真实 MVVM/WinUI DLL 和相同 CsWinRT 版本。Probe 仅调用生成接口表，验证 IClosable（`30d5a829-7fa4-4026-83bb-d75bae4ea99e`）及 WinUI INotifyPropertyChanged（`90b17601-b065-586e-83d9-9adc3a695284`）的非零 vtable。原源码构建有两条1028 warning，probe 因两个 attribute 缺失而失败；修后 Debug/Release 独立构建零警告零错误，同一 probe 通过。原 red DLL/PDB 与日志另存 ignored 目录，没有被 green 覆盖。**这不是 COM QueryInterface、Native AOT 或真实 WinUI 绑定测试**，不加载应用部署 initializer，也不重复实现业务逻辑。

现有测试直接链接真实服务、模型及 SponsorBlock 纯逻辑，没有为 partial 增加镜像测试：

| 保持要求 | 现有测试证据 |
| --- | --- |
| 默认值、通知、重复赋值 | `DefaultArgsConstructor_KeepsDocumentedDefaults`、`Behavior_ChangeRaisesPropertyChangedOnce`、`Behavior_SetToSameNewValueTwice_WritesOnlyOnFirstChange` |
| 换绑期间不持久化 | `ApplySelection_DuringBindingRefresh_IsIgnored`、`ApplySelection_AfterBindingRefresh_IsApplied` |
| 颜色通知、恢复与主题值 | `SetCustomColor_StoresValueRaisesPropertiesAndWritesOnce`、`ResetColor_FromCustom_InvokesResetOnceAndRaisesProperties`、`UpdateDefaultColor_WhenCustom_DoesNotRaise` |
| 请求隐私、超时、缓存 | `GetSegmentsAsync_SendsOnlyBvidAndCid`、`GetSegmentsAsync_Timeout_ReturnsEmpty`、`GetSegmentsAsync_SecondCallWithinCachePeriod_DoesNotRequestAgain`、`GetSegmentsAsync_EmptyResponse_IsNotCached` |
| 跳过/静音与迟到结果 | `Evaluate_AutoSkip_SeeksToEndAndMarksHandled`、`Evaluate_UserMuteChange_SuppressesMuteAndRestore`、`ShouldApplyResult_GenerationChanged_ReturnsFalse` |

fresh 基线全套535、修改前后相关各204、修改后全套535，均 Passed、Failed 0、Skipped 0；TRX 逐结果复核并保存 gzip。上述纯逻辑用例不覆盖真实播放器/控件调用。

## 环境与交接

初始 root 只有 kernel 异常：kernel HEAD `f02c33f`、索引零条目、目录仅 `.git`，指定 `0ae5e4e` 对象本地缺失。先保存 initial-root/initial-kernel 状态与 binary patch，从父 kernel 只读本地 fetch 精确对象，然后仅恢复本工作区至 `0ae5e4e000a41a07f6696746706df11c06e8835b`；gitlink 不变，结束 kernel clean。媒体资产从已有候选工作区复制到本工作区，无安装或资产替换。

按用户指定的 AGENTS/domain、tdd、code-testing、run-tests、winui-code-review/dev、verification 技能自行审核，无子代理。声明以外去掉 partial 后与父源码归一化换行逐字相同；未发现本片公开契约、通知、生命周期或绑定模式回归。专用 WinUIAnalyzer 未运行，不新增包。无 UI、部署、登录、安装、UAC 或身份变更；CoreOX 独占 desktop，未操作保护环境。无 push/PR/issue/父工作区写入。交付前 `git merge --ff-only feat/performance-45` 返回 Already up to date，父仍为 `7fcb66a4`。

准备期间有不存在的文档扩展名/PowerShell 通配路径查询，以及生成摘录脚本的一次 foreach 表达式语法错误；修正查询与摘录命令后继续，均未修改产品逻辑或重写 red。原始 strict exit1 与 probe attribute 缺失失败保持原样。

## 复验命令

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --filter "FullyQualifiedName~SponsorBlock" --logger "trx;LogFileName=sponsorblock-after.trx" --results-directory artifacts/sponsorblock-aot/tests
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-build --logger "trx;LogFileName=all.trx" --results-directory artifacts/sponsorblock-aot/tests
# Debug / Release 分别构建，生成目录也分别使用 generated-debug / generated-release。
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -r win-x64 -p:Platform=x64 "-p:SolutionDir=$pwd/src/" -p:AppxPackageSigningEnabled=false -p:EmitCompilerGeneratedFiles=true "-p:CompilerGeneratedFilesOutputPath=$pwd/artifacts/sponsorblock-aot/generated-debug" -bl:artifacts/sponsorblock-aot/debug.binlog
dotnet build docs/performance-acceptance/evidence-sponsorblock-aot/Models/Models.csproj -c Debug "-p:SourceRoot=$pwd" "-p:AppBin=$pwd/src/_build/x64/Debug/BiliCopilot.UI/bin"
dotnet run --project docs/performance-acceptance/evidence-sponsorblock-aot/Probe/Probe.csproj -c Release -- "$pwd/docs/performance-acceptance/evidence-sponsorblock-aot/Models/bin/Debug/net11.0-windows10.0.22621.0"
dotnet run --project docs/performance-acceptance/evidence-sponsorblock-aot/Metadata/Metadata.csproj -c Release -- src/_build/x64/Debug/BiliCopilot.UI/bin/BiliCopilot.UI.dll src/_build/x64/Debug/BiliCopilot.UI/bin/BiliCopilot.UI.pdb artifacts/sponsorblock-aot/debug-pdb.json
python docs/performance-acceptance/evidence-sponsorblock-aot/verify.py
```

strict 精确参数在各次 `arguments.json`，大日志/binlog/DLL/PDB 全部保留 ignored 目录并记录 hash；小生成片段、测试压缩结果和报告提交。重放须使用新的 export/output 路径，不能覆盖本轮原始 red。
