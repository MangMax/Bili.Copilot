# Native result ledger

Baseline: `red-settled` + `red-extra`. Final: `green-verified` + `green-extra-navigation`. Fixed DPI 1.5, identical 120-step width trajectory and local 1600×900 content. Both 50 and 500 items were tested per family; table shows 500 items. Not a frame-rate or all-page acceptance claim.

| Template family | Realized before → after | Image measure / arrange before → after | Resize-call p95 ms before → after | UI heartbeat max ms before → after | Final scroll heartbeat max ms |
| --- | --- | --- | --- | --- | --- |
| Recommend | 43 → 10 | 3500 / 3322 → 1249 / 989 | 57.0 → 20.4 | 120.8 → 65.8 | 70.9 |
| Hot | 43 → 10 | 3545 / 3367 → 1227 / 978 | 61.3 → 23.3 | 120.6 → 64.7 | 112.3 |
| Partition | 43 → 10 | 3538 / 3362 → 1251 / 990 | 56.8 → 19.8 | 136.3 → 61.7 | 137.6 |
| Search | 43 → 10 | 3538 / 3362 → 1251 / 990 | 54.8 → 20.7 | 107.7 → 58.0 | 87.4 |
| Live | 49 → 10 | 3338 / 3669 → 1189 / 1030 | 60.4 → 21.0 | 650.0 → 162.1 | 87.8 |
| Article | 43 → 10 | 4046 / 4992 → 1938 / 1689 | 44.1 → 22.8 | 937.6 → 139.2 | 114.1 |
| User | 103 → 22 | 2904 / 2707 → 1183 / 919 | 130.4 → 27.1 | 1705.2 → 275.6 | 115.7 |
| Stack | 38 → 8 | 0 / 0 → 0 / 0 | 55.5 → 23.7 | 86.9 → 66.3 | 80.2 |
| Moment | 40 → 10 | 3325 / 3228 → 1245 / 996 | 49.4 → 22.6 | 107.8 → 69.4 | 158.3 |
| Season | 66 → 16 | 0 / 0 → 0 / 0 | 78.4 → 31.7 | 128.8 → 78.8 | 125.5 |
| Notify | 29 → 6 | 0 / 0 → 0 / 0 | 32.7 → 19.2 | 59.8 → 47.3 | 44.1 |

Image counters are actual Basic/Recommendation MeasureOverride and ArrangeOverride calls, **not all subtree layout calls**. Zero for fixed-size or text-only templates is not zero total layout cost. Basic DrawImage counts are draws, not decoder counts. Recommendation native-in-flight counters retain their original meaning.

All final functional assertions pass. Continuous forward/reverse scrolling has zero pending visible Basic/Blur images at the 100ms checkpoints in these local fixtures; this does not prove no inter-frame placeholders or slow-network behavior. Repeated draws during recycling still occur and are recorded in the raw rows.

The unchanged 200ms serious-stall line still fails User/50 and User/500. The verifier intentionally returns 2 after passing functional/evidence checks. The unresolved two-pane diagnostic is excluded and documented in README; it is not counted as passing.

| Final native stage | Assertions | PID | Post-close observation | Exact-path WER |
| --- | --- | --- | --- | --- |
| green-verified | 592 passed | 36612 | 32.017s, absent=True | empty |
| green-extra-navigation | 222 passed | 48452 | 32.011s, absent=True | empty |
