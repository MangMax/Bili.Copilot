"""Make the existing large-image visual check fit the owned window; retain prior capture."""
from pathlib import Path
r=Path(__file__).resolve().parents[2];p=r/'artifacts/list-p1/host/CoverChecks.cs'
s=p.read_text(encoding='utf-8-sig').replace('var image=new BasicCoverImage{Source=file,Width=800,Height=450,DecodeWidth=1600};', '''window.AppWindow.Resize(new(1660,1400));await Task.Delay(300);
            var image=new BasicCoverImage{Source=file,Width=800,Height=450,DecodeWidth=1600};''')
s=s.replace('Require(image.DecodeWidth==1600', 'Require(slot.ActualHeight>=image.Height && slot.ActualWidth>=image.Width,"large-image fixture clipped");\n            Require(image.DecodeWidth==1600')
p.write_text(s,encoding='utf-8')
