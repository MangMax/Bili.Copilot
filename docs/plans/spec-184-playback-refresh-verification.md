# #184: user-triggered playback refresh verification

Implemented against the approved spec-182-playback-routing.md on top of
83efa7a6ee7ffd275c01d26e4b3aa8699f5914f3 (#187). The prepared checkout matched
integration/spec-182-playback-freeze; merging its tip before handoff returned
Already up to date. Parent owns draft #185, publishing and issue closure.

## Behavior and scope

One Refresh Playback control is added to the existing video/PGC transport bar,
with localized tooltip, UIA name and command availability. There is no node menu.
The PlaybackRouting controller owns a cancellable, single-flight operation;
PlayerViewModel's adapter joins the existing position/speed/session lifecycle.
Fetch has a 20-second deadline; an admitted native load is joined until file-loaded,
error or a 15-second loading deadline. There are no retries/watchdogs/backend changes.
Native calls themselves are awaited before teardown; the deadlines do not forcibly
abort an already submitted native command.

Both resolvers perform a new playurl request in an isolated resolver and commit
only in the current source generation. A failed request or unavailable selected
quality/codec keeps the old playable cache and playback. Exact quality filtering
still applies existing entitlement rules. Video initialization now resets its
load task, and late video/PGC completions cannot overwrite a newer media cache.
Video/audio host avoidance is independent and only breaks equal-risk ties inside
the preferred group; explicit primary/backup semantics remain unchanged.

A successful fetch uses the existing Client.PlayAsync/loadfile path, not seek or
ReplayAsync (the latter unconditionally resumes). Position, speed and volume are
read from the native client after earlier writes drain, rather than stale UI
polling values. Native pause/mute remain intact across loadfile. New seek, speed,
pause/mute/volume, quality, switch or close cancels pending fetch; once loading is
admitted, new operations/teardown wait on the existing queue. Normal initialization
must not force Resume for a refresh. Loading events and existing tip UX handle
loading/failure. PGC quality selection also updates its resolver when requested.

The 21-file scope is coherent: one controller, one player adapter, selector/base
and two resolvers, one button/resource, seven existing player partial hooks, four
test files/project registration, and these notes. Most player partial edits are
single admission/invalidation calls; no new player lifecycle or assembly was added.

## Behavioral evidence

All tests use the approved source-linked UI.Tests seams. Representative tests:

| Requirement | Evidence |
| --- | --- |
| Real playurl refresh; independent video/audio hosts | Refresh_FetchesAgainAndAvoidsPreviousTrackHosts (video and PGC) |
| Late completions cannot poison new media; completed initialization resets | Initialize_RejectsLateOldFetchAndKeepsNewMediaCache; Resolver_ReinitializeAfterSuccessFetchesNewPart (both resolvers) |
| Failure retains old source; no quality fallback | Refresh_UnavailableQualityOrFetchFailurePreservesPlayableSource |
| New quality wins over late refresh | Refresh_NewerQualitySelectionRejectsLateSource (both resolvers) |
| Codec retention and truthful unavailability | Refresh_RetainsActualSelectedCodecAndRejectsUnavailableCodec |
| Cancelled fetch and retry after initial error | Resolver_CancelledRefreshCannotCommitAndInitialFailureCanRetry |
| Single-flight/cancel/stale/timeout/error/retry | Refresh_IsSingleFlightAndCancelledFetchNeverReopens; Refresh_StaleSessionDoesNotReloadOrShowAnError; Refresh_ReportsFetchFailureOrTimeoutAndAllowsRetry |
| Actual production command calls load with state | PlayerRefresh_UsesFreshSourceAndReloadsWithCurrentPositionSpeedVolume |
| Native state instead of lagging UI | PlayerRefresh_CapturesNativeStateAfterQueuedChangesInsteadOfStaleUiPolling |
| New seek/speed win; native teardown waits | PlayerRefresh_NewerSeekDuringFetchCancelsReload; PlayerRefresh_NewerSeekAndSpeedWaitForNativeLoad; PlayerRefresh_CloseWaitsForAdmittedLoadAndRejectsLateFetch |
| Pause/mute/volume admission and switch/close | PlayerRefresh_NewPauseOrSwitchIntentDuringFetchPreventsReload; PlayerRefresh_CloseDuringFetchDiscardsLateSource (the shared gate is exercised, not the whole WinUI commands file) |
| Failure re-enables command | PlayerRefresh_LoadFailureIsReportedAndButtonCanRetry |
| Preference/risk/primary/backup boundaries | Refresh_HostAvoidanceIsOnlyATieBreakInsidePreferenceAndRisk (8 cases) |

Observed red/green runs were retained for fetch count, stale cache pollution,
single flight, timeout reporting, production command loading, new seek/speed,
native state capture and codec loss. Final affected selection: **132 passed**.
Full UI.Tests: **1058 passed, 0 failed/skipped**.

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -p:Platform=x64 `
  -p:SolutionDir="$PWD/src/" -p:GenerateAppxPackageOnBuild=false `
  -p:AppxPackageSigningEnabled=false -p:WinUIAnalyzerObservation=true
```

Release/x64: **0 errors, 114 existing CS9264 warnings** in the final incremental
observation build. The first clean dependency build also emitted the existing
unused protobuf import warning (hence the earlier 115-warning baseline).
SARIF: **0 WUI warnings/errors**, 239 note-level findings. No analyzer settings,
package versions, native dependencies or global transport defaults changed.

## Real libmpv evidence and limits

Artifacts and the reproducible source-linked harness are outside the repository:
`%USERPROFILE%/.t3/research/spec-184-refresh/` (TRX/logs, native project, server,
runner, result JSON, request ledger and mpv showinfo logs).

The harness links the actual production PlayerViewModel.PlaybackRouting.cs and
PlaybackRefreshController.cs and invokes generated RefreshPlaybackCommand against
the shipped Richasy MpvClient/MpvPlayer and the existing libmpv DLL. The playurl
service is a loopback fixture; it is not a live Bilibili account/entitlement test.
Source resolver correctness is separately covered by the production resolver tests.

Both native runs first demonstrate video terminal EOF/cache end 7.833333 while
position advances to about 14 seconds. Refresh issues a new loopback playurl call,
then video Range bytes=0- and bytes=1596519-. Both receive fresh bytes after the
outage. Decoded frame PTS resumes beyond the old EOF:

- Paused/muted: speed 1.75, volume 37, pause=yes, mute=yes after reload; position 14;
  explicit resume advances to 23.7, with decoded PTS reaching 23.8.
- Playing/unmuted: speed 1.75, volume 37, pause=no, mute=no; progresses to natural
  end 23.999995, frame 720, decoded PTS 23.966667.

The existing wrapper rounds loadfile start to whole seconds, so resumed position
has about half a second of rounding tolerance; this is not frame-exact recovery.
Null audio/video sinks prove actual native demux/decode and state, not displayed
WinUI playback or audible continuity. No seamless recovery claim is made.

An initial diagnostic run crashed when the harness used the wrapper's string-return
native inspection API. Inspection showed its generated allocator release does not
use mpv_free. Harness-only metrics now use pointer-return P/Invoke and mpv_free;
production code uses existing typed client getters and no dependency was modified.
Both retained native verification runs exited 0.

## UI acceptance boundary

Windows Sandbox is unavailable on this host. Local project-mode winapp run of the
Release/x64 build was attempted with the correct Platform/SolutionDir. Registration
was rejected because Mang.BiliCopilot is already installed as a non-development
package with the same identity. The installed app/data were left intact; no uninstall
or package replacement was performed. Thus the actual refreshed button, accessibility,
layout and full-app playback remain **not verified in a running application**.
The XAML binding and analyzer/build succeeded; those do not substitute for UI acceptance.

## Integration review follow-up

Standards review found no documented hard violations. The suggested shared resolver
refresh transaction is a P3 maintainability heuristic; the focused implementation
keeps video/PGC request and entitlement paths separate rather than introducing a
new abstraction in this repair.

Spec review found a P1 missing UI integration: touch seek/volume still wrote directly
to the client, bypassing refresh cancellation and ordering. The touch handlers now
use ChangePositionAsync/ChangeVolumeAsync; touch preview pause joins the refresh
queue, and double-tap/transport playback buttons use PlayPauseCommand. The post-seek
resume checks the captured session and joins any admitted native refresh.

Parent reran the refresh/routing/seek/speed/session selection after this wiring fix:
125 passed, 0 failed, 0 skipped. TRX is retained outside the repo under
spec-184-refresh/integration-verification/review-fix-regression.trx. These tests
exercise production state/queue logic, not actual touch dispatch; the UI acceptance
limitation above still applies. No new source-text assertion masquerades as a UI test.

The parent Release/x64 observation build after the UI entry-point correction
completed with exit 0, 0 errors and 115 existing warnings (114 CS9264 plus the
unused protobuf import). SARIF contains 239 WUI notes and no WUI warning/error.
Completed implementer worktrees were verified clean and merged, then removed;
committed evidence is available on the integration branch and private artifacts
remain under the research directory.
