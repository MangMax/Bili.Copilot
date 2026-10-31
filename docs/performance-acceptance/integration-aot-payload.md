# RC3 AOT payload 固定 tip 集成复核

2026-10-31，fresh merger 在父 checkout `t3-1d19cc80` 独占写入，未启动子代理。

起点 `feat/performance-45` / `0ab128c84c4265e1ca5947df8d1d6cb3f99f6428`，根与 kernel 均 clean。先读 AGENTS、domain 指引、WinUI review/packaging/verification 技能及完整 [实施、独立审查与父修正记录](aot-payload-and-compatibility.md)。

固定 `t3/review-aot-payload-compatibility` tip 为 **`bba3eb59f3133fcb97e9f11f94de65077b133261`**，最后提交为 `fix: verify Mpv candidate postimages across checkout line endings`。确认 `b91d634d5d91d44ee1982b30ed1449103001c055` 实施及 `ffbbb1d28ed50621feeeef178414ec30b9cbeb86` 独立审查均为其祖先。仅以 `git merge --no-ff <固定 SHA>` 合入，merge **`f9cd2a8e0f53820a3f7dc295a202632e793fa369`**；双亲分别为起点与固定 tip，无冲突，合并树与固定 tip 完全一致。未合引擎 B1 分支。集成后的额外提交仅增加本报告、复核证据及 status 账本。

## 小修的独立复核

在父 ignored `artifacts/aot-integration-fresh/mpv-source` 新目录，从冻结 bundle clone/checkout 精确 `c1ef0f403097129c3b6db4c466cc4147054471d2`。执行当前 `rebuild-mpv.ps1` 的 clone/checkout/apply/hash 前缀，未执行 build/pack 后缀。受控 `git apply --ignore-space-change` 后两份完整 postimage UTF-8/LF SHA256：

- `src/Directory.Packages.props`：`5620D9A9D7C9D604C53638876C4228C1339798AC6C8D72395C5CD36F00F3B88D`。
- `src/MpvKernel.WinUI/MpvKernel.WinUI.csproj`：`A220DF3703D0304BFE3EE3D78795F8A9562E61D4D19CBBFA5BE0421D7372BBF0`。

逐文件与原实施候选源码归一化比较相等，clone 的 diff 也只有这两文件。分别追加内容偏离，实际脚本 hash block 均抛出 `Candidate postimage mismatch`，随后还原 clone 原字节并再次通过完整校验。冻结 patch、上游源和原候选均未改。两条原有 whitespace warning 保留于 ignored recovery.log；原默认 apply 失败的 red 仍在祖先与 evidence-aot-review 中。

复算原 nupkg、包内 DLL/PRI hash 与审查 recovery.json 一致，nuspec Player 仍 `0.0.2-preview2`。这是**恢复前缀与冻结包身份复核**，不是重新 pack，更不是可字节复现包或新实机通过。正式依赖未切换，本地候选未发布。

## 合并后验证

[新断言结果](evidence-aot-integration/validation.json)、[恢复结果](evidence-aot-integration/recovery.json)、[语法文件清单](evidence-aot-integration/syntax.json) 已提交。验证命令见 [PowerShell](evidence-aot-integration/verify.ps1) 和 [证据断言](evidence-aot-integration/assert-evidence.py)；脚本使用父 ignored 输出，重复恢复需新目录。先执行原独立 `evidence-aot-review/recompute.py ../t3-0ead9a3c artifacts/aot-integration-fresh/recomputed`，再验证断言；大产物仍在原实施工作区，不进入 Git。

- 合并新增 XML 与 PowerShell 全部解析通过；生产及 ps1 `git diff --check 0ab128c8 HEAD -- src '*.ps1'` 通过。全证据 diff-check 仍报告既有 coreclr 日志、FluentIcons 摘录、原 patch、patch-whitespace-check 的空白，不清洗取证原文，不声称全证据零警告。
- 使用实际 RC3 `11.0.100-rc.3.26522.148` 在父项目进行只读 MSBuild `-getProperty` 四模式求值，未 restore/build。Debug 不运行新增 payload target，deps/runtimeconfig=true、Optimize=false；显式 Release JIT 不运行新增 target，deps/runtimeconfig=true、Optimize=true；AOT `PublishAppxPackage=true` 满足两 target 条件，deps/runtimeconfig=false、Optimize/分析器/WAE=true；legacy `GenerateAppxPackageOnBuild=true` 的 PublishAppxPackage=false，新增 target 不覆盖。对应 properties JSON 已提交。
- 原 MSIX SHA256 `7DE8C7FA5FC8265C00A9EAA24E9DA60E3908B9D13B7DAD46A5C3CC68A31D6320`、167942971 bytes；284 条目、194 个 publish 文件按路径/hash 相等、13 PE 全 native x64，无 managed PE/缺失/不匹配。应用与框架 PE import 重新解析仍确认媒体直接依赖外部 Vulkan loader。
- 原 TRX XML 重读：487 个不同 executionId、487 Passed、failed/notExecuted=0；这是既有回归证据复核，非 fresh 测试。五项源/hash 均重新吻合。诊断原文复算 309+47+8=364、IL2067×3/IL2072×1/IL2026×2/IL3050×2=8。CsWinRT1030 原 red 和 Mpv 两条 will-always-throw 不被这些计数抹除。
- 原 closed-publish exit=0、ILC/link→Prepare→Use→payload 顺序及审查摘录交叉读取；本轮未重放 ILC。无冲突或新增行为变更，故未重复四分钟编译或487套件，未产生新 build/UI PASS。WinUIAnalyzer 未运行。

## 集成结论与剩余门禁

**工具链已经成功工作，本地 Mpv 候选 Native AOT MSIX 已产生；正式 AOT 尚未通过。** 原“工具链 BLOCKED、没有 native EXE/MSIX”只适用于旧 SHA，不能继续作为当前阻塞原因。可合入的生产范围仅这 34 行发布编排，限 RC3、完整 `dotnet publish` + `PublishAppxPackage=true` 并继续扫描实际产物。DebugSymbols 实际先收集后按 OutputGroup 排除，不宣称从未收集；未增加 NoWarn 或改变正式优化/依赖。

正式 Mpv 缺失成员兼容性、严格未抑制 364+8 及 kernel unsafe 生成前提、候选完整严格审计、Vulkan 来源/许可/分发、legacy/ARM64、独立身份实机与性能仍 HOLD；#49 FAIL 保持。下一步按独立审查的单机制 ≤M 切片推进，不能用候选包成功放行正式采用。

父 kernel 始终 `0ae5e4e000a41a07f6696746706df11c06e8835b` clean/local-only；未从 child 的异常索引恢复任何内容，gitlink 不变。未部署、启动 UI、登录、UAC、修改正式依赖、发布候选、push、PR 或 issue 操作。
