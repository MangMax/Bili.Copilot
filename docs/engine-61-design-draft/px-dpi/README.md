# Core 物理像素绘制约定修正候选

2026-10-31。父级直接修正独立审查 `3ebac48d7eeb81f161dda5d2769b91cfa8988ba0` 定位的DPI问题。
引擎基线 `4a44888647eb781c4343de3512f5f70683bd2cdb`，修复 `698d546e5c9edf5c96b6871357dc532542457c45`。
正式依赖及应用src/gitlink不变；原三张public图继续FAIL，新的public视觉验收尚待完成。

## 最小修正

renderer canvas、字宽字号和坐标已使用物理像素，但D2D target/context设置96×scale DPI，使150%时
实际可绘坐标高度为240，而Bottom drawY约320，绘制在composition之前就被裁掉。
CoreNativeSurface.CreateTarget的bitmap DPI和context DPI统一为96，让D2D绘制数值与物理坐标一致。
保留swapchain像素大小、逆scale composition变换、字体/速度、槽位以及调用宿主数据集。
一个源文件3行增加、2行删除（含说明注释）。没有更改跨模式独立槽位允许重叠的规则。

## 本轮执行与证据

沿用receiving-code-review、run-tests、code-testing focused、winui-code-review。
执行 `python -X utf8 -B docs/engine-61-design-draft/px-dpi/fix-and-test.py --name px-dpi-parent-fix`。
从校验bundle新clone，测试副本使用原101 host及审查5个诊断，SDK11 RC3，真实owner/CoreRenderSurface/D2D。
friend和调度hook仅在测试副本，不进入候选引擎。

| Requirement | Evidence |
|---|---|
| 150% Bottom在真实target绘制范围内 | `REVIEW_BottomInsideTarget_Scale_1p5`：原FAIL→PASS |
| 100%原坐标边界保持 | `REVIEW_BottomInsideTarget_Scale_1`：前后PASS |
| 原public数据集布局值、迟到seek、resize年龄保持 | `REVIEW_OriginalPublicTarget_Coordinates`、`REVIEW_LateSeek_StillActiveOld_NotDuplicatedOrRejuvenated`、`REVIEW_ResizeDuringRebuild_LatestTargetRecovered` |
| 原PX/IX/RX2行为保持 | 原101项全部保留，逐名见[evidence](evidence)中的原始JSON |

原候选 **105 passed / 1 failed / 0 skipped，exit1**；修正后 **106 passed / 0 failed / 0 skipped，exit0**。
两次构建0警告/0错误，结果核验106个唯一名称及唯一预期红例。日志/JSON gzip保留原字节。
测试结束后默认diff-check因CRLF被判trailing whitespace而使wrapper退出1；测试本身exit0。
保留原输出事实，之后仅以命令级 `core.whitespace=cr-at-eol` 校验通过，未修改源码消除证据换行，
最终脚本同样明确该规则。候选源commit、53文件、bundle/ZIP/patch路径与hash见[source.json](source.json)。

## 待验收

本轮没有新public包、桌面截图或退出/WER观察；受控D2D界内断言不等于视觉已通过。
需要新clean包/独立restore/load，在原数据集100%/150%、resize/DPI变化下逐图检查Bottom、Top居中、
滚动边界、暂停和恢复；允许的跨模式重叠需解释并保留原FAIL，不能移走样例来回避。
未重跑完整QX/OX/C1x/native故障矩阵，WinUIAnalyzer/AOT未运行；静态检查未见新增UI等待或资源释放顺序变化。
保留Share独立加载记录缺口及原CodeView误称MVID的更正，须在新包封存中分别标识。
Legacy/V1、#49、AOT/#56/perf/cache、Present hang及历史FAIL/UNKNOWN均未解除。
无push/PR/issue写入、正式依赖修改或认证环境操作。
