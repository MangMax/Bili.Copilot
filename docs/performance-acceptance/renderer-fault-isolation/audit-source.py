from pathlib import Path
import hashlib,io,json,subprocess,zipfile
root=Path.cwd(); source=root/'artifacts/home-aot/source'
archive=zipfile.ZipFile(io.BytesIO(subprocess.check_output(['git','archive','--format=zip','ad4d17d3','src','packages','global.json','nuget.config'])))
sha=lambda b:hashlib.sha256(b).hexdigest()
changes=[]
for info in archive.infolist():
 if info.is_dir():continue
 p=source/info.filename
 if not p.is_file():raise RuntimeError('Missing '+info.filename)
 if sha(p.read_bytes())!=sha(archive.read(info)):
  changes.append({'path':info.filename,'parentArchive':sha(archive.read(info)),'final':sha(p.read_bytes())})
assert [x['path'] for x in changes]==['src/Desktop/BiliCopilot.UI/Controls/Danmaku/VideoDanmakuPanel.xaml.cs'],[x['path'] for x in changes]
panel=root/'src/Desktop/BiliCopilot.UI/Controls/Danmaku/VideoDanmakuPanel.xaml.cs'
fixture=root/'artifacts/home-aot/fixture/Controls/Danmaku/VideoDanmakuPanel.xaml.cs'
assert sha(panel.read_bytes())==sha(fixture.read_bytes())
(root/'artifacts/fault-isolation/final-source-audit.json').write_text(json.dumps({'reference':'git archive ad4d17d3 (repository export EOL rules)','checkedFiles':sum(not i.is_dir() for i in archive.infolist()),'changesFromParent':changes,'fixturePanel':sha(fixture.read_bytes()),'priorAuditCorrection':'git show uses LF blobs whereas git archive applies CRLF export rules; raw blob comparison was rejected and replaced by exact archive comparison, no product source changed'},indent=2))
print(changes)
