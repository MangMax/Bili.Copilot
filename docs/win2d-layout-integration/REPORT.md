# Win2D layout 有限离线集成

基线 root `5600aecec99ac471ffee1d318eeabc1dad2be51f`、kernel `496728dd54f7d1c80c764c2dd8a4f9b68e12876b`，开始时均 clean。仅更新 Legacy/Share 中央精确版本、相对 feed、校验脚本和本恢复记录；Core 保持正式 `0.0.1-seekd1fc.20261101.2`。不合并候选应用历史，不改 kernel、Buffering 映射、冻结/清晰度/滚动，不研究残余 3 秒。

## 冻结材料

Legacy/Share `0.0.1-win2dlayout.20261102.2` 原字节取自 `t3-4eafbd62/artifacts/win2d-native/full/feed`；由该区 `tools/build_fixed.py` 与 `full/NuGet.Config` 定位。没有重打包，旧包保留，禁止 `.1`。修正只用 PackageVersion，AssemblyVersion 维持 `1.0.0.0`。

| 包 | nupkg SHA256 |
| --- | --- |
| Core 正式 .2 | a8869b0a71dd15a77cc0e78bda8f868bbdb8fe1492b8cbb552464b8ccdaf0983 |
| Legacy layout .2 | a8be93f2798d816eeb157df12a0bd7e1ddc953e654704db5bfd2f71e677a01a4 |
| Share layout .2 | 5fcdd7f35736c30733d6ca206ad0892946c3b00d4c01403067c6c16b092a6313 |

恢复文件 SHA256：bundle `d42bc99897338ed8b9b45acd832fb64bca865430455e0e7536b3fff8fd63146d`；patch `551bd6c55dc8cf3001c3ab86b252571ea62913c790aa737a089b6971d1e149f6`。

本目录 bundle/patch 原字节来自 `t3-ea85b87c/docs/win2d-layout` (`7d99ad7d`)。`git bundle verify` 通过，完整 bundle 可 clone，HEAD `357fade2dcdd62f727df23caabc98407111f31af`，base `15434adfee3c80032ca12ab4840bc36f54858f65`。`git diff -w` 仅 DanmakuRender.cs 一个方法，7 增/14 删：RenderDanmakuItem 去临时 target/session，并设置 RequestedSize 复用 layout。未引入 QX/OX、几何预判、生命周期修复。独立有限准入来自 `t3-1adb1893/docs/win2d-render-review.md`，不扩张为设备/字体/异常路径普适等价性。

## 历史原生结论及边界（本轮不启动 UI）

依据 `t3-1b2a580a/docs/win2d-layout-candidate-mount/REPORT.md`，最终提交 `3befffa37ec0e4d5c120dbd99259a11e9ab98273`：修正候选一个功能臂、一个像素臂正常退出，退出后延迟 WER 查询零匹配。single sample 密集首屏 1806.7 → 1341.2 ms 只表示方向性。baseline host 是 winapp 从候选编译版本暂存的输出；baseline Danmaku DLL 为正式包，修正臂 host 重新编译。不能声称同源严格性能分布，也没有新矩阵。

80070002 已定位为 Share `1.0.0.0` 程序集标识缺失；这不豁免其他历史 8000ffff/AV。取 dump 时曾按名称误附加用户 AotDev，停止调试器后该实例退出；此事实保留，不能以“用户包完全未动”遮盖。本次仅文件离线集成测试，没有操作任何用户实例、调试器、安装或 UI，无 push/PR/issue/部署/用户更新。

## 本轮验证

`python -X utf8 -B packages/danmaku/verify.py` 核验旧包及新包哈希、中央混合 pin 和 relative ID mapping。历史 offline-evidence.zip 在本工作区缺失，脚本始终使用已提交 manifest 哈希核验历史包；ZIP 存在时另做 archive 身份和字节交叉核验。

从新空目录 restore：

```powershell
dotnet restore src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --packages artifacts/win2d-integration/packages --configfile nuget.config -p:Platform=x64 -p:PublishAot=false -p:NuGetAudit=false
```

退出 0；有源站 blob DNS 重试信息，最终成功。project.assets.json 的 packageFolders 只有本区隔离目录，无 fallback folder；三包选中 Core 正式 .2、Legacy/Share layout .2。Danmaku 来源按精确 ID 映射到仓库 feed，没有读取用户全局 Danmaku 缓存。

离线 PE 元数据检查三程序集身份均 `1.0.0.0`，Core→Share 与 Legacy→Share 均引用 `1.0.0.0`。以解码签名比较类型/方法/字段与正式 .2 相同（原始 token 会随构建移动，不能直接比较 token 字节）；恢复 Core 与候选的 Share 六个生产 cs 文件也一致。不是通过修改 Core 来闭合包图。

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --logger 'trx;LogFileName=ui.trx' --results-directory artifacts/win2d-integration/tests
python -X utf8 docs/seek-responsive-fix/replay.py artifacts/win2d-integration/seek-replay
```

测试退出 0，Passed: 611, Failed: 0, Skipped: 0。seek probe 从 bundle 恢复，red exit 1 / green exit 0，绿臂 13 项通过。该探针覆盖的 master 在本次 layout 源码中未变；不冒充 GPU 验收。旧 17 项复放依赖的 `t3-c9d5219a/artifacts/engine49/source/upstream.zip` 和 `t3-afdc1233/artifacts/legacy-seek-reviewafdc/boundary` 均不存在，未编造输入、未运行。

原始日志、binlog、TRX、元数据检查工具/JSON、恢复源码只保存在 ignored `artifacts/win2d-integration/`；此目录仅提交最小源码恢复 bundle/patch 和记录。

正式项目 JIT 构建（两次分别用 Debug / Release）：

```powershell
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Debug --no-restore -p:Platform=x64 -p:PublishAot=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false -p:NuGetAudit=false "-p:SolutionDir=$PWD/src/" -bl:artifacts/win2d-integration/debug.binlog
```

Release 命令仅将配置和 binlog 名改为 Release/release。两次 exit 0，各 1 警告/0 错误（既有 vega/v1.proto unused import），耗时 3:31.76 / 2:28.78。两种实际输出目录 `src/_build/x64/{Debug,Release}/BiliCopilot.UI/bin` 的三 DLL 与 nupkg 内逐字节一致，输出 deps.json 的三包图均匹配中央混合版本。

| 实际 DLL | Debug / Release SHA256（相同） |
| --- | --- |
| Core | 90671b115567f9ccb5c5a758fec4b479df24f073f5e6dcceb4e138c80d36ac4d |
| Legacy | c57879e3f16debd0555627681d11eab09d2c0e550a580bb3ca9c8c437b2b7c3f |
| Share | 79bba10681fe313e8d8e47646710cded96f4b97a07ccf5241a2dd879281ef710 |

构建及 611 测试不能替代原生运行/AOT/异常稳定性验收。本轮无安装、无 UI、无用户进程或调试器操作。kernel 检查仍为 `496728dd` 且 clean；root 修改仅本报告列明的有限集成，提交后再次检查 clean，最终固定 SHA 由交付消息提供。
