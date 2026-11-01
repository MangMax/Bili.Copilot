# 文章头像 OneWay 窄修复集成

2026-11-01，唯一写入父工作区 t3-1d19cc80。起点
`6133ae590daf474d606e7b0afbdbf21e857b8ed1`，root/kernel clean。
固定候选 `4ca81f88a455616f4a8142e16c2af602e029dbb6` 和独立 review
`4af16404386767ea72e95b7e65cdb9caf515c46f` 分别无冲突 `--no-ff` 合入：
生产 merge `71934c76c8a4073043d71d1e1c93cd40f0424b0d`，review merge / fresh 验证 HEAD
`6ee450b02a99fe22eb74b1f4d45117d7576344d4`。最终文档提交见交付消息。

最终生产 diff 只有 PartitionArticlePresenter.xaml 的头像 Source 加 compiled
`Mode=OneWay`。正式 Repeater.xaml cache 保持 0.5，cache 2 候选已撤回。
src/tests/packages/externals 与固定候选一致；kernel 保持
`0ae5e4e000a41a07f6696746706df11c06e8835b`，不改正式依赖。
未引入其他五个 bug 任务的独立提交。

## Fresh 验证

复用现有 renderer-fault-integration/verify.py，各阶段指定新目录：

```powershell
python docs/performance-acceptance/renderer-fault-integration/verify.py Full ArticleAvatarFull
python docs/performance-acceptance/renderer-fault-integration/verify.py Release ArticleAvatarRelease
python docs/performance-acceptance/renderer-fault-integration/verify.py RuntimeGate ArticleAvatarRuntimeGate
python docs/list-resize/article-avatar-integration-audit.py
```

使用固定 SDK 11.0.100-rc.3.26522.148。Full 新 artifacts restore/build/test，
**Passed: 577, Failed: 0, Skipped: 0**，TRX 名称与前轮 577 一致。
Release 使用新 BuildOutDir、Rebuild、x64、PublishAot=false，关闭签名/生成包。
Release Rebuild exit 0，1 个既有 protobuf unused-import warning / 0 errors，耗时 00:04:42.59。
源码及输出哈希复核、577 名称比较通过，libmpv/ffmpeg/BBDown 输出与输入哈希一致，resources.pri 非空。
runtime gate exit 0，只验证文件条件，不证明 native 加载。依赖缓存和本区已存在 runtime
输入复用，不称空缓存还原或新下载。精确命令/UTC/退出码/源码及输出 hash 见 [evidence.zip](article-avatar-integration-evidence.zip)；
大型输出保留 artifacts/renderer-fault-integration/ArticleAvatar*。

## 历史离线复算（非 fresh native）

将已提交 scroll-continuity-evidence.zip 解压到新 ignored
artifacts/article-avatar-integration/history，再运行现有
scroll-continuity-review-replay.py。9392 payload 的长度/SHA256 匹配，9323 RGB
网格样本的背景分类及持续时间汇总全部匹配。旧 real-article05/2 每组 18 检查中
保留两项身份失败，fixed-article05/2 每组 18 全过；修复后封面/头像逐角色行检查无错。
这是历史采样复算，完整录像不在紧凑包内，不能独立重解码所有 compositor 帧。
未重跑 37 轮 native；历史缺失 Temp 输入未恢复或核验，不以归档复算冒充存续核验。

仅支持回收头像 Source 陈旧的窄修复。文章内容/点击失败、整体滚动留白与 AOT
未宣称修复；User/Season/Article500 历史压力失败保持，cache 2 的反证保留。
未启动子代理、UI、应用或用户实例，未安装/发布/操作用户包、登录数据、远端，
未清理其他工作树。未修改候选历史报告“review 尚未发生”等当时记录；独立审查见
[固定 review](scroll-continuity-review.md)。

本轮证据包 9 个 manifest 条目，SHA256 `1c5d602f37a4a9076380bf052fb378435bfad46d24930c7c46a8a61a65e53686`。
