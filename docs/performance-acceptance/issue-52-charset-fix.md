# SVG response charset correction

The integration-52 review reproduced loss of HTTP charset before SVG rendering. The bounded image loader now preserves the full Content-Type header. MyImage identifies SVG using the parsed media type and decodes through the production loader helper using the declared charset (including quoted names), default UTF-8 and BOM detection. Cancellation, bounded buffering and stream ownership remain intact; the reader leaves the loader-owned stream open. No new HTTP requests, global client changes or production dependencies were introduced.

Focused regression `SvgResponsePreservesDeclaredCharset` first failed against the integrated source: expected `image/svg+xml; charset=iso-8859-1`, actual `image/svg+xml`. It now also asserts exact decoded `<svg><text>café</text></svg>` at the production consumer boundary. `SvgTextDecodesCharsetOrBom` covers UTF-8, quoted Latin-1 and UTF-16 BOM without charset. Tests use a controlled handler and the actual linked production helper; they do not instantiate the WinUI SVG renderer.

Commands from the integration checkout:

```powershell
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --filter FullyQualifiedName~SvgResponsePreservesDeclaredCharset --logger 'trx;LogFileName=svg-charset-red.trx'
dotnet test tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=svg-charset-green.trx'
dotnet build src/Desktop/BiliCopilot.UI/BiliCopilot.UI.csproj --no-restore -c Debug -p:Platform=x64 -p:SolutionDir=C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-1d19cc80/src/ -p:GenerateAppxPackageOnBuild=false
```

Red: one expected assertion failure. Green: 366 passed, zero failed/skipped; TRX counters independently parsed. Logs/TRX are in the ignored test TestResults directory. Debug x64 build exited 0 with zero warnings/errors in 2:14.29; log: `svg-charset-build.log`.

Applied focused code-testing/run-tests guidance and WinUI review: no XAML/binding changes; SVG routing still uses case-insensitive MIME comparison, and cancellation is checked before and after the renderer call. Specialized WinUIAnalyzer remains unavailable. Real SVG pixels, unload/reopen, resize/DPI and native memory remain pending; no desktop interaction was performed. This corrects the identified text-decoding defect, not all acceptance gaps in integration-52.md.
