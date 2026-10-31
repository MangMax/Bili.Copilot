"""Use this worktree's real shared style in the command-driven recommendation probe."""
from pathlib import Path
r=Path(__file__).resolve().parents[2];h=r/'artifacts/list-p1/host'
p=h/'ResizeChecks.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('foreach(var repeater in Descendants(main).OfType<ItemsRepeater>()) repeater.VerticalCacheLength=2;', '')
s=s.replace('var cards=Descendants(main).OfType<VideoCardControl>().ToArray();', 'var caches=Descendants(main).OfType<ItemsRepeater>().Select(x=>x.VerticalCacheLength).ToArray();\n                if(caches.Length==0 || caches.Any(x=>x!=0.5))throw new Exception("ProductionSharedCacheNotApplied");\n                var cards=Descendants(main).OfType<VideoCardControl>().ToArray();')
s=s.replace('var row=new {hot,target,', 'var row=new {hot,target,caches,')
s=s.replace('var expected=CoverDecodeSize.Calculate(1600,900,cover.ActualWidth,cover.ActualHeight,cover.XamlRoot.RasterizationScale);','var expected=CoverDecodeSize.Calculate(1600,900,Math.Ceiling(cover.ActualWidth*cover.XamlRoot.RasterizationScale),Math.Ceiling(cover.ActualHeight*cover.XamlRoot.RasterizationScale),1);')
s=s.replace('var probe=new RecommendationCoverImage', '''Require(surface.SizeInPixels.Width>=Math.Ceiling(cover.ActualWidth*cover.XamlRoot.RasterizationScale)
            && surface.SizeInPixels.Height>=Math.Ceiling(cover.ActualHeight*cover.XamlRoot.RasterizationScale)
            && surface.SizeInPixels.Width<=1600 && surface.SizeInPixels.Height<=900,"PhysicalCoverageWithoutUpsampling");
        var probe=new RecommendationCoverImage''')
p.write_text(s,encoding='utf-8')
p=h/'App.xaml.cs';s=p.read_text().replace('Add("ResizeChecks",()=>_ = RunResizeChecks());','Add("ResizeChecks",()=>_ = RunP1Undo());');p.write_text(s)
(h/'P1Undo.cs').write_text('''namespace Acceptance5053;
public partial class App {
 async Task RunP1Undo() {
  Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("ms-appx:///Repeater.xaml")});
  await RunResizeChecks();
  var folder=Windows.Storage.ApplicationData.Current.LocalFolder.Path;
  foreach(var file in Directory.GetFiles(folder,"resize-*"))File.Copy(file,Path.Combine(folder,"matrix-"+Path.GetFileName(file)));
  File.WriteAllText(Path.Combine(folder,"matrix-finished.txt"),DateTimeOffset.UtcNow.ToString("O"));
 }
}
''')
p=r/'docs/list-resize/p1-run.ps1';s=p.read_text().replace("'ImageChecks','Focus'", "'ImageChecks','Focus','ResizeChecks'");p.write_text(s)
