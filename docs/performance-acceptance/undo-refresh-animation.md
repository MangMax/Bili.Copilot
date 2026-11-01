# 撤销刷新按钮消失动画修复

日期：2026-11-01。父提交：`f44c22aa`（`feat/performance-45`）。分支：`t3/fix-refresh-button-flash`。

## 结论与控件

用户所述「刷新按钮消失动画还是闪烁」的实际控件是刷新按钮左侧的**撤销刷新按钮**
（`UndoRefreshButton`，`PopularMainHeader` 与 `VideoPartitionMainHeader` 各一处）。
刷新按钮本身始终显示、不会消失；分区页排序 `ComboBox` 的 `Implicit.HideAnimations`
与刷新/撤销无关，未改动。

修复后出现与消失只由一个动画控制器驱动；淡出停在 0，完成回调再折叠；重复隐藏不重启；
淡入中收到隐藏通知时等淡入结束再淡出；卸载只取消。撤销命令、列表/游标/引用恢复未改。

## 根因

父代码有两套系统同时写撤销按钮的 `Opacity`：XAML `Implicit.ShowAnimations`（组合层
出现动画）和 `UndoRefreshButtonAnimator` 的 Storyboard 淡出（`FillBehavior.Stop`，
基础值 1）。淡出与出现动画重叠、或完成回调被列表更新推迟时，释放动画会回到基础值 1，
先渲染一帧完全可见的按钮再折叠，即用户看到的闪烁。

红侧实机逐帧亮度捕获到 `1.74 → 144.65 → 0` 的回亮；绿侧同一场景单调到 0。

## 实现

- `UndoRefreshButtonAnimator` 单一控制器：出现为不透明度 0→1 + 缩放 0.96→1；
  消失为 1→0 且 `FillBehavior.HoldEnd`，完成回调折叠。
- 释放动画前先写基础值；重复隐藏幂等；出现中隐藏用 `HideAfterShow` 串行；
  卸载 `Cancel` 只停止过渡。
- 两处 XAML 移除撤销按钮的 `Implicit.ShowAnimations`，加单位 `CompositeTransform`；
  code-behind 的 `Detach` 改调 `Cancel`。
- 保留父提交新增的 `IsEffectivelyVisible`：隐藏子树内直接折叠，避免卡在 `Visible`。

## 验证

- 生产 Debug x64 构建：0 警告 / 0 错误（显式 `SolutionDir`）。
- 无头测试：591 通过 / 0 失败 / 0 跳过；新增重复撤销测试通过。
- 真实 WinUI（非 headless）：owned 窗口链接逐字产品 animator 源码，
  `winapp ui record --frames` 以 30fps 采集真实 DWM 像素，按按钮矩形算平均亮度。
  红侧（父 animator；可见窗口中父提交新增的 `IsEffectivelyVisible` 恒为真，
  不影响该路径）加上父 XAML 等价的 `Implicit.ShowAnimations` 捕获到上述回亮；
  绿侧在 takeover / hide / repeat / busy（UI 线程阻塞 700ms）/ remount 均单调到 0，
  刷新按钮区域亮度恒定。红侧回亮是调度竞态，多次运行只捕获到一次，不声称每次必现。
- Native AOT：unpackaged `PublishAot` 成功；补齐 app `.xbf`/`.pri` 后运行，
  PID 34740，存活 34s，退出码 0，逐帧单调到 0，Application 日志/WER 无该 exe 记录。

## 环境与限制

- 初始 kernel 工作区异常（HEAD `f02c33f6`、索引全量删除、工作区为空）。先记录，
  再从两个 bundle 显式 fetch，本区 reset 到父 gitlink 的 `62b376ec`；未触碰父。
- AotDev `2.2611.1.4`（EXE `ProductVersion 1.0.0+a0ee201f...`）仅只读核验；
  用户实例 PID 2368 未操作、未关闭、未升级；未读写凭据/登录数据。
- 未注入合成桌面输入；场景由 owned 进程内定时器驱动。为让 WGC 采集可见，
  owned 窗口曾短暂激活，未对其它应用输入。
- 证据（JPEG/CSV/JSON/MP4）在 ignored `artifacts/t3-4957630f/`；未提交 docs 临时数据。
- 未 push / PR / issue；未清理其它 worktree；未安装/更新用户应用。
