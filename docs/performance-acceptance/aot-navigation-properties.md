# NavigationViewModel：六个 ObservableProperty 字段迁移

2026-10-31，独立工作区 `t3-7a7ce646`，起点与本地父 `feat/performance-45` 均为 `608acc77123654c2a60d2bb3d72a5b7a7c3c2132`。生产修改只有 NavigationViewModel 六处声明。**全应用 AOT / 性能采用仍 HOLD，#49 FAIL 不变。**

已读 AGENTS、domain 指引、现有性能计划、[严格审查与切片规格](aot-payload-and-compatibility.md)、[导航构造集成报告](integration-aot-navigation-constructor.md)，应用 tdd、code-testing、run-tests、csharp-refactoring、WinUI review/dev-workflow、verification 技能。无仓库测试 overlay；未找到 implement-spec 技能文件，未声称调用。无子代理。

## 输入与修改边界

初始根源码 clean，kernel 子模块并不 clean：HEAD 为 `f02c33f639addd5d421700c43fa66382beafe323`，存在大量索引删除。先保存 `initial-root.txt`、`initial-kernel.txt`、`initial-kernel.patch`，再从父工作区只读本地 fetch 精确对象，在本工作区恢复 gitlink 指定的 `0ae5e4e000a41a07f6696746706df11c06e8835b`，结束保持该提交 clean。未修改 gitlink、kernel 源码、正式第三方依赖或父 checkout。媒体资产只从既有父工作区复制到本工作区，无下载/安装。

实际 SDK 为 `11.0.100-rc.3.26522.148`，MVVM NuGet 为 `8.4.2`；生成代码的 GeneratedCode 版本字符串仍是 `8.4.0.0`，不能据此误报 NuGet 版本。原 VM 恰好六个字段，以及一个既有 partial `IsTemporaryOverlayClosed`。

| 原字段 | 保留的 public get/set 属性 | 默认值 |
| --- | --- | --- |
| `_isOverlayOpen` | `bool IsOverlayOpen` | false |
| `_menuItems` | `List<AppNavigationItemViewModel> MenuItems` | null，Initialize 时赋值；未改为 new 或 nullable |
| `_isTopNavBarShown` | `bool IsTopNavBarShown` | false，Initialize 从设置读取，fallback true |
| `_isFavoritesPage` | `bool IsFavoritesPage` | false |
| `_isHistoryPage` | `bool IsHistoryPage` | false |
| `_isViewLaterPage` | `bool IsViewLaterPage` | false |

六字段均无初始化表达式、Dependent/CanExecute 注解或自定义 hook 实现；全源搜索没有其直接 backing-field 读写，也没有第二个 NavigationViewModel partial 源声明。仅换为 `public partial ... { get; set; }`，保持文件 BOM、行数和方法体。

实际应用生成 setter 对照：EqualityComparer 相等早退；changing 单参数/双参数 hook → 存储赋值 → changed 单参数/双参数 hook → PropertyChanged。**生产项目既有 `MvvmToolkitEnableINotifyPropertyChangingSupport=false`，不发 PropertyChanging。** 归一化 backing storage 名称和 partial 修饰后，生成 setter 执行体相同。旧 MenuItems setter 的 MemberNotNull 指向被移除的私有字段；新属性不再需要它。生成的未实现双参数 MenuItems hook 的 oldValue 从 nullable 注解变为非 nullable；该 VM 没有 hook 实现，partial void 被擦除，不改变编译后公开 API 或运行行为，不能声称生成文本逐字相同。

RootLayout.xaml 的 IsOverlayOpen（返回按钮与两个 frame 可见性）、MenuItems（菜单源）、IsTopNavBarShown（顶栏可见性）、三个子页标志（IsSelected）都是显式 `x:Bind Mode=OneWay`，无 XAML 修改。实际应用生成 WinRTCustomBindableProperties 中迁移前后都没有 NavigationViewModel 项：该 VM 本就没有 GeneratedBindableCustomProperty。生成 WinRTVtable 仍暴露 INotifyPropertyChanged；不移除 attribute，不新建 provider/command 包装，也不把诊断消失解释成完整动态绑定已实机验证。

## 严格编译红绿

使用既有 export-source.ps1 建立无 `.git` 的独立源码副本，再使用既有 audit-build.ps1 移除应用 NoWarn 中 IL / MVVMTK0045 / MVVMTK0046，设置 `CsWinRTAotWarningLevel=2`。仅审计命令全局允许 unsafe，沿用历史口径以越过 kernel CsWinRT1030；这不是正式修复。优化、分析器、warnings-as-errors 与正式依赖保持。未发布/执行 ILC。

| 去重诊断 | 迁移前 red | 迁移后 green-final |
| --- | ---: | ---: |
| MVVMTK0045 | 309 | 303 |
| MVVMTK0046 | 47 | 47 |
| CsWinRT1028 | 8 | 8 |
| 合计 | 364 | 358 |

两次严格构建都 exit 1，这是存量阻塞保留的正确结果。独立解析日志并去掉 summary 重复：集合差恰好 NavigationViewModel.cs 的六条 MVVMTK0045；新增集合为空，其余358条文件/行号/消息逐条相等。red 还与历史压缩诊断按文件、代码、消息独立核对（旧行号允许变化），并与 `level2-by-file.json` 全表相等。

第一次 green 写文件时未保留 BOM；随后恢复原 BOM，在相同审计副本重跑 green-final，仍358条。最终审计 VM 输入与提交 VM 字节完全相同。red 原源、各轮日志、binlog、命令参数、source export、完整生成代码及 PDB document 校验和均在 ignored `artifacts/navigation-properties/`；小结果见 [validation.json](evidence-navigation-properties/validation.json)，复算见 [verify.py](evidence-navigation-properties/verify.py)。

## 行为回归与验证边界

测试直接链接真实 NavigationViewModel、MainFrame、OverlayFrame、NavigationPageFactory、SearchBoxViewModel；替代 Windows 控件/窗口、设置、DI、外部 navigation item 与具体页面呈现，不复制 VM 方法。保留原23项导航用例（含17页构造目录、未知类型、异常包装/inner identity、缓存和失败状态），新增14项。网络接口适配器收到调用即抛错，不登录、不发送请求。

| Requirement | Evidence |
| --- | --- |
| 默认值、初始化设置/选择、集合 identity、初始化幂等 | `DefaultsAndInitializePreserveSettingsSelectionAndCollectionIdentity` |
| 保存页面隐藏时 fallback、顶栏关闭设置 | `InitializeFallsBackFromHiddenSavedPageAndHonorsHiddenTopBar` |
| 六属性公开 setter、变化后通知、同值不通知、还原旧值、不发 PropertyChanging | `PublicSetterNotifiesAfterChangeAndIgnoresEqualValue` ×6 |
| Favorites/History/ViewLater 参数、标志、窗口激活、返回取消与焦点 | `OverlayNavigationUpdatesFlagsAndBackClearsThem` ×3 |
| INavServiceViewModel 主页调用、菜单选择、临时关闭叠层、重访取消、下次叠层清旧栈及通知顺序 | `MainNavigationTemporarilyClosesOverlayAndRepeatedPageCancelsLoading` |
| 搜索参数/关键词保留、同页命令复用、清栈取消 | `SearchPreservesKeywordReusesPageAndClearCancelsCurrentPage` |
| 初始化前导航异常及窗口无副作用 | `NavigationBeforeInitializeRejectsWithoutActivatingWindow` |

准备阶段第一次基线命令与测试适配器替换重叠，发生 CS0246，保留 baseline-tests.log，不能计作行为 red。随后旧测试默认开启 PropertyChanging 的迁移前后37/524结果也保留，但不作为最终保真证据。核对真实生成代码后，测试项目显式对齐生产 false 配置；六个通知用例在故意 override=true 时全部失败（configuration-red.trx），恢复 false 后通过。这是测试配置敏感性 red，不冒充生产行为缺陷。

最终旧源回归通过临时 baseline-input.targets 仅替换测试项目中 NavigationViewModel 的 Compile 输入为保存的原源，其余测试及生产源相同；不会改写正在验证的工作区生产文件。撤去导入后的首次运行被增量编译复用了旧源 DLL，PDB 检查发现后将日志/TRX保存为 stale-*，不计为新源验收。随后仅对测试项目强制 Rebuild（BuildProjectReferences=false，复用既有依赖输出），再次跑相关和全套。最终在旧源和新源各自显式 Rebuild 完成后，以 --no-build 跑相关和全套，避免两种 Compile 输入互相覆盖；新源测试重建0警告，旧源冻结文件因不再位于 src/.editorconfig 作用域而显示4条原源码 nullable 警告，不改源码或新增抑制。最终相关 **Passed: 37, Failed: 0, Skipped: 0**；全套 **Passed: 524, Failed: 0, Skipped: 0**。逐个 TRX 检查 outcome、唯一 executionId 与计数，旧源/新源编译输入由 portable PDB document 校验和核实。

普通 Debug/Release x64 fresh build 均 exit0：迁移后 Debug 0 warnings / 0 errors，Release 1条既有 protobuf unused-import warning / 0 errors；迁移前 Debug 也为1条同样警告。普通构建继续使用仓库原有 NoWarn，不拿它代替严格审计。WinUI 静态审核未发现本片新增线程、生命周期、绑定名称/模式或通知顺序问题；无 XAML/命令机制/页面 factory 改动。专用 WinUIAnalyzer 未安装/运行，按用户约束未新增依赖。

无 desktop slot，因此没有 UI、应用注册、部署、登录、安装、UAC 或完整 AOT 绑定运行证据；没有触碰 caa6a7be/layout-v3/Dev/PID9608。余下303属性、47命令、CsWinRT/kernel unsafe、SharpGen、Azure、正式 Mpv 缺失成员、Vulkan 分发、真实 WinUI 首次导航及 #49 性能验收全部保留 HOLD。

## 复放入口

在仓库根执行，`$root = (Get-Location).Path`。审计输出目录必须全新；原源先从指定基线导出，再应用仅六行声明差异（export-source 导出当前 HEAD，不能在迁移提交后直接将其当成旧源）。旧源测试的 [baseline-input.targets](evidence-navigation-properties/baseline-input.targets) 复制到 artifacts/navigation-properties 后，仅在 BiliCopilot.UI.Tests 的 CoreCompile 前移除原 NavigationViewModel.cs Compile 项并加入同目录冻结原源；给 test 命令增加 `"-p:CustomAfterMicrosoftCommonTargets=$root/artifacts/navigation-properties/baseline-input.targets"`。最终测试不带该导入，切换时先强制重建测试项目。PDB 读取器 [PdbInputs.cs](evidence-navigation-properties/PdbInputs.cs) 在 ignored net11.0 控制台项目运行，参数为 PDB 路径、输出 JSON 路径；它同时记录编译器 checksum 和实际源码 SHA256。生成文件目录在下一轮构建前复制成 *-frozen，避免后续生成步骤改写证据。

```powershell
./docs/performance-acceptance/evidence-aot-payload/export-source.ps1 -Destination "$root/artifacts/navigation-properties/strict-source"
./docs/performance-acceptance/evidence-aot-payload/audit-build.ps1 -Dotnet (Get-Command dotnet).Source -SourceRoot "$root/artifacts/navigation-properties/strict-source" -Output "$root/artifacts/navigation-properties/red" -WarningLevel 2 -AllowUnsafeForAudit
# 将六属性迁移后的 VM 复制进副本，以全新 green-final 输出重跑同一审计。
dotnet build tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release -t:Rebuild -p:BuildProjectReferences=false
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~Navigation' --logger 'trx;LogFileName=after-faithful.trx' --results-directory artifacts/navigation-properties/tests
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -c Release --no-build --logger 'trx;LogFileName=all-after-faithful.trx' --results-directory artifacts/navigation-properties/tests
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug -r win-x64 -p:Platform=x64 "-p:SolutionDir=$root/src/" -p:AppxPackageSigningEnabled=false -p:EmitCompilerGeneratedFiles=true "-p:CompilerGeneratedFilesOutputPath=$root/artifacts/navigation-properties/generated-after" -bl:artifacts/navigation-properties/debug-after.binlog
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -r win-x64 -p:Platform=x64 "-p:SolutionDir=$root/src/" -p:AppxPackageSigningEnabled=false -bl:artifacts/navigation-properties/release-after.binlog
python docs/performance-acceptance/evidence-navigation-properties/verify.py
```

仅本地提交；没有 push、PR、issue 或父分支写操作。最终父 tip 仍为起点，已包含在当前历史，无额外 merge 内容。
