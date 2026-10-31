# RX2-R1：控制回执绑定原始意图

2026-10-31。父级直接修正独立审查发现的小范围问题，待新独立复核；IX 仍不准入，#49 仍 FAIL。

审查提交 `1401990b46b7d520831587ac9981dd850f85cbdb`，原 RX2 交付
`bea695e3d6f80f67c75514684a9156176b5530b3`。引擎基线 `31074a3dddd955cb725a89da882135085e856675`，
修复引擎 `abfe2641813905eeb5e0374cb4f983f4ce2ff6af`。正式应用源码、依赖、kernel gitlink 未改。

## 修正与范围

`Intent` 在 admission 锁内分配 revision，退出同步 callback 后仍返回该次 revision。
新增内部 PauseTracked / ResumeTracked；公开 void 签名不变。桥接 PauseAsync / ResumeAsync 把原 revision
传给 RenderAsync，每轮拒绝已被取代的控制，返回 Superseded。异常仍同步原样抛出；Closed/Faulted 判定优先。
没有在 callback 返回后读取当前 revision 冒充原身份；没有禁止选择及 realtime 同场景未开始准备时的重试。
差异仅两个文件，14 行增加、7 行删除；原 owner、borrow、EndDraw、释放顺序均未修改。

## 实际红绿

使用 receiving-code-review、run-tests、code-testing（focused）及 winui-code-review。
无测试 overlay。原 console fixture net11，SDK 使用已安装固定 RC3 路径；不是 dotnet test discovery。
在独立 artifacts 子目录从校验过的原 bundle clone，不修改原交付或审查工作区。

命令：`python -X utf8 -B docs/engine-61-design-draft/rx2-r1/fix-and-test.py --name rx2-r1-parent-fix`

| Requirement | Evidence |
|---|---|
| 排队 Pause 被 Resume 取代 | `Review_PauseQueuedThenResume_PauseMustBeSuperseded`，red→green |
| Pause callback 重入 Resume | `Review_PauseCallbackResumes_PauseMustBeSuperseded`，red→green |
| 对称 Resume 被 Pause 取代 | `Review_ResumeQueuedThenPause_ResumeMustBeSuperseded`，red→green |
| Resume callback 重入 Pause | `Review_ResumeCallbackPauses_ResumeMustBeSuperseded`，red→green |
| 原 RX2 行为不退化 | 原21项全部保留，包括 `RX2_PauseApplied_RealFrame_ZeroStep_Resume` 与 `RX2_PauseCloseThrow_ReentrantNewestWins` |

原候选 **21 passed / 4 failed / 0 skipped，exit1**；修正后 **25 passed / 0 failed / 0 skipped，exit0**。
两次构建均0警告/0错误；JSON 校验25个唯一名称和四个精确失败名称，不把wrapper exit0当red测试通过。
对称新增案例复用真实 Rig、owner/CoreRenderSurface 执行路径，验证当前 Pause 下一帧 Step=0。
源码副本的 friend assembly 只用于测试，未加入引擎提交。测试执行使用真实 native 准备/绘制链，但未创建
新的桌面验收宿主、未做新的 retained-handle/WER 窗口验收，不能替代旧 RX2 实机证据或实际包消费。

## 证据及审查限制

[source.json](source.json) 提供53文件、引擎提交、完整bundle/ZIP/patch路径及hash；
[candidate.patch](candidate.patch)、[ReviewCases.cs](ReviewCases.cs) 和 [evidence](evidence) 保留原始红绿日志/JSON的gzip原字节。
`seal.py` 封存时要求引擎Git clean。第一次commit因clone缺git身份失败，seal拒绝dirty；随后仅复制父仓库
已有身份到该clone本地配置后成功，重建bundle。没有修改global配置，旧基线bundle不作为修复交付。

WinUI 静态审查：没有UI同步等待、XAML/ABI变更、新资源所有者或新的原生释放路径；异常路径不新增吞异常。
WinUIAnalyzer、完整 QX/OX/C1a/R1 回归及 clean 无friend DLL/PDB封存未在本轮重跑，由独立复核补齐。
Stop/Set/Clear 的场景回执仍沿用现有语义，本片只修已证实的暂停/恢复意图身份；独立审查应继续检查
这些边界是否构成下游准入限制，不能把本片25项当整个控制API契约验证。

保留全部历史 FAIL/UNKNOWN，Present hang、cache预算、AOT、IX/PX、Legacy及实际nupkg消费均未解除。
无push、PR/issue修改、身份部署或登录数据操作。
