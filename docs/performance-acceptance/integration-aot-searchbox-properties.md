# SearchBox 两属性迁移：独立集成复核

2026-10-31，fresh reviewer/merger，独占父 checkout，无子代理。父 `feat/performance-45` 初始 HEAD `aaddb36f965979af5c53da0e8504eec47486debf`，候选工作区 `t3-d071584c` HEAD `ce4fec959affcec7c919bf3a11401a295b6f9212`，两者及 kernel `0ae5e4e000a41a07f6696746706df11c06e8835b` 均先核验 clean。已读 AGENTS/domain、code-review、WinUI review、verification、run-tests；遵循用户无子代理要求，自行执行 Standards/Spec 两轴。没有 domain glossary/ADR 或测试 overlay。

Standards 与 Spec 均无新增阻断。指定 SHA 以 `--no-ff` 合入 **`69de40582ac61345c2cfe294b9f21152252fbd04`**；双亲为上述父起点与候选，merge tree 与候选相同。后续只补集成证据、报告及 status。

## 语义与测试保真

生产 diff 恰好 SearchBoxViewModel.Properties.cs 两个 ObservableProperty 声明。Keyword 仍是 public string、可读写、默认 string.Empty；IsHotSearchLoading 仍是 public bool、可读写、默认 false。所有 partial 与消费者检索中，旧 backing field 只有声明，无直接读写；其他 VM 的同名字段未修改。生成 setter 归一化 backing storage 后执行体相同：相等早退、changing hooks、赋值、changed hooks、PropertyChanged。正式与测试均 PropertyChanging=false；OnKeywordChanged 先取消旧 CTS、清空集合，再通知。旧 MemberNotNull 私有字段标记与未实现 hook 的 nullable 注解差异不等于行为变化。

VM 方法体、250ms 去抖、CTS identity、旧结果隔离、旧 finally 不释放新请求、推荐缓存及区域切换均未改。AppSearchBox 的 Keyword TwoWay/PropertyChanged 与 IsHotSearchLoading OneWay 绑定、600ms 选择抑制、焦点代次及卸载清理原样保留。五个生成命令逐字不变；生成动态属性 provider 前后均没有 SearchBoxViewModel，不能声称新增 provider。WinRTVtable 的 INotifyPropertyChanged 与共享 ChapterItemViewModelWinRTTypeDetails 路径有效，最终 DLL 内 GetExposedInterfaces 有方法体。

新增四个测试实例（两个 Fact、一个双参数 Theory）直接调用 linked 生产 VM 与 Toolkit 属性/命令：默认值和同值去重、不同 Keyword 先取消/清空再通知及迟到结果隔离、热搜成功/失败加载通知与缓存。测试没有复制搜索算法；Windows ViewModelBase 为 ObservableObject 适配器，服务为可控完成的 fake，不能代替 XAML/IME/真实网络。原18项测试保留，历史旧源/新源22项与全套539项名称集合相同，notification-red 的三个失败为配置敏感性证据，不冒充旧功能缺陷。

## 历史证据与本轮操作

历史 strict **没有重跑**。独立核验39份大产物 hash，复算原日志诊断、TRX、生成属性/命令及1539份审计源码；重建的小证据与已提交内容一致。strict archive 另与固定父/kernel Git blob 关联核验，首次原始字节比较发现 CRLF/LF 转换，按换行归一化后1539份全部对应；实际编译 PDB checksum 检查仍按原始字节和指定算法执行。

red/green 都 exit1，349→347 仅移除目标两条 **MVVMTK0045**，不是交付消息中的 MVVV 拼写；其余301属性+46命令逐路径、位置、全文不变，无新增。level2、优化、分析器与 warnings-as-errors 保留，隔离副本移除 IL/MVVMTK NoWarn；全局 `AllowUnsafeBlocks=true` 只属于历史 audit 条件，不是正式 kernel1030 修复。protobuf cold/warm warning 差异不作为修复。正式 NoWarn、依赖与 kernel gitlink 未动。

本轮先重新读取候选 Debug/Release 最终 DLL/PDB，身份匹配，各2595份源文档全部存在且 checksum 匹配。父级 fresh 强制 Rebuild 测试，再跑全套：**Passed: 539, Failed: 0, Skipped: 0**，其中22项搜索；逐 executionId、结果及完整测试名称集合核验。测试 PDB 139文档中112份落盘源码全部匹配，27份未落盘生成文档不冒充物理文件闭包。

父级 fresh Debug/Release x64 应用构建均 exit0、0 warnings/0 errors；两配置最终 DLL/PDB 身份匹配，各2595份源文档全部存在且 checksum 匹配。新生成属性与候选一致，五个命令逐字相同，动态 provider 范围未变。构建及最终生成证据结果见本轮 [integration-validation.json](evidence-searchbox-properties/integration-validation.json)，TRX 已压缩提交。ignored 原日志/binlog/DLL/PDB manifest 在 `artifacts/searchbox-properties-integration/`。

```powershell
dotnet build tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release -t:Rebuild -p:BuildProjectReferences=false
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-build --logger 'trx;LogFileName=all.trx' --results-directory artifacts/searchbox-properties-integration/tests
$repoPath = (Get-Location).Path
# cfg 分别 Debug、Release，顺序执行
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c $cfg -r win-x64 -p:Platform=x64 "-p:SolutionDir=$repoPath/src/" -p:AppxPackageSigningEnabled=false -p:EmitCompilerGeneratedFiles=true "-p:CompilerGeneratedFilesOutputPath=$repoPath/artifacts/searchbox-properties-integration/generated-$cfg" "-bl:artifacts/searchbox-properties-integration/$cfg.binlog"
# 将已提交 Metadata.cs 编译为 ignored net11.0 console，参数 DLL、PDB、输出 manifest；读取候选与父两配置、父测试
python docs/performance-acceptance/evidence-searchbox-properties/integration-verify.py
```

完整 **AOT HOLD、#49 FAIL** 保留；CoreRX1 另一路独立进行。本片编译与 headless 证据不能升级为真实 AOT 搜索绑定、IME、焦点或600ms控件抑制通过。专用 WinUIAnalyzer 未运行。未重跑 ILC/publish，无 UI/部署/登录/安装/UAC/push/PR/issue，未扩大其他 VM、正式依赖、NoWarn 或 kernel gitlink 范围。
