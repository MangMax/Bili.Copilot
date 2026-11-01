# Continuous scrolling experiment

This fixture addresses a gap in the earlier ScrollCompleted checks. It restores
the checked-in mount-diagnosis host/assets and uses the production card controls,
templates, image lifecycles and AppRepeater policy with offline adapter models.
It does not access account state or authenticated pages.

## Replay

Use a fresh checkout/output and a kernel repository containing exactly
`0ae5e4e000a41a07f6696746706df11c06e8835b`:

```powershell
python docs/list-resize/scroll-continuity/prepare.py --kernel-repo externals/bili-kernel
dotnet build artifacts/recycled-cards/host/Harness.csproj -c Release -p:Platform=x64 -p:NuGetAudit=false
pwsh -NoProfile -File docs/list-resize/scroll-continuity/run.ps1 -Stage search-baseline -Action Focus -Family Search -Cache 0.5
pwsh -NoProfile -File docs/list-resize/scroll-continuity/video-pixels.ps1 -Stage artifacts/recycled-cards/search-baseline
pwsh -NoProfile -File docs/list-resize/mount-diagnosis/wer.ps1
python docs/list-resize/scroll-continuity/check.py artifacts/recycled-cards/search-baseline
python docs/list-resize/scroll-continuity/summarize.py artifacts/recycled-cards/search-baseline
```

Repeat with a fresh stage and `-Cache 1` or `-Cache 2`; no build is needed between
cache arms. Families: Search, User, Live, Article, Season. Each run tests both 50
and 500 fixed items at 1400×900 physical window size, with actual DPI recorded.
`UserReturn` selects the additional bounded, already-warmed-region trajectory.
`ArticleReal` and `SeasonReal` select unique synthetic PNGs at the current kernel
adapter's requested dimensions: article cover 400×220 / avatar 48×48 and season
poster 320×426. The dimensions are grounded in `ImageAdapter.ToArticleCover`,
`ImageAdapter.ToPgcCover`, and `Services.Article/Core/Adapters/ArticleAdapter.cs`.
They supplement the archived 1600×900 cover-and-avatar stress input; they do not
erase it or establish JPEG/network/codec performance. realistic.py generates
these deterministic files without a network request.

For the unchanged large-PNG User resize trajectory use the existing
`mount-diagnosis/run.ps1` with `-Action MatrixRed -Cache <value> -Family User`.
The replay preparation sets its cache **before mounting**, and changes the
selector/assertion, not its 120
native width requests, data, image, settle period or original 200ms gate.
Other existing actions remain available: MatrixGreen (unique User500 binding
oracle), ResizeChecks (undo/anchor/recommendation image checks), ImageChecks with
`-Family Images` (Basic/Blur lifecycle) or `-Family PaneNaturalShared` (two panes).
Use the existing recycled-cards/check.py or mount-diagnosis/check.py for these
actions; their raw assertions, not the button names, define their coverage.

## Measurement limits

* Initial half-viewport, animation-disabled ScrollTo attempts did not reproduce
  the reported whole-background symptom. Their raw results remain separate.
* The inertia stress applies 64 native velocity impulses, ±4000 every requested
  50ms, reversing the impulse sign every eight steps. It can cross many viewports
  and visit initially unrendered regions. All source items already exist; this is
  not a network/pagination test or a representative mouse-wheel speed claim.
* The Return trajectory issues 32 animated targets, alternating between zero and
  three viewports inside the warmup region, every requested 150ms. Raw offsets
  establish actual motion; target requests are not treated as completed motion.
* Rendering callbacks record visible outer-presenter indices and VM reference
  identity, image Sources/readiness, geometric coverage and realized counts.
  Null card/VM/Source is retained. These callbacks **cannot observe compositor
  frames during a blocked UI thread**. Vertical interval coverage alone also
  cannot detect missing columns or distinguish end-of-content overscroll.
* Independent `winapp ui record --fps 30 --frames` captures the owned window
  throughout motion. The viewport crop derives from the fixed window and native
  viewport height/DPI. RGB samples at 10-pixel spacing classify near-black
  background (`R,G,B < 6`); >90% is the reported whole-background threshold.
  Native card backgrounds/text and cover pixels remain separate from black
  page background. JPEG compression and 30fps sampling limit temporal precision;
  durations are sampled estimates, not exact compositor frame times.
* The continuous runner's resize follows scrolling immediately and is a combined
  stress observation. It does not replace the settled original PNG resize gate.
* Memory fields are process working/private bytes at case completion, not a
  texture-cache census. Realized counts directly measure retained list elements.
  Reverse work uses observed decreasing offsets, not requested impulse sign.
* Reported improvements must be read per family/count; a favorable video result
  is not evidence that every list, speed or DPI improved. No 60fps/AOT claim.

## Evidence and isolation

Only freshly generated `BiliCopilot.Recycle*.Owned` identities are launched. The
runner checks exact executable paths, retains the process handle/exit code,
observes at least 32 seconds after close, removes only its registered package,
and yields desktop control. Run wer.ps1 afterwards for exact PID/path/time-window
WER receipts. Existing user instances and credentials are outside the fixture.

`video-pixels.json` includes crop coordinates, sample stride, original JPEG hash,
and a row-major RGB grid filename for every measured frame. Grids permit an
independent recomputation of all background fractions and durations. The compact
archive preserves every grid, all raw timings/identity/exit receipts, and JPEG
sequences around each longest blank interval (with 150ms margins), plus first,
last and least-background frames. Full videos/JPEG bundles remain in the local
artifact directories; the compact archive explicitly does not claim to include
all full-resolution frames. Existing asset recovery uses the committed archive,
not missing Temp evidence.

Independent review is a separate parent-owned step. Implementation measurements
and self-review are not independent approval.

The shared cache-2 candidate was rejected after unique Article avatars were
correctly rebound: its 500-item continuous blanking regressed. The final production
change is only Article Avatar OneWay; the default cache remains 0.5. Archived
experimental host resources still say 2, with the run parameter applied before
mount. A fresh preparation reads the final checkout's 0.5 resource.
See ../scroll-continuity-results.md for unresolved visual failures. audit.py is a
receipt/identity audit, not a zero-blank acceptance. It explicitly preserves the
two pre-fix Article source-identity failures and requires the post-fix pair green.
