"""Bounded fixture-only probe: finite two-column grid, same production cards/layout."""
from pathlib import Path
r=Path(__file__).resolve().parents[2];h=r/'artifacts/list-p1/host'
p=h/'CoverChecks.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('slot.ColumnDefinitions[0].Width=new GridLength(400);', '''var paneRoot=new Grid {Width=1000,Height=400,HorizontalAlignment=HorizontalAlignment.Left};
            paneRoot.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(400)});
            paneRoot.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(600)});
            Grid.SetColumnSpan(paneRoot,2);Grid.SetRowSpan(paneRoot,2);slot.Children.Add(paneRoot);''')
s=s.replace('Grid.SetRow(a,1);Grid.SetRow(b,1);Grid.SetColumn(b,1);slot.Children.Add(a);slot.Children.Add(b);', 'Grid.SetColumn(b,1);paneRoot.Children.Add(a);paneRoot.Children.Add(b);')
s=s.replace('await Task.Delay(2000);', '''a.SizeChanged+=(_,e)=>File.AppendAllText(Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path,"matrix-pane-layout.jsonl"),JsonSerializer.Serialize(new {pane="left",width=e.NewSize.Width,height=e.NewSize.Height})+"\\n");
            b.SizeChanged+=(_,e)=>File.AppendAllText(Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path,"matrix-pane-layout.jsonl"),JsonSerializer.Serialize(new {pane="right",width=e.NewSize.Width,height=e.NewSize.Height})+"\\n");
            await Task.Delay(2000);''')
p.write_text(s,encoding='utf-8')
p=h/'IndependentFocus.cs';s=p.read_text().replace('(ReviewGreen?2:20)','(ReviewGreen?1:20)');p.write_text(s)
