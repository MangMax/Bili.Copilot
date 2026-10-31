# Navigation constructor contract — independent review history

Latest review: the constructor-wrapper blocker below was corrected in fixed tip `9bc9648499a043a4dc81293c09d24cedd250751e`. Fresh merger independently inspected the catch boundary and reran the source-built suite: Passed: 510, Failed: 0, Skipped: 0, including 23 navigation cases. Both frame failures preserve the exact original InnerException reference, current page identity and visibility; the overlay stack remains unchanged and navigation recovers after clearing the injected fault. AsyncLocal is confined to the test adapter and cleared in finally; unrelated test execution contexts do not share the injected value. It is not a production hook or proof of arbitrary child-task isolation.

The catch encloses only registered direct construction (and Type comparisons); unsupported-Type ArgumentException, SetParameter, Initialize and frame mutations remain outside it. No further blocking regression found in this slice. Fixed tip merged with `--no-ff` as `f721b9bc075df03772a1eb1900e0700301053289`. See [fresh integration evidence and remaining gates](integration-aot-navigation-constructor.md). The original review and its rejected candidate evidence below are retained verbatim as historical findings, not the current merge decision. Full AOT adoption and actual WinUI first navigation remain unaccepted.

2026-10-31. Reviewed candidate `d58342cbe0d4a391b90aa159d9192c80afbc6f3a` against parent `8994fed85c6f55c5c93dab30e2069a99d70ad608` on `feat/performance-45`. Candidate source checkout: `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-f02c3f07`. Input acceptance report: `docs/performance-acceptance/aot-navigation-constructor-contract.md` at candidate SHA. No child agents. Writes confined to ignored parent `artifacts/navigation-review`.

## Decision / Spec finding

**HOLD / P2 — preserve registered-page constructor exception wrapping.** Candidate `src/Desktop/BiliCopilot.UI/Controls/NavigationPageFactory.cs:20` (and the remaining direct-new branches), called by `MainFrame.xaml.cs:59` and `OverlayFrame.xaml.cs:67`, changes failures of supported page construction from `TargetInvocationException` with the original exception in `InnerException` to the original exception itself. This is distinct from the documented unsupported-Type boundary. The explicit review requirement is to prevent exception changes. The candidate report describes normal navigation preservation but does not disclose this change; none of the 22 tests covers constructor failure.

A focused no-UI reproduction compiles exact baseline/candidate frame sources (candidate factory unchanged), using the candidate presentation adapters with only PopularPage's constructor made to throw `InvalidOperationException("constructor sentinel")`. Both frames first receive a valid MomentPage. Fresh Release executions demonstrate:

- baseline: Main and Overlay both throw `System.Reflection.TargetInvocationException`, inner `System.InvalidOperationException`;
- candidate: both throw `System.InvalidOperationException`, no inner exception;
- `typeof(object)`: baseline returns normally; candidate throws `ArgumentException`;
- an unregistered UI adapter type: baseline navigates; candidate throws `ArgumentException`.

See `baseline.log`, `candidate.log`, and reproducible source projects `baseline/Probe.csproj`, `candidate/Probe.csproj` beside this report. These are diagnostic experiments, not actual WinUI constructors or an AOT UI run. The reproduction uses the exact two frame implementations; only the constructor fault is injected into the test presentation surface. Real page constructors call InitializeComponent and resolve ViewModels, so construction is not an exception-free contract. Exception type/message/InnerException are observable; App's unhandled exception handler also logs the exception and inspects its message.

Suggested bounded correction: retain direct static construction but preserve the old exception wrapper for exceptions thrown during registered-page construction only. Do not wrap SetParameter/cache/Initialize failures; those were outside Activator before. Add a constructor-failure regression for both frames that checks wrapper, original inner exception, and unchanged content/stack. Keep unsupported-Type validation separate. No fix or merge was performed, as requested for a substantive blocker.

## Standards / WinUI review

No new XAML/binding/threading/lifetime standard violation identified in this diff. Direct construction remains on the navigation caller's thread; no static page instance, new subscription, or cache lifetime change. WinUIAnalyzer was not installed/run in this review. Existing MVVM fields/commands, dependencies and warning policies were not expanded or altered.

## Caller and behavior review

All enabled production callers examined under src: 9 main menu/footer types (Popular, Moment, VideoPartition, LivePartition, Anime, Cinema, ArticlePartition, Message, Settings), 8 overlay types (ArticleReader, Fans, Favorites, Follows, History, Search, UserSpace, ViewLater). All are in the 17-branch factory. Download is commented out. GalleryWindow's similarly named MainFrame uses standard Frame.Navigate and is not this custom frame. No active caller or string-to-Type route into these custom frames outside that catalog was found. External AppNavigationItemViewModel passes the menu Type through the unannotated interface closure; the candidate's metadata evidence is consistent with that path.

Unsupported types are an intentional closed-catalog change disclosed in the candidate report, NOT general behavior equivalence: constructible non-UI objects previously silently returned, unregistered UIElement types previously worked, and invalid constructor shapes previously could throw other reflection exceptions. No currently enabled application route was found lost. This does not establish compatibility with arbitrary callers of the public Type API. The two rejection tests prove the NEW boundary rather than preservation of the old one.

For registered successful construction, the diff preserves parameter-before-insertion, exact runtime-type cache lookup, Main revisit SetParameter plus Initialize, hidden-page CancelLoading, Overlay cache Move to top without Initialize, GoBack removal/reveal, ClearBackStack and null frame early return. NavigationViewModel's same-main-page early return and Search shortcut are unchanged. Service-level settings/window/temporary-overlay state can change before frame failure; the candidate correctly does not claim transactional service state preservation.

The 22 new tests link real frame/factory logic, and assertions on identity, parameter, order, visibility, cancellation and initialization are useful behavior checks. However all 17 page substitutes implement all three interfaces, unlike real pages; they do not validate real constructor/DI/XAML, loaded/unloaded events, animation semantics, actual page interface behavior, or service-level paths. Their passing status cannot establish exception preservation or real first-navigation AOT UI acceptance.

## Evidence verification and fresh checks

Fresh read-only verification of candidate evidence (not a fresh complete ILC invocation):

- full red/green original audit logs equal the checked-in diagnostic extracts; both original ILC exit codes are 1;
- strict IL diagnostics 8 → 6, removing exactly MainFrame/OverlayFrame IL2067; all 6 remaining SharpGen/Azure lines identical;
- both formal Mpv UseSegoeMetrics missing-member / will-always-throw lines identical;
- both 348-entry frozen input manifests rehashed (696 hashes); only BiliCopilot.UI.dll differs between phases;
- response files equal after normalizing phase directory names, retaining -O, warnaserror, singlewarn:false and no nowarn; source/log hashes match committed validation.json;
- 221 available green PDB source/frozen pairs match at review time. This is a present-time comparison, not independent observation of the historical ILC process;
- the prior Debug/Release build and local AOT probe logs were hash-verified as existing evidence. They were not rerun or relabeled as fresh builds. The native probe links presentation/page adapters and cannot establish real UI constructor success.

Fresh test execution against candidate's EXISTING Release test DLL, no rebuild:

`dotnet vstest C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-f02c3f07/tests/BiliCopilot.UI.Tests/bin/Release/net11.0/BiliCopilot.UI.Tests.dll "/Logger:trx;LogFileName=review.trx" /ResultsDirectory:C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/artifacts/navigation-review/tests`

Exit 0. Passed: 509, Failed: 0, Skipped: 0. Fresh TRX independently checked for 509 unique executionIds and 22 navigation results. This reruns the supplied binary; it is not a fresh source build. `tests.log` and `tests/review.trx` are alongside this report.

Fresh diagnostic builds/runs: `dotnet run --project artifacts/navigation-review/baseline/Probe.csproj -c Release` and equivalent `candidate/Probe.csproj`, both exit 0 (diagnostic programs catch and print failures). Their output confirms the blocking difference; exit 0 does NOT mean the compatibility requirement passed.

No unnecessary complete ILC rerun after the defect was identified. No full UI build, deployment, registration, login, UAC, push, PR, issue activity, media package switch or other checkout write. Strict 364 compiler diagnostics, 309-property migration, third-party compatibility and distribution gates remain outside this slice. Actual first/repeated navigation and overlay back on the independently identified AOT UI remain unverified.

## Repository handoff

Merge: **not performed**. Integration report commit: **not created**, because review is blocked. Parent final HEAD remains `8994fed85c6f55c5c93dab30e2069a99d70ad608`; kernel remains `0ae5e4e000a41a07f6696746706df11c06e8835b`. Parent tracked working tree and kernel are clean. Candidate checkout is only read, not edited. This report is intentionally an ignored review artifact, leaving the parent branch unchanged.
