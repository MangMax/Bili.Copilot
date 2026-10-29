## Summary

性能规格 #45 的集成 PR，当前仅合入 #46。AI 回复按会话合并更新，取消后拒绝旧结果，最终答案仍使用 Markdown 呈现。

```text
响应分片 → 当前会话缓冲 → 合并显示 → 完成时提交全部文本
取消/切换 → 会话失效 → 丢弃迟到结果
```

后续逐票集成，所有票完成审核验收之前保持 Draft，不应合并。

## Evidence

- Before：逐分片全量渲染；行为测试复现重复发布和旧会话迟到发布。
- After：#46 六项行为测试通过；集成后 Release 全套 288 passed、0 failed、0 skipped。
- 实施工作树 Debug x64 构建通过；集成工作树 C#/XAML 已编译，但首次打包检查缺少本地 libmpv，待补齐已有依赖后复验。
- #46 的 WinUI 静态审核无阻断发现；真实渲染、链接、剪贴板、取消交互和输入延迟验收仍待桌面时段。未宣称 UI/AOT 验收通过。
- 每票证据见 docs/performance-acceptance；其余票尚在实施或排队，调查票须提交调查结果。

## Merge Danger

**Door:** two-way

可回退各票提交，无数据迁移。

**Blast Radius:** desktop

AI 会话与 XAML 展示；后续扩至已批准的播放、首页、搜索等独立切片。最终合并前需整体复核及逐票 WinUI 验收。

## Tracking

以下关闭关系仅在本规格全部验收后合并时生效，当前不关闭任何工单。

Closes #45
Closes #46
Closes #47
Closes #48
Closes #49
Closes #50
Closes #51
Closes #52
Closes #53
Closes #54
Closes #55
Closes #56
Closes #57
Closes #58
