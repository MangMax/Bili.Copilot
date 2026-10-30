# #46 真实 AIViewModel 离线验收

2026-10-30。**有限离线矩阵执行完成，但不是整票通过：10 个场景中 9 PASS / 1 FAIL；38 个断言中 37 PASS / 1 FAIL。** 失败为生成中直接执行生产 `DiscardCommand` 后遗留 `ProgressTip`。未修改生产代码。认证端到端验收、Release/AOT 与优化收益仍未通过，原认证要求不因离线 fixture 没有认证而改写。

起点及最新本地父分支均为 `83173729e9ad7c2c49174b5eada2f31843a0c600`，起始工作树干净，独立分支 `t3/ai-viewmodel-offline-acceptance`。结束执行 `git merge --no-edit feat/performance-45` 返回 Already up to date。kernel 为 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`，工作树无改动。所有新增文件仅在本报告、`harness-46-vm/`、`evidence-46-vm/`。

## 保真边界和完整调用入口

宿主直接编译生产 `AIViewModel.cs/.Chat.cs/.Prompts.cs/.Properties.cs` 全部 partial、生产 `AIQuickItemViewModel`、`AIControlBase`、AIPanel/AIBody/AIHeader/AIFooter 的代码隐藏文件、Markdown 全部源码、StreamingResponseSession、SubtitlePreference、BasicCoverImage/BasicScaleEffect、LocaleExtension、生产 PromptConstants/SettingNames。四份产品面板 XAML 是字节一致副本，相关样式从生产 Overrides 提取；不是另造回答 TextBlock。实际 NuGet WinUIKernel Share/AI 的基类、卡片、模型项和 AI 控件参与运行。

服务/设置/组合根适配器仅在 `Adapters.cs`、`Services.cs`：

- `this.Get<T>()` 和应用 Kernel 指向有限白名单注册表；AI/Share 库通过真实 `InitializeAIKernel/InitializeShareKernel` 初始化，真实 Kernel 中注册同一个 keyed `IChatService("OpenAI")`。未调用生产全应用初始化、账户服务或网络提供方。
- `IChatConfigManager.GetChatConfigAsync` 返回仅一个合成配置；`GetServiceConfigAsync` 返回受控空配置，假 `IChatService.Initialize` 接受它。这验证 VM 配置 await/cancellation 边界，不验证真实配置合法性、认证或供应商初始化。`GetPredefinedModels` 返回一个合成模型；`IChatClient.GetStreamingResponseAsync/GetResponseAsync` 分别使用 Channel/TCS 和 TCS。
- `ISubtitleService.GetSubtitleMetasAsync/GetSubtitleDetailAsync` 返回固定中文字幕；`ICommentService.GetCommentsAsync` 返回固定热门评论；没有 HTTP。提示文件 `IFileToolkit.ReadLocalDataAsync` 返回带生产占位符的固定模板。`FileToolkit.IsLocalDataExist=true`，故不存在文件时的 `WriteLocalDataAsync` 分支未跑。
- 设置为内存适配器；本轮唯一非流式场景切换合成 `IsAIStreamingResponse`。本地化返回资源键；时长格式化 helper 从生产源码精确提取。`AppViewModel.ShowTipCommand` 仅记录调用。**CopyAnswer 方法、DataPackage、Clipboard.SetContent 均是生产代码**，只有复制后的通知是适配器。
- 源注入为真实 `InjectVideoAsync/InjectArticleAsync`；视频/文章内容、字幕、评论均合成。封面 URI 为空，因此真实 BasicCoverImage 虽参与编译，但封面加载不在本轮覆盖中。AI logo 资源未完整打入宿主，截图为黑色圆形占位，不能算生产 logo 验收。

生成/取消/代次逻辑没有在 fixture 重写。外部服务 Task 的 Gate 提供 Entered/Release；模型流每个 chunk 的 Accepted 在生产消费后继续 MoveNext 时完成，Disposed 明确表示旧枚举已退出。测试还 await 生产生成命令的原 Task，避免把 provider 返回误作 VM 完成。异步 void 源注入通过代理 SynchronizationContext 的 `OperationCompleted` 确认整个注入方法结束；代理仅转发 Post，不替代 UI dispatcher。超时只用于终止缺失信号，不用 sleep 推定完成。

所有生成入口是 `SummaryVideoAsync`、`EvaluateVideoAsync`、`AskVideoQuestionAsync`、`SummaryArticleAsync`、`EvaluateArticleAsync`、`AskArticleQuestionAsync`；共用 `GetVideoSubtitleAsync`、`GetHotCommentTextAsync`、`GetArticlePlainText`、`SendMessageAsync`、`SendMessageInternalAsync`、`ScheduleStreamingAsync`。服务选择初始化经过 `InitializeCommand → ReloadAvailableServicesCommand → SelectServiceCommand → SelectModel`，没有直接给 FinalResult/TempResult/IsGenerating 赋 fixture 值。

## 逐项证据和 UI 触发方式

[原始事件](evidence-46-vm/events.jsonl)、[独立解析结果](evidence-46-vm/summary.json)、[UI 命令输出](evidence-46-vm/run-d.log)。`False` 表示文章，`True` 表示视频；分支 0 总结、1 评价、2 提问。每个分支都检查流式状态、精确完整结果/清空临时文本/结束状态/清空进度，以及模型收到的字幕或文章内容；评价额外检查评论进入 prompt。

| Requirement | Evidence |
| --- | --- |
| 文章总结、评价、提问 | `AllSixProductionGenerationBranches` 下 `False-0/1/2-Streaming/Final/Preparation` 全 PASS；实际 QuickItems[0/1].ExecuteCommand 和 SendQuestionCommand。矩阵直接调用真实命令，**没有声称逐个点击实际快捷卡片**。实际 AIBody/AIFooter 同时显示绑定结果。 |
| 视频总结、评价、提问 | 同场景 `True-0/1/2-Streaming/Final/Preparation` 全 PASS；实际 SummaryVideo/EvaluateVideo/AskVideoQuestion 与字幕/评论准备。触发方式同上。 |
| 普通分片到最终 Markdown/尾部 | 六个 Streaming/Final 断言；先等生产 TempResult 通知，再放行尾部并结束枚举，await 命令完成；精确包含 FINAL-Ω。`article-streaming.png` 与 `article-final-copy.png` 展示同一个产品面板的纯文本/Markdown 切换。没有把最终布局同步性等同于模型 Task 完成。 |
| 取消后迟到分片、迟到异常 | `CancelledLateChunkAndException`：`LateOldCannotPublishOrClearCurrent-False/True`、`CurrentCompletes-False/True` PASS。CancelCommand 后启动新问答，再释放不合作旧流的 chunk 或异常，await 旧 Disposed 和命令；新文本、Generating、Progress、Error 不受旧代影响。 |
| 延迟字幕元数据/详情期间切源 | `SourceSwitchDuringPreparation`：`StalePreparation-GetSubtitleMetasAsync/GetSubtitleDetailAsync` PASS；真实视频总结挂起，InjectArticleAsync 后新文章问答流动，再释放旧服务并 await 旧总结。 |
| 延迟评论、模型配置 | `StalePreparation-GetCommentsAsync/GetServiceConfigAsync` PASS；旧文章评价/总结挂起，重新注入另一个 ArticleDetail 实例并开始新问答，随后释放旧准备。这里是文章对象代次切换，不伪称这一子例是视频→文章。 |
| 延迟提示初始化切源 | `DelayedPromptSourceSwitch/OldPromptCannotReplaceVideoPrompts` PASS；旧文章 prompt 读取挂起，真实 InjectVideoAsync 完成，释放旧读取并 await 旧注入 OperationCompleted；当前视频标题、QuickItems 对象仍属视频。 |
| 重新生成，旧请求晚结束 | `RegenerateOldRequestLateCompletion/RegenerationOwnsState/RegeneratedAnswer` PASS；调用生产 RegenerateCommand 重发保存的问题，旧流抛错/Disposed/原命令完成后新代仍生成，最终为 REGENERATED。**是命令级重入**；生成中实际 Regenerate 按钮禁用，未宣称可点击。 |
| 停止 | `StopDiscardClear-Cancel/CancelClearsAndRejectsLate/CancelClearsProgress` PASS；命令直接调用。另 `RealInputAndDispatcherDuringStreaming/RealStopButton` 通过 UIA 调用真实 AIFooter Cancel 按钮，确认回到实际卡片和输入框。 |
| 清空 | `StopDiscardClear-Clear/ClearClearsAndRejectsLate/ClearClearsProgress` PASS；生产 ClearCommand 直接执行，await 旧请求及低优先级 dispatcher，下一非流式场景重新走服务初始化。无宿主假清理逻辑。 |
| 丢弃 | `StopDiscardClear-Discard/DiscardClearsAndRejectsLate` PASS；**DiscardClearsProgress FAIL**。失败后退出此子例，后续独立 Clear、非流式和真实输入子例继续运行。 |
| 非流式 | `NonStreaming/NonStreamingExact` PASS；生产 SendMessageInternalAsync 的 GetResponseAsync 分支，TCS 放行固定回答，精确全文相等。仅文章提问组合，未把六条业务入口的非流式笛卡尔积全算覆盖。 |
| 生产全文复制 | `RealCopyExactUnicodeMarkdown` PASS；UIA `ProductionCopy` 对应真实 AIFooter Button.Command==vm.CopyAnswerCommand，真实生产 CopyAnswer 写剪贴板，精确比较包含 emoji、预组合 é、组合 é、Markdown 代码/表格/链接的原始字符串。截图在复制之后取得；通知是记录适配器，所以截图**不证明生产 toast**。 |
| 实际输入和流式并行 | `RealInputAndDispatcherDuringStreaming` PASS；对产品 InputBox 用 send-input 输入 fixture question + Enter，实际 OnInputBoxPreviewKeyDown→SendQuestionCommand；40 次生产流式通知期间向真实模型 ComboBox 发 Home 键，最终 UIA 点 Cancel。InputBox 在生成时按产品设计隐藏，因此没有声称生成时仍向该隐藏输入框打字。 |
| 当前代 Generating/Progress | 上述 LateOld、StalePreparation 断言逐次比较当前文本和原进度，要求 Generating=true；最终六条路径要求 Generating=false/Progress 清空。Discard 缺陷单独保留，不以其它通过抵消。 |

## 保留的生产缺陷

最小执行顺序（本轮实际执行，不是猜测）：

```csharp
await Source(false);                         // 生产 InjectArticleAsync 完成
var p = Services.Plan();
var task = vm.SendQuestionCommand.ExecuteAsync("Discard");
await p.Entered.Task;
await p.Send("pending");
await Changed(() => vm.TempResult == "pending");
vm.DiscardCommand.Execute(null);             // 生产方法
p.Fail(); await p.Disposed.Task; await task;  // 明确迟到异常/旧 VM 任务完成
// TempResult=="", FinalResult=="", IsGenerating==false, ErrorMessage==""
// 但 ProgressTip=="Generating"，DiscardClearsProgress 失败。
```

根因：`AIViewModel.cs:180` 的 Discard 取消并置空当前 CTS、Erase、清请求并设置 IsGenerating=false，但没有清 ProgressTip；旧 AskArticleQuestionAsync 的 finally 在 `AIViewModel.Chat.cs:312` 受 IsCurrentGeneration 保护，已失去所有权，不能补清。Cancel 在 `AIViewModel.cs:213` 明确清 ProgressTip，所以 Cancel/Clear 通过。此守卫阻止旧代修改新代本身是必要的；本验收不替生产决定修复方式。

严重性：命令级状态一致性错误；正常 AIFooter 丢弃按钮在生成中禁用，本轮没有证明普通点击可触发。没有认证 full app 场景的可达性证据，也没有历史对照证明是 #46 新引入。未改生产、未重跑失败用例直到变绿。

## 视觉、输入和性能范围

八张 PNG 全部逐张打开原图检查，均为完整目标窗口：`00-ready`、`article-streaming`、`article-final-copy`、`video-final`、`input-ready`、`input-streaming-stop`、`stopped`、`done`。截图/UIA JSON 同目录保留；每个 capture checkpoint 在真实 panel Loaded、正尺寸及布局检查后挂起，模型数据/命令完成另有屏障。最终文档的同步检查是 MarkdownTextBlock.Text 和正高度，**不是对所有内部表格/代码子布局逐元素就绪的强断言**；完整表格/代码/链接/尾部由实际 PNG 人工确认和 UIA 内容辅助，不能推广到所有异步资源。

文章和视频最终截图均包含来源、标题、粗体、组合字符、表格、代码、链接和 FINAL-Ω；流式截图含原始 `#`。`input-ready` 名称是屏障名，截图在输入 Enter 后取得，因此显示 Generating 和 fixture question，并非仍为空框。长流截图显示视口约第 0–26 行，其余行在 ScrollView 下方，不属于回答裁剪丢失。停止后进度及回答消失，实际卡片和输入恢复。未见本轮原图/展示内容分歧，无需用推测替代像素判断。

40 个样本为工作线程提交低优先级 dispatcher callback 到回调完成的耗时，每个样本前等待一轮生产 TempResult 更新。最小 0.0561 ms，中位数 15.2928 ms，nearest-rank P95 24.6951 ms，最大 30.1205 ms。样本原值在 events.jsonl，verify.ps1 独立重算。它是 **Debug JIT、当前构建、短合成流、含 UIA/截图干扰的 dispatcher 往返代理**；不含逐键起点时间，不能叫输入响应分布、帧耗时、GC pause、优化收益或合格预算。没有改写历史 VM 算法做假对照。

## 构建、复放、原始证据

WinApp CLI 0.7.1、本地桌面。Sandbox feature-disabled 与 AOT 工具链 blocked 均未重试；未安装环境或新增正式依赖，未跑 403 全套。

```powershell
./docs/performance-acceptance/harness-46-vm/prepare.ps1 -SourceRoot $PWD -Destination <fresh-temp-directory>
dotnet build <fresh-temp-directory>/Harness.csproj -t:Rebuild -p:Platform=x64 -v:minimal
$launch = winapp run <fresh-temp-directory>/Harness.csproj --arch x64 -p Platform=x64 --no-build --detach --json --output-appx-directory <fresh-layout> | ConvertFrom-Json
./docs/performance-acceptance/harness-46-vm/run.ps1 -AppPid $launch.ProcessId -Layout <fresh-layout> -Evidence <fresh-evidence>
./docs/performance-acceptance/harness-46-vm/verify.ps1 -Evidence docs/performance-acceptance/evidence-46-vm
```

真实路径在 [provenance.json](evidence-46-vm/provenance.json)。当前宿主最终构建 `build-d2.log` 和结束 clean Rebuild `clean-rebuild.log` 均 **exit 0，66 warnings / 0 errors**；警告保留，含 MVVMTK0045/WinRT AOT 警告，未屏蔽或假称专用 WinUIAnalyzer 已运行。干净重建产物与实际启动 layout 的 Harness.dll **SHA256 完全相同**：`7EA488FD1EFEC48FA0C98B4B5302E442E1EB0BEE28AD0E0446C3430912F00E1A`。源文件、四份 XAML、fixture、DLL hashes 见 provenance。它是 source-linked 测试宿主 DLL，不是完整 BiliCopilot.UI 发行 DLL。

UI 批处理历史 `run-d.log` 退出 0 只代表 UI 步骤/清理成功，不能代表业务断言通过。新增独立 verifier 已执行，**exit 1**，明确 37/1 断言、9/1 场景。提交版 run.ps1 最后调用 verifier，并检查 wait-for 的 timedOut JSON；这两项 runner 收尾修正没有重跑桌面矩阵，原日志不覆写。PowerShell 语法检查另行执行。认证通过状态从未改动。

前置失败保留：prepare 首次误处理 XML 注释节点；首次 run 缺 Platform=x64 只返回无 AppxManifest（未启动）；PID 40308 缺少 AI kernel 初始化；PID 57192 缺 keyed OpenAI 服务。栈分别保留在 startup-events/startup-c-events。随后修复仅限宿主组合根，最终 PID 46968 完整跑完。未用旧编译产物冒充当前源代码。

## 未覆盖和交还

未覆盖：认证真实 AI/provider 协议/收费服务；实际 AppViewModel 通知视觉；完整主窗口/导航/关闭组合；生产 AIOverlayPanel 动画；全部快捷卡片/MorePrompts 的物理点击；流中通过禁用按钮触发 Discard/Regenerate（本轮只有命令级）；模型/供应商切换竞态；缓存字幕复用分支、缺字幕、空评论、空/纯 HTML 文章、当前代异常/空模型响应、空问题、提示文件缺失写入、配置为空/无服务/无模型、持久设置及失败通知；全部六条入口的非流式组合；网络图片/封面、主题/窄窗/高对比/RTL、文本物理选择和外链导航；长时压力、全应用性能和 Release/AOT。六条主要生成方法有运行证据，不意味着其每个分支都覆盖。

WinUI 审查发现的附加可维护性/可访问性范围：产品 footer 图标按钮没有稳定 AutomationId，本宿主只在实例上按真实 Command 引用附加测试 ID，未修改 XAML；不能据此签产品完整 accessibility。当前实测未发现新的 Markdown 丢内容问题；Discard 失败与旧 Markdown 证据分开记账。

未读取/复制凭据、未登录、未调用真实 API；剪贴板仅由生产 CopyAnswer 写合成字符串，之后精确比对，退出前仍等于本轮字符串才清除，未读取/保存原剪贴板。`clipboard-clear=true` 已记录。所有 owned PID 40308/57192/46968 均不存在，专用 `BiliCopilot.Acceptance46VM8B8F` 包已移除，`yield.json` 明确 `released:true`。没有操作 PID 9608、Dev/生产包或其它窗口，没有子代理、push、PR/issue 操作。**桌面独占槽已释放。**
