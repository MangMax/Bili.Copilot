# cf85c597 Season 挂载补片独立审查

**决定：允许将固定候选 cf85c597590d0050302e07c29e80c18c55803b73 作为共享列表 cache0.5 整体合入父 feat/performance-45。前审唯一 Season / 双 pane 挂载适用性阻断关闭；没有新增实质阻断。** 这是有界准入判断，不是全部页面、样式、机器或 AOT 已验证的声明。

审查源为只读 `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-d2288c4a`。父在开始及交付前均核对为 `90e215f0d303c7ae6487f1e1cbf261f677b3d1ef`。新增范围用 `git diff e37a732a cf85c597 -- src` 固定，整体用 `git diff 90e215f0...cf85c597` 固定。读取 AGENTS、code-review、winui-code-review、winui-design、winui-ui-testing、verification-before-completion；按用户禁止子代理的要求自行完成两轴。以 mount-diagnosis-results.md、mount-diagnosis-evidence.zip、recycled-cards-independent-review.md 为入口，并直接读取前审 independent-evidence.zip 的原双 pane fixture。旧审已经核准的历史证据不全部重跑。

## Standards

0 个新增阻断发现。逐 git blob 比较确认新增生产改动严格只有四个表达式：EntertainmentIndexMainControl、PgcSectionDetailControl、AnimeTimelineMainControl、PgcFavoriteBody 的直接 DataTemplate 根 SeasonCardControl.ViewModel 从 `{x:Bind}` 改成 `{Binding}`。替换后与候选逐字节相等，没有改卡片内部 typed binding、布局参数、生命周期、cache 或依赖。此前 29 处卡片绑定、Basic/Blur 重挂和 Notify OneWay 未在本片变动。

这是将当前 DataContext 对象交给已有 typed ViewModel dependency property；没有新属性字符串路径。SeasonCardControl 的 OnViewModelChanged 更新 RootCard.Command，三个相关样式均经 TemplateBinding 传递到 presenter。x:DataType 的保留不恢复运行时 Binding 的外层静态类型检查；生产编译和对象引用 oracle 没有显示类型错配。不能把默认偏好 x:Bind 或未新增 GeneratedBindableCustomProperty 本身作为此 pathless Binding 的阻断理由，也不能据此声称 AOT 已验。

独立 XML 枚举得到 4 个直接根、1 个嵌套 Season 入口。Timeline/Favorite 与两处 Index 的外层交接结构一致，补齐这两处范围合理；它们只获得静态结构检查和生产编译，没有本轮原生样式全流程覆盖。Player/PgcSeasonsSection 的根是 ItemContainer，SeasonCardControl 是其子节点，仍保留 x:Bind；这不证明它永远安全，但本片没有该结构失败的实证，不能将结构不同且未测的入口推定为当前策略的新阻断。保留后续覆盖事项，不扩大本次验收。

## Spec

0 个新增阻断发现；前审整体准入阻断关闭。

独立校验 ZIP 全部 2405 payload 的长度及 SHA256，并重跑归档 audit.py：6 组启动 binary 一致、11 条绿色 raw 检查通过。另写独立 verify.py，不依赖 PASS 汇总，核对同组源码 hash 也一致。38 个生产输入的 raw hash 均受 manifest 约束；显式 CRLF→LF 后，与固定提交 blob、只读源文件和实际构建副本一致。核对 474 个 kernel 源快照文件的归一化 hash；这确认保留快照一致，不冒充缺失的 0ae5e4e 对象已从对象库取出。

同源检查还直接比较了 host IndexSeasonPresenter/UserCardControl 与生产 blob、IndexSeasonCardStyle XML 子树，以及 SeasonFixed/User 的生产模板子树。旧/新 Season 模板只差根 ViewModel 表达式。App 的 ExtraGreen 实际调用 RunRelatedReview，ImageChecks 根据 Pane family 调用 RunPaneProbe；没有把按钮名误当成模板选择。Season 选择由 mount-fixed.txt 是否存在决定，PaneLegacy 前缀选择 UserRed；Natural 模式不调用同步 UpdateLayout。

Season 原固定数据 cache20 红与固定模板 20/0.5 绿的三个 run 共用 binary 和源码快照。红 trace 为 Loaded 后、scroll-start 前的 COMException(0x80004005)，managed stack 包含 AppItemsRepeater.MeasureOverride；两条固定模板 run 各 31 个快照、320 条可见卡检查，无错误。旧 cache0.5 两条可滚动轨迹各有 60 条坏卡记录；旧模板组件类型与固定模板 raw 的 componentType 分布也不同，支持实际切换过模板。原红保留，不能因为 baseline 也红而豁免。

**证据精度限制：** runner 没有把 mount-cache.txt / mount-fixed.txt / related-family.txt 单独归档，早期 Season raw 也不含实际 cache 字段。因此“20/0.5”的早期参数依赖执行报告及 runner/选择器来源链，不能称为每条运行都有独立实际 cache 回读收据；崩溃双 pane 同样没有完成后的 matrix 参数回读。这是可复放性的小缺口，不抹掉同 binary 红绿、模板状态差异及最终实际 cache0.5 的直接验收，不据此继续 HOLD。下一次使用该 fixture 可顺手保存解析后的运行参数，无须为本轮重跑全历史。

最终 season-final 使用自然布局、50 个唯一标题/1600×900 PNG URI及不同命令对象。独立从 raw 重算 152 个快照、1326 条可见卡检查，source/content/context/VM/文字/图片 URI/命令全部一致；120 次 resize 期间亦采样，最终 readiness 全通过，实际 cache 为0.5。采样从每个 realized 外层根判断视口相交，包括部分可见根；没有按 VM 非空过滤，缺卡、缺图片和缺命令会失败。它不是未实现区域的通用空洞检测，也不是每个瞬间的像素证明。

从归档原始 PNG 重新读取颜色，与10张最终可见卡 RenderTargetBitmap 比较，10/10通过；图片集合与 raw 最后可见 index 集合完全相同。查看 Season 与双 pane owned 截图，当前标题/图片可见，底部部分卡由 oracle 纳入。最终唯一图像证明补充 URI/引用 oracle，不外推每个快速滚动瞬间均加载完毕。

双 pane 与前审 archived fixture 核对了每侧500项、左列400 DIP、每侧高400 DIP、窗口1660×1060以及原 User Id=12345。早期7位ID实验不是确切原数据；最终 LegacyPaneData=true 恢复原值，截图也显示12345。最终自然布局单/双、共享/独立均通过；独立复算双方 offset 和完整 realized 数组，左滚不改变右，右滚不改变左，并同时实现左0与右499。最终旧共享/旧独立 cache2 都保留 E_FAIL，而同 binary 固定共享 cache2通过；共享 layout 不是旧模板失败的必要条件。准入依据是修复模板后原形状正常、当前0.5自然布局独立性直接通过，而非用 baseline 失败解释掉候选失败。

归因只到可实验区分的 compiled 整对象模板交接边界。未符号化 native 地址、stowed 栈不能确证 WinUI 内部源码原因；本轮没有读取 dump 或新增内存诊断。无需为当前裁决继续追查内部实现。

## 退出、构建和覆盖边界

22 条正式运行均核对 retained HasExited、ExitCode、PID、完整 path、至少32秒观察、owned包数量0和yield字段。8条预期崩溃的补查 Event1000，独立从原始 message 重解析十六进制 PID，核对完整应用路径和事件时间位于观察窗；正常 exit0 无匹配事件。原 runner 空 WER 不等于未崩溃，补查不覆盖原文件。yield released:false 仅表示当时无占用可释放。未执行诊断动作的 season-native20 不计为正式验收。11个构建区间与正式运行区间重新比较，无重叠。

生产首次 Rebuild 到 DLL 后因归档缺少 libmpv runtime 输入而失败，含 protobuf unused-import warning。补齐既有 runtime 后末次 production3 增量构建 exit0、0 warning/0 error；**不能称 clean Rebuild 成功**。native head.txt 是提交前 HEAD；本次以源码快照、binary hash及38输入与提交的对应作来源链，不称 native 编译了最终提交，不声称可重现构建密码学证明。

保留 User50 短列表降低 cache 后的创建/重绘与 resize 成本，不用长列表收益掩盖它。既往 cache1/cache2、cache20 remount readiness 红及历史 E_FAIL仍有效；不篡改前审数值。heartbeat 是 dispatcher 计时，不是 composition 帧时间，200ms 门槛不等于60fps。未新增 AOT publish、全机器、认证分页、所有 Season 样式、codec/572测试或单独 analyzer 验收。

本轮 fresh 工作仅为固定源码审查、归档小审计、独立 raw/hash/XML/像素/WER/构建区间复算及截图目视；没有重新构建、启动原生 app、获取桌面控制、修改/操作用户 AotDev、Caa6a7be/Dev、官方实例或凭据。审查区 kernel 初态 HEAD=f02c33f6、index缺失及 status 异常完整留存，交付前逐项核对未变；不 reset/restore/update。源区只读，父分支未动。只提交本报告及小证据，无 push、PR、issue、依赖或 gitlink 变更。

小证据见 [mount-independent-evidence.zip](mount-independent-evidence.zip)，包含 verify.py、supplement.py、pixels.ps1 与结果及保护收据。verify.py 参数为只读候选源目录和审查输出目录；像素脚本针对独立解压的原 ZIP。原始2405 payload继续引用固定候选归档，不重复打包。
