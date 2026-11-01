"""Prepare only the owned native host. Requires the read-only offline assets documented in README."""
from pathlib import Path
R=Path.cwd();D=Path(__file__).resolve().parent;O=R/'artifacts/seek-fix';F=O/'full'
assert not F.exists(), 'Use a fresh workspace/output; do not overwrite a registered layout'
s=(R/'docs/seek-responsive/prepare.py').read_text(encoding='utf8')
s=s.replace("O=R/'artifacts/seek-responsive'", "O=R/'artifacts/seek-fix'")
s=s.replace('Bili.Seek870b','Bili.Seekd1fc').replace('bili-seek870b','bili-seekd1fc').replace('0.0.1-seek870b.20261101.5','0.0.1-seekd1fc.20261101.2')
s=s.replace("Path(__file__).resolve().parent/'candidate-feed'", "D/'candidate-feed'")
s=s.replace('Path(__file__).resolve().parent', "(R/'docs/seek-responsive')")
exec(compile(s,'original-prepare','exec'))
p=F/'src/Desktop/BiliCopilot.UI/OfflineAcceptance.cs';s=(D/'OfflineAcceptance.cs').read_text(encoding='utf8');old=s.split('internal const string Root = @"')[1].split('";')[0];p.write_text(s.replace(old,str(F)),encoding='utf8')
p=F/'src/Desktop/BiliCopilot.UI/ViewModels/Core/PlayerViewModel/PlayerViewModel.Playback.cs';p.write_text(p.read_text(encoding='utf8').replace('\n\n','\n'),encoding='utf8')
import shutil
for n in ['launch-retain.ps1','cleanup.ps1']:shutil.copyfile(D/n,O/n)
