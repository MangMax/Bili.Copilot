"""Switch only the two image base classes to the candidate; instrumentation stays identical."""
from pathlib import Path
import shutil
r=Path(__file__).resolve().parents[2];h=r/'artifacts/list-resize/host';ui=r/'src/Desktop/BiliCopilot.UI'
p=h/'BasicCoverImage.cs';s=(ui/'Controls/BasicCoverImage.cs').read_text(encoding='utf-8-sig')
s=s.replace('var width = canvasBitmap.Size.Width;','Acceptance5053.ListProbe.Draws++;\n        var width = canvasBitmap.Size.Width;')
s=s.replace('private void DrawBlurImage(','''protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size s) { Acceptance5053.ListProbe.Measures++; return base.MeasureOverride(s); }
    protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size s) { Acceptance5053.ListProbe.Arranges++; return base.ArrangeOverride(s); }
    private void DrawBlurImage(''')
p.write_text(s,encoding='utf-8')
shutil.copy2(ui/'Controls/BlurCoverImage.cs',h/'BlurCoverImage.cs')
shutil.copy2(ui/'Controls/ReloadableCoverImage.cs',h/'ReloadableCoverImage.cs')
shutil.copy2(ui/'Styles/Repeater.xaml',h/'Repeater.xaml')
shutil.copy2(ui/'Controls/Message/NotifyMessageCardControl.xaml',h/'NotifyMessageCardControl.xaml')
