using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using BiliCopilot.UI;
using BiliCopilot.UI.Controls;
using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.Toolkits;
using Microsoft.UI.Xaml.Media;

namespace Acceptance5053;
public static class ResizeProbe
{
    public static int Reloads, Opens, Decoders, Pixels, Surfaces, Active, Peak, Measures, Arranges, Native, NativePeak;
    public static void Reset() { Reloads=Opens=Decoders=Pixels=Surfaces=Measures=Arranges=0; Peak=Active; NativePeak=Native; }
}
public partial class App
{
    [DllImport("user32.dll", SetLastError=true)]
    static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    async Task RunResizeChecks(bool hot=false)
    {
        var output=Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        var results=new List<object>();
        try
        {
            New("Long");
            await Task.Delay(1500);
            if(hot)
            {
                service.Batch=10; service.Limit=100;
                vm.SelectSectionCommand.Execute(vm.Sections.OfType<BiliCopilot.UI.ViewModels.Items.PopularSectionItemViewModel>().Single(s=>s.Type==BiliCopilot.UI.Models.Constants.PopularSectionType.Hot));
                await Task.Delay(1500);
            }
            SettingsToolkit.Incremental=true;
            var generation=0;
            foreach(var target in new[]{50,200,500})
            {
                while(vm.Videos.Count<target)
                {
                    if(hot) await vm.LoadVideosCommand.ExecuteAsync(null);
                    else
                    {
                        service.Scenario="Refresh"+(++generation); service.Page=0; service.Batch=10; service.Limit=4;
                        await vm.RefreshVideoCommand.ExecuteAsync(null);
                    }
                    await Task.Delay(100);
                }
                await Task.Delay(1500);
                var cards=Descendants(main).OfType<VideoCardControl>().ToArray();
                var covers=Descendants(main).OfType<RecommendationCoverImage>().ToArray();
                var visible=cards.Count(c=>{var p=c.TransformToVisual(main).TransformPoint(new(0,0));return p.Y+c.ActualHeight>0&&p.Y<main.ActualHeight;});
                var layout=0; EventHandler<object> onLayout=(_,_)=>layout++;
                main.LayoutUpdated+=onLayout;
                var beats=new List<double>();
                var timer=Dependencies.Queue.CreateTimer(); timer.Interval=TimeSpan.FromMilliseconds(16);
                var clock=Stopwatch.StartNew(); var last=clock.Elapsed.TotalMilliseconds;
                timer.Tick+=(_,_)=>{var now=clock.Elapsed.TotalMilliseconds; beats.Add(now-last);last=now;};
                ResizeProbe.Reset(); timer.Start();
                var hwnd=Microsoft.UI.Win32Interop.GetWindowFromWindowId(window.AppWindow.Id);
                var startCount=vm.Videos.Count;
                var startUpdates=updates;
                var resize=new List<double>();
                var allocated=await Task.Run(async()=>
                {
                    var before=GC.GetTotalAllocatedBytes();
                    for(var i=0;i<120;i++)
                    {
                        var t=Stopwatch.StartNew();
                        if(!SetWindowPos(hwnd,IntPtr.Zero,0,0,1400+(i<60?i:120-i)*3,900,0x16)) throw new Exception("SetWindowPos failed");
                        resize.Add(t.Elapsed.TotalMilliseconds);
                        await Task.Delay(16);
                    }
                    return GC.GetTotalAllocatedBytes()-before;
                });
                await Task.Delay(800); timer.Stop(); main.LayoutUpdated-=onLayout;
                double P95(List<double> a)=>a.Order().ElementAt((int)((a.Count-1)*.95));
                var row=new {hot,target,count=vm.Videos.Count,startCount,listUpdates=updates-startUpdates,realized=cards.Length,visible,covers=covers.Length,
                    dpi=main.XamlRoot.RasterizationScale,layout,ResizeProbe.Reloads,ResizeProbe.Opens,ResizeProbe.Decoders,ResizeProbe.Pixels,ResizeProbe.Surfaces,
                    ResizeProbe.Active,ResizeProbe.Peak,ResizeProbe.Native,ResizeProbe.NativePeak,ResizeProbe.Measures,ResizeProbe.Arranges,
                    allocated,resizeP95=P95(resize),resizeMax=resize.Max(),heartbeatP95=P95(beats),heartbeatMax=beats.Max(),
                    pass=beats.Max()<200 && cards.Length<100,resize,beats};
                results.Add(row);
                File.WriteAllText(Path.Combine(output,"resize-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
                status.Text=$"Resize {vm.Videos.Count}, realized {cards.Length}, max {beats.Max():F1}ms";
            }
            if(!hot) await VerifyCoverAndUndo(output);
        }
        catch(Exception e){File.WriteAllText(Path.Combine(output,"resize-error.txt"),e.ToString());status.Text=e.Message;}
        File.WriteAllText(Path.Combine(output,"resize-finished.txt"),DateTimeOffset.UtcNow.ToString("O"));
    }

    async Task VerifyCoverAndUndo(string output)
    {
        var checks=new List<string>();
        void Require(bool pass,string name){if(!pass)throw new Exception(name);checks.Add(name);}
        var before=vm.Videos.ToArray();
        var offset=scroll.VerticalOffset;
        service.Scenario="UndoProbe";service.Page=0;
        await vm.RefreshVideoCommand.ExecuteAsync(null);await Task.Delay(800);
        vm.UndoRefreshCommand.Execute(null);await Task.Delay(800);
        Require(vm.Videos.SequenceEqual(before),"UndoExactReferenceSequence");
        Require(Math.Abs(scroll.VerticalOffset-offset)<2,"UndoExactViewportAnchor");
        var cover=Descendants(main).OfType<RecommendationCoverImage>().First(c=>c.IsLoaded);
        var inner=(Image)((Grid)cover.Content).Children[0];
        var surface=(Microsoft.Graphics.Canvas.UI.Xaml.CanvasImageSource)inner.Source;
        var expected=CoverDecodeSize.Calculate(1600,900,cover.ActualWidth,cover.ActualHeight,cover.XamlRoot.RasterizationScale);
        Require(surface.SizeInPixels.Width==expected.Item1&&surface.SizeInPixels.Height==expected.Item2,"FinalPhysicalPixelDecode");
        var probe=new RecommendationCoverImage {Width=240,Height=135,Source=cover.Source};
        slot.Children.Add(probe);await Task.Delay(600);
        var image=(Image)((Grid)probe.Content).Children[0];
        probe.Width=320;probe.Height=180;
        await Task.Delay(30);probe.Source=null;await Task.Delay(600);
        Require(image.Source is null,"SourceClearedDuringResizeRejectsOldResult");
        probe.Source=cover.Source;await Task.Delay(600);
        probe.Width=360;slot.Children.Remove(probe);await Task.Delay(600);
        Require(image.Source is null,"UnloadCancelsPendingResize");
        slot.Children.Add(probe);await Task.Delay(600);
        Require(image.Source is not null,"RemountLoadsCurrentSize");
        slot.Children.Remove(probe);
        File.WriteAllText(Path.Combine(output,"resize-checks.json"),JsonSerializer.Serialize(checks));
    }
}
