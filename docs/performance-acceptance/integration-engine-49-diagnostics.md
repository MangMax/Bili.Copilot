# #49 引擎诊断证据集成复核

2026-10-30。**仅集成文档与证据，#49 仍 FAIL/未完成，候选不可发布。当前权威更正：撤回“历史两张 Win2D frozen 图证明白字消失”的结论。两图白字区域和整个渲染区域像素相同。** 这不撤销其他暂停首帧失败，也不解除线程所有权、UI 等待及关闭安全阻断。

## 集成边界

父分支 `feat/performance-45` 起点为 `51154f0052f86428282550e2ca8910dccc228a03`，合并前父工作区和 kernel 均 clean。`git merge-base` 返回该起点，待合入历史包含候选 `c15db2d1f52254cd2b49cb7e2bdd8937f750f587`、根因审查 `3957d96b238e93fd665a5a4697ae7cfcd2d4fd00` 和 trace `0690b3e6af260a44d9ee24c710ff450a75927f12`。按要求 `--no-ff` 合并，merge 为 `1cddcd6614591d1a28f6ac5a91cae484f57748c9`，双亲依次为父起点和 trace 提交。

已完整阅读 AGENTS、docs/agents、winui-code-review、verification-before-completion，以及[候选报告](engine-49-candidate/README.md)、[根因审查](engine-49-root-cause-review/README.md)、[trace 报告](engine-49-trace/README.md)。这些历史报告原文及其原始证据保持不变；涉及冲突的结论以本页和[统一账本](status.md)为准。尤其候选的“最终无插桩”声明，以及候选/根因审查的两图消失判断，不得脱离更正继续引用。

相对父起点，docs 之外无任何 diff：正式 `src`、项目/包依赖配置、kernel gitlink 均未改变。kernel HEAD 为 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`。`scheduler-candidate.patch`、`harness.patch`、失败的 render patch 都只是 docs 中的审查材料，本轮未应用任何 patch、未修改正式依赖。没有源归档、大 dump 或包加入提交。

## 已独立重算的证据

1. 在父 checkout 只读执行 [historical-pixels.ps1](engine-49-trace/historical-pixels.ps1)，exit 0。脚本逐像素读取 RGBA，并分别计算 RGB、RGBA hash、白色阈值计数、alpha 范围；未改图。结果与 [historical-pixels.json](engine-49-trace/evidence/historical-pixels.json) 一致。两张原 PNG 文件 hash 分别为 `727206068a49addedef901253024cd70e0bebf0d8efd2c10d180eaf508917e8a`、`4c1a5803c9b531dd0408b284406c0c6d922610fb5cec057b8e55b272c9678e1f`，各自与候选原工作区相同。
2. 白字区 `x890..1309,y390..444` 两图均有 4482 个 RGB 各通道大于 150 的像素，RGBA SHA256 均为 `4afe935975b7eeb9207f099d343b4bb5d7ed99db3d51e92a48dce6dd8453e2c8`；整个渲染区 `x20..1359,y320..799` 均为 `67237563bbb14751d483ba4b455e71a3bde1fc2391cbd54785fe139fc97ac9c8`。两个区域 alpha 均为 255。文件字节不同不等于渲染内容不同；更正仅针对这两个区域和这对样本，不声称整个窗口像素相同或所有运行从未消失。
3. 使用 [metadata.ps1](engine-49-trace/metadata.ps1) 对原路径的 original/candidate/trace 三组 DLL 重新做 PEReader 扫描，各 5 条，全部 hash/MVID/diagnosticFields 与存档匹配。original/candidate 的所有条目无 `_test*` 和 trace 字段；trace Legacy 明确含 `_trace49Instance`、`_trace49NextInstance`、`_trace49Generation`、`_trace49Sequence`、`_trace49Owner`。使用 [inspect-metadata.ps1](engine-49-root-cause-review/inspect-metadata.ps1) 重新读取历史 layout-final，3 个 type 记录的 hash/fields 匹配，Core/Legacy 调度器仍有 `_testDequeued`、`_testIdle`。
4. [候选 artifact 清单](engine-49-candidate/evidence/artifact-hashes.json) 的 24 个文件和 [trace artifact 清单](engine-49-trace/evidence/artifact-hashes.json) 的 45 个 path 文件 SHA256 均重新计算匹配；后者另有 3 个包内 entry 比较记录，本轮未重新解包验证。24 项中包含上游 ZIP，SHA256 为 `5b6465b63c438cdbfa8c0c2d7697719a677539522d98feeb7c2b7b87f1691bc0`。
5. [source-hashes](engine-49-trace/evidence/source-hashes.json) 中 original/candidate/trace 的 45/45/46 个源文件分别逐项匹配。本轮核对文件 hash，不冒称重跑准备、补丁应用或构建。首次临时核验遍历到清单顶层的非源文件列表时出现 TypeError；改为显式三组后成功 exit 0，不采用失败调用作为通过依据。

关键程序集 SHA256（详细路径/MVID 见 [original](engine-49-trace/metadata-original.json)、[candidate](engine-49-trace/metadata-candidate.json)、[trace](engine-49-trace/metadata-trace.json)、[历史 metadata](engine-49-root-cause-review/binary-metadata.json)）：

| 产物 | Core | Legacy |
| --- | --- | --- |
| clean original | `9cbf889d39dc89e9b0d1cf6c684c43236e948941f677b135a4676418f6e62180` | `572b236ac109d81e33a8c8ee125c0ac943e61a8eb2f7b4e4b385998ae950278a` |
| clean candidate | `043e7076aab40d0aa09927b7e00432d3d32cb90885b19f65168c22a31a24dd35` | `e06cf825e7bef76aa798dd85d3ece5fcc7eb0e91abdb65d29e7f814d3a2509b1` |
| trace | `35cb361c37626f4e00fd92d48ea336ec14cc9cb0d26c2c0fab0e9a64b2fd7ca2` | `0182ad4dc51afc0e3f93c1913b5be4f39ec18e23c9770c4fd2f4af59f21bf14d` |
| 历史含测试钩子 | `b67972a3e09b3b548dffbe0c5d9e85a74b2322f886d30e7c228d9a8dd078dd56` | `68f030bbdafe03a28fc0ca3722d7f4c9c8cd8393964cfb4fb66f7bc64303c91d` |

**新 clean 对照只提供来源/构建与元数据证据，trace 轮和本轮均未对它们做 UI 行为验收。** 它们不能追认历史“无钩子”声明；实际 trace 动态运行是另一组明确插桩的 DLL。无 `_test*` 也不等于无所有插桩，更不等于线程安全。

## 证明边界与剩余阻断

- **已证明/已有证据支持：** 两图不能证明白字消失；历史产物有钩子，新 clean 产物无该类字段。trace 报告在有限窗口观测到 item 保留、DrawQueued 和 BatchDisposed 完成，未复现消失；本轮审查这些记录，未重新执行动态采集。CPU API 返回和 batch Dispose 不证明 GPU completion/compositor present。历史 26 项调度屏障测试只支持受测意图/代次/游标行为，不证明完整 #49 通过，本轮没有重跑它们。
- **仍阻断：** 暂停前 seek、双向暂停切换的目标首帧失败保留；目标项未提交、stopped 与 paused-prepared 混用、暂停挡住初始化/Draw 的审查结论未被两图更正推翻。stateGate 跨 render 调用、双执行线程、Stop 非 drain、限时 Dispose 非完成屏障、引用快照缺资源保活等所有权/关闭协议问题仍需处理。新 trace 不覆盖这些失败矩阵。
- **假设/未定位：** 所报告消失的真实发生条件尚未确立；不能从未复现推断修复，也不能把它归为 GPU/driver/compositor 故障。两次 rejected render 实验的 stowed 根异常、Debug D3D 创建失败的具体 HRESULT 仍未闭合；不拿早期 logger IOException 代替引擎关闭根因。
- **未验：** clean 对照 UI、包从 feed restore 后运行、完整播放器/真实视频时钟、登录业务、真实网络、多生产者/设备丢失、长时高密度与性能采样均有缺口。Native AOT 工具链仍 **BLOCKED**；WinUIAnalyzer 未运行。继承的 403 和历史 build 不是本轮 fresh 结果。

下一步先按[架构审查第 6–7 节](engine-49-root-cause-review/README.md#6-资源亲和性和最小设计边界)明确 Close 返回语义、active-set、Clear/Stop/Restart/列表别名兼容，再分切片设计：纯数据有序 admission/intent/generation；Core 单 render owner 与 Legacy 动画 owner 的资源退役/关闭；零时间步 paused-prepared 首帧与 active-set；完整双引擎视觉/生命周期/兼容及包消费验收。整体已涉及 L 级架构决策，不能把原 M 票标完成，也不授权发布当前候选或继续多点试修。

## 复核范围与本地依赖

命令均为文件只读核验及 Git 文档集成。metadata 复核使用 `pwsh -NoProfile -File .../metadata.ps1 -Root C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-9d663a29/artifacts/engine49-trace/{original,candidate,trace}`，历史 metadata 脚本使用默认原工作区参数；像素脚本从仓库根运行。源/产物清单用 Python hashlib / PowerShell Get-FileHash 逐项断言。

原工作区 `t3-c9d5219a`、`t3-9d663a29` 的 ignored artifacts 是 DLL、ZIP、包、完整 trace 和 dump 的**本地依赖**，不随 Git 文档交付，也未复制到本提交；它们缺失时只能审阅存档，不能声称重跑成功。本页、status 和三份 README 共 5 个 Markdown 文件的 67 个相对链接逐个解析存在，所引用章节锚点也匹配；忽略大 artifacts 作为可移交链接的要求，明确保留其本地依赖限制。历史绝对 skill 链接同样依赖原机器。

候选目录相对 c15db2d、根因审查目录相对 3957d96 均 `git diff --quiet` exit 0；相对父起点的非 docs diff 为零。新增文档与账本单独做 `git diff --check`，历史补丁/raw 日志已记录的 whitespace 不悄悄清理。本轮不跑 403/build、不安装、不操作桌面、不 push、不改 PR/issue、不启动子代理。最终提交 SHA、kernel clean 和父工作区 clean 由提交后命令给出。
