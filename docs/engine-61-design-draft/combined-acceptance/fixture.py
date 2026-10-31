from pathlib import Path
import re,subprocess, json
R=Path.cwd();D=Path(__file__).resolve().parent;O=R/'artifacts/combined-60cd';F=O/'full';A=F/'src/Desktop/BiliCopilot.UI'
s=(D.parent/'legacy-reconstruct/OfflineAcceptance.cs').read_text(encoding='utf-8-sig')
s=re.sub(r'internal const string Root = @"[^"]+";',lambda _:f'internal const string Root = @"{F}";',s)
s=s.replace('SettingNames.DanmakuRenderer,DanmakuRendererType.Win2D','SettingNames.DanmakuRenderer,DanmakuRendererType.DirectX')
a=s.index('        var master=Field(panel,');b=s.index('\n    }',a)
s=s[:a]+'''        var master=Field(panel,"_danmakuMaster");
        var core=Field(panel,"_danmakuRenderer");
        var surface=core==null?null:Field(Field(core,"_owner")!,"surface");
        var native=master!=null?Field(master,"_render"):surface==null?null:Field(surface,"renderer");
        if(native==null){Log(new{kind="visual",panel=true,renderer=false});return;}
        var rows=new List<object>();
        var total=surface==null?TimeSpan.Zero:(TimeSpan)(Field(surface,"total")??TimeSpan.Zero);
        if(Field(native,"_renderLayerList") is Array layers)foreach(var layer in layers){
            if(Field(layer!,"RenderList") is System.Collections.IList list)lock(list)foreach(var item in list){
                var model=Field(item!,"Item");
                var first=(TimeSpan)(Field(item!,"FirstRenderTime")??TimeSpan.Zero);
                rows.Add(new{text=model==null?Field(item!,"Text"):Field(model,"Text")??model.GetType().GetProperty("Text")?.GetValue(model),x=Field(item!,"X"),y=Field(item!,"StartY"),width=Field(item!,"Width"),height=Field(item!,"Height"),age=core!=null?(total-first).TotalMilliseconds:Field(item!,"ReconstructedAgeMs"),first=first.TotalMilliseconds,drawable=Field(item!,"IsFirstRenderTimeSet")});
            }
        }
        object? Val(string n)=>Field(native,n)??native.GetType().GetProperty(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(native);
        Log(new{kind="visual",panel=true,renderer=true,engine=core!=null?"Core":"Legacy",master=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(core??master!),paused=Val("_isPaused"),stopped=Val("_isStopped"),time=master==null?vm.Danmaku.Position*1000:Field(master,"_lastTimeMs"),width=Val("CanvasWidth"),height=Val("CanvasHeight"),scale=Val("_scale"),speed=vm.Danmaku.DanmakuSpeed,extra=vm.Danmaku.ExtraSpeed,font=vm.Danmaku.DanmakuFontSize,rows});''' + s[b:]
s=s.replace('            Button("Seek20",', '''            Button("Core",()=>{vm!.Danmaku.Renderer=DanmakuRendererType.DirectX;return Task.CompletedTask;});
            Button("Legacy",()=>{vm!.Danmaku.Renderer=DanmakuRendererType.Win2D;return Task.CompletedTask;});
            Button("Seek19",()=>Seek(19));
            Button("Seek22",()=>Seek(22));
            Button("Seek23",()=>Seek(23));
            Button("Seek24",()=>Seek(24));
            Button("FastLatest",async()=>{await Task.WhenAll(Seek(21),Seek(19),Seek(20));});
            Button("Config",()=>{vm!.Danmaku.DanmakuSpeed=1;vm.Danmaku.DanmakuFontSize=1;vm.Danmaku.IsRollingEnabled=true;vm.Danmaku.IsTopEnabled=true;vm.Danmaku.IsBottomEnabled=true;vm.Danmaku.Redraw();return Task.CompletedTask;});
            Button("Blank",()=>{vm!.Danmaku.IsRollingEnabled=false;vm.Danmaku.IsTopEnabled=false;vm.Danmaku.IsBottomEnabled=false;vm.Danmaku.Redraw();return Task.CompletedTask;});
            Button("Seek20",''')
s=s.replace('control.Content=buttons;', 'control.Content=new ScrollViewer {Content=buttons};')
s=s.replace('new DanmakuInformation("prior","PRIOR19",5,19,0xffffff,25), new DanmakuInformation("future","FUTURE21",5,21,0xffffff,25)', 'new DanmakuInformation("roll","ROLL18",1,18,0xffffff,25), new DanmakuInformation("rev","REV18",6,18,0xffffff,25), new DanmakuInformation("prior","PRIOR19",5,19,0xffffff,25), new DanmakuInformation("bottom","BOTTOM19",4,19,0xffffff,25), new DanmakuInformation("future","FUTURE21",5,21,0xffffff,25)')
s=s.replace('            GlobalDependencies.Initialize();','''            Log(new{kind="modules",modules=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name!.StartsWith("Danmaku.")).Select(a=>new{name=a.GetName().Name,path=a.Location,mvid=a.ManifestModule.ModuleVersionId,sha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location)))}).ToArray()});
            GlobalDependencies.Initialize();''')
(A/'OfflineAcceptance.cs').write_text(s,encoding='utf8');(D/'OfflineAcceptance.cs').write_text(s,encoding='utf8')
cmd=['dotnet','build',str(A/'BiliCopilot.UI.csproj'),'-c','Release','-p:Platform=x64','-p:PublishAot=false','-p:AppxPackageSigningEnabled=false','-p:GenerateAppxPackageOnBuild=false','-p:NuGetAudit=false','-p:SolutionDir='+str(F/'src')+'/', '--configfile',str(F/'NuGet.Config')]
with (O/'build.log').open('w',encoding='utf8') as f:p=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT)
(O/'build-command.json').write_text(json.dumps(dict(command=cmd,exit=p.returncode),indent=2));print('build exit',p.returncode);p.check_returncode()
