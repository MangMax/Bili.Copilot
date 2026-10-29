# Release / Native AOT 证据集成审查

2026-10-30，仅在父 checkout `t3-1d19cc80`、分支 `feat/performance-45` 审查并集成。起点 `a236aaa2266156804651223743444a0418af4822` 干净；以 `--no-ff` 合入 `562dcfa4b38b587621c7aa2c5bafd1142a08c0d0`，合并提交 `c9d0c781a08e40e0bfd17aafd267b2c9965bc300`。来件仅新增 `release-aot.md` 及 `evidence-release-aot/` 下 9 个小型文件。

按 AGENTS/docs 规则、winui-code-review、verification-before-completion 审查 [报告](release-aot.md)、诊断摘要与全部结构化 JSON，结论为接受失败证据，**AOT 验收 BLOCKED（工具链）**，不是发布或运行通过。

- Error / 环境阻塞：[最终日志](evidence-release-aot/final-publish.txt) 在 UI DLL 输出后报 Platform linker not found；[探测结果](evidence-release-aot/result.json) 记录退出 1、C++ 组件匹配为空、`vcvarsall.bat` 与桌面 `lib/x64` 缺失。仅存在 link.exe 不足以完成 Native AOT。应在完整工具链环境重新验收，本次不安装或重跑已知失败。
- Note / 调用历史：第一次全局打包属性传播导致 resolver 类库 MakeAppx 失败；第二次缺 SolutionDir 导致 StringNames 缺失；第三次补参数后仍复用 Models 增量残留。报告记录局部 Rebuild 后最终失败转为工具链阻塞，不能把这些调用问题算作性能生产缺陷。增量跳过的原始 replay/binlog 未随小证据入库，本次未访问其他工作区独立复核，保留此证据边界。
- Warning / 验收边界：[PE 记录](evidence-release-aot/intermediate-pe.json) 的 CLR header RVA 为 8192，现存 DLL 仅为 x64 托管中间物；native/MSIX 产物均为 null。既有 NoWarn 保留，ILC/native link 未完成，不能宣称完整 AOT/trim 警告清零；专用 WinUIAnalyzer 未运行。没有部署、启动或运行验收签字。
- Note / #52：依据父任务同步更新真实空图 FAIL、独立修复任务进行中。`4c5dc7c` 证据尚未合入，未创建证据链接或宣称修复通过；桌面仍由该任务独占。

源码树核验：`git diff --exit-code a236aaa HEAD -- . ':(exclude)docs/performance-acceptance'` 无差异；`git ls-tree HEAD externals/bili-kernel` 仍为 `2ca79bd3376b911b1b6d3e5f76ed8ddaa5dd972d`。生产代码、gitlink、构建配置及 NoWarn 均未变化。本次补充提交只修改 `status.md` 和本文，并以 `git diff --check` 核验格式。仅解析已提交 JSON、核对日志和 Git 树，不新增构建/测试通过声明；原始大日志哈希是来源清单，不代表本次重新计算原始文件哈希。

本次无安装、构建重跑、桌面输入、部署启动、其他工作区操作、push、PR、issue 关闭或子代理。
