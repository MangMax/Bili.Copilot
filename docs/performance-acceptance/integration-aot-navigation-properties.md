# NavigationViewModel 六属性：固定候选集成复核

2026-10-31，fresh reviewer/merger，无子代理。父 `feat/performance-45` 起点 `608acc77123654c2a60d2bb3d72a5b7a7c3c2132`、kernel `0ae5e4e000a41a07f6696746706df11c06e8835b` 均核验 clean；候选工作区 `t3-7a7ce646` HEAD 为固定 `a1b83d3cc9168f7c577cb56ac1a835ccfdda34b3` 且 clean。已读 AGENTS、domain、code-review、winui-code-review、verification-before-completion、run-tests；无 domain glossary/ADR 或测试 overlay。遵循用户无子代理约束，独立执行 Standards/Spec 两轴。

两轴均无新增阻断。指定 SHA 以 `--no-ff` 合入 **`a4fce3de8e8a9313325892d669b67a466c38a9e8`**，双亲为上述父起点与候选，合并 tree 等于候选。随后仅补集成报告、状态与复核脚本/结果；正式 Mpv/Vulkan/kernel、warning 配置、其他 VM 均未改。

## 语义与 WinUI 审查

生产 diff 仅六字段转为 public partial 属性，类型、公开名称、get/set 可访问性均保持；五个 bool 默认 false，MenuItems 仍在 Initialize 前为 null，没有新增初始化。原 backing field 的全源命中仅六处声明，没有直接读写或其他 partial 声明、hook 实现。方法体、初始化顺序、导航、菜单、缓存、搜索、叠层返回完全未改。

实际应用生成 setter 归一化 backing storage 后相等：EqualityComparer 早退、changing hooks、赋值、changed hooks、PropertyChanged 顺序保持；生产与测试均配置 PropertyChanging=false。旧 MenuItems MemberNotNull 私有字段标记消失，未实现的双参 hook nullable 注解差异不形成可执行行为变化。不能声称生成文本逐字相同。

RootLayout 的六属性消费者均为显式 OneWay x:Bind，名称和模式无变化。partial 声明使属性在输入语法树对其他生成器可见，六条 MVVMTK0045 消失是本片的编译器证据。迁移前后 WinRTCustomBindableProperties 都没有 NavigationViewModel 条目，WinRTVtable 仍含 INotifyPropertyChanged；**没有“新增动态 provider”的证据，也没有 AOT 实机绑定通过的证据**。专用 WinUIAnalyzer 未运行。无新线程、事件订阅或生命周期修改。

## 测试保真与旧 DLL 排除

新增14项直接链接生产 VM、两个 frame、factory、SearchBoxViewModel；页面、窗口、DI、设置和导航项为适配器，不能代替真实 XAML/DI/动画。六属性测试检查同值不通知、变更后值可见、还原通知及不发 PropertyChanging；主页重访测试检查 IsOverlayOpen → IsTemporaryOverlayClosed → IsFavoritesPage 的顺序。各测试新建 VM/帧/列表，服务和设置放在 AsyncLocal，Dispose 清除；无新普通静态共享字典。AsyncLocal 仍按 ExecutionContext 流入自身子任务，不承诺任意子任务隔离。旧 factory 故障注入的 AsyncLocal/finally 和 InnerException identity 检查保留。

历史 baseline-tests 的 CS0246、不保真 PropertyChanging 默认配置运行及 stale-* 结果均不作验收。只读检查 stale PDB manifest，确实引用 NavigationViewModel.before.cs；最终候选测试 PDB 本轮从实体重新读取，已引用提交源且 checksum 匹配。候选最终旧源/新源的37与524 TRX、configuration-red 六失败、Rebuild 成功日志均核 hash；旧源 PDB 属历史 manifest 复核，不冒充本轮重建旧源。

父级本轮先强制 Rebuild 测试项目，再 --no-build 执行全套；PDB 检查排除旧源文件，并核对所有仍在磁盘的 document checksum。原510测试名称集合完整包含在524中，原23导航与新增14均通过。原 factory 异常修正及510基线报告不覆盖、不改写。

## 新运行与历史复核的边界

新运行日志、TRX、binlog、PDB manifests 和输出 hash 在 `artifacts/navigation-properties-integration/`。命令在父根执行，RC3 SDK / xUnit 2 / VSTest：

本轮 **Passed: 524, Failed: 0, Skipped: 0**，524个唯一 executionId；测试 Rebuild、Debug x64、Release x64 均 exit0、0 warnings/0 errors。Debug/Release 各2587个PDB document全部对应磁盘输入；父/候选测试各134 document中108个实际文件核验通过，26个未落盘生成 document不冒充全文件闭包。应用新生成 setter hash 与候选最终输出一致。

集成校验器首轮发现新测试文件 checkout 的CRLF/LF差异，归一化后1118个源码/测试/global.json内容相同，VM本身仍逐字节匹配。其次发现 ComputeSharp GlobalUsingDirectives 的PDB使用SHA-1而读取器 actual 字段统一为SHA-256；已按PDB算法分别重算，并保留actual SHA256验证，不忽略差异。长生成文件路径使用Windows extended path读取。以上是验证脚本修正，未改生产或测试输入。

```powershell
dotnet build tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release -t:Rebuild -p:BuildProjectReferences=false
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-build --logger 'trx;LogFileName=all.trx' --results-directory artifacts/navigation-properties-integration/tests
$repoPath = (Get-Location).Path
# 分别将 $configuration 设为 Debug、Release，$suffix 设为 debug、release，顺序执行：
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c $configuration -r win-x64 -p:Platform=x64 "-p:SolutionDir=$repoPath/src/" -p:AppxPackageSigningEnabled=false -p:EmitCompilerGeneratedFiles=true "-p:CompilerGeneratedFilesOutputPath=$repoPath/artifacts/navigation-properties-integration/generated-$suffix" "-bl:artifacts/navigation-properties-integration/$suffix.binlog"
python docs/performance-acceptance/evidence-navigation-properties/integration-verify.py
```

PDB 读取器用已提交 PdbInputs.cs 在父 ignored net11.0 console 项目编译，依次读取父测试、候选最终测试及父 Debug/Release PDB。源码/测试/global.json 与候选逐文件对比，实际生成 setter hash 与候选最终 hash 对比；依赖资产及输出 hash 记录在[集成结果](evidence-navigation-properties/integration-validation.json)。它证明本次编译输入闭包，不是发布包或完整 ILC 输入闭包。

严格审计为**历史原始日志本轮复算**，不是新跑 strict build。red 309+47+8，green-final 303+47+8；集合差恰好本 VM 六条 MVVMTK0045，无新增，其余358条文件/行号/消息相同。既有 verify.py 复算历史明细、文件账本、生成 setter 及审计源码一致性通过；该脚本重写候选同内容 validation.json 和 ignored manifest，候选 tracked 状态复查仍 clean，未改候选代码。后续集成 verifier 仅写父目录。原严格构建 exit1 保留，audit-only AllowUnsafeBlocks 不视为 kernel 正式修复，正式 NoWarn 未新增/修改。

没有无理由重做完整 ILC；原 factory 阶段完整 ILC 的6条IL+2条正式Mpv缺失成员是历史结论，不声称本次重新证明。**本片有限集成通过，完整 AOT/性能采用仍 HOLD**：剩余303属性、47命令、8条CsWinRT1028、kernel生成前提、正式Mpv兼容/Vulkan分发以及真实首次/重复导航、叠层返回和绑定仍待 AOT 实机。#49 继续 FAIL/未关闭，由另一路 CoreQX 处理，本次未介入。无 UI、部署、登录、安装、UAC、push、PR 或 issue 操作。
