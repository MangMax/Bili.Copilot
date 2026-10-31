using BiliCopilot.UI.Controls;
using Richasy.WinUIKernel.Share.Base;
using System.Text.Json;
using System.Security.Cryptography;

namespace Acceptance5053;
public partial class App
{
    async Task RunOnlyImageChecks()
    {
        if(matrixRunning)return;matrixRunning=true;
        ImageExBase.EnableBackgroundDecoding=false;
        var output=Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("ms-appx:///Repeater.xaml")});
        try {await RunCoverChecks(output);}
        catch(Exception e){File.WriteAllText(Path.Combine(output,"matrix-error.txt"),e.ToString());}
        File.WriteAllText(Path.Combine(output,"matrix-results.json"),"[]");
        File.WriteAllText(Path.Combine(output,"matrix-finished.txt"),DateTimeOffset.UtcNow.ToString("O"));
    }
    async Task RunCoverChecks(string output)
    {
        var results=new List<object>();
        async Task Check(string name,Func<Task> test) {
            try {await test();results.Add(new{name,pass=true});}
            catch(Exception e){results.Add(new{name,pass=false,error=e.ToString()});}
            File.WriteAllText(Path.Combine(output,"matrix-cover-checks.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
        }
        var file=new Uri(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath,"Assets","Cover.png"));
        var other=new Uri(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath,"Assets","Logo.png"));
        async Task Mount(ImageExBase image){slot.Children.Clear();Grid.SetRowSpan(image,2);Grid.SetColumnSpan(image,2);slot.Children.Add(image);await Task.Delay(800);}
        void Require(bool value,string message){if(!value)throw new Exception(message);}
        foreach(var blur in new[]{false,true}) {
            await Check((blur?"Blur":"Basic")+"RepeatedUnloadSourceReplacementAndRemount",async()=>{
                ImageExBase image=blur?new BlurCoverImage():new BasicCoverImage();
                image.Width=320;image.Height=180;image.Source=file;image.DecodeWidth=600;
                await Mount(image);Require(HasImage(image),"initial image missing");
                var first=Path.Combine(output,$"matrix-{blur}-first.png");await Capture(image,first);
                slot.Children.Clear();await Task.Delay(50);slot.Children.Add(image);await Task.Delay(700);
                Require(HasImage(image),"same-source remount missing");
                var same=Path.Combine(output,$"matrix-{blur}-same.png");await Capture(image,same);
                Require(SHA256.HashData(File.ReadAllBytes(first)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(same))),"same-source remount changed pixels/alpha");
                for(int i=0;i<4;i++) {
                    slot.Children.Clear();await Task.Delay(50);image.Source=i%2==0?other:file;
                    slot.Children.Add(image);await Task.Delay(700);Require(HasImage(image),$"remount {i}");
                }
                image.Source=other;await Task.Delay(700);Require(HasImage(image),"replacement missing");
                var second=Path.Combine(output,$"matrix-{blur}-replacement.png");await Capture(image,second);
                Require(!SHA256.HashData(File.ReadAllBytes(first)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(second))),"replacement did not change rendered pixels");
            });
        }
        await Check("NonListOriginalSizeAndRoundedAvatarPreserved",async()=>{
            var image=new BasicCoverImage{Source=file,Width=800,Height=450,DecodeWidth=1600};
            await Mount(image);Require(HasImage(image),"original-size image missing");
            Require(image.DecodeWidth==1600 && image.DecodeHeight==900,"large-image decode surface changed");
            await Capture(image,Path.Combine(output,"matrix-original-size.png"));
            image.CornerRadius=new CornerRadius(24);image.Width=image.Height=48;image.DecodeWidth=100;image.Source=other;
            await Task.Delay(700);Require(HasImage(image)&&image.CornerRadius.TopLeft==24,"rounded avatar lost");
            await Capture(image,Path.Combine(output,"matrix-avatar.png"));
        });
        await Check("ImageDiskCacheReusedAcrossRecycledInstances",async()=>{
            var uri=new Uri("https://fixture.invalid/"+Guid.NewGuid().ToString("N")+".png");
            var image=new BasicCoverImage{Source=uri,Width=320,Height=180,DecodeWidth=600};
            var before=FixtureNetwork.Hits;await Mount(image);Require(HasImage(image),"network fixture image missing");
            Require(FixtureNetwork.Hits==before+1,"unexpected first request count");
            var second=new BasicCoverImage{Source=uri,Width=320,Height=180,DecodeWidth=600};
            await Mount(second);Require(HasImage(second),"cached image missing");
            Require(FixtureNetwork.Hits==before+1,"second instance redownloaded cached bytes");
        });
    }
    // Separate unresolved diagnostic: its MeasureOverride failure also occurs with independent layouts.
    async Task RunSharedLayoutDiagnostic()
    {
        void Require(bool value,string message){if(!value)throw new Exception(message);}
            slot.Children.Clear();
            // Both panes must accommodate the production 220-DIP card plus scroll padding.
            // The host's ordinary 220-DIP sidebar is not a valid user-card viewport.
            slot.ColumnDefinitions[0].Width=new GridLength(400);
            var templates=new MatrixTemplates();
            var shared=(VirtualizingLayout)Resources["UserLayout"];
            var left=new Richasy.WinUIKernel.Share.Base.Repeater.AppRepeater{Layout=shared,ItemTemplate=(DataTemplate)templates["User"],ItemsSource=new System.Collections.ObjectModel.ObservableCollection<object>(Enumerable.Range(0,500).Select(_=>(object)new BiliCopilot.UI.ViewModels.Items.UserItemViewModel()))};
            var right=new Richasy.WinUIKernel.Share.Base.Repeater.AppRepeater{Layout=shared,ItemTemplate=(DataTemplate)templates["User"],ItemsSource=new System.Collections.ObjectModel.ObservableCollection<object>(Enumerable.Range(0,500).Select(_=>(object)new BiliCopilot.UI.ViewModels.Items.UserItemViewModel()))};
            var a=new IncrementVerticalScrollControl{Element=left};var b=new IncrementVerticalScrollControl{Element=right};
            a.Height=b.Height=400; Grid.SetRow(a,1);Grid.SetRow(b,1);Grid.SetColumn(b,1);slot.Children.Add(a);slot.Children.Add(b);
            await Task.Delay(2000);
            var av=(ScrollView)a.FindName("View");var bv=(ScrollView)b.FindName("View");
            var ai=(ItemsRepeater)left.FindName("Repeater");var bi=(ItemsRepeater)right.FindName("Repeater");
            Require(bv.ViewportWidth>av.ViewportWidth,"independent widths missing");
            var rightBefore=bv.VerticalOffset;
            av.ScrollTo(0,av.ScrollableHeight,new ScrollingScrollOptions(ScrollingAnimationMode.Disabled));await Task.Delay(700);
            Require(Math.Abs(bv.VerticalOffset-rightBefore)<1,"scroll leaked across shared layout");
            Require(ai.TryGetElement(499)!=null && bi.TryGetElement(0)!=null,"shared layout lost independent realized range");
    }
}
public static class FixtureNetwork { public static int Hits; }
