# Final-source runtime comparison replay

Parent source is pinned in `prepare.py`. Production source is never edited.
Read [the protocol and results](../runtime-final-baseline.md) before replay.

Run from this repository root using PowerShell and Python 3, with exclusive
authorized local desktop access and the existing 150% DPI. Prerequisites are
the locally installed exact two SDKs, winapp 0.7.1, cached NuGet/native assets
and the existing local kernel bundle. Do not install or change defaults.
The scripts refuse an existing artifact destination or run log.

1. `python -B -X utf8 docs/performance-acceptance/runtime-final-baseline/prepare.py`
   builds full-source net10 and RC11 fixtures serially and freezes each before
   starting the next. The narrowly scoped recovery switch records the original
   net10 post-freeze JSON decoding interruption; it is not a rebuild/retry mode.
2. `python -B -X utf8 docs/performance-acceptance/runtime-final-baseline/make-replay.py`
   derives the runner from the retained v3 implementation. Only new owned
   identities may be used. Never reuse an old layout or application data.
3. `python -B -X utf8 docs/performance-acceptance/runtime-final-baseline/sequence.py`
   runs source/package preflight then A/B/B/A once. Any failure stops the
   sequence. No retries, native repairs or additional sequence are authorized.
4. After all process/registration cleanup, run `analyze.py`, inspect every
   window-only PNG individually, then `archive.py` and `verify.py --live`.
   `chart.py` creates the self-contained descriptive chart.

`raw/` holds gzip snapshots of original bytes, including failed preparation
attempts, raw per-run counters, safe filtered WER evidence, module hashes,
commands, complete resolved assets, source/frozen ledgers and actual replay
scripts. `ledger.json` hashes the *decompressed original bytes*, avoiding Git
newline rewriting. Images are retained under `images/`. Large complete source,
build and immutable activation trees remain in ignored
`artifacts/runtime-final-baseline/`.

Portable verification: `python -B -X utf8 .../verify.py`. Live verification
additionally requires this checkout's retained artifacts and verifies every
deployed DLL both ways and loaded local-module hashes. No prior runtime run is
pooled with these measurements. Archive generation refuses overwriting history.

Interpretation: runtime CPU is kernel+user total process time divided by
monotonic elapsed wall time (1000 ms/s is one logical core). Private bytes and
working set are process proxies; handles/threads are counts. Min/max are
observed sample ranges, not confidence intervals. First/last halves expose
drift. Background snapshots cover surviving processes only. The sampling
helper's measured wall overhead is not its CPU consumption.
