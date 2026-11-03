# Agent guide

## Agent skills

### Issue tracker

Issues and specs live as GitHub issues in `MangMax/Bili.Copilot`; use the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default five triage labels (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `GLOSSARY.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.

### WinUI analyzer

Opt-in observation build for `Microsoft.Windows.SDK.BuildTools.WinUIAnalyzer`: baseline (254 warnings), SARIF output, and the order for tightening it into a blocking check. See `docs/agents/winui-analyzer.md`.
