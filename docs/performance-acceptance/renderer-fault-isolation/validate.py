"""Validate a completed owned replay; never turn a failed assertion into success."""
from pathlib import Path
import argparse, hashlib, json, re, struct
p=argparse.ArgumentParser();p.add_argument('--run',default='verified');args=p.parse_args()
root=Path(__file__).resolve().parents[3];run=root/'artifacts/home-aot'/args.run
read=lambda name:json.loads((run/name).read_text(encoding='utf-8-sig'))
home=read('controlled-results.json');assert len(home)==27 and all(x['status']=='PASS' for x in home)
lines=(run/'seek.tsv').read_text().splitlines();passed=[x.split('\t',2)[2].split(' ',1)[0] for x in lines if '\tPASS\t' in x]
assert len(passed)==32 and not any('\tFAIL\t' in x for x in lines)
required={'FaultRejectsFurtherSeek','FaultMediaProgress','FaultRendererRetired','FaultSubsequentSeek','FaultHidden','FaultDoesNotRetry','FaultMediaContinues','FaultUnloadAndClose','NewPlaybackRecovers','RecoveredSeek'}
assert required.issubset(passed)
assert lines[-1].endswith('DONE\tbounded AOT seek and terminal isolation')
exit=read('exit-observation.json');verdict=read('exit-verdict.json');cleanup=read('cleanup.json')
assert exit['handleRetained'] and exit['terminated'] and exit['exitCode']==0 and not exit['forced']
assert exit['queries'][-1]['startedSecondsAfterExit']>=31 and verdict['status']=='PASS_BOUNDED'
assert cleanup['processAbsent'] and cleanup['registrationAbsent']
assert read('yield.json') in [{'released':False},{'released':True}]
modules=read('all-modules.json');assert not any(re.search(r'(coreclr|clrjit|hostfxr|hostpolicy)\.dll$',s,re.I) for s in modules)
pe=read('native-inputs.json');assert all(x['clrRva']==0 for x in pe)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
exe=run/'layout/BiliCopilot.UI.exe';expected=next(x['sha256'].lower() for x in pe if x['path'].endswith('BiliCopilot.UI.exe'))
assert sha(exe)==expected
header=exe.read_bytes();pe_offset=struct.unpack_from('<I',header,0x3c)[0];machine=struct.unpack_from('<H',header,pe_offset+4)[0]
assert machine==0x8664
logs='\n'.join(p.read_text(encoding='utf-8-sig') for p in (run/'Logger').glob('*.txt'))
assert logs.count('Danmaku renderer failed and was retired.')==1
assert 'Controlled diagnostic terminal state' in logs
assert 'Failed to retire the faulted danmaku renderer.' not in logs
app_unhandled=logs.count('Unhandled exception occurred.')
terminal_phase=logs[logs.index('Danmaku renderer failed and was retired.'):]
assert terminal_phase.count('Unhandled exception occurred.')==0
# The inherited exit gate's managedUnhandled observes only controlled.jsonl.
# Preserve and expose the separate application log stream rather than calling it clean.

result={'homePassed':len(home),'seekAndFaultPassed':len(passed),'terminalLogCount':1,'applicationGlobalExceptionLogs':app_unhandled,'terminalPhaseGlobalExceptionLogs':0,'wholeHostDiagnosticsClean':'[ERR]' not in logs and '[FTL]' not in logs,'nativeExeSha256':expected,'nativeMachine':hex(machine),'pid':exit['pid'],'exitCode':exit['exitCode'],'wer':verdict,'observationSeconds':exit['queries'][-1]['startedSecondsAfterExit'],'cleanup':cleanup,'yield':read('yield.json')}
(run/'validation.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
