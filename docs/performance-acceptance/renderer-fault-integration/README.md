# Renderer 单 panel 故障隔离：本地集成

2026-11-01，父工作区 `t3-1d19cc80`。开始核验 HEAD
`ad4d17d3a521cf2930caca247a69757f356b056e`、kernel
`0ae5e4e000a41a07f6696746706df11c06e8835b`，两者 clean；使用父已恢复的
kernel 元数据，未再次恢复或切换 kernel。

固定候选 `ef802ad6ad8dc274ad415475bc5f574ac3d78991` 和独立 review
`b6d323d6238e37d53f4f4378df6673bc8bd0961c` 均直接基于上述起点，依次 `--no-ff`
无冲突合入。生产 merge：`0e2458e8d1ca07095038674dc10cc3b45559e809`；
review merge / fresh 验证 HEAD：`9990a68893e13974a94fd1f29321e17261f23cdc`。
最终报告提交 SHA 见交付消息。

生产差异仅 `VideoDanmakuPanel.xaml.cs`，src/tests 与固定候选一致。公开 Legacy
WorkerFailure 的当前实例终态在 UI owner 上一次记录、退役；无关异常继续传播。
普通重挂、同 VM 换集、刷新/样式改变不能恢复；新 VM 可恢复，清理失败要求新 panel。
不自动重试，不扩修 Core、VM、播放器或全局异常处理。三包版本
`0.0.1-seekd1fc.20261101.2`、正式依赖、NoWarn、kernel gitlink 不变。

## Fresh 验证与来源

使用已安装 `C:/Program Files/dotnet/dotnet.exe`，固定 SDK
`11.0.100-rc.3.26522.148`。本目录 `verify.py` 将每次命令、UTC 时间、退出码、
源码和输出哈希写入独立 artifacts；精确参数见归档 provenance.json。

```powershell
python docs/performance-acceptance/renderer-fault-integration/verify.py Panel PanelFresh
python docs/performance-acceptance/renderer-fault-integration/verify.py Full
python docs/performance-acceptance/renderer-fault-integration/verify.py Release
python docs/performance-acceptance/renderer-fault-integration/verify.py RuntimeGate
python docs/performance-acceptance/renderer-fault-integration/audit.py
```

每个验证目录只允许新建，复放应使用新的第二参数。Panel/Full 使用独立
`--artifacts-path`，重新 restore/build/test。Release 使用全新 BuildOutDir 执行
Rebuild，PublishAot=false、关闭签名与生成包；依赖缓存复用本区此前
seek-responsive-integration/packages，不称本轮隔离空缓存还原。

结果（全部实际新执行）：

- PanelFresh：**Passed: 12, Failed: 0, Skipped: 0**；本轮日志无编译 warning。
- Full：**Passed: 577, Failed: 0, Skipped: 0**；TRX 逐条结果及名称集合与历史 577 一致。
  构建日志有 1 个既有 protobuf `google/protobuf/empty.proto` unused-import 警告。
- Release x64 JIT Rebuild：**exit 0，1 warning / 0 errors**，MSBuild 用时 **00:03:28.28**。
  警告同为上述既有 protobuf unused import；NETSDK1057 是预览 SDK 提示。
- ValidateRuntimeDependencies：**exit 0**，无警告/错误；libmpv、ffmpeg、BBDown
  输出与输入哈希一致，resources.pri 非空。这是文件门禁，不证明实际 native 加载。
- 三个固定包在 assets/deps 中版本相符，输出 DLL 与已提交 nupkg 内字节一致。
  完整源码和输出哈希复核通过。全套测试 PDB 168 文档中 128 份可读、应用 PDB
  2566 文档中 1297 份可读，全部匹配，应用包括实际 panel；DLL/PDB 身份匹配。
  不可读取文档不宣称字节核验。本轮未安装或运行专用 WinUIAnalyzer。

小证据见 [evidence.zip](evidence.zip)：包含精确命令/原始日志/TRX、首次命令失败、
来源和输出哈希、PDB、恢复输入及归档核验结果，附逐文件 manifest。大型输出/binlog
保留在 `artifacts/renderer-fault-integration/`，无 Temp 依赖冒充存续核验。

首次 Panel 命令误加 `-t:Rebuild`，被 dotnet test 解释为 list-tests 选项，
MSB1008 / exit 1，未执行任何测试。原始 command.log/provenance 原样归档。
修正调用后在 PanelFresh 全新目录构建运行，未修改生产或测试代码。

本区起初缺 `Assets/libmpv/x64/libmpv-2.dll` 与 `Assets/ffmpeg/x64/ffmpeg.exe`。
只读 `t3-c10467bb/artifacts/home-aot/fixture/Assets` 的仍存副本，复制至本区 ignored
资产目录并逐字节 SHA256 核对，记录原缺失、来源、长度和哈希。未下载、安装、
覆盖用户包或改动源工作树。Temp 外部清理后的历史产物不假称完整；本轮所需
测试/Release 输出均新建，缺失输入恢复记录与历史产物可读性记录分开保留。

## 历史证据与限制

固定 [候选报告](../renderer-fault-isolation/README.md) 和
[独立审查](../renderer-fault-review/README.md) 原样保留。候选 README 有既有
GB18030/UTF-8 编码不匹配，本轮未扩修。原 evidence.zip 的 88 项长度及 SHA256
重新核验；ZIP SHA256 为
`e0fcfd865fc5df6c2fd93f1bba9e472c85edcb4a189436f0d40efbc7b8759955`。
独立审查 audit 在本区针对归档与本轮 PanelFresh 复放，输出另存，不覆盖原 review。
两轮历史 native-inputs 所列 26 个文件仍可读取，当前 SHA256 全部匹配；这仅为
原文件复核，非重新执行 native/AOT，也不代表所有历史 Temp 输入仍存在。

保留原 parent red 4 failed / 1 passed。原 AOT `native-final` / `final` 恢复断言
仍为失败：27 home、31 media/danmaku PASS、1 RecoveredSeek FAIL；exit 0 不能抹掉
行为断言 red。fixture 等待异步 position/progress 后的 `verified` 为历史
27 home + 32 media/danmaku PASS，本轮未重跑。`native-fixed` 是未执行中间产物。

历史两次完整 home fixture 各有 6 次全局 TemplateRoot 异常及既有离线分区/图片错误；
终态退役之后全局异常为 0 不代表全 App 零错误。controlled.jsonl 的 managedUnhandled
计数不是全局计数。退出 WER 门禁仅为历史有界观察；yield 的 released=false 仍保留。

历史 Caa6a7be 注册从前快照到后快照消失，原因未知，不能归因用户或称保护状态全未变。
更早 AotDev 2.2611.1.1 到启动前 2.2611.1.2 的变化同样保留。本轮未查询或操作
用户注册、进程、凭据，也未恢复或重启任何保护包。

仅完成已审单 panel 隔离的本地集成与 headless/JIT 文件验证。未执行 UI、安装、
应用启动、用户包操作、native 发布、push/PR/issue 或其它 worktree 清理；未启动子代理。
不宣称全面性能/AOT通过，不以离线 fixture 或 JIT build 代替签名最终用户包验收。

集成证据 ZIP：27 个 manifest 条目，SHA256
`41616fef838d83517407d552e01ebaf3f9aca0093f06af491c948f1707fec73b`。
