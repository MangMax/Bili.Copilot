"""Commit only allowlisted metadata, stage/stack excerpts and hashes, never dumps/traces."""
import collections
import gzip
import hashlib
import json
import pathlib
import subprocess
from exit_gate import event, verdict

base = pathlib.Path('artifacts/exit-reproduction')
run = base / 'original1'
out = pathlib.Path(__file__).parent
def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p):
    with p.open('rb') as file:
        return hashlib.file_digest(file, 'sha256').hexdigest()
observation = read(run / 'exit-observation.json')
events = [event(x) for x in read(run / 'event-xml.json')]
fields = {'ProcessId', 'ProcessCreationTime', 'AppPath', 'IntegratorReportId', 'PackageFullName',
          'ExceptionCode', 'ModuleName', 'ModuleVersion', 'FaultingOffset', 'ReportId', 'P1', 'P5', 'P8', 'P9'}
events = [dict(e, data={k: v for k, v in e['data'].items() if k in fields}) for e in events]
trace = [json.loads(line) for line in (run / 'exit-trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
start = next(i for i, row in enumerate(trace) if row['stage'] == 'explicit-exit')
dump = read(run / 'parsed-dump.json')
modules = read(run / 'modules.json')
module_names = {'coreclr.dll', 'System.Private.CoreLib.dll', 'BiliCopilot.UI.dll', 'CoreMessagingXP.dll', 'combase.dll', 'Microsoft.UI.Xaml.dll'}
old = gzip.decompress(subprocess.check_output(['git', 'show', 'e55180:docs/performance-acceptance/net11-primary/evidence/final-rc/accepted.json.gz']))
evidence = dict(
    status='HOLD', production='f6ed3ec063df8701bb57115abb40c4f7bcffc386',
    kernel='0ae5e4e000a41a07f6696746706df11c06e8835b', originalAttempts=1, controllerComparisons=0,
    historicalAcceptedSha256=hashlib.sha256(old).hexdigest(),
    observation=observation, events=events, verdict=verdict(observation, events),
    exitStages=trace[start:], traceStageCounts=dict(collections.Counter(x['stage'] for x in trace)),
    dump={k: v for k, v in dump.items() if k not in ('modules', 'streams')},
    moduleIdentities=[m for m in dump['modules'] if m['name'] in module_names],
    loadedModules=[m for m in modules if m['name'] in module_names],
    cleanup=read(run / 'cleanup.json'), finalCleanup=read(base / 'final-cleanup.json'),
    collectorStopped=read(run / 'collector-stopped.json'),
    yields={k: read(run / p) for k, p in [('player', 'player/yield.json'), ('main', 'main/yield.json'), ('outer', 'yield.json')]},
    packageComparison=read(base / 'package-comparison.json'),
    hwnd=read(run / 'main/main-ready.json'),
    artifacts=[dict(path=str(p), bytes=p.stat().st_size, sha256=digest(p)) for p in
               [run / 'crash-full.dmp', run / 'exit-trace.jsonl', base / 'build.log', base / 'build-final.log',
                base / 'fixture/frozen-all-files.json', base / 'fixture-input-files.json',
                base / 'source-comparison.json', base / 'replay.ps1',
                base / 'symbols/CoreMessagingXP.pdb']])
(out / 'evidence.json').write_text(json.dumps(evidence, indent=2), encoding='utf-8')
for name, p in [('frozen-files.json', base / 'fixture/frozen-all-files.json'),
                ('source-comparison.json', base / 'source-comparison.json')]:
    (out / (name + '.gz')).write_bytes(gzip.compress(p.read_bytes(), mtime=0))
print(json.dumps(evidence['verdict']))
