"""Add deterministic source-sized fixtures; retain the original oversized PNG mode."""
from pathlib import Path
import struct, zlib
root=Path(__file__).resolve().parents[3]
h=root/'artifacts/recycled-cards/host'
p=h/'Models.cs';s=p.read_text(encoding='utf-8-sig')
start=s.index('public partial class ArticleItemViewModel')
end=s.index('public partial class UserItemViewModel',start)
part=s[start:end]
if 'public static bool Realistic' not in part:
 part=part.replace('public ArticleCardStyle Style', 'public static bool Realistic;\n        public int Index { get; }\n        public ArticleItemViewModel(int index=0) { Index=index; }\n        public ArticleCardStyle Style')
 part=part.replace('"Assets","Cover.png"','"Assets",Realistic ? $"article-{Index}.png" : "Cover.png"')
 part=part.replace('public Uri Avatar => Cover;', 'public Uri Avatar => Realistic ? new(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath,"Assets",$"article-avatar-{Index}.png")) : Cover;')
 part=part.replace('public string Title => "Article local fixture with a wrapping title";', 'public string Title => Realistic ? $"Article local fixture with a wrapping title {Index:0000}" : "Article local fixture with a wrapping title";')
 p.write_text(s[:start]+part+s[end:],encoding='utf-8')
p=h/'ExtraModels.cs';s=p.read_text(encoding='utf-8-sig')
if 'public static bool Realistic' not in s:
 s=s.replace('public static bool Unique;','public static bool Unique;\n    public static bool Realistic;')
 s=s.replace('Unique ? $"season-{Index}.png" : "Cover.png"','Realistic ? $"poster-{Index}.png" : Unique ? $"season-{Index}.png" : "Cover.png"')
 s=s.replace('public string Title=>Unique ?', 'public string Title=>(Unique || Realistic) ?')
 p.write_text(s,encoding='utf-8')
def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data))
for i in range(500):
 color=bytes((40+i%180,40+(i//180)*50,80+(i*37)%140,255))
 for prefix,w,height in [('article',400,220),('article-avatar',48,48),('poster',320,426)]:
  raw=(b'\0'+color*w)*height
  png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,height,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b'')
  (h/'Assets'/f'{prefix}-{i}.png').write_bytes(png)
print('Added source-sized article 400x220 / avatar 48x48 / season 320x426 PNG fixtures')
