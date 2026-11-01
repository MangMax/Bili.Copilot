"""Restore a fresh disposable bundle checkout and replay the same assertions red/green."""
from pathlib import Path
import subprocess,shutil,sys
D=Path(__file__).resolve().parent;O=Path(sys.argv[1]).resolve();assert not O.exists(), 'Use a new output directory'
O.mkdir(parents=True)
subprocess.run(['git','clone',str(D/'legacy.bundle'),str(O/'legacy')],check=True)
shutil.copytree(D/'probe',O/'probe',ignore=shutil.ignore_patterns('bin','obj'))
original=O/'original-master.cs';original.write_bytes(subprocess.check_output(['git','-C',str(O/'legacy'),'show','221bbb01917b71f2d305506572c0526138950e28:src/Danmaku.Legacy/DanmakuFrostMaster.cs']))
for mode,extra,expected in [('red',['-p:MasterSource='+str(original)],1),('green',[],0)]:
 r=subprocess.run(['dotnet','run','--project',str(O/'probe/Probe.csproj'),*extra],stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
 (O/(mode+'.txt')).write_bytes(r.stdout)
 assert r.returncode==expected,(mode,r.returncode,r.stdout.decode(errors='replace'))
 if mode=='red':assert b'FAIL ListReplacementLatestSeek: expected NEW; actual OLD' in r.stdout and b'FAIL FailureThenLatestSeek_Stop' in r.stdout
 else:assert r.stdout.count(b'PASS ')==13 and b'Failed: 0' in r.stdout
 print(mode+': verified exit '+str(r.returncode))
