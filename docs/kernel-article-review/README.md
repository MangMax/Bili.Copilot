# 独立离线 review

固定输入：主报告 d23fb324339b8c7ffccac1b23ae345b347990062；kernel 41891eeb28652b357c7ba0a34733347d36733722，父 0ae5e4e000a41a07f6696746706df11c06e8835b；本区父 4986fccf。按 code-review / run-tests / code-testing / verification-before-completion 技能核验；遵从用户要求未启动子代理。

结论：可采用必需字段 KernelException、可选统计 null、空结构/无 URL 媒体跳过的方向；暂不批准以“杜绝空成功”的完整修复合入/发布。无修改生产实现，无 gitlink 提交。

## 阻断（P1）

ArticleAdapter.cs:110–116 将 words=null、空串或纯空白包装成带 style 的 p，FormatHtmlContent 只删除精确的无属性空 p，ToArticleDetail:57–61 对 HTML 字符串做 IsNullOrWhiteSpace 因此误判为成功正文。ReviewCounterexamples.EmptyWordMustNotReturnSuccessfulBody 三个输入全部失败（预期 KernelException，实际成功）；例如 null 输出包含 `<p style="font-size:'17'"></p>`。这违背报告第4节“正文为空不再静默返回空文章”的承诺。应按有效文本/媒体决定是否有正文，并保持合法文本/图片原模板；不应简单 strip 全部标签而误拒图片文章。补测源码与 review.trx 在 evidence，可复制回原测试工程复现。此项是修复承诺未覆盖的既有边界，不声称由新增 guard 引入。

## 已核实与边界

- 真实适配器、真实 SourceGenerationContext：原12项绿12/12；仅还原适配器到0ae5e4e并保留测试接缝时红9失败/3通过，随后恢复41891ee。红退出1，绿退出0；本区新TRX已提交。
- MissingReadInfoFailsWithIdentifiableError / MissingAuthorFailsWithIdentifiableError 验证 KernelException；与 ArticleDiscoveryClient 既有解析/数据缺失异常约定一致。缺键与显式null在当前模型均落默认null；不把 nullable 注解当JSON校验。
- MissingOptionalStatsDoNotFailAndStayEmpty / MissingMetadataObjectStillRendersBody 验证可选统计/关系默认值。上游 ArticleReader.xaml.cs:65 的 ViewCount、ArticleReaderPageViewModel.cs:185–187 的赞/评/藏均 `?? 0`，静态兼容；VM异常 catch 仍仅记录日志，未验证真实错误提示。
- EmptyTextNodesAreSkippedWithoutFailing、PayloadlessMediaIsSkippedWithoutFailing、UnsupportedParagraphTypeIsIgnoredWhenSupportedContentExists 通过。新增 NullItemsAndMissingMediaPreserveEncodedText 通过：null段落/null节点/null图片/无URL图片/未知段落混排保留合法 `<&>` 并正确编码。只有未知段落原测试正确失败为 KernelException。未知段落的内容仍可能有损，这是当前声明的支持范围，不扩展解析器。
- LegacyHtmlContentKeepsParagraphsLinksAndImages、OpusContentKeepsTextParagraphImageAndLineSemantics 通过；diff确认原文本/加粗/图/分割线模板、HtmlEncode、旧HTML替换逻辑未改变。原来嵌套p与引号字号模板也原样保留，不声称产出符合HTML规范。opus存在但为空时不会回退到readInfo.content，保持原优先级。未发现此次变更误删可由现模型表达的合法文本。
- 不根据NRE推断HTTP成功，亦不推断七次同字段。主报告第2节“两个请求都…通过ParseAsync”表述过强：页面抓取不是同一JSON ParseAsync路径；只可按调用顺序说明本次进入适配器前未抛异常，不能据此证明HTTP状态或远端内容正确。未读取任何用户日志/认证/AotDev/真实文章。

## 恢复闭包和CI

本区起始kernel为f02c33f、474跟踪文件暂存删除、缺0ae5e4e；依用户授权仅恢复本区。已从本地f02c33f历史建立独立空bare仓，再依次fetch已提交旧kernel.bundle、新bundle，fsck --full退出0；tip=41891ee，tree=fafafbf743e626602593601537b1ddb1058f2d77，与本区完全一致。新bundle要求0ae5e4e，须连同父仓已提交 docs/performance-acceptance/dependency-upgrade/kernel.bundle 使用，不能单独视为自包含。新bundle SHA256=82960118afbea1bd179e2ce2332f3e422d7ab70d3d6ef20dbdff1ec37e8bc0c9；patch reverse --check通过；实现、项目、fixtures均在41891ee的git tree，不依赖ignored唯一实现。

现run-unit-tests.yml只运行UI.Tests，paths不含kernel、checkout未启用submodules；新工程不在kernel solution，故当前CI不会验证此修复。父级采用时建议仅为该工程添加明确测试步骤/小job、kernel gitlink路径触发及本地bundle恢复步骤（未发布SHA不能依赖远端submodule fetch），并安装net8运行时及匹配根global.json的SDK。无需扩全库、无需修改当前工作流于本次review。先解决P1再接入验证。

## 复跑

离线restore：`dotnet restore externals/bili-kernel/src/Tests/Services.Article.Tests/Services.Article.Tests.csproj --source C:/Users/hp/.nuget/packages -v quiet`。
测试：`dotnet test externals/bili-kernel/src/Tests/Services.Article.Tests/Services.Article.Tests.csproj --no-restore -c Debug -f net8.0 --filter FullyQualifiedName~ArticleAdapterTests --logger "trx;LogFileName=green.trx" --results-directory docs/kernel-article-review/evidence -v quiet`。
红态同命令，仅适配器切到父版并更换TRX名；补测复制evidence/ReviewCounterexamples.cs到测试工程后筛选 `FullyQualifiedName~ReviewCounterexamples|FullyQualifiedName~ArticleAdapterTests`，结果13通过/3失败（退出1）。补测运行后已移除临时源码，kernel工作区干净、HEAD41891ee；父仓正式gitlink仍0ae5e4e。所有结果仅覆盖适配器与本地反序列化，不能作为真实UI/HTTP/NativeAOT验证。
