# 固定 e37a732a 独立审查

审查目标 `e37a732a2ab6648f17eb8d1d8316ed79ffec27de`，生产修复 `29acb496dc03749849414972227a7533194651d3`；集成基线/交付时父分支 `feat/performance-45` 为 `90e215f0d303c7ae6487f1e1cbf261f677b3d1ef`。来源 `t3-9f8d1e03` 只读。分别审查 `git diff 29acb496^ 29acb496 -- src` 的本次修复和 `git diff 90e215f0 e37a732a -- src` 的拟集成整体；没有用审查区 HEAD 冒充候选。

**决定：允许拆分合入绑定修复、Basic/Blur 重挂修复和通知 OneWay；暂不批准 711660de + 本修复作为全局共享 cache0.5 策略整体采用。** 前审持续空用户卡的 P1 已由有识别力的同 binary 红绿证据关闭。剩余阻断是已知 Season/双 pane 挂载 E_FAIL 的整体适用性缺口，不是新认定的 Binding/AOT 错误，也不是要求重做全部历史验收。

本次按 AGENTS、code-review 两轴、winui-code-review、winui-ui-testing 和 verification-before-completion 执行；用户明确禁止子代理，因此两轴均由本审查者独立完成。读过前审 `p1-independent-review.md`。没有适用的新增领域文档约束。没有修改生产、父/来源工作区、依赖或 gitlink，没有 push、PR、issue 操作。

## Standards：未发现本次 29 处替换的阻断缺陷

独立逐 blob 比较：26 个 XAML 文件，29 个表达式，旧文件仅把 `{x:Bind}` 换为 `{Binding}` 即逐字节等于新文件。没有生产 C#、内层 typed property binding、cache 值或样式尺寸改动。User 三入口（搜索、粉丝、关注）全部在内；Video/Live/Article/Moment 的对应 AppRepeater 入口及 Popular 的 ContentControl→所选模板整对象两级传递在内。`VideoSeasonSection` 是 VideoItemViewModel 入口，不代表 SeasonCardControl 已修复。

独立 XML 枚举固定候选共 40 个 AppRepeater，目前无 Style 属性覆盖。29 处不是 29 个独立页面或 40 个入口全覆盖。Season、通知模板入口、comment/filter、直接 ItemsRepeater 保持原状；未将这些说成已通过新的快速回收验证。内层 compiled bindings 和卡片现有 ViewModel 依赖属性更新路径保留；无新增全局缓存、调度器或反射属性串。

### pathless Binding 的类型与 AOT 判断

这里绑定源是当前 DataContext 对象本身，目标仍是已有 typed ViewModel dependency property（Popular 的中转目标为 Content）。没有 `Name`、`Avatar` 或 `Command` 这类动态 source property path。默认 Binding OneWay 随 DataContext 替换更新对象，内层原有 x:Bind/OnViewModelChanged 再更新实际控件。保留 x:DataType 不会让 `{Binding}` 自动得到 x:Bind 的编译期检查；这确实减少了外层静态类型检查，但当前模板与集合类型对应、外层 Content/VM 引用检查及生产编译没有显示类型错配。

Microsoft 的 [Binding.Path 文档](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.data.binding.path?view=windows-app-sdk-1.8) 说明空路径指向源对象；[CsWinRT AOT 文档](https://github.com/microsoft/CsWinRT/blob/master/docs/aot-trimming.md) 说明动态属性访问的 ICustomPropertyProvider/source-generation 要求。结合本 diff，我没有发现新增需要反射查找的 VM 属性，故不能套用“所有 Binding 都必须新增 GeneratedBindableCustomProperty”来判阻断。这是基于绑定形式的判断，不是 AOT 发布实测：归档 host 是 Release JIT，adapter VM 本身带 bindable 标记；没有本轮 Native AOT publish 或单独 WinUI analyzer 执行证明。不要把这个非阻断限制改写成“AOT 已验证”，也不要仅因偏好 x:Bind 而扩大改造。

## Spec：回收绑定反例已关闭，整体适用性仍有一个保留阻断

### 同 binary User50/500 红绿：通过归档独立审计

直接重读 raw 数组，不依赖 final-summary 的 PASS 数字。`final-red50`/`final-green50` 的 Harness.dll 均为 `82D01B46F5BF04380D035815145EF9FCF5F55435DE1187966893E16FBDA32A39`；`final-red500`/`final-pair-green500` 均为 `8D2085BFE696FA0E861E8160D983CD412E5008E43C31658C4CEE79C31580E09A`。每对归档的其余启动 binary hash 也相等。50 和 500 两对之间有测试选择器重编译，不称四次共用一个 binary。

采样源码从 `ir.TryGetElement(i)` 取得外层 realized presenter，并检查与视口相交（含部分可见）的每个根。`GetElementIndex` 调在外层，`ItemsSourceView.GetAt(index).Content`、presenter.Content、card.DataContext、ViewModel 均与 `items[index]` 引用核对。逐卡检查 ID、实际 TextBlock/TrimTextBlock、唯一头像 URI、RootCard 的确切命令对象。cards 数必须等于 visible 根数；VM/Source/command 为 null 会失败，不因过滤而消失。原 images 子集统计仍存在，但不再用于替代此 oracle。该 oracle 针对 realized 可见卡，不宣称是所有虚拟布局空洞/所有交互状态的通用证明。

两条旧模板轨迹各有 62 次快照、200 个失败的“卡×快照”记录，最终均为 12 张相交卡中 3 张缺绑定；修复的 User50、配对 User500 以及补充 User500 各 62 次快照全部 0 个错误，最终头像 readiness 也为 0 pending。旧模板在额外等待后 VM、头像和文字仍缺失；固定模板没有采样到旧 VM/文字/主命令残留。本人查看两对 owned 窗口截图，旧的两张完整空卡可见，新的姓名/ID/不同头像恢复；底部部分卡由 raw oracle 计入，不能仅凭窗口截图漏掉。

独立验证了 500 个归档头像文件 SHA256 各不相同；最终三个 green run 的全部 12 张可见卡分别用其外层 index 计算预期颜色，在 PNG 的中心横坐标/四分之一高度取样，共 36/36 符合 alpha=255、RGB 误差≤3。头像底色公式每180项循环，文件另含编号，故这不是“500 种互不重复底色”；像素检查与全程唯一 ID/URI/引用 oracle 合用，不能单独宣称每个快速瞬间像素都完成加载。

Search(Video)、Live、Article、Moment 的旧记录存在 VM/主命令反例，修复各 31 个快照未见失败；Notify 的旧记录亦无绑定反例，新记录保留 OneWay 后通过。早期 related red 没有 text/image 字段，只按当时存在的 VM/context/content/command 字段判断；green 增强字段通过。相关家族不是 User 那种独特头像像素对照，也不是所有页面风格/认证分页验收。

### P1（保留，整体采用阻断）— Season/双 pane 挂载失败仍未闭合

定位：`Styles/Repeater.xaml:9` 的全局隐式 AppRepeater 样式使 cache0.5 覆盖尚未完成挂载验证的路径；`recycled-cards/related-red-season/trace.jsonl` 保留新进程 Season-only 的 `COMException (0x80004005)`，前审另有 baseline/candidate 双 pane 挂载 E_FAIL。均未到滚动正确性断言。没有证据证明这些是本修复引入，也没有证据证明它们只是 fixture 错误，更没有证据证明两者同因。Season 入口没有本次修复或完成的 fresh rapid case，不能用其他家族通过替代。

因此不阻断独立的 29 处绑定正确性补丁及已验证的局部生命周期/通知修复；**阻断把尚未合父的全局隐式 cache0.5 注册一起签收为“全部类似列表已完成”。** 建议父在 `90e215f0` 上选择性应用 `29acb496` 的26个XAML，以及候选中的 BasicCoverImage/BlurCoverImage/ReloadableCoverImage 和 NotifyMessageCardControl OneWay；本次不整体 merge 候选，不带入 App.xaml 的 Repeater 字典注册/全局 cache0.5。以上是审查允许的子集，本审查没有代父实施拆分。

**唯一下一最小片：** 使用新的 owned 身份，从已保留 Season-only 场景开始，固定一个生产 template、同一50项数据、窗口尺寸、单次 mount 生命周期；记录 mount/Loaded/measure/scroll-start 的异常阶段及堆栈。先只切换 cache20↔0.5，不变模型或模板。若两臂同阶段失败，把该挂载原因定位清楚后用同一用例复验，不能直接删 fixture。随后复用前审双 pane 的固定模板/尺寸/数据，一次只改变单↔双 pane 或共享↔独立 layout，确认两个 pane 的独立 realized range/offset。无需重跑572、全部格式或所有页面；无需引入新调度架构。明确根因与受影响边界后即可裁定整体策略，不无限增加无关验收。

## resize 与快速滚动的明确权衡

独立从 raw beats 重算 max 和原 `(count-1)*.95` floor-index p95，120 次 width changes 数量一致。修复 PNG workload 的 User50 max=121.8969ms、User500=121.1692ms，均通过原200ms门槛；修复+持续cache20 policy 对照分别为78.1089ms、859.3039ms。短列表50仍比cache20慢，500改善明显。对照也是修复 build，不是 untouched parent；较早直接改内部 cache20 的对照在 remount 被生产策略恢复0.5，不能使用该末行 cache 值作为有效持续cache20对照。

正确的 cache20-policy User500 仍有一秒 remount image readiness 红，原样保留，不推测原因、不拿它替换生产门槛。User rapid 轨迹修复50/500 draw分别154/155；同binary旧模板117/116包含未绑定卡而少画，不能当旧模板更优的有效性能胜利。前审相同慢/快场景的全保留短列表回收成本与本次 resize 结果共同说明降低缓存有创建/重绘权衡，draw也不是HTTP请求数或精确解码取消数。当前瞬间pending与持续null VM是两个不同问题。

计时是16ms请求的dispatcher heartbeat，含800ms settle，非composition帧时间；通过200ms不等于60fps。不同头像 rapid 和原PNG resize是两个工作负载。原cache2、cache1等历史红不得因本次结果而删除；没有随机交错/置信区间，不外推所有机器或认证页面。

## 保留语义与证据闭包

* 通知 `ActiveCommand, Mode=OneWay` 保留。Basic/Blur 的 `_active` 防重入、重挂重取brush和补surface沿用原尺寸/DPI/alpha/crop分支，本次没有再修改它们。归档两者多次卸载换源/重挂通过；本人比较同源截图字节完全相同、替换截图不同，并查看1200×675原尺寸输出，无此前底部裁切。原尺寸及圆角断言、离线HTTP同URL跨实例复用只按其实际测试范围认可。
* undo 的 `SequenceEqual(before)` 对当前引用型VM检查序列，viewport允许<2 DIP误差，并非0像素数学相等。归档实际Popular为70/220/520项，count保持且listUpdates均0、cache=[0.5]；原尺寸物理覆盖、清源拒绝旧结果、unload取消、remount当前尺寸断言均在。这里是归档复核，不声称本轮再次执行原生。
* ZIP共2371条，其中2370个payload的长度/SHA256逐项通过，manifest自身不自签。22个run的host源码快照hash逐项相符；15个最终验证/对照run均有retained HasExited/ExitCode=0、相同PID/path、至少32秒观察、空匹配WER、owned包数量0和yield收据。早期7个诊断没有retained exit code，仍保持null，未升级为最终通过证明；无WER也不抹掉trace中的E_FAIL。
* 34个production-input raw文件hash正确，显式CRLF→LF后的hash逐个等于固定提交blob。这确认编译前未提交snapshot中的受审输入与已提交修复内容对应，不能依据run/head.txt中的预修复merge SHA否认其实际源码，也不能把head收据当“编译了最终提交”。host adapter、copy模板和插桩与生产编译分开看；源码hash/启动binary hash是可审计来源链，不等于可重现构建的密码学证明。
* 生产Rebuild归档exit0、一条protobuf unused-import warning；所有有记录build区间与这22次native区间无重叠。最早diagnostic build缺start receipt，保留该限制。没有新的依赖升级、AOT发布证明或analyzer结果。

## 本轮 fresh 工作与交付

本轮 fresh：固定git diff/XML枚举、2370文件manifest校验、34输入raw/normalized/blob校验、22份源码与退出/WER/清理收据核对、同binary配对、raw全卡oracle及resize数组独立复算、36头像像素检查、截图目视、父最新ref和kernel初态比对。**本轮未启动原生app、未重新编译、未执行572项测试。** 572/113是前审历史记录，不是本次fresh结果；无需为了重复相同红绿再次占用桌面。

审查区初态kernel HEAD为`f02c33f6`、偏离gitlink且index异常；已留存初态status/index证据，并在完成时按字节复核未变，没有reset/restore/update。未操作任何AotDev、Caa6a7be/Dev、官方或用户现存实例、凭据和试用包。本轮未取得桌面输入控制，因此无新native退出/yield可报告。

必要复算证据见 [recycled-cards-independent-evidence.zip](recycled-cards-independent-evidence.zip)：`verification.json`、`pixel-verification.json`、`related-verification.json`、`inventory.json` 及可重跑的 `verify.py`。原始native材料继续引用固定e37a732a的原zip，不重复复制。交付只含本报告和审查证据。

Standards：0个新增阻断发现。Spec：原空卡P1关闭；1个既存整体集成阻断保留（Season/双pane挂载适用性）。允许上述子集，整体暂不签收。
