# IX-R1 配置快照与请求身份同锁校验

2026-10-31。父级直接修正审查 `7f70a99d9a16df248f3ef7c58c02a4ef763c91be` 的 P1。
待新独立复核和新候选包验证，PX 尚未准入，#49 仍 FAIL。

基线引擎 `c9dd6799cd6b6c9006ffb3bb71c6447f40bb4a42`。真实修复提交、53源文件、完整bundle/ZIP/patch
路径与hash见[source.json](source.json)。正式应用src、依赖及kernel gitlink没有变化。

## 单点修正

原请求先检查configurationRevision，随后离开锁取owner stamp，再进入锁取快照。
新配置可在两个锁之间发布，旧请求因此拿到新配置并错误返回Applied。

只在第二个锁内再次校验requestedConfiguration，然后在同一个临界区捕获config和revision。
没有删除先前terminal/身份检查，没有改最后执行校验、未开始prepare时的重试规则或native释放路径。
ConfigureAsync、SetDebugAsync、SetLayerAsync共享此路径；不存在持锁等待UI/owner的新增操作。
保留公开签名与旧callback语义。仅一个文件7行增加、1行删除。

## 本轮红绿

沿用 receiving-code-review、run-tests、code-testing focused、winui-code-review。
`fix-and-test.py`从已校验bundle新clone，使用固定SDK11 RC3与已有console宿主；
测试副本的friend和调度屏障不进入候选引擎。屏障只暂停原校验后、快照前的执行位置，不写状态或伪造返回。

最终命令：`python -X utf8 -B docs/engine-61-design-draft/ix-r1/fix-and-test.py --name ix-r1-parent-final`。

| Requirement | Evidence |
|---|---|
| 配置请求不能借更新快照成功 | `IX_ConfigurationChangesBetweenIdentityCheckAndSnapshot`，red→green |
| Debug请求同一身份保证 | `IX_DebugChangesBetweenIdentityCheckAndSnapshot`，red→green |
| 图层开关请求同一身份保证 | `IX_LayerChangesBetweenIdentityCheckAndSnapshot`，red→green |
| 原控制、scene、ready、realtime与关闭不退化 | 原IX/RX2的56项全部保留，逐名见原始results |

三类都在Ready前发出旧请求，同一调用线程在其后台续体通过第一次检查后提交新请求。
最新请求Applied后释放旧请求，旧请求必须Superseded。
最终基线 **56 passed / 3 failed / 0 skipped，exit1**；修正后 **59 passed / 0 failed / 0 skipped，exit0**。
两次构建0警告/0错误，JSON核验59个唯一名称和三项精确红例。
先行单配置版本也完成56/1→57/0，保留于ignored `artifacts/ix-r1-parent-fix`；最终三路径证据
保存在[evidence](evidence)，gzip逐字节验证，未覆盖先行证据。

## 尚需独立复核

本轮未重跑完整QX/OX/C1等旧回归；没有新clean包/隔离restore/public UI/WER验收。
测试运行经过真实owner/CoreRenderSurface链，但不等于正常public构造或桌面验证。
旧IX图像和包验收只对应旧c9dd679，不能追认此新引擎。
下一独立复核应补clean DLL/PDB/本地包来源与适当回归，且按原review指出的最小范围验证public配置身份
及constructor child-cleanup负例，避免无差别重跑所有native矩阵。

WinUI静态检查未见新增UI等待、XAML/ABI或释放次序变化；未运行WinUIAnalyzer/AOT。
保留全部历史FAIL/UNKNOWN、Present hang未证、cache/#56/性能/AOT、Legacy/PX及完整C1x限制。
无push/PR/issue、正式依赖修改、身份部署或登录数据操作。
