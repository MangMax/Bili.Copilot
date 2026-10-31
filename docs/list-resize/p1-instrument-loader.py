"""Diagnostic-only source build of the pinned public loader, not a production replacement.

Requires the source zip at the nuspec commit, extracted under artifacts/list-p1.
Namespace/type aliasing keeps the shipping NuGet untouched. The template is copied
from the same commit; do not use these timings as a binary-identical NuGet run.
"""
from pathlib import Path
import hashlib, json
r = Path(__file__).resolve().parents[2]
h = r/'artifacts/list-p1/host'
d = r/'artifacts/list-p1/dependency-source/winui-kernel-3f1086f6cfa8c1e0c3a64b930a8577dc5496c616/src/WinUIKernel.Share/Base/ImageExBase'
files = list(d.glob('*.cs'))
manifest = []
for p in files:
    s = p.read_text(encoding='utf-8-sig')
    manifest.append(dict(path=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
    s = 'using Richasy.WinUIKernel.Share;\nusing Richasy.WinUIKernel.Share.Base;\n'+s.replace('namespace Richasy.WinUIKernel.Share.Base;', 'namespace Acceptance5053.Diagnostic;')
    s = s.replace('WinUIKernelShareExtensions.Logger', 'DiagnosticLogger.Instance')
    s = s.replace('protected ImageExBase() => DefaultStyleKey = typeof(ImageExBase);', 'protected ImageExBase() { DefaultStyleKey = typeof(ImageExBase); Style = (Style)Application.Current.Resources["P1ImageStyle"]; }')
    if p.name == 'ImageExBase.Methods.cs':
        s = s.replace('return await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), stream).AsTask();', 'using var decode = Acceptance5053.P1Probe.Span("decode");\n            var operation = CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), stream);\n            Acceptance5053.P1Probe.Mark("decode-submitted");\n            return await operation.AsTask();')
        s = s.replace('private void CreateCanvasImageSource(CanvasDevice device)\n    {', 'private void CreateCanvasImageSource(CanvasDevice device)\n    {\n        using var surface = Acceptance5053.P1Probe.Span("surface");')
    (h/('Diagnostic-'+p.name)).write_text(s,encoding='utf-8')
for p in h.glob('*.cs'):
    if p.name.startswith('Diagnostic-'):
        continue
    s = p.read_text(encoding='utf-8-sig')
    if 'ImageExBase' in s:
        p.write_text('using ImageExBase = Acceptance5053.Diagnostic.ImageExBase;\n'+s,encoding='utf-8')
p = h/'ReloadableCoverImage.cs'
s = p.read_text(encoding='utf-8-sig').replace('=> new(CanvasDevice.GetSharedDevice(), (float)DecodeWidth, (float)DecodeHeight, 96, alphaMode);', '{ using var surface = Acceptance5053.P1Probe.Span("surface-remount"); return new(CanvasDevice.GetSharedDevice(), (float)DecodeWidth, (float)DecodeHeight, 96, alphaMode); }')
p.write_text(s,encoding='utf-8')
s = (d/'ImageExBase.xaml').read_text(encoding='utf-8-sig').replace('xmlns:local="using:Richasy.WinUIKernel.Share.Base"','xmlns:package="using:Richasy.WinUIKernel.Share.Base" xmlns:local="using:Acceptance5053.Diagnostic"').replace('local:Shimmer','package:Shimmer')
s = s.replace('DefaultImageExBaseStyle', 'P1ImageStyle')
(h/'DiagnosticImage.xaml').write_text(s,encoding='utf-8')
p = h/'App.xaml'; s = p.read_text(encoding='utf-8-sig').replace('</ResourceDictionary.MergedDictionaries>','<ResourceDictionary Source="DiagnosticImage.xaml" /></ResourceDictionary.MergedDictionaries>'); p.write_text(s,encoding='utf-8')
(h/'diagnostic-provenance.json').write_text(json.dumps(manifest,indent=2))
(h/'Diagnostic-Logger.cs').write_text('''using Microsoft.Extensions.Logging;
namespace Acceptance5053.Diagnostic;
public sealed class DiagnosticLogger : ILogger {
 public static readonly ILogger Instance = new DiagnosticLogger();
 public IDisposable BeginScope<TState>(TState state) where TState:notnull => null;
 public bool IsEnabled(LogLevel level)=>true;
 public void Log<TState>(LogLevel level,EventId id,TState state,Exception error,Func<TState,Exception,string> formatter)
   => Acceptance5053.Trace.Write("loader-error",formatter(state,error));
}
''')
p = h/'P1Probe.cs'; s = p.read_text().replace('static readonly Stopwatch Clock', 'static int next; static readonly Stopwatch Clock').replace('Mark(kind+"-start"); return new End(kind);', 'var id=++next; Mark(kind+"-start",id); return new End(kind,id);').replace('End(string kind)', 'End(string kind,int id)').replace('Mark(kind+"-end");', 'Mark(kind+"-end",id);'); p.write_text(s)
p = h/'Matrix.cs'; s = p.read_text().replace('beats.Add(now-last);last=now;', 'beats.Add(now-last);last=now;P1Probe.Mark("heartbeat");'); p.write_text(s)
p = h/'Fixture.cs'; s = p.read_text().replace('if(kind=="unhandled")', 'if(kind is "unhandled" or "loader-error")'); p.write_text(s)
