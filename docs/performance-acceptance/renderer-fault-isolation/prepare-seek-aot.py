"""Adapt the existing offline home/full-player fixtures without editing product inputs."""
from pathlib import Path
import argparse, re, shutil
root = Path(__file__).resolve().parents[3]
parser = argparse.ArgumentParser()
parser.add_argument('--artifact-root', type=Path, default=root / 'artifacts/home-aot')
parser.add_argument('--media-root', type=Path, default=root.parent / 't3-39df52c6/artifacts/combined-60cd/full')
args = parser.parse_args()
fixture = args.artifact_root.resolve() / 'fixture'
assert fixture.is_dir() and not (fixture/'SeekAot.cs').exists(), 'Prepare a fresh home fixture first'
kernel = args.artifact_root.resolve() / 'source/externals/bili-kernel/src/BiliKernel.Abstractions/Bili'
assets = args.media_root.resolve()
for name in ('fixture-media.mkv', 'libmpv-2.dll'):
    shutil.copyfile(assets/name, fixture/name)
headers = '''using System.Net.WebSockets;
using Richasy.BiliKernel.Bili.Media;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;
using Richasy.BiliKernel.Models.Danmaku;
using Richasy.BiliKernel.Models.Subtitle;
using Richasy.BiliKernel.Models.Authorization;
namespace BiliCopilot.UI;
'''
overrides = {
'GetVideoPageDetailAsync': 'Task.FromResult(SeekAot.View)',
'GetVideoPlayDetailAsync': 'Task.FromResult(new DashMediaInformation([new("80",Path.Combine(SeekAot.Root,"fixture-media.mkv"),1000000,"video/mp4","avc1",960,540,"","")],null,[new(80,"Offline",false,false)],60))',
'GetToken': 'new BiliToken { UserId=224 }',
'GetCookieString': 'string.Empty',
'GetCookies': 'new Dictionary<string,string>()',
}
for filename, interface in [('Media/IPlayerService.cs','IPlayerService'),('Media/ISubtitleService.cs','ISubtitleService'),('Media/IDanmakuService.cs','IDanmakuService'),('Authorization/ITokenResolver.cs','IBiliTokenResolver'),('Authorization/ICookiesResolver.cs','IBiliCookiesResolver')]:
    text=(kernel/filename).read_text(encoding='utf-8-sig')
    headers += f' internal sealed class Aot{interface} : {interface} {{\n'
    for ret,name,args in re.findall(r'^\s+([^/\n]+?)\s+(\w+)\(([^\n]*)\);',text,re.M):
        ret=ret.strip()
        expr=overrides.get(name)
        if expr is None:
            if ret.startswith('Task<IReadOnlyList<'): expr='Task.FromResult<'+ret[5:-1]+'>([])'
            else: expr='throw new NotSupportedException("Offline service: '+name+'")'
        headers += f' public {ret} {name}({args}) => {expr};\n'
    headers+='}\n'
(fixture/'AotServices.cs').write_text(headers,encoding='utf-8')
shutil.copyfile(Path(__file__).with_name('SeekAot.cs'),fixture/'SeekAot.cs')
p=fixture/'SeekAot.cs';p.write_text(p.read_text().replace('FIXTURE_ROOT',str(fixture)),encoding='utf-8')
p=fixture/'GlobalDependencies.cs';s=p.read_text(encoding='utf-8-sig').replace('.WithHomeDiscovery().Build()', '.WithHomeDiscovery().WithAotPlayback().Build()');p.write_text(s,encoding='utf-8')
p=fixture/'ControlledRefresh.cs';s=p.read_text(encoding='utf-8-sig').replace('}catch(Exception e){Check("Exception"', 'File.WriteAllText(Path.Combine(Folder,"controlled-results.json"),JsonSerializer.Serialize(results,HomeJson.Default.ListHomeResult));\n await SeekAot.Run();\n }catch(Exception e){Check("Exception"');p.write_text(s,encoding='utf-8')
p=fixture/'ViewModels/Core/PlayerViewModel/PlayerViewModel.Playback.cs'
s=p.read_text(encoding='utf-8-sig').replace('            if (succeeded && isCurrent()', '            SeekAot.MediaConfirmed(position, succeeded);\n            if (succeeded && isCurrent()')
p.write_text(s,encoding='utf-8')
