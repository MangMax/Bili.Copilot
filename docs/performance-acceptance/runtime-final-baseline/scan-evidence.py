"""Scan delivered decoded text without printing any suspected values."""
import gzip, json, re, urllib.parse
from prepare import HERE, save

patterns={
    'signed-media-query': re.compile(r'(?:[?&]|&amp;)(?:upsig|wsSecret|uparams|osig|trid|deadline|nbs|gen|uipk)=[^\s"<>]+',re.I),
    'credential-value': re.compile(r'(?:SESSDATA|bili_jct|access_token|refresh_token)\s*[=:]\s*["\']?[A-Za-z0-9_%.-]{12,}',re.I),
}
hits=[];count=0
for row in json.loads((HERE/'ledger.json').read_text(encoding='utf-8')):
    if 'archive' not in row:continue
    raw=gzip.decompress((HERE/row['archive']).read_bytes())
    text=raw.decode('utf-8-sig',errors='replace')
    decoded=urllib.parse.unquote(text.replace('\\u0026','&').replace('\\u003D','='))
    count+=1
    for category,pattern in patterns.items():
        if pattern.search(decoded):hits.append({'path':row['path'],'category':category})
save(HERE/'delivery-scan.json',{'decodedTextFiles':count,'hits':hits,'scope':'Pattern scan plus separately recorded individual PNG inspection; not proof of absence of every possible secret.'})
print(json.dumps({'decodedTextFiles':count,'hits':hits}))
raise SystemExit(1 if hits else 0)
