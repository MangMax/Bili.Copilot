using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Richasy.Danmaku.Enums;
using Richasy.Danmaku.Legacy;
using Richasy.Danmaku.Models;

// Managed reconstruction arithmetic/slot seam against the sealed DLL.
// No Canvas/device construction: this is not native lifecycle or pixel evidence.
class Independent
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Type R = typeof(DanmakuFrostMaster).Assembly.GetType("Richasy.Danmaku.Legacy.DanmakuRender")!;
    static object Read(object o, string n) => o.GetType().GetField(n,F)!.GetValue(o)!;
    static void Set(object o,string n,object v) => o.GetType().GetField(n,F)!.SetValue(o,v);
    static object Call(object o,string n,params object[] args) => o.GetType().GetMethod(n,F)!.Invoke(o,args)!;
    static void Require(bool ok,string message) { if(!ok) throw new Exception(message); }
    static (object render,object layer,IList list,object slots) Scene()
    {
        var render=RuntimeHelpers.GetUninitializedObject(R);
        var lt=R.GetNestedType("RenderLayer",BindingFlags.NonPublic)!;
        var layer=Activator.CreateInstance(lt,new object[]{0u,false})!;
        var layers=Array.CreateInstance(lt,1);layers.SetValue(layer,0);
        Set(render,"_renderLayerList",layers);Set(render,"<CanvasWidth>k__BackingField",800f);
        Set(render,"_rollingSpeed",0.1f);
        var slots=Read(layer,"YSlotManager");Call(slots,"UpdateLength",100u);
        return(render,layer,(IList)Read(layer,"RenderList"),slots);
    }
    static object Item(IList list,object slots,DanmakuMode mode,float x,ulong duration=0)
    {
        var t=R.GetNestedType("DanmakuRenderItem",BindingFlags.NonPublic)!;
        // Public model defaults call Microsoft.UI.Colors; keep this seam managed.
        var model=(DanmakuItem)RuntimeHelpers.GetUninitializedObject(typeof(DanmakuItem));
        model.Mode=mode;model.Text="independent";model.DurationMs=duration;
        var item=Activator.CreateInstance(t,new object[]{model})!;
        Set(item,"Width",100f);Set(item,"Height",20f);Set(item,"X",x);
        var args=new object[]{Read(item,"Id"),20u,0u};
        var occupied=(bool)slots.GetType().GetMethod("GetY",new[]{typeof(uint),typeof(uint),typeof(uint).MakeByRefType()})!.Invoke(slots,args)!;
        Set(item,"StartY",(float)(uint)args[2]);Set(item,"NeedToReleaseYSlot",occupied);
        list.Add(item);return item;
    }
    static void Main()
    {
        var a=typeof(DanmakuFrostMaster).Assembly;
        Console.WriteLine($"Loaded {a.Location}; SHA256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location)))}; MVID={a.ManifestModule.ModuleVersionId}");
        int count=0;
        void Case(string name,Action test){test();Console.WriteLine("PASS "+name);count++;}
        Case("TopExactBoundaryThenOneMillisecondReleasesSlot",()=>{
            var s=Scene();var i=Item(s.list,s.slots,DanmakuMode.Top,0,3000);
            Call(s.render,"AdvanceReconstruction",3000u);Require(s.list.Count==1,"exact lifetime removed");
            Call(s.render,"AdvanceReconstruction",1u);Require(s.list.Count==0,"expired retained");
            var next=Item(s.list,s.slots,DanmakuMode.Top,0);Require((float)Read(next,"StartY")==0,"expired slot leaked");
        });
        Case("RollingStrictExitAndReverseInclusiveExit",()=>{
            var s=Scene();Item(s.list,s.slots,DanmakuMode.Rolling,-100);
            Call(s.render,"AdvanceReconstruction",0u);Require(s.list.Count==1,"rolling strict boundary changed");
            Call(s.render,"AdvanceReconstruction",1u);Require(s.list.Count==0,"rolling exit retained");
            Item(s.list,s.slots,DanmakuMode.ReverseRolling,800);
            Call(s.render,"AdvanceReconstruction",0u);Require(s.list.Count==0,"reverse inclusive boundary changed");
        });
        Case("EntranceReleaseDoesNotReleaseNewOwnerAtOldExpiry",()=>{
            var s=Scene();var old=Item(s.list,s.slots,DanmakuMode.Rolling,651);
            Call(s.render,"AdvanceReconstruction",20u);Require(!(bool)Read(old,"NeedToReleaseYSlot"),"entrance slot retained");
            var next=Item(s.list,s.slots,DanmakuMode.Top,0,100000);
            Require((float)Read(next,"StartY")==0,"entrance not reusable");
            Call(s.render,"AdvanceReconstruction",10000u);Require(s.list.Count==1,"old rolling retained");
            var third=Item(s.list,s.slots,DanmakuMode.Top,0);Require((float)Read(third,"StartY")==20,"new owner released by old expiry");
        });
        Case("ZeroSpeedLongAgeAndNegativeSpeed",()=>{
            var s=Scene();Set(s.render,"_rollingSpeed",0f);var i=Item(s.list,s.slots,DanmakuMode.Rolling,800);
            Call(s.render,"AdvanceReconstruction",uint.MaxValue);Require(s.list.Count==1 && (float)Read(i,"X")==800 && (double)Read(i,"ReconstructedAgeMs")==uint.MaxValue,"zero speed finite lookback/age overflow");
            Set(s.render,"_rollingSpeed",-0.1f);Call(s.render,"AdvanceReconstruction",1000u);Require((float)Read(i,"X")>800,"negative speed changed");
        });
        Console.WriteLine($"Passed: {count}, Failed: 0, Skipped: 0");
    }
}
