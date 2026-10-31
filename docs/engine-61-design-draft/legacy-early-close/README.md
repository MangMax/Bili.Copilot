# Legacy early-close candidate

Status: isolated candidate, native acceptance pending. Formal dependencies unchanged.

The independent review at `C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-774b56c5/artifacts/ready-review-774b/REVIEW.md` reproduced both source arms: Close returned before first Loaded, then Canvas.Loaded and Win2D AV; CreateResources never entered. Historical failures remain failures.

This candidate overlays `legacy-ready/source`. Close retains the container until its owned Canvas has been removed from the parent Panel, then performs existing internal cleanup. It also clears initial readiness on Close. It does not address device-reset readiness or concurrent list replacement.

Rebuild: `python docs/engine-61-design-draft/legacy-early-close/build.py`. Pinned upstream archive hash is verified before restoration. Parent Release x64 build exited 0, zero warnings/errors; raw log under `artifacts/legacy-early-close/build.log`. No UI test has been performed for this candidate.

WinUI review: only the owned child is removed, siblings are preserved; no public API, XAML, dependency or application changes. UI-thread lifecycle requirements remain. Dedicated analyzer not run. Removal before first Loaded must be tested across subsequent dispatcher turns, not inferred safe merely from Close returning.

Next validation: reproduce original early-close sequence with actual parent Grid; verify normal loaded close, sibling preservation, late Loaded behavior and exact process exit. If successful, continue the already pending full application three remount rounds with the isolated package. Do not promote formal dependency or declare #49 complete.
