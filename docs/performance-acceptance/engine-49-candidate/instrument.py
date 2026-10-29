"""Test-only dequeue barriers in the actual source; never part of candidate packages."""
from pathlib import Path
import sys

root = Path(sys.argv[1])
for project, file in [('Core', 'DanmakuScheduler.cs'), ('Legacy', 'DanmakuFrostMaster.cs')]:
    p = root / 'src' / ('Danmaku.' + project) / file
    s = p.read_text(encoding='utf-8-sig')
    marker = 'private volatile bool _isSeeking;'
    assert '_testDequeued' not in s
    s = s.replace(marker, marker + '\n    internal Action _testDequeued = null;\n    internal Action _testIdle = null;')
    worker = s.index('private void Updater')
    pos = s.index('_updateEvent.WaitOne();', worker)
    s = s[:pos] + '_testIdle?.Invoke();\n                ' + s[pos:]
    start = s.index('lock (_updateTimeQueue)', pos)
    brace = s.index('{', start)
    end, depth = brace + 1, 1
    while depth:
        depth += (s[end] == '{') - (s[end] == '}')
        end += 1
    s = s[:end] + '\n                _testDequeued?.Invoke();\n' + s[end:]
    pos = s.index('finally', worker)
    brace = s.index('{', pos)
    s = s[:brace + 1] + '\n            _testIdle?.Invoke();' + s[brace + 1:]
    p.write_text(s, encoding='utf-8-sig', newline='\r\n')
