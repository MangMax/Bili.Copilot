# WinUI Analyzer（阻断）

`Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer` 是 WinUI 官方分析器，覆盖 UWP 遗留 API、
x:Bind 与布局陷阱、MVVM 旧写法、互操作问题。本文件记录它的接入方式、当前基线，
以及 #114 把"观测"收紧成"阻断"之后的现状、误报处理与回滚方式。

## 现状：常驻，参与阻断

- 分析器由 `src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj` **无条件**引用，版本集中在
  `src/Directory.Packages.props`（`0.7.1`），`PrivateAssets="all"`，不外传给其它工程。
- **默认构建就加载分析器**：它参与每一次编译，`src/Directory.Build.props` 的
  `TreatWarningsAsErrors=true` 把任何新的 WUI warning 直接变成编译错误 —— 新写的 `x:Bind`
  漏 `Mode`、字段式 `[ObservableProperty]` 之类当场让构建失败，不用等观测 job 或人工比对 SARIF。
- `WinUIAnalyzerObservation=true` 只剩两个职责：把编译器 `ErrorLog` 指向 SARIF（CI 与人工
  比对用），以及给它建好父目录。它**不再**关掉 warnings-as-errors，本工程与全仓一致；唯一
  例外是 CS9264 的"不升级为错误"，理由见下面「CS9264」一节。
- 分析器只在 UI 工程引用，`BiliCopilot.UI.Models` 等工程不加载它；不改任何 `NoWarn`
  （`MVVMTK0046` 除外，见口径）、不写 `#pragma warning disable`、不降级任何规则。

## 跑一次带 SARIF 的构建

平时改代码不需要这个开关（默认构建已经在拦 WUI 诊断）；要拿 SARIF、核对基线或看 note 级
规则时，在仓库根目录执行：

```powershell
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -p:Platform=x64 `
  -p:SolutionDir="$PWD/src/" `
  -p:GenerateAppxPackageOnBuild=false `
  -p:AppxPackageSigningEnabled=false `
  -p:WinUIAnalyzerObservation=true
```

- `SolutionDir` 必须给：`BiliCopilot.UI.Models` 靠它找 `Resources.resw`，
  少了它资源生成器不产出 `StringNames`，UI 工程会编译失败。
- `GenerateAppxPackageOnBuild=false`：只要编译结果，不跑 MSIX 打包。
  打包前的运行时依赖校验只挂在打包目标上，打包不跑它自然也不跑，所以本机没有
  libmpv / ffmpeg 也能得到退出码 0。观测开关本身**不会**跳过这条校验：一旦真的打包，
  无论开不开观测开关都会先校验依赖。
- `AppxPackageSigningEnabled=false`：不签名。CI runner 上没有 `CN=Mang` 发布证书，而 MSIX
  打包管线即使 `GenerateAppxPackageOnBuild=false` 也会跑签名校验，报
  `SigningCertificateThumbprintNotInStore` 直接打红；本机装了这张证书、不带也能过，命令里保留
  它是为了和 CI 完全一致。
- SARIF 默认写到 `artifacts/winui-analyzer.sarif`（`artifacts/` 已被忽略），
  需要换位置时加 `-p:WinUIAnalyzerSarifPath=<绝对路径>`。
- csc 只在真正执行编译时写 ErrorLog。上一次构建是最新的时候不会重跑编译器，
  这时先 `dotnet clean` 或删掉 `src/Desktop/BiliCopilot.UI/obj` 再构建。

## 当前基线

分析器 `0.7.1`，Release / x64，2026-11-03 的结果（#114 收紧后复核：默认构建与带 SARIF 的构建都是这个数；
#116 把 master 的 #94 字幕改动合进来后按同一口径复核过，warning 仍是 0，note 由 272 降为 264）：

| 规则 | 级别 | 数量 | 说明 |
| --- | --- | --- | --- |
| WUI3001 | warning | 0 | `[ObservableProperty]` 旧字段写法，未用 partial property（#109 批次一 100 条 + #110 批次二 201 条已全部迁完） |
| WUI2011 | warning | 0 | `x:Bind` 缺显式 `Mode`（#111 已清零：73 个命中位置 —— 25 处补 `OneWay`、48 处写显式 `OneTime`；同一元素上的多个绑定各写一个 `Mode`，实际新增 81 个属性（25 `OneWay` + 56 `OneTime`）） |
| WUI2010 | warning | 0 | 嵌套 `x:Bind` 路径缺 `FallbackValue`（#112 已清零：52 处逐条补 `FallbackValue`，保留原有 `Mode`） |
| WUI1001 | warning | 0 | 已清零（#113）：保存图片改用 `Microsoft.Windows.Storage.Pickers` |
| **warning 小计** | | **0** | 0 + 0 + 0 + 0；四类 warning 全部清零，只剩 note 级 WUI2020 / WUI1010 |
| WUI2020 | note | 264 | 缺失 `AutomationId`（#116 合入 #94 字幕改动后 272 → 264，见口径） |
| WUI1010 | note | 2 | 迁移表的特性提示 |

口径：

- 小计是**按位置去重**后的 warning 数（基线 427；#111 清零 WUI2011 后 354，
  #109 迁移 WUI3001 批次一后 254，#113 清零 WUI1001 后 253，#112 清零 WUI2010 后 201，
  #110 清零 WUI3001 后 0）。WUI2010 清零前 SARIF 里的原始条目是 54，其中
  `Controls/Comment/CommentMainPanel.xaml:63` 与
  `Controls/Search/SearchHeader.xaml:49` 各被上报两次（同一位置），按位置去重后为 52。
- note 级（WUI2020 / WUI1010）不计入小计，单独记。
- WUI2020 从 272 降到 264 是 #94 字幕设置改版的直接结果，不是漏报：字幕设置控件从
  `Controls/Core/PlayerOverlay.xaml` 搬进 `Controls/Core/OverlayTransportControls.xaml`，
  PlayerOverlay 的 note 由 13 降到 6（旧的入口按钮、开关、两条滑杆、字体下拉、延迟数字框随
  TeachingTip 一起删除），OverlayTransportControls 由 27 降到 26（新按钮/开关/滑杆/下拉/数字框
  都写了 `AutomationId`，旧的可视化体积被合并）。SARIF：`artifacts/winui-analyzer.sarif`，
  WUI2020 264 条、WUI1010 2 条、四类 warning 一条不剩。
- WUI3001 分两批，均已完成：批次一 #109 迁移了警告最密集的 5 个 ViewModel（100 条），
  批次二 #110 收掉剩下的 201 条。每批都要求“该批文件在观测构建里 WUI3001 为 0”，
  而不是整体清零后才算完成。
- #110 收口时删掉了 `MVVMTK0045`（只为字段写法存在，迁移后 0 命中）；`MVVMTK0046` 留在
  `NoWarn` 里，它与字段写法无关（命中的是 8 个 `[GeneratedBindableCustomProperty]` 条目
  视图模型里的 46 个 `[RelayCommand]` 方法，属 AOT 绑定遗留），删掉会让默认构建直接失败
  （跟踪：#117）。
- WUI1001 的 1 条在 #113 一次清零：`GalleryPageViewModel.SaveImageAsync` 换成
  `Microsoft.Windows.Storage.Pickers.FileSavePicker`（构造时传 `WindowId`，取消返回
  `null`，落盘走 `FileStream`）。该 API 由 `Microsoft.WindowsAppSDK.Foundation 2.6.4`
  提供，已用 `winapp find-api` 核对成员；规则留在表里作为回归项，不再是待清存量。

### CS9264：只在写 SARIF 的构建里出现

`[ObservableProperty]` 从字段写法换成 partial property 后，编译器用 `field` 关键字实现属性；
类有显式构造函数、构造函数里又没给某个非空引用类型属性赋值时，会报 CS9264
（"退出构造函数时该属性可能为 null"）。字段写法时代报的是 CS8618，而 `src/.editorconfig`
只把 CS8618 设成 `none`，所以 CS9264 是迁移后才冒出来的新 ID。
#110 迁移完成后同一棵树、同一份源码的实测结果：

| 构建 | CS9264 |
| --- | --- |
| 观测构建（`-p:WinUIAnalyzerObservation=true`，会写 SARIF） | 57 个属性；SARIF 里 73 条原始记录 / 45 个不同位置 |
| 同上，只把 `ErrorLog` 清空（`-p:ErrorLog=`） | **0**（控制台一条都没有） |
| 默认构建（warnings-as-errors 开，`-t:Rebuild` 全量编译） | **0** |
| 默认构建（`-p:TreatWarningsAsErrors=false`，`-t:Rebuild`） | **0** |

所以原因不是增量编译、条件编译、editorconfig 降级或 `WarningLevel`：两边都是
`/warn:11`、`/nullable:enable`、同一份 `/nowarn`、同样三份 `/analyzerconfig`，
只把 `ErrorLog` 清空后 WUI2010 / WUI1001 照旧上报、CS9264 却全部消失，
说明"写 SARIF 的那次编译"才会产出这些诊断（用 `-p:RunAnalyzers=false` 复测仍有 CS9264，
可以排除是分析器加的）。

#114 收紧时的实测（同一棵树）：

- **默认构建（分析器常驻 + warnings-as-errors）仍然绿**：它不写 SARIF，一条 CS9264 都没有。
- **带 SARIF 的构建必须把 CS9264 从 error 降回 warning**：那次编译同时有 `/warnaserror+` 与
  `/errorlog`，114 条 CS9264（57 个属性 × `XamlPreCompile` 与 `CoreCompile` 各一遍）会被升成
  error，先把 `XamlPreCompile` 打挂，再级联出 22 条 WMC0001（`Unknown type '...Converter'`）
  与 WMC9999（构建 69 个错误）。所以 `BiliCopilot.UI.csproj` 的观测开关里留了
  `<WarningsNotAsErrors>$(WarningsNotAsErrors);CS9264</WarningsNotAsErrors>`：warnings-as-errors
  照常开着（WUI 诊断照样能挡住），只这一条"只在 /errorlog 编译里出现"的编译器诊断不升级成
  错误。这不是 WUI 规则降级，也不影响默认构建。
- **不要因此把 `ErrorLog` 搬进默认构建**：搬进去这些诊断就会随 warnings-as-errors 变成 57 处
  构建失败（最小复现工程验证：`/warnaserror+` 下 CS9264 是 error）。
- CI 的 SARIF 会继续带这些条目（73 条原始记录 / 45 个位置），不要当成新回归。

## 已经阻断（历史）

四类 warning 一次只收紧一类，每一步的判据都是「带 SARIF 的构建 + `-p:WarningsAsErrors=<规则>`
为 0 错误」；四类都归零之后才收紧成阻断（#114）：

1. **WUI3001 已清零（#109 + #110）**：`[ObservableProperty]` 的字段写法迁成
   `[ObservableProperty] public partial T Name { get; set; }`，保留生成的属性名、初始化器、
   可空性与手写 `OnXChanging/OnXChanged` 钩子。批次一（#109）迁移警告最密集的 5 个 ViewModel
   共 100 条，批次二（#110）收掉剩余 201 条（64 个文件），并删掉只为字段写法保留的
   `MVVMTK0045` 抑制。`MVVMTK0046` 属于另一件事（见上面口径，跟踪：#117），没有删除。
2. **WUI2011 已清零（#111）**：逐个判断绑定是否真的随数据变化 —— 路径上能发变更通知的才写
   `Mode=OneWay`（25 处）；源没有变更通知的（内核 POCO、`x:Name` 元素引用、构造期定值的
   模板项）写显式 `Mode=OneTime`（48 处），把"就是一次性的"讲清楚，给这类源补 `OneWay` 只会
   得到一条永远不更新的死绑定；事件绑定、命令绑定和整项 `{x:Bind}` 本来不触发规则，保持原样。
   仓库没有 `x:DefaultBindMode`，所以这些命中不是继承模式造成的误报。
   合计 73 个命中位置；有的元素同时挂 2~3 个 `x:Bind`，每个绑定都要单独写一个 `Mode`，所以
   diff 里实际新增 81 个属性（25 `OneWay` + 56 `OneTime`）—— 按 diff 数属性会看到 81，按命中
   位置数是 73，两个数都对，别拿其中一个去核另一个。
3. **WUI2010 已清零（#112）**：52 个位置全部补 `FallbackValue`，`Mode` 原样保留（#111 的判定
   不被覆盖）：文本/路径类给空串或空引用，布尔/可见性/命令类给"不成立"一侧，数值类给控件
   初值。没有改绑扁平属性 —— 这些中间段在对应页面都是单例注入或构造期就位，拍平反而要把
   派生状态再复制一份。
4. **收紧为阻断（#114）**：引用移出条件开关常驻；观测开关只保留 SARIF 的 `ErrorLog` 与建目录，
   不再关 warnings-as-errors（CS9264 那一条例外见上一节）；CI job 去掉 `continue-on-error`、
   去掉 `pull_request.paths` 过滤，纳入分支保护的必需检查。基线从此由构建本身守着：默认构建
   0 警告，新增任何 WUI warning 当场失败。

## 误报怎么办

收紧之后任何 WUI 诊断都会让构建失败。若某条确实是误报：

- 只能**窄范围**抑制并写明原因：在出问题的那一行 / 那一个文件加最小的
  `#pragma warning disable WUIxxxx`（或 XAML 里等价的局部写法），紧邻一行注释说明
  "为什么是误报、什么条件下应该删掉"；
- 不能整类规则降级：不改 `.editorconfig` 里 `dotnet_diagnostic` 的严重级别、不加 `NoWarn`、
  不把分析器重新挂回条件开关、也不为了绕过它关掉 warnings-as-errors；
- 在本文件记一条（规则、位置、理由、复查条件），让下一个人知道这条例外从哪来.

## CI

`.github/workflows/winui-analyzer.yml` 是独立工作流，只有一个 job
`winui-analyzer-observation`（job id 就是分支保护里的必需检查上下文名，改名字会让规则失效；
`observation` 是历史遗留，它现在跑的是阻断构建）。这个 job **应当**是分支保护里的必需检查，
构建变红就是合并被挡；保护规则由维护者设置（agent 的令牌没有管理分支保护的权限）：

```bash
gh api --method PUT repos/MangMax/Bili.Copilot/branches/master/protection/required_status_checks \
  -f strict=false -f 'contexts[]=test' -f 'contexts[]=winui-analyzer-observation'
```

设置前它只会把 PR 变红、不会真正挡住合并 —— 这是配置缺失，不是工作流失效。

- 用同一个开关跑一次构建：warnings-as-errors 开着，所以这一步既是 SARIF 报告，也是门禁 ——
  新增 WUI 诊断会让这个必需检查直接变红、挡住合并；
- 构建带 `-p:GenerateAppxPackageOnBuild=false -p:AppxPackageSigningEnabled=false`：
  **签名是关掉的** —— runner 上没有 `CN=Mang` 发布证书，而 MSIX 打包管线在
  `GenerateAppxPackageOnBuild=false` 时仍会跑签名校验，直接报
  `SigningCertificateThumbprintNotInStore`，与 WUI 无关地把 job 打红。这个 job 只要编译器诊断，
  不产包也不签名；本机装了这张证书所以不带也能过，CI 必须带；
- `pull_request` **没有** `paths` 过滤：必需检查必须在每个 PR 上都触发，否则没触发时检查会
  停在 pending，永远等不到绿色；
- 没有 job 级 / 步骤级 `continue-on-error`：构建失败就是红色、就是挡合并；构建步骤也没有
  吞错的 `if:`（`if: always()` 只挂在汇总、过滤、上传这些事后步骤上）；
- 送 code scanning 的是**过滤副本**：构建的 ErrorLog 里除 WUI 外还有约 2751 条 IDE/CA/RCS
  诊断，全量上传会把代码扫描变成 IDE 风格检查面板、并留下规则清零后的陈旧告警。工作流在构建
  之后用 pwsh 只保留 `WUI*` 结果（`version`、`$schema`、`runs[0]` 的
  `tool`/`artifacts`/`invocations` 原样保留，只替换 `results`），完整 SARIF 仍作为
  artifact 上传；送 code scanning 的仍然是 `category: winui-analyzer`。fork PR 上
  `security-events` 被降级成只读、`upload-sarif` 必然失败，所以那一步在 fork PR 上直接跳过
  （SARIF 仍留在 artifact 里），免得用与规则无关的步骤把外部 PR 挡红；
- 构建失败时只上传已经存在的文件（`if-no-files-found: warn`），
  摘要里会写明是没生成 SARIF 还是规则计数。

## 回滚方式

阻断由三处设置共同生效，回滚要一起改回去（等价于 revert #114 的合并提交）：

1. `BiliCopilot.UI.csproj`：把 `Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer` 的
   `PackageReference` 放回 `Condition="'$(WinUIAnalyzerObservation)' == 'true'"` 的 `ItemGroup`，
   默认构建不再加载分析器；
2. 同一个开关的 `PropertyGroup` 里加回 `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>`，
   并清掉 `WarningsNotAsErrors` 里的 CS9264；
3. `.github/workflows/winui-analyzer.yml`：job 级与步骤级加回 `continue-on-error: true`，
   `pull_request` 加回 `paths:` 过滤；同时从分支保护的必需检查里移除
   `winui-analyzer-observation`，否则没触发的 PR 会一直停在 pending.

## 不要做

- 不要为了消灭诊断去加 `NoWarn`、`#pragma warning disable` 或降级规则；误报按「误报怎么办」处理。
- 不要把分析器重新挂回条件开关，也不要把 `ErrorLog` 搬进默认构建（CS9264 会变成 57 处硬错误）。
- 不要在其他工程里引用这个分析器。
- 不要为凑数批量补 `Mode` / `FallbackValue`：先判断绑定是否会变化、嵌套路径是否真的可空，
  再决定改法；分析器只是把候选暴露出来，不是要求逐条照改.
