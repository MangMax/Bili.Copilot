// Copyright (c) Bili Copilot. All rights reserved.

using Microsoft.UI.Xaml.Data;

namespace BiliCopilot.UI.Converters;

/// <summary>
/// Moves an element between two adjacent grid columns: the second column when
/// true, the first when false.
/// </summary>
internal sealed partial class BoolToGridColumnConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? 1 : 0;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
