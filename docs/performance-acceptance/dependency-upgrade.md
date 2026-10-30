# Dependency upgrade / .NET 11 experiment

Work in progress. Base: `eff8d24c87620cf2b6025a11b8f8ddda27b1a116`; kernel `98d0b9d0f0c81bd3904b1425ef3b90f637af37d7`. No remote mutations or changes to other checkouts.

Scope L, high difficulty (native lifetimes, generator compatibility, controlled measurements). Authorized review/rollback boundaries:

0. Inventory official metadata and reproduce baseline; retain historical fixtures unchanged.
1. Test tooling and analyzers: fresh suite and production build, record diagnostics.
2. Managed dependencies, including kernel: compile/regression gates and recoverable bundle.
3. WinUI, graphics, tray dependencies: compile and owned native replay.
4. Isolated .NET 11 RC SDK/TFM candidate: verify official archive checksum, preserve stable branch if blocked.
5. Matched performance/native runs, final gates and evidence review.

Each stage is a local reviewable commit; finer splits and blocked latest versions will be recorded here. No blanket warning suppression or analyzer disabling is permitted. Existing warnings/suppressions are baseline, not new acceptance.

Historical 183 samples are not new A/B evidence. New runs must keep SettingsPage, notifications disabled, 150% scale, Player suite before Main, 5s warmup, 2s phase settling, visible/hidden/restored 61 samples each and 20 hide/restore loops. Pure list workload: 100/1000/5000, prepend two/undo, 3 warmups and 10 measured repetitions; no build or concurrent benchmark during UI sampling. No authenticated-home or general performance claims. AOT missing MSVC, hardware device loss/physical DPI, #49 pause FAIL and #56 limits remain explicit.

Only fresh owned identities may run; protected Caa6a7be, PID9608, Dev and historical layouts/data are untouched. No credentials or real account mutations. Raw logs reside in ignored `artifacts/dependency-upgrade`.

Initial checkout contained only `.git` in kernel with 474 staged deletions at f02c33f. Saved initial deletion patch in ignored artifacts, verified committed kernel.bundle and restored exact authorized baseline 98d0b9d. Main and nested status then clean. No changes in parent checkout.
