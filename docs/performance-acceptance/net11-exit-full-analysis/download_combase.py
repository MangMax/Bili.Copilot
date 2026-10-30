"""Bounded official symbol download; no dump bytes leave this machine."""
import concurrent.futures
import pathlib
import re
import subprocess

root = pathlib.Path('artifacts/exit-full-analysis/symbols')
url = 'https://msdl.microsoft.com/download/symbols/combase.pdb/4AC91D91223808F3AE6A3164A1B8B4041/combase.pdb'
size, chunk = 83357696, 4194304
parts = root / 'combase-parts'
parts.mkdir(parents=True, exist_ok=True)

def fetch(i):
    start, end = i * chunk, min(size, (i + 1) * chunk) - 1
    path, header = parts / f'{i}.part', parts / f'{i}.headers'
    command = ['curl.exe', '--fail', '--location', '--connect-timeout', '15',
               '--max-time', '180', '--silent', '--show-error', '--range',
               f'{start}-{end}', '--dump-header', str(header), '--output', str(path), url]
    result = subprocess.run(command, capture_output=True, timeout=190)
    ranges = re.findall(r'content-range:\s*bytes\s+(\d+)-(\d+)/(\d+)',
                        header.read_text().lower() if header.exists() else '')
    if result.returncode or not ranges or tuple(map(int, ranges[-1])) != (start, end, size) or path.stat().st_size != end-start+1:
        raise RuntimeError(f'Part {i}: incomplete, wrong range, or curl exit {result.returncode}; not accepted')
    print(f'Validated part {i}: {end-start+1} bytes', flush=True)
    return path

with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    files = list(pool.map(fetch, range((size+chunk-1)//chunk)))
dest = root / 'combase.pdb'
with dest.open('wb') as out:
    for path in files:
        out.write(path.read_bytes())
assert dest.stat().st_size == size
print('Complete transport; RSDS/MSF validation is still required.', flush=True)
