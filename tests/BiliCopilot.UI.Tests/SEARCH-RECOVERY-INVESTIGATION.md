# Search recovery investigation (#124, spec #123)

This is an opt-in investigation, not the #125 fix. Production files are unchanged.
The full report stays outside the repository:
`C:/Users/hp/.t3/userdata/search-spec-123/investigation.md`.

Run from repository root (pinned .NET 11 preview SDK, VSTest/xUnit v2):

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -p:SearchRecoveryInvestigation=true --filter FullyQualifiedName~SearchRecoveryInvestigationTests
```

Expected investigation baseline: **Passed: 7, Failed: 2, Skipped: 0**.
The two `InvestigationRed` cases intentionally assert spec behavior and fail on
unchanged production code. Hand them to #125 to turn green; do not mark skipped:

- `Investigation_CompletedEmptyAllowsExplicitSameKeywordRetry`: expected two service requests, actual one.
- `Investigation_LateEmptyResponseMustNotOverwriteNewKeywordVideo`: B's video disappears when older A completes empty.

`CompletedEmptyThenDifferentKeywordPublishesNewVideoOnReusedPage` passes.
`LateEmptyOverwriteDoesNotPermanentlyBlockAnotherDifferentKeyword` also passes.
Neither same-keyword deduplication nor the reproduced race establishes the reported
persistent failure for every subsequent different keyword.

The opt-in build replaces the existing fake SearchPage with the **linked production**
SearchPage, SearchPageViewModel and video section, retaining real SearchBoxViewModel,
NavigationViewModel, OverlayFrame and page factory. The two old navigation test classes
require their original fake SearchPage and are excluded only in this opt-in build;
run them in the default build. Other partition objects are inert, explicitly out of
scope, and throw if their load command is invoked. The OS Loaded/Unloaded events,
dispatcher, item presentation, localization and playback surfaces are adapted.
No actual XAML binding, Enter/IME/suggestion-click event, real HTTP, or non-video
partition behavior is proved by this harness. The full report specifies follow-up.

After switching build modes, rebuild: `--no-build` can otherwise run the previous
mode's assembly. Investigation results must report nine discovered cases.
