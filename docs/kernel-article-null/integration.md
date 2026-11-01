# Article adapter integration

Original adapter candidate `41891ee` was reviewed in `41df6c7c`. Parent reproduced all three whitespace-only text counterexamples: 3 failed. The follow-up skips null/empty/whitespace words before generating HTML. Valid text and image templates are unchanged. Fresh real-adapter suite: **16 passed / 0 failed / 0 skipped**, including the review's mixed-node/encoding probe. No HTTP, desktop or AOT execution.

Kernel final: `62b376ec9dba705b4280f5d4ac61dd4589551034`, based on `41891ee` and `0ae5e4e`. The final bundle includes both article commits; it requires `0ae5e4e` from the already committed dependency-upgrade/kernel.bundle. Bundle SHA256: `C2D5C33C1D91D75D85CC2613BCA0F8F9D3FDCD8E99630E26B5B13A2E46225898`; git bundle verify passed. Reference: refs/heads/integration/article-null-final. No remote publication; remote-only submodule checkout remains unavailable until published or restored from these bundles.

The adapter test project is not currently run by CI; local execution is recorded here. Existing root workflow/SDK and submodule restoration need a separate focused CI update. This integration does not claim CI coverage or infer that all seven historical NREs share a particular field.

Evidence: integration-empty-red.trx and integration-green.trx. Optional stats remain nullable; missing required payload and empty/unsupported body throw KernelException. Unknown paragraph support is not expanded. User AOT package 2.2611.1.3 remains on af46c3ea and does not contain this change.

Fresh host Release UI suite after kernel update: 580 passed / 0 failed / 0 skipped. No production WinUI app build was run in this step.
