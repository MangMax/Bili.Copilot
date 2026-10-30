# 已登录完整应用：NET11 RC3 组合验收

2026-10-31（UTC+08）。**真实完整应用验收 PARTIAL；三次退出 PASS_BOUNDED，总体性能 HOLD。**
本轮覆盖最新 PR27/43、依赖升级和 timer fix 的 Release x64 JIT 生产入口，非离线 fixture，非 AOT。
两次关闭后重开均直接进入有内容的首页；未读取、复制或清除认证存储。

## 来源与同身份更新

测试分支起点及交付前父本地 tip：`043d1e8a87f67e0e56967a2bfc536818c2bd4282`。
使用父 `t3-1d19cc80/artifacts/integration-net11-player-timer-fix/be7105b003470782c74fbc8aaac0ea6a23a44a60/Release/activation`。
完整 Release 冻结集合 **1887 文件**重新校验大小、SHA256及双向集合；activation **670 文件**，无 recipe。
`be7105b` 与本轮 HEAD 的 src/tests/global.json/kernel 指针差异为空；未重做487测试或构建。
RC runtime `11.0.0-rc.3.26522.148`，coreclr SHA256
`46a09d97ad8dd30d400a86c90af707bee7593b585d3ccb8586c4fffca404d1e2`。
运行中模块再次按路径与文件哈希验证，run1 播放阶段含163个包内模块，run2/3 启动阶段各146个；
不把启动模块清单描述成全部播放模块跟踪。见 [provenance](full-app-net11/provenance.json)、[verification](full-app-net11/verification.json)。

唯一目标：`Mang.BiliCopilot.PerfAcceptance.Caa6a7be`，Publisher `CN=BiliCopilotDev`，
family `Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg`。
旧注册精确指向 `t3-1630ecee/artifacts/full-app-duration-fix/layout`、版本2.2610.0.1，已保留。
新复制仅更改生成的验收 manifest Identity（Name/Publisher/Version），所有其他payload逐文件相等；
**没有修改任何 tracked manifest**。

部署失败记录未覆盖：同版本 `Add-AppxPackage -Register -DisableDevelopmentMode` 未报错但仍指旧layout，
路径门禁阻止启动；版本递增后该参数在两个新目录均报 `0x80073CF9`（manifest不在包根目录）。
去掉不适合 loose layout 的 `DisableDevelopmentMode` 后，同身份 `Add-AppxPackage -Register` 成功。
没有卸载、`--clean`、删除数据、覆盖旧layout或安装全局工具链。工具没有要求清除数据。

**最终新注册位置（后续必须以此发现PID，不用旧EXE）：**

```text
C:\Users\hp\.t3\worktrees\Bili.Copilot\t3-4d615e72\artifacts\full-app-net11\layout-v2
Mang.BiliCopilot.PerfAcceptance.Caa6a7be_2.2610.0.2_x64__y1mf40g4r9srg
shell:AppsFolder\Mang.BiliCopilot.PerfAcceptance.Caa6a7be_y1mf40g4r9srg!App
```

## 真实UI覆盖及边界

| Requirement | Evidence |
| --- | --- |
| 保留登录、关闭后重开 | `first-home`、`reopen-home`、`second-reopen-home`：三个精确PID均有内容卡片、登录标记为0；[samples](full-app-net11/samples.jsonl)。账户菜单仅内存检查，未保存个人资料树/截图。 |
| PR27推荐刷新/撤销 | 原增量设置Off，公开UI开启后 `RefreshRecommendButton`→`UndoRefreshButton` 可操作，卡片变化及撤销后画面保留。初始6、刷新后32、撤销后13是UIA枚举数量，不是实际Items或稳定页数；后续自动加载/虚拟化存在，**未证明整个序列精确恢复**。 |
| 栏目离开/返回 | 推荐→热门→推荐，两个栏目都有卡片，返回后刷新启用、无残留撤销按钮；仅有限交互，不证明所有旧请求取消。 |
| 空页有限行为 | 无匹配词搜索实际返回40个UIA卡片，未命中真正空页；**NOT OBSERVED**，不能替代PR27空批次/失败批次受控证据。未注入服务/断网或声称50页测试。 |
| 推荐封面与字体 | [初始推荐](full-app-net11/home-initial.png)、[撤销后](full-app-net11/home-after-undo.png)已裁掉主窗口个人区域并逐张查看。封面实际解码、中文文字可读；标题按卡片设计截断，不是全部视觉/物理DPI/硬件device-loss通过。 |
| 搜索IME简要 | `ime-vk-commit.json`：真实虚拟按键 Shift/Z/H/O/N/G/Space 提交“中”，随后同一Shift虚拟键切回原模式并清空。最初文字token `shift` 被工具当文本输入，失败记录保留；不作为产品IME失败。未覆盖候选翻页/多音字/所有输入法。 |
| BV1qW4y1a7fU P1/P2/P8 | P1画面及选中分集（列表06:54），P2普通/全屏/小窗后恢复（播放器06:55），P8明确点击“07-第一个Python程序-练习讲解”（01:45）均实际播放。P8 00:02→00:13、UIA计数662；后一次计数文本在面板滚动外，不冒充可见截图读数。未声称所有弹幕完整。 |
| 播放器模式 | P2正常→全屏→Esc正常→小窗→Esc正常，截图逐张检查；密集公开弹幕遮挡是实际状态，未改用户弹幕参数，也未重测#49暂停seek。 |
| 字体首次打开 | `DanmakuFontComboBox`打开后30个可见ListItem；前后均Segoe UI，未选择/更改字体。仅验证可用性，非字体枚举次数或启动收益测量。 |
| 可访问PGC换集 | 《凡人风起天南1重制版》00:03/20:03→标注“限免”的第2集00:01→00:26/17:55；[第1集](full-app-net11/pgc-first.png)、[第2集播放](full-app-net11/pgc-second-playing.png)。最初栏目入口自动恢复既有集进度，随后只选择无会员标记或“限免”集；未购买或选择会员集。 |
| 托盘hide/restore | 公开UI开启驻留；关闭播放器仍存活，点击托盘恢复原主HWND2624988；主窗口Close后可见窗口0、PID仍存活，再以托盘公开UIA Invoke恢复。**首次恢复未取得前台**，WinApp拒绝后续鼠标/Focus，AUMID也未解除；最终通过精确控件UIA Invoke继续，未做坐标盲点。不能宣称前台恢复完全通过。 |
| 显式退出与设置恢复 | run2真实托盘右键菜单→“退出应用”；run1/run3为驻留关闭后的主窗口Close。最终增量Off、驻留Off；字体不变；[settings-final](full-app-net11/settings-final.json)。 |

所有实际操作时间、已知selector及工具退出码保留在 [actions](full-app-net11/actions.jsonl)。
过期分集selector、导航过渡inspect、遮挡按钮拒绝、foreground拒绝及shell解析错误是工具探索失败；
不覆盖它们，也不将它们自动归为应用崩溃。没有点赞、收藏、投币、评论、发送弹幕、下载、购买或真实AI调用。
没有点击退出登录。公开视频/PGC播放会按应用正常行为更新播放历史，本轮不读取认证文件或恢复服务器历史。

## 退出门禁

独立观察器在AUMID启动后立即验证精确EXE路径并保留 `Process.Handle`，记录PID和creation FILETIME。
退出后继续约30秒查询Application 1000/1001；只保留精确包/路径相关白名单字段，
通过既有 `exit_gate.py`合并exact PID/path/creation-time AppError和linked/unresolved WER。
没有仅凭进程消失判绿，没有强杀，没有dump上传。未来精确事件可撤销有限结论。

| Run | PID | 路径/创建时间证据 | retained code | gate |
| --- | --- | --- | --- | --- |
| run1 | 32244 | [observation](full-app-net11/run1/exit-observation.json) | 0 | PASS_BOUNDED |
| run2 | 37344 | [observation](full-app-net11/run2/exit-observation.json) | 0 | PASS_BOUNDED |
| run3 | 20236 | [observation](full-app-net11/run3/exit-observation.json) | 0 | PASS_BOUNDED |

三个run的事件安全集合均空、查询完整，应用日志固定Unhandled分类均0。
日志仅读取精确包当日Logger文本，在内存按本轮时间头切块、输出分类/类型/计数；没有保存原始Message、签名URL、headers或认证文件。
run1搜索建议取消链2条；run2搜索建议1条，另68条 `other-exception` 含TaskCanceledException，未进一步归因；run3摘要另存。
这些不是“网络全部取消”的证明，也未用零分片失败推断弹幕完整。
初版日志摘要被PowerShell自动日期转换误选UTC+08窗口，已保留 `log-summary-invalid-time-window.json`，
改用 `ConvertFrom-Json -DateKind String`后重新计算正确窗口；不得引用无效摘要。

## 交付与限制

已读取AGENTS/docs规则及指定WinUI/verification技能；无子代理、无生产修复，无新WinUIAnalyzer安装，
不声称本轮重新通过全应用静态审查。验收脚本语法、生产树未变、完整payload/加载模块与退出门禁再次验证。
所有13张PNG均逐张打开查看；最终图只含目标内容/公开视频/托盘菜单，不上传整桌面、原始日志或dump。

外部样本记录仅为操作时资源观察：状态、历史、卡片数量均不匹配，**没有A/B性能收益推断**，固定短等待只用于交互就绪，不是稳态。
**AOT BLOCKED、#49 pause FAIL、历史net9 crash未归因、#56预算和任意大重排复杂度限制保留**。
空页、撤销精确列表恢复、前台恢复限制意味着完整性能验收仍为HOLD；既有离线绿例不替代这些真实应用缺口。

当前工作树kernel最初是独立gitdir的未展开状态，HEAD f02c33f且index列出删除；没有实际文件或untracked内容。
初始index/status已留ignored；从现存bundle恢复并checkout指定 `0ae5e4e000a41a07f6696746706df11c06e8835b`，clean。
首次误用bundle ref失败，随后按list-heads的 `integration/dependency-upgrade-d48`恢复。仍为**local-only bundle**，不声称remote可达。

最终 [final-state](full-app-net11/final-state.json)：owned应用0、观察器0，源代码无差异；
旧layout、新layout、旧试部署目录和登录均保留，最终注册精确位置见上。
`winapp ui yield --on local --json` exit0、released:false（4秒grace早已过期），此后没有桌面输入。
PID9608/Dev/生产及其他任务未操作；没有临时非认证注册需要清除。
本地父tip仍043d1e8a，已包含于本分支；提交仅本验收报告/脚本/安全证据，未改父分支、push、PR或issue。
