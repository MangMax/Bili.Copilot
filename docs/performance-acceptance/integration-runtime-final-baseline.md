# Final runtime baseline 独立集成复核

2026-10-31：Standards 与 Spec 两轴均无新增合入阻断。按用户授权独立审查，不再委派。
固定父 `76af64717dc7bd0e95e1d34c8622d5e8cad48805`（初始 clean），候选
`cb0268870bfffac71398bbc183bab39c5e94c08a`，以 `--no-ff` 合入
`77e7c886b3226da3dab0f50a02679e4d875edba8`；两个 parent 依次为上述父与候选，
合并树与候选相同。变化仅 docs/evidence/replay；生产代码、tests、SDK 配置和 gitlink 不变。

## 当前交付摘要与权威索引

已本地集成 PR27/43（刷新撤销、播放器模式相关交付），见
[PR27/43 独立集成](integration-pr27-pr43-review.md)。稳定依赖升级已交付，
**69 个声明/pin、66 个不同 package ID**，不是 69 个独立包；见
[依赖升级清单与边界](dependency-upgrade.md)。这些交付不能直接等同于总体性能提升。

当前主候选仍为 **NET11 RC3 Release x64 JIT**，net10 只作本轮对照。
[退出/timer 修复](integration-net11-player-timer-fix.md)与
[托盘前台修复](integration-tray-foreground-fix.md)已合入，有限兼容门禁完成。
[已登录完整应用](integration-full-app-net11.md)、
[受控刷新/撤销](integration-refresh-undo-controlled.md)及
[完整应用托盘前台定向回归](integration-full-app-tray-foreground-fix.md)
已有新增受控或认证证据：应按其精确路径和有界结论解读，不能被旧 status 每票“未验”笼统覆盖。
完整应用仍 PARTIAL，真实公网空页、全面网络与长期稳定性等未覆盖内容仍存在。

本次新增[最终同源 ABBA 基线](runtime-final-baseline.md)及
[复放说明](runtime-final-baseline/README.md)，可离线复算；实际重放需另行获得桌面授权。
**兼容交付与性能验收分开：perf HOLD 保留**。#49 暂停/seek/首帧已确证 FAIL，
不是未测；NativeAOT 因 MSVC 工具链缺失仍 BLOCKED；#56 长期预算与任意大重排缺口、
历史 net9 native crash 未归因均未消除。没有关闭整票或宣称总体收益/回退。

## 独立复算

直接解压每轮 samples-{state}.json.gz，与 idle-baseline 的逐行原始计数比对；
未调用会写分析产物的 analyze.py。每状态样本序号 0..60（61 个读取、60 个差分区间），
单调时钟严格增加、累计 CPU 不减。CPU=(末累计 kernel+user 毫秒−首累计毫秒)/
(末 monotonicSeconds−首 monotonicSeconds)；前半为 0..30，后半 30..60，
中点共享但不重复计入任何差分区间。累计差分和与首尾差值一致。
下表独立重算全部 12 行，与 archive analysis 和原报告一致。

| Run | State | CPU ms/s (first/last half) | Private MiB median [min,max] | WS MiB median [min,max] | Handles median [min,max] | Threads median [min,max] |
|---|---|---|---|---|---|---|
| a1-net10 | visible | 42.974 (29.170/56.778) | 435.91 [435.60,439.15] | 544.31 [544.00,547.23] | 4492.00 [4483.00,4528.00] | 251.00 [248.00,262.00] |
| a1-net10 | hidden | 0.521 (1.042/0.000) | 435.71 [435.00,436.22] | 543.80 [543.17,544.19] | 4471.00 [4465.00,4491.00] | 245.00 [242.00,251.00] |
| a1-net10 | restored | 36.980 (22.914/51.049) | 435.50 [435.25,435.78] | 545.91 [545.67,546.12] | 4444.00 [4441.00,4449.00] | 242.00 [242.00,245.00] |
| b1-net11 | visible | 49.482 (63.028/35.938) | 436.11 [435.96,437.00] | 543.68 [543.14,544.29] | 4431.00 [4426.00,4467.00] | 253.00 [250.00,263.00] |
| b1-net11 | hidden | 3.385 (4.166/2.604) | 431.58 [430.91,432.19] | 539.82 [539.30,540.45] | 4408.00 [4402.00,4432.00] | 246.00 [243.00,253.00] |
| b1-net11 | restored | 36.198 (43.754/28.644) | 431.53 [431.35,431.77] | 543.70 [543.52,543.93] | 4389.00 [4387.00,4394.00] | 243.00 [243.00,246.00] |
| b2-net11 | visible | 48.958 (50.516/47.400) | 475.36 [475.17,479.68] | 541.50 [541.32,545.42] | 4559.00 [4555.00,4602.00] | 252.00 [251.00,268.00] |
| b2-net11 | hidden | 1.562 (2.604/0.521) | 475.42 [474.48,475.77] | 541.22 [540.45,541.50] | 4554.00 [4522.00,4558.00] | 251.00 [243.00,254.00] |
| b2-net11 | restored | 42.710 (32.807/52.616) | 475.19 [475.01,475.33] | 540.38 [540.21,540.49] | 4512.00 [4511.00,4516.00] | 243.00 [243.00,246.00] |
| a2-net10 | visible | 50.001 (44.794/55.208) | 436.88 [436.64,440.06] | 537.04 [536.80,539.94] | 4467.00 [4463.00,4506.00] | 252.00 [251.00,264.00] |
| a2-net10 | hidden | 2.604 (3.125/2.084) | 436.90 [436.02,437.39] | 536.28 [535.49,536.64] | 4468.00 [4441.00,4473.00] | 251.00 [244.00,254.00] |
| a2-net10 | restored | 33.334 (31.247/35.421) | 436.50 [436.28,436.89] | 535.21 [535.04,535.58] | 4434.00 [4430.00,4439.00] | 246.00 [244.00,247.00] |

四轮共 732 样本；每臂独立进程 n=2，不把逐秒样本视作独立重复。
各状态 CPU run 范围重叠；不做显著性、总体收益/回退或泄漏推断。
采样器实现与 fixture 核对：先 Settings warmup 5s，各状态 settle 2s，原子 marker 后
以 monotonic 一秒 deadline 读取；marker 与每行 PID/HWND/visibility/DPI/几何一致，
150% DPI、(120,90,1200,800)，每轮同 HWND，20 次 hide/restore 结构断言通过。
这些几何/可见性字段是边界 marker 复制，不是逐秒重新查询窗口状态。
CPU 读取后才记录单调时间，采样并非零耗时的瞬时快照，结论保留 observer 扰动边界。

Observer 墙钟 median/max ms（ABBA）：13.226/19.379、14.563/21.170、
12.830/18.413、11.130/211.482；最后尖峰保留，未剔除。墙钟开销不是 observer CPU，
不能从目标 CPU 扣除。背景 before/after 只覆盖整轮存活且身份相符进程，
遗漏中途退出进程，不等于三个精确测量窗口的 on-CPU 归因。
协议时间先于四轮执行；sequence 固定 A1/B1/B2/A2，日志只有四组 START/END、均 exit0，
遇失败停止且拒绝覆盖日志，无方向性补跑证据。未混入旧 183 行或列表微基准。

## 来源、字节闭包与退出

阅读 verify.py/prepare.py/gate.py/exit_gate.py 后，执行只读
`python -B -X utf8 <来源>/docs/performance-acceptance/runtime-final-baseline/verify.py --tracked --live`：
400 项 portable 原始字节长度/hash、1798 个 freeze 文件、部署 DLL 双向集合
359+363+363+359=1444、包内加载模块 150+151+151+150=602 全通过。
使用已审阅 audit 的计算逻辑，将 save 重定向到内存，重新核验 source/fixture/freeze，
结果与归档 preflight 完全相同；未重写原证据。

两臂源码差异仅 global.json 和 UI/Visor/Visor.Models TFM；fixture 仅 SDK 与 UI TFM/
臂绝对引用路径差异。SDK 10.0.403 / 11.0.100-rc.3.26522.148，runtime
10.0.14 / 11.0.0-rc.3.26522.148；实际 coreclr hash 与本臂 freeze 匹配。
NotifyIcon 2.4.1 一致。原始字节 hash 与 BOM/换行/末尾换行规范化的源码等价分开，
未把允许 preparation 末尾换行的比较称为字节相等。

独立从 raw assets 图计算：138 vs 134 包；共同 134 包 library 元数据/hash 全等。
net10 额外四个 Microsoft.Extensions.* 10.0.0 包均在 RC packagesToPrune 中，
普通及 win-x64 两 target 共 28 项选择/依赖边差异与 preflight 完全一致。
这是 SDK/TFM/框架资产组合对照，不能纯 JIT 归因。

四轮 Player15/Main4、字体结构断言、cycles 及清理门禁通过。retained handle exit0，
exact PID/path/creation/start/package 与 ready 相等，safe Application1000/WER 为空，
无 forced exit 或 managedUnhandled。末次 query 开始/结束相对 observed exit：

| Run | Start seconds | End seconds |
|---|---:|---:|
| a1-net10 | 30.1300895 | 30.156797 |
| b1-net11 | 30.4463130 | 30.462298 |
| b2-net11 | 30.4705081 | 30.486416 |
| a2-net10 | 30.4336093 | 30.452029 |

全部 >=30s，仅 PASS_BOUNDED，未来精确故障仍可撤销。准备 GBK、非唯一替换守卫、
134 包误假设、末尾换行比较失败及截图枚举失败记录均保留，不以最后绿例抹掉历史。
本次直接查看 b2 字体图，确认没有设置浮层；结构字体 PASS 不等于全视觉 PASS。
48 图 hash 全量验证，其余图的逐张观察引用原视觉记录，本次不冒称独立逐张复看。

## 操作范围与验证

已读 AGENTS/agent docs、code-review、winui-code-review、verification-before-completion；
docs-only 不引入 WinUI API/XAML 变化，不宣称重跑 analyzer、487、build、UI 或性能轮。
352 个解压文本按签名媒体查询/凭据值模式重新扫描无命中；扫描仅输出文件名/类型，
不输出值，不宣称穷尽所有秘密。没有读取原始私人日志。

未触碰当前登录 layout-v3、Dev、PID9608、注册或部署；无 push/PR/issue。
父 kernel `0ae5e4e000a41a07f6696746706df11c06e8835b` clean，committed bundle
验证通过，含 prerequisite `6501985d0f30c55c28b3f78c6e045c2536e1d389`；仍 local-only。
合并树等于候选，`git diff --check` 通过。最终交付 SHA 由本报告后的文档提交及交接输出标识，
避免将报告自身 SHA 写入自身造成循环。
