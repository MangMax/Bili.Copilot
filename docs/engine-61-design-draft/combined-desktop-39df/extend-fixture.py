from pathlib import Path
import json,subprocess
R=Path.cwd();O=R/'artifacts/combined-60cd';F=O/'full';D=R/'docs/engine-61-design-draft/combined-desktop-39df';p=F/'src/Desktop/BiliCopilot.UI/OfflineAcceptance.cs'
s=p.read_text(encoding='utf-8-sig');s=s.replace('    static int generation;','    static int generation;\n    static bool loadedModulesLogged;')
s=s.replace('        var rows=new List<object>();','''        if(!loadedModulesLogged && AppDomain.CurrentDomain.GetAssemblies().Count(a=>a.GetName().Name!.StartsWith("Danmaku."))==3) {
            loadedModulesLogged=true;
            Log(new{kind="loaded-modules",pid=Environment.ProcessId,modules=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name!.StartsWith("Danmaku.")).Select(a=>new{name=a.GetName().Name,path=a.Location,mvid=a.ManifestModule.ModuleVersionId,sha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location))),pdbSha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.ChangeExtension(a.Location,".pdb"))))}).ToArray()});
        }
        var rows=new List<object>();''')
p.write_text(s,encoding='utf8');(D/'OfflineAcceptance.cs').write_text(s,encoding='utf8')
cmd=json.loads((O/'build-command.json').read_text())['command']
with (O/'build2.log').open('w',encoding='utf8') as f:r=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT)
(O/'build2-command.json').write_text(json.dumps(dict(command=cmd,exit=r.returncode),indent=2));print('build2',r.returncode)
