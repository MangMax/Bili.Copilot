# Performance #45: historical integration evidence for #47

Recorded 2026-10-30 during #48 integration. This is an independent reading of existing local artifacts, not a rerun of the old revision.

## Provenance and results

The #47 merger reported verification at `175b2c07a67a8654d1046112fde0ae095db45913`. Git confirms this is the #47 merge and it remains an ancestor of the #48 integration. The logs identify this integration checkout but do not embed a Git SHA; attribution to that revision comes from the merger handoff.

- Read `%TEMP%/integration-47-tests.log` in full: **Passed: 305, Failed: 0, Skipped: 0**. Independently parsed `tests/BiliCopilot.UI.Tests/TestResults/integration-47.trx`: total/executed/passed 305; failed/error/notExecuted 0.
- Read `%TEMP%/integration-47-build.log` in full: Debug x64 output paths, successful build, **0 warnings, 0 errors**, elapsed **00:04:50.20**.
- Read `%TEMP%/integration-47-runtime-gate.log` in full: only `MSBuild 18.9.15+5a2162bea` banner (49 bytes), no errors printed. The merger reported runtime gate PASS; this minimal log does not independently preserve the command or exit code. Do not treat the banner alone as proof of successful execution. A fresh explicit gate is recorded separately in integration-48.md.

SHA-256 of the historical logs, in the order above:

```text
94597694B0EB30CAD5087BFCC6939D85351551B85BA115642A372D084DDF0FC5
C078E49A10A9DA063739782A7FFBA562C1FFFF37C7AF52DEA19D98284B507E81
E2EE1D6722AD05DD5177997D7F608F81475DD3CEA5CF5E9BEB84AC09C75F7053
```

## Acceptance boundary

No real UI acceptance or GC sampling was performed by this integration pass. Historical compile success resolves the earlier implementation checkout build limitation; it does not establish Markdown clear/reopen/reattach correctness, image lifetime, GC pause reductions or retained-object behavior. The detailed pending scenarios in issue-47.md remain pending. No desktop input, deployment, issue closure or push was performed.
