global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
global using Windows.Foundation;
global using Windows.Win32;
global using Windows.Win32.Foundation;
using Microsoft.UI.Dispatching;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI {
    // Only service resolution is replaced. This returns the actual current UI queue.
    internal static class GlobalDependencies {
        public static T Get<T>(this object caller) where T:class => typeof(T)==typeof(DispatcherQueue)
            ? (T)(object)DispatcherQueue.GetForCurrentThread() : throw new NotSupportedException(typeof(T).Name);
    }
}
namespace BiliCopilot.UI.Models.Constants {
    public enum SettingNames { PlayerWindowPositionLeft, PlayerWindowPositionTop, PlayerWindowWidth, PlayerWindowHeight, IsPlayerWindowMaximized }
}
namespace BiliCopilot.UI.Toolkits {
    // In-memory, per-owned-process settings. Never reads the user's app data.
    public static class SettingsToolkit {
        public static readonly Dictionary<Models.Constants.SettingNames,object> Values = new();
        public static T ReadLocalSetting<T>(Models.Constants.SettingNames key,T fallback) => Values.TryGetValue(key,out var v)?(T)v:fallback;
        public static void WriteLocalSetting<T>(Models.Constants.SettingNames key,T value) {
            Values[key]=value; Acceptance54.Log.Write("settings-write",new {key=key.ToString(),value});
        }
    }
}
namespace BiliCopilot.UI.Controls.Core {
    // Controlled visual only: no PlayerViewModel, media engine, authentication or network.
    // Production PlayerWindow type-checks this overlay to schedule its own title-bar code.
    public sealed partial class PlayerOverlay:UserControl {
        public readonly ProbeHeader Probe = new();
        public Func<bool> Closed;
        public int LiveHeaderReads, LateHeaderReads;
        public ProbeHeader HeaderBar { get {
            var closed=Closed?.Invoke()??false;
            if(closed)LateHeaderReads++;else LiveHeaderReads++;
            Acceptance54.Log.Write("header-read",new {closed});return Probe;
        }}
        public PlayerOverlay() {
            Background=(Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
            IsTabStop=true;
            var content=new Grid { Background=Background };Content=content;
            content.Children.Add(HeaderBar);
            content.Children.Add(new TextBlock {Text="#54 real PlayerWindow\nOffline controlled overlay",TextWrapping=TextWrapping.Wrap,
                HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});
        }
        public void ShowTip(string message,InfoType type) => Acceptance54.Log.Write("tip",new {message});
    }
    public sealed partial class ProbeHeader:Grid {
        public bool Destroyed;
        public int PaddingCalls,AfterDestroyCalls,AlignmentChanges;
        public ProbeHeader() {
            Height=48; VerticalAlignment=VerticalAlignment.Top;
            Children.Add(new TextBlock {Text="Controlled header probe",Margin=new Thickness(16,8,16,8)});
            RegisterPropertyChangedCallback(HorizontalAlignmentProperty,(_,_)=> {
                AlignmentChanges++; if(Destroyed)AfterDestroyCalls++;
                Acceptance54.Log.Write("header-alignment",new {value=HorizontalAlignment.ToString(),Destroyed});
            });
        }
        public void UpdatePadding() {
            PaddingCalls++; if(Destroyed)AfterDestroyCalls++;
            Acceptance54.Log.Write("header-padding",new {Destroyed,PaddingCalls});
        }
    }
}
