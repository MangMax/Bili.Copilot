# #49 remount integration review

2026-10-30. **重挂有限实机通过；暂停 seek/切换 FAIL 仍阻断 #49 整体验收。** 本轮独立集成审查，没有重新部署、启动或操作桌面，没有子代理、push、PR/issue 写操作或发布。

## 来源与合并

父 checkout `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80`、分支 `feat/performance-45` 开始 clean，HEAD 为预期 `b9698993df3437edaeb306753455c4c4b001a347`，也是目标的 merge-base。执行 `git merge --no-ff bccc36b177a6792eb00caaae468052f691cd21bb -m 'Merge #49 panel remount fix and bounded UI evidence'`，无冲突，得到 `7aa05616f990183261e21295e5c9a2a70a6323de`，两父提交准确为上述父 HEAD 与目标。合并树与目标一致。

读取 AGENTS、docs/agents、winui-code-review、run-tests、verification-before-completion、code-review，及 GitHub #49/#50/#53、ui-49-remount-fix.md、danmaku-49-followup.md。按用户要求独立执行两轴审查，不采用技能的子代理步骤。无 run-tests overlay；SDK 10.0.403，net9.0 xUnit2/VSTest。

## Standards 审查

未发现本次生产差异的实质缺陷或新文档标准违规。生产差异只有 VideoDanmakuPanel.xaml.cs；未改引擎、依赖、VM、XAML 或 session 算法。构造函数自持 Loaded/Unloaded，和 #50/#53 既有模式一致。NuGet 基类首次 Unloaded 注销自己的处理器，已有 IL 及匹配 DLL 哈希支持该根因。派生类不再覆盖生命周期虚方法，避免基类和自持事件重复执行业务逻辑。

`_isActive` 使重复通知幂等；null VM 加载仍进入 active，后续 VM 赋值可订阅。卸载先关闭 active、失效 session 代次、对称解除九项 VM 订阅，再释放两个 renderer、置 null、清空容器。VM 替换失效旧重绘并仅在 active 时订阅。重绘入口与执行均检查 active/IsLoaded/current VM，回调不捕获已释放 renderer；旧代次回调不能消费新挂载的 pending action。上述依赖 UI dispatcher 串行假设，不证明引擎 worker 内部迟到工作安全。

没有新增等待、反射、业务逻辑或无关重构。专用 WinUIAnalyzer 未运行；编译器检查不能冒充它。仅修正文档，将“用户中途收窄”改为父级按现有授权限定本轮范围，用户未直接作此决定；清理描述不再把过期占用表述为 yield 返回 true。

## Spec 审查与证据核验

范围内重挂修复可接受；原 #49 “暂停状态、当前位置、样式与弹幕内容正确”仍有已知 Error/阻断，不能宣称整票完成。

- `82dfe1c1796e3f5696de750a30cfdd1c8a339b31` 的 ui-49.md、harness-49、evidence-49-ui 与当前树逐路径 diff 为空，77 张历史 PNG 和 red 原样保留。
- 独立解析新 red-02：18 项中 8 passed/10 failed（结构 8/16）；green：24 项中 21 passed/3 failed，结构 20/20。失败准确为 DirectX-PausedSeek、Win2D-PausedSeek、Win2D-PausedSwitch。单次 DirectX-PausedSwitch 通过不消除竞态；green 只是目录名称。red/green 检查总数不同，不做性能比较。
- 阅读 runner 断言及 source-linked harness：真实控件/VM/session/NuGet 双引擎配合离线组合适配器。六次重挂、两次最终卸载有直接失败→通过证据；隐藏/显示与旧 VM 隔离支持有限生命周期结论，不是完整播放器导航。
- 本审查打开两张 green/*-remount-visible.png，均可见从右侧进入的 PUBLIC VM REALTIME（部分裁剪）。仅支持第三次重挂后公开 Resume/AddDanmaku 的可见输出。其余八张由原逐图记录标为空白，不计视觉通过；本审查未重做十张全量视觉验收。
- 当前 panel/XAML SHA256 与来源清单匹配；本 checkout 的 Core/Legacy/Share/WinUIKernel DLL 四项 SHA256 均匹配部署证据。源码生产提交为 `e4c1cfb7eafac07eb5b57df9f15a60e99c54928c`。核验结果见 [verification.json](evidence-integration-49-remount/verification.json)，继承证据逐文件哈希见 [清单](evidence-integration-49-remount/inherited-evidence-hashes.json)。清单建立完整性基线，不独立证明原采集环境。
- green/events.jsonl 是累积日志，包含 red PID57036 和后续 green PID20256 两次 launch；不能把整文件都当作修复后事件。结构结论来自对应 results/runner，不用早期 red 事件洗绿。
- 读取 cleanup.json：owned PID57036/20256 remainingProcesses=0、独立包 remainingPackages=0；yield 的实际返回为 **released=false**。原报告记录 grace 已过期，无后续桌面操作；本审查未重新探测桌面占用，不能声称工具主动释放成功。

## Fresh integration checks

仅使用现有依赖和资产，未安装工具、下载/复制运行库。命令在父 checkout 执行：

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false --logger 'trx;LogFileName=release.trx' --results-directory docs/performance-acceptance/evidence-integration-49-remount
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
dotnet msbuild src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -t:ValidateRuntimeDependencies -p:Configuration=Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false -v:normal
```

全部 exit 0。[完整日志/TRX](evidence-integration-49-remount) 已提交。

- Release **Passed: 403, Failed: 0, Skipped: 0**。TRX total/executed/passed/result-entry 均 403，其他失败/未执行计数 0，四项 session 测试通过。另独立解析继承 targeted 4/0/0、full 403/0/0。它们不执行实际 panel，不能替代 WinUI 验收。
- Release 编译日志保留既有 protobuf 警告：`pgc/gateway/vega/v1.proto(10,1)` unused `google/protobuf/empty.proto` import，不能称全部编译零警告。
- Debug x64：0 warnings、0 errors，00:01:05.36。继承 app-build 的 1 个 protobuf warning 属于原实现工作区构建，与本次增量构建的零警告区分。
- runtime gate：0 warnings、0 errors，00:00:01.06；只验证资产存在，不证明原生加载/解码。

kernel gitlink 与嵌套 HEAD 保持 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`，嵌套工作区 clean。未做生产小修，后续只有报告、账本及证据改动。

## 未完成边界

Release app Native AOT 仍因既有 MSVC/linker 工具链缺失 **blocked/未验**，未重试或安装。完整播放器、真实视频时钟、账号/API、长时间高密度负载、性能增益、暂停可见静止首帧及完整视觉矩阵未验。

[后续方案](danmaku-49-followup.md) 的引擎内部最新播放意图、旧操作代次隔离与关闭保护尚未实现/验证；可等待 API 仅在具体需求下考虑，不能代替内部正确性。该方案没有引擎或 NuGet 发布授权，不能更新依赖或发布。标准轴无新增生产缺陷；需求轴保留暂停正确性的阻断，合入本修复不改变此结论。
