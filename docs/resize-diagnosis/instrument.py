"""Instrument a prepared integration harness; production files remain untouched."""
import pathlib, sys, subprocess, hashlib, json
r=pathlib.Path(__file__).resolve().parents[2]
h=pathlib.Path(sys.argv[1])
def edit(file, old, new):
    p=h/file; s=p.read_text(encoding='utf-8-sig'); assert old in s,(file,old); p.write_text(s.replace(old,new),encoding='utf-8')
if '--refresh' not in sys.argv:
    edit('App.xaml.cs','Add("Close",()=>window.Close());','Add("ResizeChecks",()=>_ = RunResizeChecks()); Add("HotResizeChecks",()=>_ = RunResizeChecks(true)); Add("Close",()=>window.Close());')
    # Avoid synchronous JSON/file writes and visual-tree traversal on every layout event.
    edit('Fixture.cs','File.AppendAllText(path,JsonSerializer.Serialize(new{utc=DateTimeOffset.UtcNow,ms=clock.Elapsed.TotalMilliseconds,kind,data})+"\\n");','if(kind=="unhandled") File.AppendAllText(path,JsonSerializer.Serialize(new{kind,data})+"\\n");')
    edit('App.xaml.cs','CompositionTarget.Rendering+=','/*CompositionTarget.Rendering+=')
    edit('App.xaml.cs','Trace.Write("launch",','*/Trace.Write("launch",')
    edit('App.xaml.cs','main.Loaded+=(_,_)=>Snapshot("main-loaded");main.Unloaded+=(_,_)=>Snapshot("main-unloaded");','')
    edit('App.xaml.cs','side.Loaded+=(_,_)=>Snapshot("side-loaded");side.Unloaded+=(_,_)=>Snapshot("side-unloaded");','')
    edit('Adapters.cs','"Assets","Logo.png"','"Assets","Cover.png"')
    edit('App.xaml.cs','service.Batch=60;service.Limit=1;','service.Batch=10;service.Limit=1;')
    edit('Harness.csproj','<Compile Include="$(SourceRoot)/src/Desktop/BiliCopilot.UI/Controls/RecommendationCoverImage.cs" Link="Production/RecommendationCoverImage.cs" />','')

s=(r/'src/Desktop/BiliCopilot.UI/Controls/RecommendationCoverImage.cs').read_text(encoding='utf-8-sig')
if '--baseline' in sys.argv:
    s=subprocess.check_output(['git','show','3f607587cdda285f0331068427ec8ad433f9b0e3:src/Desktop/BiliCopilot.UI/Controls/RecommendationCoverImage.cs'],cwd=r).decode('utf-8-sig')
if '--fixed' in sys.argv:
    s=s.replace('(Source, (int)Math.Ceiling(ActualWidth * scale), (int)Math.Ceiling(ActualHeight * scale))','(Source, 600, 338)')
s=s.replace('private async void Reload()\n    {','private async void Reload()\n    {\n        Acceptance5053.ResizeProbe.Reloads++;')
s=s.replace('using var stream = await OpenAsync', 'Acceptance5053.ResizeProbe.Opens++;\n            using var stream = await OpenAsync')
s=s.replace('var decoder = await BitmapDecoder.CreateAsync(random).AsTask();','Acceptance5053.ResizeProbe.Decoders++; nativeEntered=true; Acceptance5053.ResizeProbe.Native++; Acceptance5053.ResizeProbe.NativePeak=Math.Max(Acceptance5053.ResizeProbe.NativePeak,Acceptance5053.ResizeProbe.Native);\n            var decoder = await BitmapDecoder.CreateAsync(random).AsTask();')
s=s.replace('var pixels = await decoder.GetPixelDataAsync','Acceptance5053.ResizeProbe.Pixels++;\n            var pixels = await decoder.GetPixelDataAsync')
s=s.replace('var surface = new CanvasImageSource','Acceptance5053.ResizeProbe.Surfaces++;\n            var surface = new CanvasImageSource')
s=s.replace('var token = request.Token;', 'var token = request.Token; bool nativeEntered=false;\n        Acceptance5053.ResizeProbe.Active++; Acceptance5053.ResizeProbe.Peak=Math.Max(Acceptance5053.ResizeProbe.Peak,Acceptance5053.ResizeProbe.Active);')
s=s.replace('finally\n        {\n            if (ReferenceEquals(_request, request))','finally\n        {\n            Acceptance5053.ResizeProbe.Active--; if(nativeEntered) Acceptance5053.ResizeProbe.Native--;\n            if (ReferenceEquals(_request, request))')
s=s.replace('private static async Task<Stream> OpenAsync', 'protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size s) { Acceptance5053.ResizeProbe.Measures++; return base.MeasureOverride(s); }\n    protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size s) { Acceptance5053.ResizeProbe.Arranges++; return base.ArrangeOverride(s); }\n    private static async Task<Stream> OpenAsync')
(h/'RecommendationCoverImage.cs').write_text(s,encoding='utf-8')
(h/'ResizeChecks.cs').write_bytes((r/'docs/resize-diagnosis/ResizeChecks.cs').read_bytes())
(h/'probe-provenance.json').write_text(json.dumps({
    'mode':sys.argv[2:], 'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=r).decode().strip(),
    'instrumentedCoverSha256':hashlib.sha256((h/'RecommendationCoverImage.cs').read_bytes()).hexdigest(),
    'files':{str(p.relative_to(h)):hashlib.sha256(p.read_bytes()).hexdigest() for p in h.glob('*') if p.is_file() and p.suffix in ('.cs','.xaml','.csproj')}
},indent=2),encoding='utf-8')
