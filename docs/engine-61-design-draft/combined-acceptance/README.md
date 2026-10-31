# 双引擎组合：离线准备完成，实际组合验收待恢复桌面

固定父 `de41a77eb46ca5d6e01cbbe78d9aa0cac11429bf`。本区未启动子代理、未改正式依赖/gitlink、未写父或来源区，无 push／PR／issue／发布。

**结论：包组合与完整应用构建可进入下一轮实际验收；尚不能签署组合运行准入。** 用户在构建期间要求让桌面给 AOT 开发版安装任务，本任务当时尚未启动应用、注册身份或发送 UI 输入。不存在需要清理的本轮 owned 应用；桌面从未占用。后续保持不发 UI 输入，未取消或丢弃证据。这里的 `PENDING_NOT_EXECUTED` 不是新产品缺陷，也不是通过。

## 固定唯一组合

| 包 | 版本 | DLL SHA256 |
|---|---|---|
| Core | `0.0.1-pxdpi.9dc8c6c9.clean` | `b15225ecec93b08943ecdbf889c74bebe8873e7851b43cc3c1feb98fc84f97fd` |
| Legacy | `0.0.1-reconstruct0b4f.final` | `2cc03ebaf8557cbf0abb96344276af717ba7a756541e0223a29a353313adfabf` |
| Share | `0.0.1-reconstruct0b4f.final` | `0541962accff2ca6823213b5e52449a744f0378dbf61492c82a04a45abfe8329` |

Core 对应引擎 `698d546e5c9edf5c96b6871357dc532542457c45`；Legacy 对应 `d7984643f4792133e7664c55e01df6e95e9cbc21`。直接使用原 nupkg/snupkg，不重编、不换源码、不重写包元数据；Legacy nuspec 仍保留历史应用提交来源字段，不能将它误当引擎源码提交。

两份 Share 共用相同 API 和实现。7 个原字节差异仅为 BOM／换行，逐文件规范化文本完全相等，原 hash 和差异名均封存。Core 对 Share 的 nuspec 依赖是最低版本 `0.0.1-pxdpi.9dc8c6c9.clean`，Legacy 要求最低 `0.0.1-reconstruct0b4f.final`；选择后者满足两个范围。不存在需要修改 Share 的真实合同冲突。

`package-closure.json` 记录完整应用还原后的三个唯一包节点、各目标图的依赖及 nupkg→已有 NuGet cache→应用 build DLL 相等；Core 不是正式 preview6。构建 **exit0、1 警告、0 错误**，唯一警告为已有 grpc proto unused import，没有 NU1605／NU1608，没有新增 NoWarn。使用本机已有 cache，不声称干净机器还原。

`metadata.json` 是本轮离线 PE 检查：三 DLL 的 MVID、CodeView／Portable PDB 配对及全部文档 checksum 成立，原 PDB 已配到本区 build。没有执行 native 入口、dump、外部进程内存或逆向。实际进程加载路径尚未核验，不能把 build 路径称为 loaded 路径。

## 应用与夹具

完整隔离副本在本区 `artifacts/combined-60cd/full`，应用基于 `44ce6cc5afd5bd503c454aaebfbee4df2e0b5ede`，固定 kernel `0ae5e4e000a41a07f6696746706df11c06e8835b` 仅恢复到该副本。父 HEAD 尚无 44ce 的显式短距／暂停 seek，不能整文件覆盖回父。speed partial 与父逐字相同；Overlay 与父差异只有 44ce 的 seek 接线，保留 speed／卸载生命周期修复。详见 `application-baseline.json`、`app-source-closure.json`。

生产 C#/XAML 相对 44ce 只有原有离线启动注入 `App.xaml.cs`、`GlobalDependencies.cs`；额外 `OfflineAcceptance.cs` 是本轮夹具。所有播放器 VM、真实 Overlay、x:Load、CloseAsync 和媒体状态走原应用代码。媒体／原宿主资源只读复制自已有 reconstruct-app，身份独立为 `Bili.Combined60cd`，不接触 Caa6a7be／Dev／AOT 新身份及登录 layout。合成账号／本地媒体不使用凭据。

夹具通过公开 VM 属性／命令切换引擎、播放与显示；反射仅在本进程读取 native renderer 行状态和年龄，不调用内部调度器，不改引擎。`OpenHeld` 使用已有离线服务 Task gate，属于明确的受控加载屏障，不冒充真实网络加载竞争。原初始 kernel HEAD/status/staged/worktree 二进制 diff 已备份在 ignored `artifacts/combined-60cd/kernel-backup`，本轮校验原字节不变。

使用 implement-spec、WinUI code review／UI testing、run-tests、verification；夹具扩展使用 code-testing，由本任务直接处理，按用户要求不执行代理编排。无 run-tests overlay／testing extensions catalog，沿用现有自有 JSON/WinApp runner；未运行 WinUIAnalyzer、dotnet test 或单引擎全矩阵。WinUI 自审未修改生产 XAML/API/同步等待；本轮只证明夹具编译与离线闭包，不证明 UI 断言或视觉结果。

## 后续组合验收清单

| Requirement | Evidence |
|---|---|
| Share/API/依赖兼容、固定原候选 | 本轮 `share-normalized.json`、`package-closure.json`、`metadata.json` 通过 |
| 完整应用构建／源码约束 | 本轮 `build-command.json` exit0、`build.log`、`app-source-closure.json` |
| 暂停 Core→Legacy→Core、隐藏／播放切换恢复 | **未执行**；`run-ui.py` 已准备场景，恢复桌面后实际运行 |
| 四模式年龄／位置／剩余寿命符合目标配置 | **未执行**；夹具记录尺寸、配置、行坐标／宽度／年龄。runner 只有年龄初始断言，仍需依据实测坐标、恢复位移与到期时刻完成位置／剩余寿命判定，不以 rows 代替 |
| 短距前后／同目标／连续 latest | **未执行**；准备真实 ForwardButton/BackwardButton 与 VM latest 场景 |
| reload／x:Load 三轮、加载中／seek后关闭与重开 | **未执行**；脚本准备三轮及独立标记的 service gate；需实际观测，不以脚本存在当通过 |
| 原像素正例＋空白／不同文本负例 | **未执行**；已准备各引擎截图命名，坐标裁剪和独立解码应按实际窗口确定。必须查看未缩放局部，不能仅 rows 或大图目测 |
| 实际 loaded DLL/PDB/source／包图闭环 | build/包/PDB/source 已闭合，**loaded 待执行** |
| retained exit、退出≥31秒精确PID/path WER、清理/yield | **未执行／本轮无应用进程**；已有 `launch-retain.ps1`、精确身份 `cleanup.ps1`，不得借用历史退出记录 |

所有既有红／UNKNOWN保持：Core 跨模式遮挡的历史图像 FAIL、AV/c000027b/UNKNOWN、device-reset、AOT、性能等均未签绿。Legacy 前缀重建最坏 O(N²)，本轮无桌面执行，既无卡顿观测也无性能通过结论；不做大 benchmark 或重构。

## 恢复与续跑

当前桌面暂停期间只允许离线命令：

```powershell
python -X utf8 -B docs/engine-61-design-draft/combined-acceptance/restore.py
```

该脚本校验 `offline-evidence.zip` 及逐文件 hash，解到新的本区 `artifacts/combined-restored-60cd`，不安装、不注册、不启动。ZIP 包含原三包和符号包、原 DLL/PDB、PDB 源文档、完整 package assets 图和 metadata 检查器源码。完整应用的准备入口为 `prepare.py` → `fixture.py`；只读依赖本机原来源路径和媒体，非可携带通用安装器。当前本区完整副本已构建，无需再次构建。

**仅在用户恢复组合桌面工作后**：将 `launch-retain.ps1` 放在 `artifacts/combined-60cd/`，以新 Run/layout 启动；保留启动进程句柄，等待 identity.json 再执行 `run-ui.py <Run>`。该 UI 脚本未实跑，必须保留首轮失败，允许只修小夹具问题。补足位置／寿命和原像素检查后，正常关闭；等待 retainedexit／31秒 WER 完成，再执行精确身份 cleanup。严禁按进程名批量退出或卸载其他身份。

初始化阶段两次夹具命令错误（PowerShell New-Item 参数未分隔、随后父目录未存在）发生于任何构建／桌面操作前，修正创建路径后成功。首次封存脚本用 UTF-8 解码某个非 UTF-8 原源码失败，改成字节级只去 BOM／CRLF 比较，同时用一次 git archive 避免逐文件启动 git；不改源码。这些不属于产品失败。没有删除历史结果或把历史红重复登记为本轮新缺陷。

本目录 `-text` 保持证据字节；默认 diff-check 会把 CRLF 报为 trailing whitespace。采用命令级 `core.whitespace=cr-at-eol` 后，仅原 `prepare.log` 中 nuspec 输出的 CRCRLF 保留告警，不改写原日志。检查脚本／文档时排除该原日志，不能将日志格式告警记作编译或产品失败。
