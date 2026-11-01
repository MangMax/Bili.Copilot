using System.Collections.Concurrent;
namespace Microsoft.Extensions.Logging { public interface ILogger<T> {} }
namespace Microsoft.Graphics.Canvas.Text { }
namespace Windows.Foundation { public interface IAsyncAction {} }
namespace Windows.Foundation.Diagnostics { }
namespace Windows.UI { public struct Color {} }
namespace Microsoft.UI { public static class Colors { public static Windows.UI.Color Transparent => default; } }
namespace Microsoft.UI.Xaml { public class XamlRoot { public double RasterizationScale => 1; } }
namespace Microsoft.UI.Xaml.Media { public class SolidColorBrush { public SolidColorBrush(Windows.UI.Color c) {} } }
namespace Microsoft.UI.Xaml.Controls { public class Grid { public List<object> Children = new(); public Microsoft.UI.Xaml.XamlRoot XamlRoot = new(); } }
namespace Microsoft.Graphics.Canvas.UI.Xaml { public class CanvasAnimatedControl { public bool UseSharedDevice; public float DpiScale; public Windows.UI.Color ClearColor; public object Background; } }
namespace Windows.System.Threading {
 public static class ThreadPool {
  public static Thread Worker; public static ManualResetEventSlim Enter = new(false); public static ManualResetEventSlim Go = new(false);
  public sealed class Operation { public Task AsTask()=>Task.CompletedTask; }
  public static Operation RunAsync(Action<Windows.Foundation.IAsyncAction> action) { Enter.Reset(); Go.Reset(); Worker=new Thread(()=>{Enter.Set(); Go.Wait(); action(null);}){IsBackground=true}; Worker.Start(); return new(); }
 }
}
namespace Richasy.Danmaku.Enums { public enum DanmakuMode { Top, Bottom, Rolling, ReverseRolling, Advanced } }
namespace Richasy.Danmaku.Models { public class DanmakuItem { public uint StartMs; public string Id; public Richasy.Danmaku.Enums.DanmakuMode Mode; public bool AllowDensityControl, IsRealtime; } }
namespace Richasy.Danmaku.Legacy {
 public static class DanmakuDefaultLayerDef { public const uint DefaultLayerCount=5, TopLayerId=0, BottomLayerId=1, ReverseRollingLayerId=2, RollingLayerId=3, AdvancedLayerId=4, DefaultLayerId=0; }
 public static class Logger { public static ConcurrentQueue<string> Messages=new(); public static void SetLogger(object o){} public static void Log(string s)=>Messages.Enqueue(s); }
 public class DanmakuRender {
  public static DanmakuRender Last; public bool DebugMode; public bool ResourcesReady=true; public event Action ResourcesCreated;
  public ConcurrentQueue<string> Items=new(); public int Starts; public bool FailStop; public string FailOperation; public bool LastPaused; public Action OwnerClose; public Action BeforeItem; public void Wake()=>ResourcesCreated?.Invoke(); void Fail(string op){if(FailOperation==op){FailOperation=null;throw new InvalidOperationException("injected-"+op);}}
  public DanmakuRender(object a,object b){Last=this;}
  public void Pause(){Fail("Pause");} public void Start(bool paused=false){Fail("Start"); LastPaused=paused; Interlocked.Increment(ref Starts);} public void Stop(bool log=true){Fail("Stop");if(FailStop){FailStop=false;throw new InvalidOperationException("injected-native-stop-failure");} Items.Clear();} public void Close(bool log=true){}
  public void RenderDanmakuItem(uint layer,Richasy.Danmaku.Models.DanmakuItem item,bool reconstructing=false,uint ordinal=0,uint age=0){BeforeItem?.Invoke();Fail("Item");Items.Enqueue(item.Id);}
  public void SetAutoControlDensity(bool v){} public void SetRollingDensity(int v){} public void SetRollingAreaRatio(int v){} public void SetRollingSpeed(double v){} public void SetOutlineSize(double v){} public void SetOpacity(double v){} public void SetIsTextBold(bool v){} public void SetDanmakuFontSizeOffset(double v){} public void SetDefaultFontFamilyName(string v){} public void SetBorderColor(Windows.UI.Color v){} public void SetNoOverlapSubtitle(bool v){} public void SetRefreshRate(int v){} public void SetForceSoftwareRenderer(bool v){} public void SetLayerRenderState(uint l,bool v){}
 }
}
