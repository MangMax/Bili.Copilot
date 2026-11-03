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
  观测开关本身也会跳过打包前的运行时依赖校验，所以本机没有 libmpv / ffmpeg 也能得到
  退出码 0。
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
   再讨论默认构建是否打开分析器。
2. **WUI2011（73 条）**：逐页给 `x:Bind` 补 `Mode=OneWay` / `Mode=TwoWay`。
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
- `continue-on-error: true`：构建或上传失败都只留日志，不挡 master、
  不进分支保护，也不影响 `run-unit-tests.yml` 的 `test` 必需检查；
- 构建失败时只上传已经存在的文件（`if-no-files-found: warn`），
  摘要里会写明是没生成 SARIF 还是规则计数。

## 不要做

- 不要为了让观测构建变绿去加 `NoWarn`、`#pragma warning disable` 或降级规则。
- 不要在其他工程里引用这个分析器，也不要把它挂进默认构建。
- 不要顺手清理存量：清理按上面的顺序单独开票，一次一类。
