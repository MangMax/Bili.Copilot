# Populated partition ranking idle correction

Integration review #53 identified a pre-existing feedback path: a populated partition ranking skips its network branch but still raises VideoListUpdated; PopularMainBody schedules another delayed bottom check. The global ranking already returned early. The correction applies the same early return to populated partition rankings. Refresh still clears the list before loading, so it remains available. Empty/error responses and other page types are not changed by this focused correction and remain part of broader real-UI acceptance.

`PopulatedRankingStopsLoadMoreNotificationsButRefreshStillLoads` exercises the actual linked PopularPageViewModel with existing headless adapters, for both global and partition rankings. After initial content, three more load commands must produce no updates or network requests, and leave loading false. A subsequent refresh must request again, notify once and restore content.

Red: the partition case failed with 3 updates instead of 0; global case passed. Green: full suite 380 passed, zero failed/skipped; parsed TRX counters confirm the result. Debug x64 build exited 0 with zero warnings/errors in 2:05.94. This tests the production command/event boundary, not a measured Windows timer loop or CPU improvement.

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --filter FullyQualifiedName~PopulatedRankingStopsLoadMoreNotificationsButRefreshStillLoads --logger 'trx;LogFileName=rank-idle-red.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=rank-idle-green.trx'
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
```

TRX and build output remain in ignored TestResults (`rank-idle-build.log`). Focused WinUI review: only the existing command guard changes, with no XAML, bindings, dispatcher or scroll threshold changes. Dedicated WinUIAnalyzer was unavailable. Desktop belongs to #57; no deployment/input performed. Real short-list pagination, terminal/error behavior, DPI/resize and performance remain pending.
