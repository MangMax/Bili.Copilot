using Microsoft.UI.Xaml.Media;

namespace Acceptance54;

internal static class VisualProbe {
    private static string Brush(Brush b)=>b is SolidColorBrush s?s.Color.ToString():b?.ToString();
    public static void Dump(FrameworkElement root) {
        var rows=new List<object>();
        void Visit(DependencyObject node,int depth) {
            if(node is FrameworkElement e) rows.Add(new {
                depth,type=e.GetType().FullName,e.Name,e.IsLoaded,e.ActualWidth,e.ActualHeight,
                desiredWidth=e.DesiredSize.Width,desiredHeight=e.DesiredSize.Height,
                e.Opacity,visibility=e.Visibility.ToString(),theme=e.ActualTheme.ToString(),
                background=Brush(e is Control c?c.Background:e is Panel p?p.Background:null),
                foreground=Brush(e is Control cc?cc.Foreground:e is TextBlock t?t.Foreground:null),
                template=e is Control ct?ct.Template?.ToString():null,
                content=e is UserControl u?u.Content?.GetType().FullName:null,
                text=e is TextBlock tb?tb.Text:null,
                children=VisualTreeHelper.GetChildrenCount(e)
            });
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Visit(VisualTreeHelper.GetChild(node,i),depth+1);
        }
        Visit(root,0);Log.Write("visual-tree",rows);Log.Save("visual-tree.json",rows);
    }
}
