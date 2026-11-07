# Playback routing (#187) verification

Implemented against approved #182 and integration tip `70e0de91da769eb45bee2a3e0190662b50f81928` (2026-11-07).

## Behavior and boundary

`PlaybackRouting/PlaybackRouteSelector.cs` selects one track's current API candidates. It has no WinUI/mpv dependency. It validates HTTP(S) candidates, deduplicates exact strings, preserves signed URLs byte-for-byte, filters known risk when requested and an alternative exists, then applies the preference. Auto and fallback sort by risk with stable API order. Main/backup identity survives filtering and deduplication. Provider matches use exact known hosts; risk domains use exact domain/subdomain boundaries, case-insensitive with DNS trailing-dot normalization. IP literals rank as risk. Unknown hosts are not proof of safety; this is a bounded heuristic, not network measurement or a guarantee against freezes.

`PlaybackRouteSettings` is the small local-settings adapter. Only a stable integer preference is persisted (Auto=0, Primary=1, Backup=2, Ali=3, Tencent=4, Huawei=5, Overseas=6). Invalid integers read as Auto. The dedicated settings partial binds the existing playback settings control. Fallback and next-source-load timing are explained in localized text. Existing quality, entitlement, authentication, preview handling, caching and playback lifecycle remain in the resolvers.

Video and PGC invoke the same selector independently for video/audio. Live, external-player and download routing are not changed. No refresh command, URL rewriting, new assembly, network backend, dependency or health storage was added.

## Automated evidence

Tests use the existing xUnit/VSTest source-linked project. The new resolver tests execute production `GetSourceAsync` and quality selection with playback API fakes and OS/player-output adapters. Settings tests execute the production partial with the existing in-memory settings adapter; they do not test Windows storage durability.

| Requirement | Evidence |
| --- | --- |
| Auto risk preference, original signed string | `Auto_PrefersLowerRiskAndPreservesSignedUrl` |
| Main/backup and no-P2P precedence | `ExplicitRoutes_RespectNoP2PBeforePreferenceAndKeepAllRiskFallback` |
| Providers and lookalike-domain rejection | `Provider_SelectsOnlyMatchingSignedCandidate` |
| Known risk domains, casing, trailing dot and boundaries | `Auto_RiskUsesDomainBoundaries` |
| Empty/invalid/duplicate candidates, missing backup/provider, IP literals | `EmptyInvalidDuplicateAndMissingPreferredCandidates_FallBackStably` |
| Stable all-risk fallback and same-provider ties | `AllRiskFallbackAndProviderTies_PreserveApiOrder` |
| Auto default, all seven choices, invalid stored preference, integer-only persistence | `Settings_DefaultAndEveryChoiceRoundTripWithoutPersistingUrls` |
| Both resolvers, every preference, independent audio/video, no-P2P, retained auth headers, no signed-URL storage | `Source_SelectsVideoAndAudioIndependently` (16 cases) |

Recorded red-green cycles: initial Auto selection (1 failing then passing); explicit preference/precedence; risk/provider classification; both resolver outputs (2 failing then passing); settings persistence (1 failing then passing). Final module/resolver coverage: 33 cases. An affected run also covered quality selection, playback sessions, seeks, late callbacks and playback-unavailable classification: 128 passed before the final additional tie/fallback case.

Final complete UI test command:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore `
  --logger 'trx;LogFileName=ui-suite-final.trx' --results-directory <evidence-dir>
```

Exit 0: **Passed: 1023, Failed: 0, Skipped: 0**. TRX parsed successfully. Existing test-project warnings include CS8765 and xUnit1026.

App/analyzer command:

```powershell
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj -c Release -p:Platform=x64 `
  -p:SolutionDir="$PWD/src/" -p:GenerateAppxPackageOnBuild=false `
  -p:AppxPackageSigningEnabled=false -p:WinUIAnalyzerObservation=true `
  -p:WinUIAnalyzerSarifPath=<evidence-dir>/winui-analyzer.sarif
```

Exit 0: **0 errors, 115 warnings** (114 documented observation-only CS9264 warnings and one unused protobuf import). SARIF: **0 WUI warnings/errors**, 237 WUI2020 notes and 2 WUI1010 notes. No analyzer rules or warnings-as-errors policy changed. No native dependency copies were necessary for this non-packaging build.

## Native UI evidence and limits

An isolated unpackaged WinUI host compiled the actual dropdown markup (localized resource values substituted), production settings partial, and settings adapter with in-memory storage. Its real ComboBox selected each of the seven values, verified two-way binding and re-created the settings VM to verify saved-value reading. All seven passed. UIA exposed the expected name and AutomationId; dropdown expansion and dark-theme collapsed/expanded screenshots were inspected. Labels and fallback text were readable, with no clipped dropdown items.

This is **not full app UI acceptance**: the host does not include the actual SettingsCard layout or Windows local-settings storage. Keyboard input was refused by winapp with `foreground_not_target`; follow-up UIA item selection was inconclusive because popup selectors became ambiguous/stale. No keyboard pass is claimed. Full settings-page layout, Light/High Contrast, DPI variants, restart durability and real authenticated video/PGC playback remain unverified. The host was closed. No installed app was replaced.

Local evidence and temporary host: `C:/Users/hp/.t3/research/spec-187/`. These are diagnostic artifacts, not portable prerequisites. Integration merge returned `Already up to date`; `git diff --check` passed.
