#nullable enable
using Richasy.Danmaku;
using Richasy.Danmaku.Enums;
using Richasy.Danmaku.Models;
using System.Text.Json;
static partial class Program
{
 static void DiagnosticCases(Action<string,Action> run)
 {
  Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Initialize(0x00010008);
  try {
   foreach(float scale in new[]{1f,1.5f})
   run("REVIEW_BottomInsideTarget_Scale_"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture).Replace('.','p'),()=>{
    using var r=new Rig();var epoch=r.Owner.Capture().Epoch;r.Owner.Resize(480,240,scale);
    Check(SpinWait.SpinUntil(()=>r.Owner.Capture().Epoch>epoch,10000),"resize timeout");
    var items=new List<DanmakuItem>();
    foreach(var (mode,text,font) in new[]{(DanmakuMode.Rolling,"PX ROLL",18),(DanmakuMode.ReverseRolling,"PX REV",18),(DanmakuMode.Top,"PX TOP",22),(DanmakuMode.Bottom,"PX BOTTOM",20)})
    {var item=Entry(text);item.Mode=mode;item.BaseFontSize=font;item.HasOutline=mode==DanmakuMode.Top;items.Add(item);}
    r.Applied(r.Bridge.SetListAsync(items));r.Applied(r.Bridge.SeekAsync(0));
    float y=0,height=0,clipHeight=0;
    Done(r.Owner.ExecuteAsync(r.Owner.Capture(),_=>{
     var renderer=(DanmakuDirectXRender)Read(r.Surface.Real,"renderer");
     var native=(CoreNativeSurface)Read(r.Surface.Real,"native");var context=native.RenderContext;
     var rows=r.Surface.Items().Select(i=>new{ i.Item.Text,i.X,i.Width,i.Height,slot=i.StartY,drawY=r.Surface.DrawY(i)}).ToArray();
     var bottom=rows.Single(i=>i.Text=="PX BOTTOM");y=bottom.drawY;height=bottom.Height;clipHeight=context.Size.Height;
     Console.WriteLine(JsonSerializer.Serialize(new{kind="geometry",scale,canvasWidth=renderer.CanvasWidth,canvasHeight=renderer.CanvasHeight,targetWidth=context.Size.Width,targetHeight=clipHeight,pixelWidth=context.PixelSize.Width,pixelHeight=context.PixelSize.Height,rows}));
    }));
    Check(y>=0 && y+height<=clipHeight+0.01,$"Bottom outside actual target: y={y}, height={height}, target={clipHeight}, scale={scale}");
   });
   if(typeof(ICoreExecutionSurface).GetMethod("BeginRebuild")!=null){
    run("REVIEW_LateSeek_StillActiveOld_NotDuplicatedOrRejuvenated",()=>{
     using var r=new Rig();r.Applied(r.Bridge.SetListAsync(new(){Entry("old",0),Entry("new",100)}));
     Task<CoreResourceOutcome>? next=null;int once=0;
     r.Surface.BeforeAdd=()=>{if(++once==1)next=r.Bridge.SeekAsync(100);};
     var old=r.Bridge.SeekAsync(0);Done(old);Check(old.Result==CoreResourceOutcome.Superseded,"late seek Applied");r.Applied(next!);
     var values=r.Items();Check(values.Select(x=>x.Item.Text).SequenceEqual(new[]{"old","new"}),"duplicate/missing current selection");
     var now=r.Surface.Steps.Last().Total;
     Check(now-values[0].FirstRenderTime==TimeSpan.FromMilliseconds(100)&&now-values[1].FirstRenderTime==TimeSpan.Zero,"stale age restored");
     Check(r.Surface.Steps.Last().Step==TimeSpan.Zero,"nonzero seek step");
    });
    run("REVIEW_ResizeDuringRebuild_LatestTargetRecovered",()=>{
     using var r=new Rig();r.Applied(r.Bridge.SetListAsync(new(){Entry("resize",19000)}));long epoch=r.Owner.Capture().Epoch;int once=0;
     r.Surface.BeforeAdd=()=>{if(++once==1)r.Owner.Resize(800,450,1);};
     var old=r.Bridge.SeekAsync(20000);Done(old);Check(old.Result==CoreResourceOutcome.Superseded,"old epoch Applied");
     Check(SpinWait.SpinUntil(()=>r.Owner.Capture().Epoch>epoch,10000),"resize pending");r.Applied(r.Bridge.FrameAsync());
     var item=r.Items().Single();Check(Math.Abs(item.X-(800-item.Width)/2)<0.01,"stale dimensions");
     Check(r.Surface.Steps.Last().Total-item.FirstRenderTime==TimeSpan.FromMilliseconds(1000),"lost target age");
    });
    run("REVIEW_OriginalPublicTarget_Coordinates",()=>{
     using var r=new Rig();long epoch=r.Owner.Capture().Epoch;r.Owner.Resize(480,240,1.5f);Check(SpinWait.SpinUntil(()=>r.Owner.Capture().Epoch>epoch,10000),"resize");
     var roll=Entry("PX ROLL",18000);roll.Mode=DanmakuMode.Rolling;roll.BaseFontSize=18;
     var rev=Entry("PX REV",18000);rev.Mode=DanmakuMode.ReverseRolling;rev.BaseFontSize=18;
     var top=Entry("PX TOP",19000);top.HasOutline=true;
     var bottom=Entry("PX BOTTOM",19500);bottom.Mode=DanmakuMode.Bottom;bottom.BaseFontSize=20;
     r.Applied(r.Bridge.SetListAsync(new(){Entry("EXPIRED",0),roll,rev,top,bottom,Entry("FUTURE",21000)}));r.Applied(r.Bridge.SeekAsync(20000));
     Done(r.Owner.ExecuteAsync(r.Owner.Capture(),_=>{
      var rows=r.Surface.Items().Select(i=>new{i.Item.Text,i.X,i.Width,i.Height,slot=i.StartY,drawY=r.Surface.DrawY(i),age=(r.Surface.Steps.Last().Total-i.FirstRenderTime).TotalMilliseconds}).ToArray();
      Console.WriteLine(JsonSerializer.Serialize(new{kind="original-public-target",rows}));
      Check(rows.Length==4 && Math.Abs(rows.Single(i=>i.Text=="PX TOP").X-294.02026)<0.01,"original observation mismatch");
      Check(rows.Where(i=>i.Text!="PX BOTTOM").All(i=>i.slot==0),"different independent first slots");
     }));
    });
   }
  } finally {Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Shutdown();}
 }
}
