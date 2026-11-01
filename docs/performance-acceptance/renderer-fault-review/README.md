# ef802ad6 独立审查

结论：**可合入固定提交 ef802ad6；可进入最终用户包打包验证。此审查不等于签名用户更新包已验收。** Standards：0 项阻断；Spec：0 项阻断。没有要求扩修 Core 或泛化设计。

审查目标 `ef802ad6ad8dc274ad415475bc5f574ac3d78991`，直接父提交 `ad4d17d3a521cf2930caca247a69757f356b056e`。来源工作区在审查前后仍为该目标提交，仅显示既有 kernel 修改。本审查区开始于父提交；kernel HEAD 为 `f02c33f639addd5d421700c43fa66382beafe323`，初态已有大量 staged deletions，未还原、构建或修改它。目标 gitlink 仍为 `0ae5e4e000a41a07f6696746706df11c06e8835b`。

使用 code-review、winui-code-review、run-tests、verification-before-completion 技能；用户禁止子代理，故两轴均由本 reviewer 独立完成。生产 diff 仅 VideoDanmakuPanel。未改源区、父区、依赖、gitlink，未启动 native host、操作注册、进程、凭据、push、PR 或 issue。

## Standards

没有实质性阻断。核对全部 panel 调用路径及固定包内 Legacy XML 公开的 `DanmakuFrostMaster.WorkerFailure`，未发现新增产品反射或全局 catch-all。

- `WithRenderer` 的普通调用过滤只接受当前 Legacy 公布的同一异常对象或以其为 InnerException 的 InvalidOperationException；并发出现无关 ArgumentException 的测试仍向外传播。
- 已知终态先设置失败标志、失效 redraw，再清空字段并 Close。只在已经故障的清理范围记录清理异常，保留异常对象，并永久禁止此 panel 恢复。普通未故障 Close 的异常不被该过滤器吞掉。清理 catch 的宽度是此限定恢复规则所需，不是任意 renderer 调用的吞错。
- 后台输入在 UI dispatcher 执行前检查 active、VM 和两种 renderer 引用。排队旧 VM、卸载重挂和 renderer 替换用例覆盖资源边界；Loaded/Unloaded 由 UI 生命周期调用。终态 redraw 被失效，替换中 Close 才发布故障也不会创建新 renderer。
- 不扩修没有相同公开协议的 Core。没有改播放器、VM、全局 App 异常处理。

未另外跑 WinUI analyzer/整机 native 历史；本次编译有测试 adapter 的 CS8604 警告，未称零警告。

## Spec

没有实质性阻断。一次记录/退役后 progress、seek、style/speed、pause/resume、add/clear、hide/show、redraw 不能复用旧 renderer。新的 VM 可恢复；相同 VM 的换集、刷新列表、重挂不重置失败位。Close 失败后即使换 VM 也不恢复，需新 panel。主媒体由真实 AOT 的 `FaultMediaProgress`、`FaultSubsequentSeek`、`FaultMediaContinues` 及恢复后 28 秒进度证据覆盖。

## 独立复放与归档核验

从 Git 固定提交导出生产 panel、redraw session 和测试至本区独立 artifacts；未链接脏 kernel。命令：

```powershell
& ../t3-d48c749d/artifacts/dependency-upgrade/sdk11/dotnet.exe test artifacts/review-ef802ad6/source/tests/BiliCopilot.DanmakuPanel.Tests --logger 'trx;LogFileName=review-panel.trx' --results-directory artifacts/review-ef802ad6/results
python docs/performance-acceptance/renderer-fault-review/audit.py
```

独立复放 **Passed: 12, Failed: 0, Skipped: 0**。原测试已有并发异常、排队、清理失败、替换和重挂的小边界，未新增重复测试。附 replay.trx、replay.log 和机器核验结果。初次全仓导出撞 Windows 长路径，改为所需文件的定向导出；该准备失败不是测试失败。

只读原始 evidence.zip SHA256 `e0fcfd865fc5df6c2fd93f1bba9e472c85edcb4a189436f0d40efbc7b8759955`；独立核验全部 **88** 个 payload 的长度与 SHA256。归档 fresh full TRX 是 **577/0/0**，本次没有重跑它。归档 source audit 记录 1,084 个输入、仅 panel 差异，kernel 474 文件来自精确 gitlink。独立导出 panel SHA256 为 `96547fb549b4bccbc7aa998dbe1ed9f57dadfa3fedcfefc7c9c04293d8923e7c`，与最终源码/fixture receipt 一致。

核对 ILC response 中三个 `seekd1fc.20261101.2` 和 MPV WinUI local AOT 包路径；三个固定提交 nupkg 字节 SHA256 与 dependency audit 一致。MPV 的 DLL/cache/feed 链、kernel 全导出比较采用归档 receipt；按限定来源未重新读取源区 payload/binlog 或重建整个链。因此 native EXE 的 PE、加载和 SHA 校验是归档交叉核验，不冒充本 reviewer 重新执行该 EXE。

`native-fixed` 是未执行中间产物，不计最终验收。`native-final` 对应 `final`：27 home，31 media/danmaku PASS、1 FAIL。保留 `RecoveredSeek` 首次失败，36536.427 ms 断言早于 36601.048 ms 的实际 media=28 事件（约 65 ms；README 的 66 ms 为近似）。fixture 后来等待最多 1 秒的真实 position 和 progress；最终 `verified` 中 33223.743 ms progress=28，33225.752 ms media=28，33245.950 ms 才断言成功。最终 **27 home + 32 media/danmaku PASS**，无 FAIL。

最终 native EXE receipt SHA256 `5b7441bae0c8d0b0de11a0b7fbc37fa056213d6cf371b005e5dcea841128cc2f` 在 native-inputs、loaded-modules 与 validation 中一致，CLR directory=0，模块记录无 coreclr/clrjit/hostfxr/hostpolicy，有真实 libmpv。两次产品运行的 terminal log 各 1；终态记录之后全局异常各 0。每次全局异常总数均为 **6**，保留 TemplateRoot 及离线分区/图片错误；baseline 原始日志已有相同签名。`managedUnhandled=0` 只描述 controlled.jsonl，不代表全 App 零错误。

`final` PID31476 / `verified` PID48252 均 retained handle、正常 exit=0、无强杀；退出后观察分别 31.3456 / 31.3796 秒，exact WER gate 为 PASS_BOUNDED。cleanup 中 processAbsent/registrationAbsent 均真，yield receipt 为 `released=false`（命令成功，不宣称实际释放了 lease）。不把正常退出掩盖成首次行为断言成功。

## 保护与最终打包边界

核对 runner：只卸载精确 `Mang.BiliCopilot.HomeRegression.T5df5.final` / `.verified` 名称且 InstallLocation 匹配 owned layout 的 PackageFullName；没有通配移除用户包。归档前后 protected PID47980 的创建时间和路径相同；官方与 AotDev 包身份/路径相同。但是前快照中的 **Caa6a7be 注册在后快照缺失，原因未知**。更早 AotDev `2.2611.1.1` 到 launch 前 `2.2611.1.2` 的变化也不能报告为全程未变。现有脚本与 receipts 不能确定失踪归因，不能归因用户；本次未恢复或重启任何保护包。

最后打包准入：该 panel 修复与现有固定依赖可合入并进入最后打包；验收证据仅适用于本次完整 UI 离线 AOT fixture。签名最终用户包仍应对实际最终输入/产物留存来源和退出验收，不能用 fixture 成功替代用户升级安装验收，也不能签保护状态全未变或全局零错误。

README 字节核验：Git blob 与导出文件均无法严格 UTF-8 解码（offset 2），开头可按 GB18030 解码。因此此次 Get-Content 乱码有实际编码不匹配依据，不作为 renderer 缺陷或扩修事项。
