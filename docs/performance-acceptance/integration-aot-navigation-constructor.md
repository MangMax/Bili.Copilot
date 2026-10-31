# 导航构造契约：固定 tip 集成复核

2026-10-31，fresh merger 独占父 `t3-1d19cc80`，无子代理。起点 `8994fed85c6f55c5c93dab30e2069a99d70ad608` 与 kernel `0ae5e4e000a41a07f6696746706df11c06e8835b` 均 clean。已读 AGENTS、domain 指引、WinUI code review、verification-before-completion、run-tests 技能，以及[实施报告](aot-navigation-constructor-contract.md)与[原独立审查](aot-navigation-constructor-review.md)。不存在仓库 run-tests overlay；RC3 SDK、xUnit 2 / VSTest 命令由 global.json、测试项目与 props 确认。

固定候选 **`9bc9648499a043a4dc81293c09d24cedd250751e`** 包含原 `d58342cbe0d4a391b90aa159d9192c80afbc6f3a` 和父级包装小修。独立静态复核无新增阻断后，`git merge --no-ff` 合入 **`f721b9bc075df03772a1eb1900e0700301053289`**；双亲为指定起点与固定 tip，无冲突，合并 tree 与固定候选相同。合后仅补本轮文档、验证脚本及证据，未再改生产或测试代码。

## 异常与状态复核

factory 的 catch 仅围住注册页面 Type 比较及直接 new；构造失败抛出 TargetInvocationException，保留原异常引用为 InnerException。未知类型 ArgumentException 在 catch 外。两个 frame 的 SetParameter、Main 的 Initialize、动画、缓存插入/Move、可见性与 CancelLoading 均在 factory 之外，不会被误包装。构造失败在 frame 状态写入前发生。

新增回归在 Main 已显示 Moment、Overlay 已显示 Search 后，向 Popular 注入同一个异常；Assert.Same 独立检查两个包装的 InnerException、两个当前页 identity，并检查可见性、叠层单项栈和清除故障后的正常导航。AsyncLocal 故障值在当前测试逻辑上下文内，finally 清除，不再是影响并行测试的普通静态 Exception；它会按正常 ExecutionContext 规则流入自身子任务，不承诺任意子任务隔离。生产无故障注入钩子。

只读解析候选 `artifacts/navigation-wrapper-fix/red.trx`：1 Failed，明确为 Expected TargetInvocationException / Actual InvalidOperationException；green.trx 为23 Passed。该历史红绿的哈希单独记录，不冒充本轮重现 red。本轮从合并源码构建并运行完整套件：**Passed: 510, Failed: 0, Skipped: 0**，510 个唯一 executionId，23 项导航结果全部通过，包含上述包装/identity/状态回归。

9 个菜单页与8个叠层页的生产调用集合重新扫描，与17分支目录相等。空 Type 早退、缓存精确类型匹配、主页重访 Initialize、叠层置顶与返回行为未变。未知类型的封闭目录边界是已披露的行为变化，不声称任意 Type API 兼容；服务层设置/窗口状态可能先变化，也不声称导航服务事务性。

WinUI 静态审查未发现本片新增线程、绑定或生命周期违规；不增加页面单例/事件订阅，无 XAML 修改。专用 WinUIAnalyzer 未安装/运行。适配器页统一实现三个接口，与实际页面有差异；这些测试不覆盖真实 DI/XAML、Loaded/Unloaded 或动画。

## 本轮命令与证据

在父根目录执行；完整日志与 TRX 在 ignored `artifacts/navigation-integration`。本机既有媒体资产可用，无下载、安装或复制新依赖。Debug 与 Release 顺序构建；完整 ILC 输入冻结后不再重编，避免 CodeView 绝对 PDB 路径争用。

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --logger 'trx;LogFileName=all.trx' --results-directory artifacts/navigation-integration/tests
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -r win-x64 -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:AppxPackageSigningEnabled=false
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -r win-x64 -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:AppxPackageSigningEnabled=false
dotnet publish docs/performance-acceptance/evidence-navigation-aot/Probe/Probe.csproj -c Release -r win-x64 -o artifacts/navigation-integration/probe
./artifacts/navigation-integration/probe/Probe.exe
New-Item -ItemType Directory -Force artifacts/navigation-aot | Out-Null
python docs/performance-acceptance/evidence-navigation-aot/freeze-ilc.py integration ../t3-0ead9a3c/artifacts/aot-payload-verification/audit-ilc-isolated/original.rsp ../t3-0ead9a3c
& C:/Users/hp/.nuget/packages/runtime.win-x64.microsoft.dotnet.ilcompiler/11.0.0-rc.3.26522.148/tools/ilc.exe '@artifacts/navigation-aot/full-integration/audit.rsp'
python docs/performance-acceptance/evidence-navigation-integration/verify.py
```

输出目录须新建；exit.txt 为每条实际命令退出码记录，不能将完整 ILC 的预期非零改成通过。[验证脚本](evidence-navigation-integration/verify.py)校验 TRX、历史红绿、源码及冻结输入/PDB 哈希、目录覆盖与逐条诊断；[结果](evidence-navigation-integration/validation.json)绑定本轮源码与日志。

首次 freeze 调用因父 `artifacts/navigation-aot` 不存在而在 mkdir 阶段退出1；创建父目录后成功冻结348项。这是验证准备失败，未启动 ILC，不计为导航 red。

Fresh Debug / Release x64 普通构建均 exit0、0 warnings / 0 errors，保留项目原有分析器及 warning 策略；普通构建成功不等于未抑制 AOT 审计通过。局部 Native AOT publish exit0、无 IL 诊断，native Probe.exe 运行 exit0，17 类型的首次构造、复用、参数和叠层返回通过。Probe 未新增 root；它不专门注入构造异常，异常 identity/状态由本轮 source-linked xUnit 回归验证，不能说 native probe 覆盖了故障注入。

**修补后的源码已 fresh 重跑完整 ILC，exit1。** 使用正式 NuGet Mpv，348项输入独立冻结；rsp 移除 nowarn，保留 -O、warnaserror、全部原 feature/runtime 开关和 roots，singlewarn:false。完整输出仍是 SharpGen 2条、Azure 4条分析错误，以及正式 Mpv UseSegoeMetrics getter/setter 两条 will-always-throw，全部8行与旧 green 逐条相等，无额外输出；MainFrame/OverlayFrame 的 IL2067 均未再出现。退出后348项原输入/冻结输入及全部可用 PDB sidecar 哈希再次相等。[本轮诊断原文](evidence-navigation-integration/full-ilc.txt)和 validation 中的哈希绑定新源码；旧8→6红绿保留其原候选身份，本轮只声明新源码重新得到6条，未重跑 baseline red，不倒签旧日志。

## 边界与剩余门禁

**真实独立 AOT 身份的首次导航、重复导航、叠层返回仍待实机验证。** 无桌面 native probe 只验证链接的真实 frame/factory 控制逻辑及适配器页面，不执行实际 WinUI 构造函数。正式 Mpv 缺失成员、其余6条 IL、历史严格364编译诊断（309属性+47命令+8其他）、kernel CsWinRT1030/unsafe 前提、Vulkan分发及性能门禁保留。364 是历史证据，本轮未重跑或宣称解除。全应用 AOT 正式采用仍 HOLD，#49 FAIL 不变。

未新增 warning suppression、程序集 root、DAM All 或正式依赖切换；项目/props/rd.xml/gitlink 与父基线无差异。未 UI、部署、注册、登录、安装、UAC、push、PR 或 issue 操作；kernel 保持指定提交 clean/local-only。
