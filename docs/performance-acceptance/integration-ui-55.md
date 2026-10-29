# #55 UI 证据集成审查

2026-10-30：**有限适配器实机通过；真实 Explorer 与完整生产应用生命周期未验，工单保持开放。** 本次是既有实机原始记录的独立只读校验，没有重新运行 UI 或下载实验。

## 集成与保全

- 唯一 checkout：`C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80`，`feat/performance-45`；起点 `c3f1be42e86ad84864d76a30ca03b7eb0e95370b`，干净。
- 已读 AGENTS.md、docs/agents 三份规则、winui-code-review、verification-before-completion，以及 issue-55/integration-55/status 和本次 harness/证据。
- 合入前验证完整 delta 仅新增 `harness-55/`、`evidence-55-ui/`、`ui-55.md`。唯一合入命令：`git merge --no-ff d43c8f4e5da76017cc1256f685a03b4ea4bd6123 -m 'Merge limited download UI acceptance evidence (#55)'`。
- 无冲突 merge：`99a306b9b83418f4b2cc61ed12e8885686e3f9bf`，两个父节点恰为上述起点和指定来源。此次后续提交只补集成文档、只读校验脚本/输出，并纠正文档的 yield 引用。
- `git diff --exit-code c3f1be4 HEAD -- src tests externals` 无差异；kernel gitlink/checkout 均保持 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`，子模块干净。保存的 8 个生产源文件 SHA256 与父 checkout 全部匹配。既有 #46–54、#56–58 证据未改。

## 真实执行边界审查

Harness.csproj 直接链接生产 DownloadViewModel 两个 partial、DownloadProcessRunner、DownloadButton、DownloadSettingControl 及基类。真实菜单命令调用生产命令构造/复制分支；设置 VM、持久化、服务定位、认证 DTO、提示和部分视觉资源均是明确的 adapters。每次修改自动开目录设置后重建 VM；没有证明已有 VM 动态更新缓存设置。

ViewModel 命名空间的 Process 子类继承 System.Diagnostics.Process；实例 Start/StartInfo/Wait 未重写。Toolkits 命名空间中的真实 runner 以 System.Diagnostics.Process 参数调用 Start、WaitForExitAsync、ExitCode，在 using 内持有对象。Dispose observer 在 base.Dispose 后检查捕获句柄已关闭。仅子类静态 Start 拦截 explorer.exe，核对 owned 路径并记 folder-adapter；因此开目录条件及顺序有证据，真实 Explorer 打开没有证据。子 EXE 是忽略 argv 的离线门控替身，并非真实 BBDown。

生产 ViewModel 的 external + copy 分支在 Task.Run 前返回；生产 DataPackage/Clipboard 调用真实执行，复制提示由 adapter 记录。生产 runner 只在退出码 0 且设置允许时调用目录 action，非零/启动失败经真实 ViewModel 记录安全日志，无下载成功提示。未发现本次证据合入所引入的生产缺陷。

## 原始记录交叉核验

新执行以下只读命令，均 exit 0；补充脚本包含原 verifier 调用，输出保存在 [integration-verification.txt](evidence-55-ui/integration-verification.txt)：

```powershell
./docs/performance-acceptance/harness-55/verify.ps1 -Evidence docs/performance-acceptance/evidence-55-ui
./docs/performance-acceptance/evidence-55-ui/integration-verify.ps1
```

原 verifier 检查精确的 6+1 PASS、最终目录计数、正常进程句柄关闭、复制无创建、最后窗口外部观察、UIA 调用退出状态及复制清理。它本身未逐项断言全部错误日志、host-alive 外部观察和时间顺序；补充脚本直接解析原 JSON/JSONL，并核对每个 case 与 final-localstate 的文件 hash 一致，没有只凭 PASS 摘要签收。

| 原始 case | 独立核对结果 |
| --- | --- |
| SuccessOpen | child 43448 退出 0；captured/closed=true；Dispose 后恰好一个目录 adapter；无 tip/log。门控期间未打开由 run.ps1 的 alive/folder=0/disposed=0 断言及通过记录支持，不把事后日志当作新的现场观测 |
| SuccessDisabled | child 27564 退出 0；prepared.open=false；句柄关闭，最终无目录/tip/log |
| NonzeroExit | child 60496 退出 7；句柄关闭，无目录/tip；精确 Warning `Download process exited with code 7.` |
| StartFailure | 创建/Dispose 各一次，未捕获进程句柄且无 child；无目录/tip；精确 Error `Download process failed (Win32Exception).` |
| CopyOnlyNoProcess | 零创建/Dispose/child/目录；仅 Copied 提示；synthetic clipboard matches=true，owned clear 一次 |
| CloseWindowHostAlive | 外部记录宿主 58912、child 43200 均存活；main-closed 在 Dispose 之前；child 门控退出 0 后句柄关闭、目录 adapter 一次 |
| CloseLastWindowChildSurvives | 外部记录宿主 59160 已退出，child 34496 仍存活；随后外部门控释放、child 退出 0；全程无托管 Dispose/目录 callback |

run.ps1 先保留外部 child Handle，再 WaitForExit 并检查真实 ExitCode；child-exit.json 是退出前写入的辅助记录，不能单独证明 OS 已退出。外部观察和门控正常收尾共同支持 child 存活/回收。最后窗口宿主的 hostExitCode=null，不能声称宿主正常退出码为 0。宿主退出时 OS 回收句柄不能冒充托管 using 或异步 continuation 完成。

keeper 是 harness 生命周期正对照，生产 MainWindow、播放器隐藏/托盘、服务保存、App.Exit/Environment.Exit、激活和账号导航未链接。两个关闭实验仅支持受测下载代码在这两种宿主边界中没有主动杀 child；不能签完整应用生命周期通过。

## 证据勘误及限制

- `cleanup.json` 保存 ownedProcessesAlive=[]、ownedPackageGone=true、noKillUsed=true；本次只校验历史记录，不重新访问桌面/包或按旧 PID 查询当前进程。
- 修正 ui-55.md 的引用错误：cleanup-yield.json 实为 released=false；释放证据来自 run3/yield.json 与 last2/yield.json 的 released=true。原始记录保留，没有把 false 改成成功，也不推断当前桌面所有权。
- 原历史失败、unhandled 日志、构建尝试和截图均保留。16 张图片的逐张视觉结论属于来源报告，本次没有重新逐张查看或签收完整视觉矩阵。executed-hashes.json 是历史运行产物清单，本次未访问另一 checkout 或重建产物来独立重现其运行身份。
- 生产未改，按任务边界不重跑 app suite/build、runtime gate 或 harness，不部署/启动/桌面输入；不进行 push、PR、issue 操作、其他 worktree 操作或委派。专用 WinUIAnalyzer 未运行。历史 harness 构建 4 warnings/0 errors 不等于新构建通过。
- 真实 BBDown/凭据下载、真实 Explorer、生产通知视觉/持久化、完整主应用生命周期、视觉/可访问性矩阵、长期资源/吞吐与性能收益仍未验。Release/Native AOT 沿用既有工具链 BLOCKED，未重试。7/7 是有限适配器行为证据，30 次句柄样本也不能证明长期无泄漏。
