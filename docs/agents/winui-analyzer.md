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
| WUI3001 | warning | 301 | `[ObservableProperty]` 旧字段写法，未用 partial property |
| WUI2011 | warning | 73 | `x:Bind` 没写 `Mode`，默认 OneTime，数据变了 UI 不更新 |
| WUI2010 | warning | 52 | 嵌套 `x:Bind` 路径缺 `FallbackValue`，中间段为 null 时显示异常 |
| WUI1001 | warning | 1 | `Windows.Storage.Pickers` 应换成 `Microsoft.Windows.Storage.Pickers` |
| **warning 小计** | | **427** | 与 #91 的临时检测一致 |
| WUI2020 | note | 272 | 缺失 `AutomationId` |
| WUI1010 | note | 2 | 迁移表的特性提示 |

两点口径：

- 427 是**按位置去重**后的 warning 数。SARIF 里 WUI2010 的原始条目是 54，
  其中 `Controls/Comment/CommentMainPanel.xaml:63` 与
  `Controls/Search/SearchHeader.xaml:49` 各被上报两次（同一位置）。
- note 级（WUI2020 / WUI1010）不计入 427，单独记。

## 从观测到阻断的顺序

一次只收紧一类，每一步都要同步更新本文件的基线：

1. **WUI3001（301 条）**：把 `[ObservableProperty]` 的字段写法迁移成 partial property。
   迁移完成后先在观测构建里把它升成 error（`-p:WarningsAsErrors=WUI3001`）验证清零，
   再讨论默认构建是否打开分析器。实测这条命令有效：`-t:Rebuild` 下会得到
   `301 个错误`，清零即可作为收尾判据。迁移完还能顺手删掉 `BiliCopilot.UI.csproj`
   里为此保留的 `MVVMTK0045;MVVMTK0046` 抑制，让抑制清单只留必要项.
2. **WUI2011（73 条）**：逐个判断绑定是否需要随数据变化——只有这类绑定才补
   `Mode=OneWay` / `Mode=TwoWay`。事件/命令绑定（`Click`、`Command`）、
   一次性静态文案、以及本来就只读一次的模板项不需要补，别为了清零批量加 Mode。
   仓库当前没有任何 `x:DefaultBindMode`，所以这些命中不是继承模式造成的误报；
   XAML 改动要跟着页面一起回归，不要一次铺开。
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
