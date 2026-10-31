"""Single-variable cache diagnostic on the existing production-template harness."""
from pathlib import Path
p = Path('artifacts/resize-diagnosis/host/ResizeChecks.cs')
s = p.read_text(encoding='utf-8-sig')
s = s.replace('SettingsToolkit.Incremental=true;', '''SettingsToolkit.Incremental=true;
            foreach(var repeater in Descendants(main).OfType<ItemsRepeater>()) repeater.VerticalCacheLength=2;''')
p.write_text(s, encoding='utf-8')
