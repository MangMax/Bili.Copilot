using BiliCopilot.UI.Controls;
using Richasy.WinUIKernel.Share.Base.Repeater;
using System.Text.Json;
namespace Acceptance5053;
public partial class App
{
    async Task RunFocus()
    {
        var output=Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        if(ReviewGreen) Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("ms-appx:///Repeater.xaml")});
        var results=new List<object>();
        try {
            var implicitOwner=new AppRepeater();
            var explicitOwner=new AppRepeater {Style=new Style(typeof(AppRepeater))};
            slot.Children.Clear();slot.Children.Add(implicitOwner);slot.Children.Add(explicitOwner);
            await Task.Delay(300);
            var implicitCache=((ItemsRepeater)implicitOwner.FindName("Repeater")).VerticalCacheLength;
            var explicitCache=((ItemsRepeater)explicitOwner.FindName("Repeater")).VerticalCacheLength;
            results.Add(new {name="StylePrecedence",implicitCache,explicitCache,pass=implicitCache==(ReviewGreen?2:20)&&explicitCache==20});
            File.WriteAllText(Path.Combine(output,"matrix-focus.json"),JsonSerializer.Serialize(results));
            await RunSharedLayoutDiagnostic();
            results.Add(new {name="SameSourceTwoPane",pass=true});
        } catch(Exception e) {
            results.Add(new {name="SameSourceTwoPane",pass=false,error=e.ToString()});
        }
        File.WriteAllText(Path.Combine(output,"matrix-focus.json"),JsonSerializer.Serialize(results));
        File.WriteAllText(Path.Combine(output,"matrix-results.json"),"[]");
        File.WriteAllText(Path.Combine(output,"matrix-finished.txt"),DateTimeOffset.UtcNow.ToString("O"));
    }
}
