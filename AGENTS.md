# Agent guide

## Agent skills

### Issue tracker

Issues and specs live as GitHub issues in `MangMax/Bili.Copilot`; use the `gh` CLI. See `docs/agents/issue-tracker.md`.

Task-specific investigation notes, acceptance results, and outstanding work belong in the relevant issue, not in `docs/`. Keep temporary scripts, logs, screenshots, and build packages outside the repository or in ignored local output directories; do not commit them.

### Triage labels

Default five triage labels (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `GLOSSARY.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.

### WinUI analyzer

Blocking WinUI analyzer (`Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer`) in the default build: baseline 0 warnings (only note-level rules left), SARIF output via the observation switch, false-positive policy, and the rollback path. See `docs/agents/winui-analyzer.md`.
