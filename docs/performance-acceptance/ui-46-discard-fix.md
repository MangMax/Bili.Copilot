# #46 Discard 修复独立复验与集成

2026-10-30。**有限真实 VM / 产品面板离线矩阵通过：10/10 场景、38/38 断言。** 原 `DiscardClearsProgress` 的 `Generating` 残留变为 null；认证端到端、完整应用、Native AOT 和性能收益仍未验，#46 未关闭。

## 合并与唯一生产变化

父 checkout `t3-1d19cc80`、分支 `feat/performance-45` 起点准确为 `83173729e9ad7c2c49174b5eada2f31843a0c600`。首先检查未提交 diff：只有 `AIViewModel.cs` 的 Discard 在 `RequestText = string.Empty;` 后增加 `ProgressTip = default;`，diff-check 通过。没有 reset/stash 或扩大重构。

执行 `git merge --no-ff 217b8b100b996945c30248021adeefbd014c8b2b -m "Merge offline UI 46 ViewModel acceptance evidence"`，得到 `d54bb0dadbbcb8bbb0acc0f082594c5f586742bc`，无冲突，未提交的一行原样保留。原 [报告](ui-46-viewmodel-acceptance.md)、harness、38 断言 37 pass/1 fail、全部原图与失败日志对目标提交逐路径 diff 为空。历史红来自原运行，本轮没有撤销已授权修复去制造第二次红。

遵照 AGENTS/docs/agents、winui-code-review、winui-ui-testing、winui-dev-workflow、run-tests、verification-before-completion；无 run-tests overlay，SDK 10.0.403、xUnit2/VSTest。没有新增测试断言或改测试逻辑；读取了 code-testing，未触发测试生成或子代理流程。

## 新独立实测与保真度

新 prepared 目录 `C:/Users/hp/AppData/Local/Temp/ai46vm-fix-1d19`，新 layout `C:/Users/hp/AppData/Local/Temp/ai46vm-fix-1d19-layout`，独立身份 `BiliCopilot.Acceptance46Fix1D19`，仅 owned PID **53272**。按已授予桌面独占槽使用本地桌面；沿用已知 Sandbox feature-disabled 边界，不启用功能、不安装工具。宿主是实际生产 source-linked VM、四个产品面板和 Markdown 控件，服务使用原有受控离线替身，不是发行应用。

原 prepare 生成全新目录后，只替换包身份、窗口标题、runner workflow id 和 provenance owned PID。`App.xaml.cs` 除标题外逐字一致；`Services.cs` 的 stream/TCS、原 verifier 字节 hash 一致。未改 oracle、没有直接赋值伪造生产状态。新 run/provenance/manifest 保存在 [证据目录](evidence-46-discard-fix)，其余原 fixture 在 `harness-46-vm`。

| 要求 | 本轮证据 |
| --- | --- |
| 真实六入口 | `AllSixProductionGenerationBranches`：文章/视频 × 总结/评价/提问，18 个 Streaming/Final/Preparation 断言全部通过；生产源准备、最终全文、临时文本清理和进度结束共同验证 |
| Discard 原红转绿 | `StopDiscardClear-Discard` 的 `DiscardClearsAndRejectsLate`、`DiscardClearsProgress` 通过；旧任务迟到异常释放、Disposed 与生产命令 await 后，ProgressTip=null，回答/错误为空且 IsGenerating=false |
| 取消、清空 | `StopDiscardClear-Cancel/Clear` 两类清理断言及 `RealStopButton` 通过 |
| 旧分片/异常不清新进度 | `LateOldCannotPublishOrClearCurrent-False/True`、`CurrentCompletes-False/True` 全通过，当前文本和进度不会被旧 finally 清理 |
| 重新生成 | `RegenerationOwnsState`、`RegeneratedAnswer` 通过，旧请求迟到异常不会夺取新代状态 |
| 切源 | `StalePreparation-GetSubtitleMetasAsync/GetSubtitleDetailAsync/GetCommentsAsync/GetServiceConfigAsync`、`OldPromptCannotReplaceVideoPrompts` 通过 |
| 全文复制 | 实际产品 CopyAnswer 按钮 → 生产 DataPackage/Clipboard，`RealCopyExactUnicodeMarkdown` 精确 Unicode/Markdown 全文比对通过；未读取原用户剪贴板，仍等于写入合成全文才清理，`clipboard-clear=true` |
| 实际输入 | send-input 向真实 InputBox 输入 `fixture question` 并 Enter；流中向模型控件发送 Home，实际停止按钮取消；UI 输出和截图保留 |
| 非流式 | `NonStreamingExact` 通过，仅代表该有限组合 |

[summary.json](evidence-46-discard-fix/summary.json)、[原始事件](evidence-46-discard-fix/events.jsonl)、[红绿独立对照](evidence-46-discard-fix/integration-verification.json) 和 [UI 批处理日志](evidence-46-discard-fix/ui-run.log) 相符，runner/verifier exit 0，没有新启动失败或 unhandled 事件。

8 张新 PNG 均逐张打开查看，记录在 [visual-review.json](evidence-46-discard-fix/visual-review.json)：ready、流式文本、文章最终全文复制、视频最终、真实输入后、长流/焦点、停止后、done。最终标题/组合字符/表格/代码/链接/FINAL-Ω 完整可见；长流第 26 行处是滚动视口边界，不是全文丢失。停止后进度与文本消失、快捷卡片和输入恢复。AI logo 仍为黑色占位，本地化有资源键，不能据此签完整产品视觉。没有覆盖所有主题/DPI/窄窗或每个卡片物理点击。

## Fresh 构建和来源

以下命令全部实际执行并 exit 0，日志和 TRX 在证据目录。`SolutionDir` 明确指向本 checkout，未复制运行库、未安装依赖或工具。

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false --logger 'trx;LogFileName=release.trx' --results-directory docs/performance-acceptance/evidence-46-discard-fix
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
dotnet msbuild src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:ValidateRuntimeDependencies -p:Configuration=Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false -v:normal
dotnet build C:/Users/hp/AppData/Local/Temp/ai46vm-fix-1d19/Harness.csproj -t:Rebuild -p:Platform=x64 -v:minimal
winapp run C:/Users/hp/AppData/Local/Temp/ai46vm-fix-1d19/Harness.csproj --arch x64 -p Platform=x64 --no-build --detach --json --output-appx-directory C:/Users/hp/AppData/Local/Temp/ai46vm-fix-1d19-layout
& C:/Users/hp/AppData/Local/Temp/ai46vm-fix-1d19/run.ps1 -AppPid 53272 -Layout C:\Users\hp\AppData\Local\Temp\ai46vm-fix-1d19-layout -Evidence docs/performance-acceptance/evidence-46-discard-fix
```

- Release **Passed: 403, Failed: 0, Skipped: 0**。TRX total/executed/passed/实际结果条目均 403，其他失败/未执行计数为 0。该 suite 不是实际 VM UI 验收的替代品。
- Debug x64 build：0 warnings / 0 errors，73.40 秒；runtime gate：0 warnings / 0 errors，0.55 秒，仅验证资产存在，不证明解码/原生实际加载。
- 新宿主干净 Rebuild：66 warnings / 0 errors，20.14 秒。保留 MVVMTK0045、CsWinRT 等既有警告，未屏蔽；不宣称 AOT 兼容。
- 实际 layout `Harness.dll` 与干净 Rebuild 产物 SHA256 均为 `E255939A7E1EC329870A7FC01C6B5BFA5F583EF146E03E49ACB19C886E901281`。73 个生产源码 hashes、4 份 byte-identical 产品 XAML 和 fixture hashes 见 [provenance](evidence-46-discard-fix/provenance.json)。与历史 73 源比较仅 AIViewModel.cs 改变，见 [source-comparison](evidence-46-discard-fix/source-comparison.json)。这是源链接宿主 DLL，不是全应用发行 DLL；provenance sourceHead 为合并提交，当时这一行尚未提交，必须结合源码 hash 识别测试版本。

## 构建/UI 后 WinUI 审核

本次一行变化未发现新增阻断。Discard 先取消/置空 CTS，使旧 token 不再拥有状态，再 Erase、清请求、同步清进度和结束 Generating。六条生成入口的 `IsCurrentGeneration` finally/catch 和延迟 dispatcher guard 原样保留；旧代不得替当前代做清理。

Cancel 原本先清进度，再调用 Discard；重复设置同一默认值符合现有 observable setter 语义，不引入额外异步工作。Clear 继续通过 Cancel 清理再卸掉服务/模型。正常结束 finally 已清进度且置空 CTS，随后 Discard 仍清结果/请求/当前 prompt；新增清 null 是幂等操作，该正常结束后语义由静态检查确认，本轮没有新增单独按钮序列声称动态覆盖。

**可达性限制：**真实 AIFooter 的 Discard/Regenerate `IsEnabled` 对 IsGenerating 取反，流中禁用；本轮对应竞态是直接执行真实 Command。没有证明普通用户能在生成时点击丢弃触发原缺陷，不把命令级复现升级为认证 full app 必现。

没有新增 XAML、绑定、线程等待、集合替换或凭据路径。既有 footer 图标按钮缺稳定 AutomationId，宿主仅按命令实例附测试 ID；不能签完整 accessibility。**专用 Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer 未运行**，普通构建分析器不代替它。没有擅自引入包或展开 MVVM 重构。

## 边界与清理

原报告的服务适配器、全部未覆盖分支、认证/真实供应商/完整主窗口/导航/通知/overlay/持久设置边界继续有效。40 个 dispatcher 往返代理样本只证明该短合成流中回调被执行，不是输入延迟分布、性能预算或优化收益，本轮不据此作性能对照结论。Release suite 通过不等于 Release UI/AOT 通过；MSVC/linker 缺失的既有 AOT blocker 未重复尝试。

owned PID 53272 已退出、专用包已移除；独立复查均 absent，`yield.json` 实际返回 **released:true**。未碰 PID 9608、Dev/生产包、凭据、真实 AI 服务，无 push/PR/issue 操作、无子代理。kernel gitlink/HEAD 保持 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d` 且 clean。原始红和截图不覆写；此次提交只有已授权的一行生产修复、此报告、status 和新证据。
