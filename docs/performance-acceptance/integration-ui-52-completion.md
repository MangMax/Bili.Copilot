# #52 补充验收资料集成复核

2026-10-30。独立 reviewer/merger，未启动子代理、操作桌面、部署、构建、运行 403 测试或操作 push/PR/issue。结论：资料可集成，#52 仅有限通过，不能关闭。

父分支 `feat/performance-45` 起点 clean，HEAD 为 `9608cc8f6ac2ff04324dae402161b0ed8f20e6ce`；以 `--no-ff` 合入 `c17ad971db91b55532f7bf192e95dc7d5e2ccf22`，merge 为 `d0c3ff01ae9918c1b759ae68773d36ea1e2f3caf`。`bc42983` 与原 `a69635d08fde3597c26c37fefc693502e9d028d4` 的 completion 报告及证据目录 diff 为空。新增内容限于 docs/performance-acceptance；生产、依赖和 kernel 未改。

已读取 AGENTS、docs/agents、winui-code-review、verification-before-completion，以及两份报告、实验/runner/离线脚本和原始记录。专用 WinUIAnalyzer 未运行。本次离线检查不是新一轮 UI 运行，也不是生产全套测试。

## 独立检查

- 在独立临时副本执行 `python docs/performance-acceptance/harness-52-resources/analyze.py <copy>` 和 `harness-52-resources/review-pixels.ps1 -Evidence <copy>`，均成功；不覆盖原始证据。六对、两模式、每次七边界，共 84 个样本完整，重算 summary 与原文件相等，SVG/portrait 像素检查的结构和哈希与原文件相等。核验结果保存在 [verification.json](evidence-integration-ui-52-completion/verification.json)。
- 三路 path/text 输入的 UTF-8 字节相同，Latin-1 HTTP 解码后与其等价，café 保留。三张 path 内部各 5,699 非白像素且 RGB 哈希相同；三张 text 内部均零非白像素。逐张打开 svg-comparison、portrait-complete、dpi-left、dpi-right 原图，path 星形清晰，文字未出现，portrait 24 行及两端完整，三张 portrait 的 240×480 像素相同。
- 两个不同 monitor handle 的移动记录成立，但 before/after 均为 96 DPI、scale 1.0；同 DPI 双屏通过不等于异 DPI 重解码通过。
- 单独打开六张 completion 原图：RepeatedAttach1Ready/2Ready 均有完整粉色 logo 与 FINAL-OMEGA；LateProviderClear/Reattach 均有完整 logo；SlowBodyClear/Reopen 均有完整横向棋盘格。独立解析 results 为 23/23 passed（含 cleanup）。仅这六项新视觉结论得到本轮补充；历史失败、原六项可见性不足及 LateProviderClear 历史评语分歧原样保留，不反向改写。
- 当前 checkout 的 completion 56 项、resources 60 项源码 hash 全部匹配。实际仍在磁盘上的两份 Harness.dll 重新 SHA256 分别为 `BA3DFD697D9D366049F715AF5009541F2746F77CA4E85C764E2B9492BDDF28F9` 与 `92BF33AAFC9B164075B2CA916EBE7B3B8D6B0D2FA775EA7CE8599FD023D49FD7`，与各自证据相符；后者同时匹配运行时 module 记录。四份生成 harness 输入 hash 匹配，提交的 Experiment.cs 也匹配。此为产物/来源一致性核验，不是重新启动运行或可重现构建证明。
- 两报告的本地 Markdown 引用均按实际路径检查存在；`../../src` 从 docs/performance-acceptance 正确回到仓库 src，无需修正。

## 平台限制与未验范围

独立读取官方 [SvgImageSource 1.8 文档](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.media.imaging.svgimagesource?view=windows-app-sdk-1.8)：底层 SVG 实现是 Direct2D；[Direct2D SVG 支持清单](https://learn.microsoft.com/en-us/windows/win32/direct2d/svg-support) 含 path、不含 text，未支持元素会被忽略。结合三路等价输入和直接 WinUI 对照，支持将本 fixture 的 visible café glyph 缺失定位为平台 text 限制，而非本次 charset 回归。字形仍未显示，不能将其改为视觉 PASS，也不能外推其他 SVG 特性全部支持。

六对代理测量已完成，但反向中位数受自然 GC、缓存/分配器复用、HTTP、JIT 与组合影响，不能证明任一路径更省内存或优化收益。DecodePixelWidth/Height 是请求，不能当作 native backing allocation。没有 native 栈归因、峰值或长期泄漏结论。full control 使用现有 provider seam，并非所有生命周期内部路径完全相同。

attempt-b 原始 records 的 NullReferenceException 位于 LoadResource 读取 Source 后访问位图属性的路径；最终 harness 改为异步 TCS continuation、帧后再次检查当前 Source、每个 loaded 边界检查就绪。已对照保存的 snapshot 与最终 Experiment.cs。原失败、前五对及第六对 full 数据、最终就绪修订均保留；最终单次完整通过不能证明生产生命周期问题不存在，亦不是生产修复。attempt-a timeout 和 launch-before-build 编排失败同样保留。

异 DPI/200%、native allocation 与收益、长期压力、Release/Native AOT、完整生产应用仍待验；已知非合作 SVG 占槽边界不变。AOT 工具链历史阻塞未重试。

## 清理和集成门禁

原记录确认 owned PID 4780、52172、46400、38584 与各自临时包均已清理；本次不重新探测或控制桌面。completion、attempt-b、最终运行的实际 yield 均为 **released:false**，不能称工具主动释放成功；launch-before-build 也是 false，PID 0 只是 sentinel，其旧 processAbsent=false 不能理解成残留 owned app。历史 attempt-a 单独记录 released:true，按原事实保留，不将最终 false 改写成 true。本轮没有新 owned UI 进程。

最近完整生产验证仍为 [#49 重挂集成](integration-49-remount-fix.md) 的 Release 403/0/0、Debug x64 与 runtime gate；之后 docs-only 不构成新的生产验证。status 顶部已更新，旧 401 验证保留为历史段落。

集成提交前执行 `git diff --check`，并检查生产/依赖无差异；kernel HEAD 为 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`、工作区 clean。提交后再次核验父工作区及 kernel clean。未运行 build/403。
