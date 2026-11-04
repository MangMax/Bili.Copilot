# Search recovery regression (#125, spec #123)

The #124 baseline on integration tip `60f1c74646e0ca88a059942a7b83b27b507c0a49`
was 7 passed / 2 failed: same-keyword empty retry and late empty response ownership.
#125 turns these into passing regressions and extends the lifecycle/concurrency coverage.

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj -p:SearchRecoveryRegression=true --filter FullyQualifiedName~SearchRecoveryInvestigationTests
```

Expected: 21 passed, 0 failed, 0 skipped. The old `SearchRecoveryInvestigation=true`
switch remains an alias. No intentional red/skip tests remain. Rebuild when switching
modes: `--no-build` can otherwise run the previous mode's assembly.

This opt-in mode links production SearchBoxViewModel, NavigationViewModel, OverlayFrame,
page factory, SearchPage, SearchPageViewModel, video and PGC section commands. Controlled
ISearchService responses prove request identity, empty/error recovery, stale publication,
latest loading, close/reopen, video paging/refresh and lazy PGC query snapshots. Live,
user and article sections remain inert adapters; they throw if loaded. Windows lifecycle,
dispatcher, card presentation, localization and playback remain adapted.

NavigationFrameTests and NavigationViewModelTests need their existing SearchPage stub;
they are excluded only in this mode. Run them separately without the switch, alongside
SearchSuggestionTests and PopularPageLoadingTests. No shared test adapter redesign.

The XAML refresh assertion is a **source binding contract**, not actual UI automation.
The harness does not prove Enter/IME/suggestion-click, runtime bindings, HTTP/authentication
or live/user/article section behavior. It does not reproduce the original report of all
subsequent different words permanently failing; the confirmed defects are narrower.

External reports and actual command evidence:
- `C:/Users/hp/.t3/userdata/search-spec-123/investigation.md`
- `C:/Users/hp/.t3/userdata/search-spec-123/implementation.md`

## Review-fix CI and submission diagnostics

The required `test` job retains the default navigation suite, then builds and runs
`SearchRecoveryRegression=true` with the recovery-class filter. Never use
`--no-build` when switching between these modes: they share an output assembly.

`SearchSubmission <random Guid> submitted` is recorded first in QuerySubmitted
(before suggestion suppression), or at SearchCommand for other submitters.
`command`, `navigation`, and `page` show how far dispatch progressed. A page
`Search <PageId> <QueryId> submission <Guid>` links this identity to the existing
start/completion/cancellation/failure records, including deferred Loaded and reuse.
A submitted ID with no page/start distinguishes a submission that did not reach
the request. AV/BV playback shortcuts can reach page without starting a search.
`empty` and `region` classify submissions outside the global result path.
Only IDs and fixed stages are logged; navigation still carries the keyword in
memory. No keyword/hash, exception object/message, response or credential is logged.
These headless tests do not prove real AutoSuggestBox dispatch or runtime bindings;
Windows authenticated Enter/suggestion/refresh acceptance remains outstanding.
