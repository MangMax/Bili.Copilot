// Copyright (c) Bili Copilot. All rights reserved.

// Headless presentation/OS boundaries only. The VM and MVVM 8.4.2 command generator
// are production code. These adapters do not exercise CsWinRT or Windows activation.
namespace Richasy.WinUIKernel.Share.ViewModels
{
    public abstract class ViewModelBase<T>(T data) : ViewModelBase
    {
        public T Data { get; set; } = data;
    }
}

namespace Humanizer
{
    // The real non-generic Humanizer overload wins when supplied transitively.
    // Command-only headless tests also compile without that optional dependency.
    public static class NotifyMessageTimeAdapter
    {
        public static string Humanize<T>(this T? value, DateTimeOffset? dateToCompare, System.Globalization.CultureInfo culture)
            where T : struct => "relative time";
    }
}

namespace WinRT
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class GeneratedBindableCustomPropertyAttribute : Attribute
    {
        public GeneratedBindableCustomPropertyAttribute() { }
        public GeneratedBindableCustomPropertyAttribute(string[] propertyNames, Type[] indexerPropertyTypes) { }
    }
}

namespace Windows.System
{
    public static class Launcher
    {
        public static global::System.Threading.AsyncLocal<Func<Uri, Task<bool>>?> Handler { get; } = new();
        public static LaunchOperation LaunchUriAsync(Uri uri) => new((Handler.Value ?? throw new InvalidOperationException("Unexpected OS launch"))(uri));
    }

    public sealed class LaunchOperation(Task<bool> task)
    {
        public Task<bool> AsTask() => task;
    }
}
