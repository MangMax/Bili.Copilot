"""Extend only the final Season regression, after the fixed-data mount pairs."""
from pathlib import Path
import struct
import zlib

root = Path(__file__).resolve().parents[3]
host = root / "artifacts/recycled-cards/host"
p = host / "ExtraModels.cs"
s = p.read_text(encoding="utf-8-sig")
if "public static bool Unique;" in s:
    raise SystemExit("Already extended: the archived final host needs no extension pass")
s = s.replace("public partial class SeasonItemViewModel : ViewModelBase\n{", """public partial class SeasonItemViewModel : ViewModelBase
{
    public static bool Unique;
    public int Index { get; }
    public SeasonItemViewModel(int index=0) { Index=index; }""")
s = s.replace('"Assets","Cover.png"));', '"Assets", Unique ? $"season-{Index}.png" : "Cover.png"));', 1)
s = s.replace('public string Title=>"Local poster fixture";', 'public string Title=>Unique ? $"Season {Index:0000}" : "Local poster fixture";')
p.write_text(s, encoding="utf-8")
original = (host / "Assets/Cover.png").read_bytes()
width, height = struct.unpack(">II", original[16:24])
def chunk(name, data):
    return struct.pack(">I", len(data)) + name + data + struct.pack(">I", zlib.crc32(name + data))
for i in range(50):
    color = bytes((40 + i * 3, 80 + i * 2, 160 - i * 2, 255))
    raw = (b"\0" + color * width) * height
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b"")
    (host / f"Assets/season-{i}.png").write_bytes(png)
p = host / "RelatedReview.cs"
s = p.read_text(encoding="utf-8-sig")
s = s.replace('object Item(int i)=>', 'SeasonItemViewModel.Unique=File.Exists(Path.Combine(output,"mount-unique.txt"));\n    object Item(int i)=>')
s = s.replace('"Season"=>new SeasonItemViewModel()', '"Season"=>new SeasonItemViewModel(i)')
s = s.replace('view.UpdateLayout();Trace.Write("mount-phase","measure-returned");', 'if(!SeasonItemViewModel.Unique)view.UpdateLayout();Trace.Write("mount-phase",SeasonItemViewModel.Unique?"measure-scheduled":"measure-returned");')
s = s.replace('return new{textCorrect=', '''var sourceItem=ir.ItemsSourceView.GetAt(x.i);
      var actualImage=images.FirstOrDefault();
      var imagePosition=actualImage?.TransformToVisual(sv).TransformPoint(new(0,0));
      return new{sourceCorrect=ReferenceEquals(sourceItem.GetType().GetProperty("Content").GetValue(sourceItem),expected),
       pixel=actualImage==null?null:new {x=imagePosition.Value.X+actualImage.ActualWidth/2,y=imagePosition.Value.Y+Math.Min(actualImage.ActualHeight/4, Math.Max(2,(sv.ViewportHeight-imagePosition.Value.Y)/2)),i=x.i},
       textCorrect=''')
s = s.replace('await Task.Delay(1000);samples.Add(Sample("after1000",30));results.Add(new{family,samples});', '''await Task.Delay(1000);samples.Add(Sample("after1000",30));
    if(SeasonItemViewModel.Unique) {
     var beats=new List<double>();var widths=new List<int>();
     var clock=System.Diagnostics.Stopwatch.StartNew();double last=clock.Elapsed.TotalMilliseconds;
     var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};
     timer.Tick+=(_,_)=>{var now=clock.Elapsed.TotalMilliseconds;beats.Add(now-last);last=now;};timer.Start();
     for(int step=0;step<120;step++) {var width=1200+(step%20)*20;widths.Add(width);window.AppWindow.Resize(new(width,900));await Task.Delay(16);samples.Add(Sample("resize",step));}
     await Task.Delay(800);timer.Stop();
     File.WriteAllText(Path.Combine(output,"matrix-season-resize.json"),JsonSerializer.Serialize(new{widths,beats,cache=ir.VerticalCacheLength}));
     await Task.Delay(1000);samples.Add(Sample("resize-settled",120));
    }
    results.Add(new{family,samples});''')
p.write_text(s, encoding="utf-8")
source = p.read_text(encoding="utf-8")
source = source.replace('await Capture(sv,Path.Combine(output,$"matrix-related-{family}.png"));', '''await Capture(sv,Path.Combine(output,$"matrix-related-{family}.png"));
    if(SeasonItemViewModel.Unique) {
     foreach(var i in Enumerable.Range(0,50)) {
      if(ir.TryGetElement(i) is not FrameworkElement element) continue;
      var point=element.TransformToVisual(sv).TransformPoint(new(0,0));
      if(point.Y+element.ActualHeight<=0 || point.Y>=sv.ViewportHeight)continue;
      var image=Descendants(element).OfType<BlurCoverImage>().FirstOrDefault();
      if(image!=null)await Capture(image,Path.Combine(output,$"matrix-season-image-{i}.png"));
     }
    }''')
p.write_text(source, encoding="utf-8")
print("Season unique regression prepared", width, height)
