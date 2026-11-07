"""Extract an allowlisted, numbered evidence excerpt from the original attachment.

Never copies the original file. Removes complete URLs AND bare signed media
filenames/query strings. Inspect the resulting excerpt before publishing it.
"""
import argparse
import hashlib
from pathlib import Path
import re

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("original")
parser.add_argument("output")
args = parser.parse_args()
raw = Path(args.original).read_bytes()
expected = "6971b66139e3f9037ac852eecf985afba0baf652232c578d813fff58423862aa"
if hashlib.sha256(raw).hexdigest() != expected:
    raise SystemExit("Not the inspected attachment; refuse fixed-line extraction.")
lines = raw.decode("utf-8-sig").splitlines()
selected = []
for number, line in enumerate(lines, 1):
    # Fixed evidence windows from the independently inspected 1718-line attachment.
    if number in (1032, 1088, 1093, 1095, 1097, 1099, 1111, 1165, 1167, 1181, 1217, 1233, 1717) or 1610 <= number <= 1672:
        if not line.strip():
            continue
        line = re.sub(r"https?://[^\s\"']+", "<URL-REDACTED>", line)
        line = re.sub(r"[^\s\"']+\?[^\s\"']+", "<MEDIA-QUERY-REDACTED>", line)
        selected.append(f"L{number}: {line}")
header = f"Original SHA-256: {hashlib.sha256(raw).hexdigest()}\nOriginal logical lines: {len(lines)}\n\n"
Path(args.output).write_text(header + "\n".join(selected) + "\n", encoding="utf-8")
