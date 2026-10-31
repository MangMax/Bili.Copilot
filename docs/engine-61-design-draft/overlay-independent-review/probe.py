import pathlib, subprocess, sys
root=pathlib.Path.cwd(); out=root/'artifacts/overlay-review/adapter'/sys.argv[1];out.mkdir(parents=True,exist_ok=True)
s=(root/'src/Desktop/BiliCopilot.UI/Controls/Core/OverlayTransportControls.xaml.cs').read_text(encoding='utf-8-sig')
def method(sig):
 start=s.index(sig); b=s.index('{',start)
 arrow=s.find('=>',start,b)
 if arrow>=0:return s[start:s.index(';',arrow)+1]
 i=b+1;d=1
 while d:
  d+=(s[i]=='{')-(s[i]=='}');i+=1
 return s[start:i]
bodies=[method(x) for x in ['public OverlayTransportControls()', 'protected override void OnControlLoaded()', 'protected override void OnControlUnloaded()', 'protected override void OnViewModelChanged(']]
for sig in ['private void OnOverlayLoaded(', 'private void OnOverlayUnloaded(']:
 if sig in s:bodies.append(method(sig))
fields=s[s.index('    private readonly List<double>'):s.index('    public OverlayTransportControls()')]
prefix='''using System; using System.Collections.Generic;
class RoutedEventArgs:EventArgs {}
class DispatcherTimer {public TimeSpan Interval{get;set;} public event EventHandler<object>? Tick; public int Count=>Tick?.GetInvocationList().Length??0; public bool IsEnabled; public void Start(){IsEnabled=true;} public void Stop(){IsEnabled=false;} }
class PlayerViewModel {public event EventHandler<double>? ProgressChanged; public int Count=>ProgressChanged?.GetInvocationList().Length??0; public object Subtitles=new();}
class Repeater {public object? ItemsSource;}
class PlayerControlBase {public PlayerViewModel ViewModel=new(); public event EventHandler<RoutedEventArgs>? Loaded; public event EventHandler<RoutedEventArgs>? Unloaded; bool wired=true; protected virtual void OnControlLoaded(){} protected virtual void OnControlUnloaded(){} protected virtual void OnViewModelChanged(PlayerViewModel? a,PlayerViewModel? b){} public void Load(){if(wired)OnControlLoaded();Loaded?.Invoke(this,new());} public void Unload(){if(wired){OnControlUnloaded();wired=false;}Unloaded?.Invoke(this,new());}}
class OverlayTransportControls:PlayerControlBase {
 void InitializeComponent(){} void BalanceLeftRightWidth(){} Repeater SubtitleRepeater=new(); void OnSpeedChangeTick(object? s,object e){} void OnProgressChangeTick(object? s,object e){} void OnViewModelProgressChanged(object? s,double e){}
'''
tests='''public static int Main(){var c=new OverlayTransportControls();int fails=0;void Check(bool ok,string name){Console.WriteLine($"{(ok?"PASS":"FAIL")} {name}");if(!ok)fails++;}for(int round=0;round<3;round++){c.Load();c.Load();Check(c._speedChangeTimer.Count==1&&c._progressChangeTimer.Count==1&&c.ViewModel.Count==1,$"single subscription {round}");var old=c.ViewModel;var next=new PlayerViewModel();c._speedChangeList.Add(2);c._speedChangeTimer.Start();c.ViewModel=next;c.OnViewModelChanged(old,next);Check(old.Count==0&&next.Count==1&&c._speedChangeList.Count==0&&!c._speedChangeTimer.IsEnabled,$"active VM change {round}");c._speedChangeList.Add(2);c._progressChangeList.Add(42);c._speedChangeTimer.Start();c._progressChangeTimer.Start();c.Unload();c.Unload();Check(c._speedChangeTimer.Count==0&&c._progressChangeTimer.Count==0&&next.Count==0&&c._speedChangeList.Count==0&&c._progressChangeList.Count==0&&!c._speedChangeTimer.IsEnabled&&!c._progressChangeTimer.IsEnabled&&c.SubtitleRepeater.ItemsSource==null,$"unload clears all {round}");old=c.ViewModel;next=new PlayerViewModel();c.ViewModel=next;c.OnViewModelChanged(old,next);Check(old.Count==0&&next.Count==0,$"inactive VM change {round}");c.Load();Check(next.Count==1&&ReferenceEquals(c.SubtitleRepeater.ItemsSource,next.Subtitles),$"remount current subtitles {round}");c.Unload();}Console.WriteLine($"failures={fails}");return fails==0?0:1;} }
'''

(out/'Program.cs').write_text(prefix+fields+'\n'.join(bodies)+tests)
(out/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>')
r=subprocess.run(['dotnet','run','--project',str(out/'Probe.csproj'),'-c','Release'],capture_output=True,text=True,encoding='utf-8',errors='replace');(out/'run.log').write_text(r.stdout+r.stderr,encoding='utf-8');print(r.stdout+r.stderr);sys.exit(r.returncode)
