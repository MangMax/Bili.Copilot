# WinUI Analyzer 观测接入

`Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer` 是 WinUI 官方分析器，覆盖 UWP 遗留 API、
x:Bind 与布局陷阱、MVVM 旧写法、互操作问题。本文件记录它的接入方式、当前基线和
"先观测、后阻断" 的推进顺序。

## 现状：只观测，不阻断

- 分析器只由 `src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj` 引用，版本集中在
  `src/Directory.Packages.props`，`PrivateAssets="all"`。
- **默认构建不加载分析器**：引用挂在 `WinUIAnalyzerObservation=true` 开关下，
  不开开关时包不会被还原，分析器不参与编译；`src/Directory.Build.props` 的
  warnings-as-errors、0 警告的现状都不变。
- 观测构建会把**本工程**的 warnings-as-errors 关掉（存量诊断必然命中），
  不改任何 `NoWarn`、不写 `#pragma warning disable`、不降级任何规则。
  默认构建仍是 warnings-as-errors。

## 跑一次观测构建

在仓库根目录执行：

```powershell
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -p:Platform=x64 `
  -p:SolutionDir="$PWD/src/" `
  -p:GenerateAppxPackageOnBuild=false `
  -p:WinUIAnalyzerObservation=true
```

- `SolutionDir` 必须给：`BiliCopilot.UI.Models` 靠它找 `Resources.resw`，
  少了它资源生成器不产出 `StringNames`，UI 工程会编译失败。
- `GenerateAppxPackageOnBuild=false`：只要编译结果，不跑 MSIX 打包。
  打包前的运行时依赖校验只挂在打包目标上，打包不跑它自然也不跑，所以本机没有
  libmpv / ffmpeg 也能得到退出码 0。观测开关本身**不会**跳过这条校验：一旦真的打包，
  无论开不开观测开关都会先校验依赖。
- SARIF 默认写到 `artifacts/winui-analyzer.sarif`（`artifacts/` 已被忽略），
  需要换位置时加 `-p:WinUIAnalyzerSarifPath=<绝对路径>`。
- csc 只在真正执行编译时写 ErrorLog。上一次构建是最新的时候不会重跑编译器，
  这时先 `dotnet clean` 或删掉 `src/Desktop/BiliCopilot.UI/obj` 再构建。

## 当前基线

分析器 `0.7.1`，Release / x64，2026-11-03 的观测构建结果：

| 规则 | 级别 | 数量 | 说明 |
| --- | --- | --- | --- |
| WUI3001 | warning | 0 | `[ObservableProperty]` 旧字段写法，未用 partial property（#109 批次一 100 条 + #110 批次二 201 条已全部迁完） |
| WUI2011 | warning | 0 | `x:Bind` 缺显式 `Mode`（#111 已清零：25 处补 `OneWay`，48 处写显式 `OneTime`） |
| WUI2010 | warning | 52 | 嵌套 `x:Bind` 路径缺 `FallbackValue`，中间段为 null 时显示异常 |
| WUI1001 | warning | 1 | `Windows.Storage.Pickers` 应换成 `Microsoft.Windows.Storage.Pickers` |
| **warning 小计** | | **53** | 0 + 0 + 52 + 1；WUI3001、WUI2011 已清零，剩 WUI2010 与 WUI1001 两类存量 |
| WUI2020 | note | 272 | 缺失 `AutomationId` |
| WUI1010 | note | 2 | 迁移表的特性提示 |

口径：

- 小计是**按位置去重**后的 warning 数（基线 427；#111 清零 WUI2011 后 354，
  #109 迁移 WUI3001 批次一后 254，#110 清零 WUI3001 后 53）。SARIF 里 WUI2010 的原始条目是 54，其中
  `Controls/Comment/CommentMainPanel.xaml:63` 与
  `Controls/Search/SearchHeader.xaml:49` 各被上报两次（同一位置）。
- note 级（WUI2020 / WUI1010）不计入小计，单独记。
- WUI3001 分两批，均已完成：批次一 #109 迁移了警告最密集的 5 个 ViewModel（100 条），
  批次二 #110 收掉剩下的 201 条。每批都要求“该批文件在观测构建里 WUI3001 为 0”，
  而不是整体清零后才算完成。
- #110 收口时删掉了 `MVVMTK0045`（只为字段写法存在，迁移后 0 命中）；`MVVMTK0046` 留在
  `NoWarn` 里，它与字段写法无关（命中的是 8 个 `[GeneratedBindableCustomProperty]` 条目
  视图模型里的 46 个 `[RelayCommand]` 方法，属 AOT 绑定遗留），删掉会让默认构建直接失败。

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
唯一去掉 `ErrorLog` 后 WUI2010 / WUI1001 照旧上报、CS9264 却全部消失，
说明"写 SARIF 的那次编译"才会产出这些诊断（用 `-p:RunAnalyzers=false` 复测仍有 CS9264，
可以排除是分析器加的）。

对 #114 的影响：**不会挡住收紧**。默认构建（warnings-as-errors）当前一条 CS9264 都不报，
收紧成"分析器常驻 + warnings-as-errors"仍然绿；前提是 `ErrorLog` 继续只挂在观测开关上。
一旦把 `ErrorLog` 也搬进默认构建，这些诊断会随 warnings-as-errors 直接变成错误
（最小复现工程验证：`/warnaserror+` 下 CS9264 是 error），届时会变成 57 处构建失败。
另外 CI 的 SARIF 会继续带这些条目，不要当成新回归。

## 从观测到阻断的顺序

一次只收紧一类，每一步都要同步更新本文件的基线：

1. **WUI3001（301 → 0，已完成，#109 + #110）**：把 `[ObservableProperty]` 的字段写法迁移成
   partial property。清零判据是观测构建里把它升成 error（`-p:WarningsAsErrors=WUI3001`）
   得到 0 错误，这条命令实测有效：`-t:Rebuild` 下未迁移完时按条数报错，归零即收尾。
   批次一（#109）迁移警告最密集的 5 个 ViewModel 共 100 条；批次二（#110）收掉剩余 201 条
   （64 个文件，含 `SubtitleViewModel`、`PlayerNotificationItemViewModel` 等不叫 `*.Properties.cs`
   的 ViewModel），并删掉 `BiliCopilot.UI.csproj` 里只为字段写法保留的 `MVVMTK0045` 抑制。
   `MVVMTK0046` 属于另一件事，见上面口径里的说明，这次没有删除。
   迁移方式统一为 `[ObservableProperty] public partial T Name { get; set; }`，保留生成的属性名、
   初始化器、可空性与手写 `OnXChanging/OnXChanged` 钩子，不新增任何 `NoWarn` / `#pragma`.
2. **WUI2011（73 → 0，#111 已完成）**：逐个判断绑定是否真的随数据变化，判定规则：
   - 路径上能发出变更通知的，才写 `Mode=OneWay`（共 25 处）：页面级
     `ItemsSource="{x:Bind ViewModel.X}"`（16 处，`ViewModel` 是依赖属性、集合本身可观察）、
     子控件 `ViewModel` 交接到依赖属性（9 处，父级替换要能传导）；
   - 源没有变更通知的，写显式 `Mode=OneTime` 把"就是一次性的"讲清楚，不要补 `OneWay`：
     内核模型（`VideoPart`、`BiliImage`、`UserGroup` 等都是纯 POCO，不实现
     `INotifyPropertyChanged`）、`x:Name` 元素引用（`TeachingTip.Target`）、
     构造时就定值的模板项。给这类源补 `OneWay` 只会得到一条永远不更新的死绑定；
   - 事件绑定（`Click`、`SelectionChanged`）、命令绑定（`Command`、`*Command` 属性）
     和单独的 `{x:Bind}`（整项 DataContext）本来就不触发这条规则，保持原样。
   仓库当前没有任何 `x:DefaultBindMode`，所以这些命中不是继承模式造成的误报。
   命中全部清零后，新写的 `x:Bind` 必须显式写 `Mode`。
3. **WUI2010（52 条）**：给嵌套 `x:Bind` 补 `FallbackValue` 或改绑扁平属性。
4. **收紧为阻断**：三类清零后，把 UI 工程的引用从条件开关里挪出来常驻、
   恢复 warnings-as-errors，并把 `winui-analyzer-observation` job 的
   `continue-on-error` 去掉、纳入必需检查。届时本文件只保留基线表格和这条历史。

## CI

`.github/workflows/winui-analyzer.yml` 是独立工作流，只有一个 job
`winui-analyzer-observation`：

- 用同一个开关跑观测构建，SARIF 作为 artifact 上传，并送 code scanning
  （`category: winui-analyzer`）；
- `pull_request` 带 `paths` 过滤（UI 工程与 `UI.Models`、`Directory.Packages.props`、
  `Directory.Build.props`、工作流自身）：它不是必需检查，不必每个 PR 都跑一次；
  转成必需检查时**必须**先删掉这个过滤，否则没触发的 PR 会让必需检查一直停在 pending；
- `continue-on-error: true`：构建或上传失败不会让工作流变红、不挡 master、
  不进分支保护，也不影响 `run-unit-tests.yml` 的 `test` 必需检查。但 job 本身仍然会在
  PR 的 checks 列表里显示成失败（红色叉），只是不阻断合并——看到红色先看日志，
  不要当成主流程挂了；
- fork PR 上 `security-events: write` 会被降级成只读，`upload-sarif` 在那里**必然失败**，
  靠 `continue-on-error` 吞掉，SARIF 只留在 artifact 里；
- 构建失败时只上传已经存在的文件（`if-no-files-found: warn`），
  摘要里会写明是没生成 SARIF 还是规则计数。

## 不要做

- 不要为了让观测构建变绿去加 `NoWarn`、`#pragma warning disable` 或降级规则。
- 不要在其他工程里引用这个分析器，也不要把它挂进默认构建。
- 不要顺手清理存量：清理按上面的顺序单独开票，一次一类。
- 不要为清零批量补 `Mode` / `FallbackValue`：先判断绑定是否会变化、嵌套路径是否真的可空，
  再决定改法；观测构建只是暴露候选，不是要求逐条照改.
