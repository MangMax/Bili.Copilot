"""Use the real shared style in the existing command-driven recommendation acceptance host."""
from pathlib import Path
import subprocess, shutil, json, hashlib
r=Path(__file__).resolve().parents[2]
h=r/'artifacts/resize-diagnosis/host'
subprocess.run(['python',str(r/'docs/resize-diagnosis/instrument.py'),str(h),'--refresh'],check=True)
p=h/'Harness.csproj';s=p.read_text(encoding='utf-8-sig')
for name in ('ReloadableCoverImage','RepeaterCachePolicy'):
    if f'/{name}.cs' not in s:
        s=s.replace('</Project>',f'<ItemGroup><Compile Include="$(SourceRoot)/src/Desktop/BiliCopilot.UI/Controls/{name}.cs" Link="Production/{name}.cs" /></ItemGroup></Project>')
p.write_text(s,encoding='utf-8')
p=h/'ResizeChecks.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('var expected=CoverDecodeSize.Calculate(1600,900,cover.ActualWidth,cover.ActualHeight,cover.XamlRoot.RasterizationScale);', '''// Reload first quantizes the requested physical viewport, then preserves source aspect ratio.
        var expected=CoverDecodeSize.Calculate(1600,900,Math.Ceiling(cover.ActualWidth*cover.XamlRoot.RasterizationScale),Math.Ceiling(cover.ActualHeight*cover.XamlRoot.RasterizationScale),1);''')
s=s.replace('Require(surface.SizeInPixels.Width==expected.Item1', '''File.WriteAllText(Path.Combine(output,"resize-pixel-diagnostic.json"),JsonSerializer.Serialize(new {
            cover.Source,cover.ActualWidth,cover.ActualHeight,dpi=cover.XamlRoot.RasterizationScale,
            surfaceWidth=surface.SizeInPixels.Width,surfaceHeight=surface.SizeInPixels.Height,
            expectedWidth=expected.Item1,expectedHeight=expected.Item2,
            requestWidth=Math.Ceiling(cover.ActualWidth*cover.XamlRoot.RasterizationScale),
            requestHeight=Math.Ceiling(cover.ActualHeight*cover.XamlRoot.RasterizationScale)}));
        Require(surface.SizeInPixels.Width==expected.Item1''')
s=s.replace('var probe=new RecommendationCoverImage', '''Require(surface.SizeInPixels.Width>=Math.Ceiling(cover.ActualWidth*cover.XamlRoot.RasterizationScale)
            && surface.SizeInPixels.Height>=Math.Ceiling(cover.ActualHeight*cover.XamlRoot.RasterizationScale)
            && surface.SizeInPixels.Width<=1600 && surface.SizeInPixels.Height<=900,"PhysicalCoverageWithoutUpsampling");
        var probe=new RecommendationCoverImage''')
p.write_text(s,encoding='utf-8')
shutil.copy2(r/'src/Desktop/BiliCopilot.UI/Styles/Repeater.xaml',h/'Repeater.xaml')
p=h/'App.xaml';s=p.read_text(encoding='utf-8-sig')
if 'Source="Repeater.xaml"' not in s:
    s=s.replace('</ResourceDictionary.MergedDictionaries>','<ResourceDictionary Source="Repeater.xaml" /></ResourceDictionary.MergedDictionaries>')
p.write_text(s,encoding='utf-8')
files={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in h.iterdir() if p.is_file() and p.suffix in ('.cs','.xaml','.csproj')}
(h/'probe-provenance.json').write_text(json.dumps({'mode':'shared-style-and-current-image-lifecycle','files':files},indent=2),encoding='utf-8')
