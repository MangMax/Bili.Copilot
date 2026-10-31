# Implementation self-review

Fixed point: `90e215f0d303c7ae6487f1e1cbf261f677b3d1ef`.
Spec: the user's expanded list-family scope and safety/verification requirements in this task.
Standards: root `AGENTS.md`, `src/.editorconfig`, the requested WinUI, debugging, testing and verification skills. The explicit no-subagent/no-PR instructions override delegation and issue-closing suggestions in implement-spec/code-review. This is a self-review, not the independent review requested at handoff.

## Standards

Production changes are confined to one implicit style, one named-inner-repeater attached property, a shared lifecycle base for the existing Basic/Blur controls, and the notification card's command binding mode. The latter fixes a native-proven recycled-item command mismatch; it adds no per-page scheduling logic. No resize handler, visual-tree scan, framework migration, dependency upgrade, data-item cap or incremental-refresh removal is introduced. The attached policy is instance-owned and stays subscribed across remount; it does not retain controls in a static collection. Existing explicit image sizes, URI/cache selection and effects remain at their call sites.

The lifecycle guard prevents duplicate base subscriptions on first load when both the package handler and the local handler fire. Unload still calls the package cleanup and cancels its request. Remount reconnects the surviving template brush and creates the surface discarded by the package, rather than retaining an offscreen GPU surface. Basic and Blur use the same helper for their existing aspect-ratio surface replacement. The native decoder implementation and its token-free await/stream lifetime are untouched, including the recommendation WIC fix.

The helper intentionally depends on the pinned package's named `Repeater` and image lifecycle behavior. A package upgrade must recheck these assumptions. It is not a new general-purpose image framework. Forced device-loss recovery and multiple independent dispatchers are not newly verified.

## Spec

The shared policy reaches all AppRepeater families in the inventory, including live, article, dynamic, user/account, history, favorites, player and ordinary message lists. Direct platform lists were inventoried and retain their existing cache policy. Native tests use actual typed production templates and controls for eleven representative families, rather than extending video results to non-video templates. The table distinguishes tested templates from same-policy coverage.

Remaining acceptance gaps are explicit: user-avatar resize stalls remain over the original 200ms line; the synthetic two-pane shared-layout diagnostic still raises MeasureOverride E_FAIL even with independent layouts and has no established cause; real-network saves, animation playback, authenticated navigation/context menus, and every distinct presenter are not fully exercised. These prevent a claim of full all-page performance acceptance. No threshold was relaxed, and failed runs remain in the evidence.

Validation results and exact commands are recorded in README and the evidence archive. Parent integration and the requested independent review are separate from this self-review.
