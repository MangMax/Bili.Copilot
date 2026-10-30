"""Read only the authorized full dump; reuse the retained bounded parser."""
import pathlib
import hashlib

ROOT = pathlib.Path(__file__).resolve().parents[3]
DUMP = pathlib.Path('C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-bc1a3769/artifacts/exit-reproduction/original1/crash-full.dmp')
OUT = ROOT / 'artifacts/exit-full-analysis'
OUT.mkdir(parents=True, exist_ok=True)
assert DUMP.stat().st_size == 1550491329
with DUMP.open('rb') as f:
    assert hashlib.file_digest(f, 'sha256').hexdigest() == 'eeab35bb591c37016da7ec29ceb607a55ece8d6135d08e5d4511704d8a1a0d3d'
source = (ROOT / 'docs/performance-acceptance/net11-exit-reproduction/parse_dump.py').read_text()
source = source.replace("root=pathlib.Path('artifacts/exit-reproduction/original1')", 'root=OUT')
source = source.replace("P=root/'crash-full.dmp'", 'P=DUMP')
exec(compile(source, 'retained_parse_dump.py', 'exec'))
