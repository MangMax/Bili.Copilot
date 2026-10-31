# Season / 双 pane 有界挂载诊断

本片接续 `e37a732a` 与独立评审 `17cabda1`，父基线 `feat/performance-45=90e215f0`。只处理评审指定的 Season / 双 pane 挂载适用性阻断；已批准的 29 处绑定、Basic/Blur 重挂、通知 OneWay 未再扩改。实现者未启动子代理；本报告是实现证据与自审，交父作一次独立 review，不冒充独立批准。

## 归因与最小生产变更

新增生产 diff 仅四行：`EntertainmentIndexMainControl.xaml`、`PgcSectionDetailControl.xaml`、`AnimeTimelineMainControl.xaml`、`PgcFavoriteBody.xaml` 的直接DataTemplate根 `SeasonCardControl.ViewModel="{x:Bind}"` 改为 `ViewModel="{Binding}"`。前两处Index样式有本轮原生红绿；后两处为相同AppRepeater/ContentPresenter/SeasonCardControl直接根交接，按静态同源范围补齐，并经生产编译；不声称Timeline/Favorite内层样式有新的原生全流程验收。内层 typed property binding、SeasonCardControl 生命周期、图片绘制、布局资源、缓存策略和依赖均未改。

XML枚举另发现`Player/PgcSeasonsSection.xaml`先以ItemContainer作DataTemplate根、SeasonCardControl为嵌套子控件，结构不同，保留原状并列后续；本片没有它的失败证据或认证播放器验证。`season-scope.json`明确区分4个直接根与这个嵌套入口，没有称作全部Season入口通过。

固定原生产 IndexSeason 模板、50 个原 adapter 对象、1400×900 物理窗口、一次 mount 后，只切 cache20 / cache0.5。原模板 cache20 重复在 Loaded 后、scroll-start 前的 `AppItemsRepeater.MeasureOverride` 抛 `COMException(0x80004005)`；cache0.5 此次可进入滚动，却存在 presenter.Content / card.DataContext 为当前对象而 ViewModel、文字、图片 Source、命令缺失的持续空卡。坏根的 compiled DataTemplate component 为空，与已审查的回收交接缺口一致。历史 Season-only cache0.5 的 E_FAIL 仍保留，不声称每次都在相同缓存值复现。

`season-binding20`、`season-binding-red20`、`season-binding05` 启动 binary hashes 相同。只切旧/新 Season 外层模板：旧 cache20 再次在相同阶段崩溃；新 cache20 与新 cache0.5 都完成快速滚动、逐可见卡对象/文字/图片 URI/命令检查、最终图片 readiness 与 retained exit=0。没有用 baseline 也失败来豁免，也没有删掉原 fixture。

由此定位到生产模板的 compiled 整对象绑定与 ContentPresenter 模板复用的交接边界，且最小绑定替换有同 binary 因果对照。**没有声称找到了 WinUI native 内部的某一源码行。** managed first-chance 堆栈、unhandled 和阶段日志保存在各 run；native dump 提取的五个 stowed exceptions 指向 XAML/Controls 布局栈。winapp 0.7.1 的 ClrMD 不支持本机 .NET 11 调试版本，自动分析失败；匹配 PDB 的公开符号下载返回 404，符号失败与原始地址均保留。结构解析依据 [Microsoft stowed exception 文档](https://learn.microsoft.com/en-us/windows/win32/wer/stowed-exception-information-v2) 和 [WinUI ErrorContextStructure](https://github.com/microsoft/microsoft-ui-xaml/blob/main/dxaml/xcp/components/base/inc/ErrorContextStructure.h)，不拿未知函数名补全因果。

## 双 pane：原红保留，布局独立性得到直接验证

保留前审 User 模板形状、每 pane 500 项、1660×1060 物理窗口、左列400 DIP、每 pane高400 DIP。共享 layout 取同一个 `UserLayout` 对象；独立臂只复制其布局参数。当前绑定模板的 `pane-single` → `pane-shared` → `pane-independent` 共用 binary，cache均0.5。这些初轮沿用候选adapter的7位ID `9000000`，前审原值实际为`12345`，不能称作完全相同数据。最终`pane-final*`恢复原ID及其他字段，并取消强制UpdateLayout，重做限定对照。各 run保存每次 scroll 的两个 offset、viewport、extent和全部 realized indices；检查左滚动不改变右 offset/range，右滚动不改变左 offset/range，以及左首项与右末项同时实现。

前审 `independent-evidence.zip` 的 `candidate-two-pane` 实际使用旧 User x:Bind 与 cache2，不能冒充本轮cache0.5。旧模板在新进程cache0.5下的 `pane-legacy-shared` 挂载/范围测试可完成（此运行没有内容绑定正确性断言）。固定前审cache2，`pane-legacy-cache2` 和 `pane-legacy-independent2` 仅切共享/独立layout，均在Loaded后、滚动前 E_FAIL。`pane-fixed-cache2` 保持cache2、双pane共享layout、数据和尺寸，只切已批准的User Binding后通过；这组三个run同binary。因此共享layout不是该旧模板异常的必要条件，无须新增layout实例策略或全局调度。保留原baseline/cache2红，不将其转写为成功。

## 最终自然布局回归与准入

最终 `season-final` 取消诊断的同步 `UpdateLayout`，使用自然measure、50个唯一标题/唯一1600×900 PNG URI和不同命令实例。覆盖30步快速往返/跳转、settle、120次width变更及再次settle；每个相交外层根都检查当前items source、presenter.Content、card.DataContext、ViewModel、实际文字、图片Source、确切命令对象，null不会被过滤。最终按可见index逐图截图检查预期RGBA，图片文件与可见卡集合必须完全对应。

`season-final`的152个快照共1326条可见卡检查通过，120次width变更的最大heartbeat为74.7689ms；最终10张可见卡图片全部通过RGBA检查（单卡RenderTargetBitmap，不是每个快速瞬间的屏幕像素证明）。每张唯一PNG保持原1600×900尺寸。

`pane-final-single` / `pane-final` / `pane-final-independent` 同样取消强制UpdateLayout，使用前审确切数据，再次验证当前cache0.5的单/双与共享/独立layout。最终原ID/cache2的旧共享、旧独立、固定共享由`pane-final-legacy2` / `pane-final-legacy-independent2` / `pane-final-fixed2`记录。最终准入结论见交付结果。

### 交付结果与自审

**结论：前审唯一的Season/双pane挂载适用性阻断已有关闭证据，建议将既有候选加本片四处直接根修复作为共享cache0.5整体候选提交独立review。** 这是对前审规定的有界准入条件的判断，不是全部页面/样式或全机器完成声明。没有因baseline也失败而免除归因；两组同binary旧模板红→固定模板绿明确了修复边界。源码与raw自审按systematic-debugging、WinUI开发/测试、winui-code-review和verification-before-completion执行，无新增全局机制。

| 要求 | 直接证据 |
| --- | --- |
| Season挂载及绑定归因 | 同binary `season-binding-red20` 为Loaded后measure E_FAIL；`season-binding20` / `season-binding05` 的 `MountLoadedMeasureScrollStagesRecorded`、`EveryVisibleSeasonCardMatchesCurrentItem` 通过 |
| 快速回收内容、图片和resize | `season-final` 的 `EveryVisibleSeasonCardMatchesCurrentItem`、`SeasonItemsSourceMatchesCurrentItem`、`SeasonResize120Steps200msGate`、`SeasonBindingsSampledThroughoutResize` 通过；`AllSettledVisibleSeasonImagesMatchCurrentItem` 10/10 |
| 原数据、自然布局、单/双与共享/独立 | `pane-final-single`、`pane-final`、`pane-final-independent`通过；双pane的 `LeftScrollPreservesRightOffsetAndRange`、`RightScrollPreservesLeftOffsetAndRange`、`OppositePaneRangesIndependent`、`RequestedLayoutSharing`均通过 |
| 前审双pane E_FAIL归属 | 最终原ID/cache2旧共享与旧独立均复现E_FAIL，固定共享通过；三者与最终自然布局回归同binary，未更换尺寸、数据或依赖 |
| 最小生产变更和编译 | `season-scope.json`逐blob确认四文件只替换四个表达式，4个直接根/1个嵌套根清晰分开；`production3-exit.txt=0`，最终增量生产build 0 warning/0 error |
| 生命周期、WER、保护 | `OwnedRetainedExitAndWER`、`OwnedCleanupAndYield`、`lifecycle-audit.json`、`wer-exact.json`、`protection.json`；原崩溃红与初态偏离不抹掉 |

最终自然布局截图已目视复核：Season当前标题/封面可见；双pane保持不同滚动位置，边缘部分卡按相交规则纳入oracle，没有将它们过滤掉。源码38个输入与生产构建副本一致；归档审计逐payload校验、重算raw并核对同binary组。父最新ref再次核对为`90e215f0`且已包含于本分支。交付后由父安排一次独立review；本实现者没有创建review子代理。

最终ZIP为4,895,751 bytes、2405个受manifest校验的payload；独立解压后audit exit0，六组binary一致性与11条绿运行raw断言全部通过，8条崩溃红和2条旧Season空卡红仍在。原生`head.txt`记录的是提交修复前的合入HEAD；实际编译输入由每run源码快照/binary hash、显式Season旧/新模板选择器以及生产38项输入hash固定，不能把head收据冒充“原生编译了最终提交”。四处生产XAML的最终编译与adapter host验收分别记录。

## 保留的代价与限制

* 原 `e37a732a` 的 User50 cache0.5 resize max 121.8969ms vs cache20 78.1089ms 代价仍成立；不重复测量或篡改该记录。User500 的优势、cache20 remount readiness红、cache1/cache2历史红及所有原E_FAIL继续引用原归档。
* heartbeat是16ms请求的dispatcher计时，不是composition帧时间；200ms门槛不等于60fps。本轮是本机离线生产控件/模板加adapter，不代表所有机器、所有认证页面、分页或全部Season样式验收。没有重跑572或codec矩阵。
* pathless Binding只传当前对象，内层compiled binding保留；没有新增动态属性字符串。没有本轮Native AOT publish或新增WinUI analyzer包证明；正式依赖禁止变更，未为满足技能默认建议安装新包。
* 归档脚本原先只记录unhandled，最早配对的新增阶段日志被过滤，故其阶段证据不足；随后修正fixture日志，保留原红。旧checker把任意trace文件当失败，本片使用读取具体异常kind的checker，未删除trace来制造通过。
* 调试启动器首次路径分隔符比较未匹配到owned PID，`season-native20`未调用诊断动作；另行精确PID/path关闭并保留32秒retained exit=0收据，不算验收运行。后续启动器修复路径归一化。
* 最后批处理的两次Python→Windows PowerShell启动在`Get-FileHash`模块发现处失败，均在app启动前；空stage移入本区metadata保留，没有复用/覆盖native结果。改回此前验证过的PowerShell直接驱动；它们不算native样本。

## 构建、保护与可复放证据

所有构建与native采样分时执行，11个记录的build区间与22个正式native区间无重叠。生产源码在本区 `artifacts/recycled-cards/source` 编译；正式工作区kernel HEAD/index不动。初始kernel HEAD为`f02c33f6`，index文件不存在，status已保存；不得把其相对gitlink的初始偏离列成本片修改。当前对象库缺少要求的`0ae5e4e`对象，故从指定来源区已有kernel源快照只读复制，474个非bin/obj源文件归一化hash逐项一致。最终生产38个相关输入与本区生产文件归一化hash匹配。

准备脚本原始CRLF校验失败和SET_BY_PREPARE的失败构建日志保留。生产首次Rebuild已编译到DLL，但源码归档缺libmpv触发仓库打包输入校验；只从来源区复制既有libmpv/ffmpeg/BBDown至本区构建副本后增量复验，exit0、0 warning、0 error，不改版本、不发布/注册生产包。首次Rebuild的1条protobuf unused-import warning和末尾输入校验错误原样保留，不称为一次clean Rebuild成功。

owned identity仅`BiliCopilot.RecycleD228Mount.Owned`；22条正式正常/崩溃run均记录 retained handle 的HasExited/ExitCode、精确PID/path、至少32秒观察、匹配WER、owned包数量0及yield收据。稍后的精确PID/path复查在原观察时间窗内补获8条预期崩溃红的Event1000，均同时匹配PID和完整image path；原runner的空WER文件不覆盖，补查见`wer-exact.json`，不把最初未读到事件当作没崩溃。正常exit0的run均无匹配事件。另一个未执行动作的`season-native20`恢复运行有独立retained退出收据，但不计入这22条完整收据。yield的`released:false`表示当时没有剩余占用，不伪称拿到了新桌面锁。未操作AotDev、Caa6a7be/Dev、官方或用户现有实例/认证，未改试用包、未push/PR/issue。

精简 [evidence ZIP](mount-diagnosis-evidence.zip) 保存所有本轮红/绿raw、源码快照、binary hashes、截图、阶段与managed堆栈、构建/退出/WER/清理收据及SHA256/长度manifest。大型native dumps留在本机诊断路径，不重复放入ZIP；stowed提取结果、原生debug log和解析脚本入档。原两份候选/评审ZIP继续随git历史保留，不再复制。

新checkout可运行 `mount-diagnosis/prepare.py --kernel-repo <含0ae5e4e的只读仓库>`；若使用保留源快照，则传 `--kernel-source <快照>`，会逐文件校验。随后构建生成的Harness项目，再用同目录run.ps1运行如下限定case：

```powershell
dotnet build artifacts/recycled-cards/host/Harness.csproj -c Release -p:Platform=x64 -p:NuGetAudit=false
powershell -NoProfile -File docs/list-resize/mount-diagnosis/run.ps1 -Stage old20 -Action ExtraGreen -Cache 20
powershell -NoProfile -File docs/list-resize/mount-diagnosis/run.ps1 -Stage fixed20 -Action ExtraGreen -Cache 20 -Fixed
powershell -NoProfile -File docs/list-resize/mount-diagnosis/run.ps1 -Stage fixed05 -Action ExtraGreen -Cache 0.5 -Fixed
powershell -NoProfile -File docs/list-resize/mount-diagnosis/run.ps1 -Stage final-season -Action ExtraGreen -Fixed -Unique
powershell -NoProfile -File docs/list-resize/mount-diagnosis/run.ps1 -Stage final-pane -Action ImageChecks -Family PaneNaturalShared
python docs/list-resize/mount-diagnosis/check.py artifacts/recycled-cards/final-season
powershell -NoProfile -File docs/list-resize/mount-diagnosis/pixels.ps1 -Stage artifacts/recycled-cards/final-season
```

旧模板运行预期失败，必须保留runner/checker各自exit。`PaneSingle`、`PaneShared`、`PaneIndependent`是当前0.5配对；`PaneLegacyShared -Cache 2`、`PaneLegacyIndependent -Cache 2`、`PaneShared -Cache 2`复放前审归因。解压证据后运行`mount-diagnosis/audit.py <解压根目录>`复核manifest、同binary组和raw断言，无须重占桌面。
