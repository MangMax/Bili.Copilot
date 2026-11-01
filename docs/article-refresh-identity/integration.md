# Article refresh integration

Integrated fixed candidate `1b7b479f` and review `9ecf9a81` with merges `1b417f05` and `1b3043b9`. Avatar OneWay was already present; no duplicate change.

The review boundary reproduced in the parent: refresh queued, an intervening page returned cursor 42, then refresh requested page 42. Moved cursor/cache reset into the same dispatcher callback that clears the list and starts the refreshed request. Entry still invalidates pending work.

Fresh Release UI tests: **580 passed, 0 failed, 0 skipped**. Includes `RefreshQueuedCallbackMustRestartAtZeroAfterInterleavedLoad`, existing pending-page and latest-reader tests. No production WinUI build, desktop or AOT run in this integration.

The first post-fix run was 579/1 because the review assertion required the intervening request itself to use zero. Moving reset into the callback legitimately leaves that request on old cursor 5. The regression now asserts three requests and that the actual refresh (third request) starts at zero; old implementation still fails with 42. All three receipts are retained in evidence.

Kernel article parser NRE remains a separate active fix. Cache ownership and reader auxiliary-field-on-failure limitations in the independent review remain. No package deployment, user-instance operations, dependency/gitlink changes or remote operations.
