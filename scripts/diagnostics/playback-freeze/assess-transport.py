"""Assert real-DLL transport outcomes, not successful option/seek submissions.

--expect-progress CASE is the same red/green oracle for baseline and mitigation.
Without it, check the retained matrix and optionally emit compact public evidence.
"""
import argparse
import importlib.util
import json
from pathlib import Path
import re
import statistics
from transport import summarize


def frames(d):
    result = []
    for e in d['events']:
        m = re.search(r'pts_time:([0-9.]+).*?checksum:([0-9A-F]+)', e.get('text', ''))
        if m:
            result.append((e['t'], float(m[1]), m[2]))
    return result


def requests(d):
    return [r for r in d['requests'] if r.get('path') == '/video.mp4']


def progress(d):
    assert len({f[2] for f in frames(d) if f[1] >= 18}) >= 30, 'No 30 distinct decoded frames at/after PTS 18'


def stalled(d):
    assert max(f[1] for f in frames(d)) < 9, 'Unexpected video progression beyond truncation'
    assert max(s['audio-pts'] or 0 for s in d['samples']) > 10, 'Separate audio did not continue'


def compact(d):
    s = summarize(d)
    spacing = s.pop('attempt_spacing')
    s['attempt_spacing_seconds'] = dict(min=min(spacing, default=0), median=statistics.median(spacing) if spacing else 0, max=max(spacing, default=0))
    s['final'] = {k: s['last'][k] for k in ['time-pos', 'eof-reached', 'pause', 'idle-active']}
    del s['last']
    s['effective'] = d['effective']
    s['overrides'] = {k: v for k, v in d['options'].items() if k.startswith(('curl-', 'stream-lavf-', 'demuxer-donate-', 'demuxer-max-back-'))}
    s['fault'] = {k: d['fixture'][k] for k in ['fault', 'release', 'failures', 'limit', 'tls']}
    # Columns make repeated retries compact while preserving exact observed offsets/times.
    s['request_columns'] = ['since_first_video', 'first', 'last', 'sent', 'truncated', 'connection', 'stream', 'alpn']
    s['requests'] = [[r.get(k) for k in s['request_columns']] for r in requests(d)]
    s['actions'] = [{k: a[k] for k in ['t', 'kind', 'value', 'call_seconds']} | {
        'cache_before': {k: (a['cache_before'] or {}).get(k) for k in ['bof-cached', 'eof-cached', 'seekable-ranges']},
        'new_frame_pts_after': sorted({f[1] for f in frames(d) if f[0] > a['t'] and f[1] > 8})[-1:]
    } for a in d['actions']]
    return s


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('root')
    p.add_argument('--output')
    p.add_argument('--expect-progress')
    a = p.parse_args()
    root = Path(a.root)
    read = lambda n: json.loads((root / n / 'result.json').read_text(encoding='utf8'))
    if a.expect_progress:
        progress(read(a.expect_progress))
        print('PASS decoded progression:', a.expect_progress)
        return
    spec = importlib.util.spec_from_file_location('matrix', Path(__file__).with_name('transport-matrix.py'))
    matrix = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(matrix)
    data = {n: read(n) for n in matrix.cases([])}
    for name, d in data.items():
        assert d['hashes'] == data['default']['hashes'], (name, 'binaries/media differ')
        assert d['close_seconds'] < 2, (name, 'slow destruction')
        assert not any('overflow' in e.get('text', '').lower() for e in d['events']), (name, 'log overflow')
    for n in ['default', 'retry20', 'retry100', 'range-outage', 'attempts-default',
              'range-limit-default-v2', 'h2-reset', 'h2-outage', 'h1-outage', 'lavf-default',
              'lavf-small-total', 'app-within', 'app-beyond', 'seek-exact', 'seek-keyframe',
              'seek-relative', 'app-consumed-beyond', 'app-consumed-within']:
        stalled(data[n])
    for n in ['attempts-20', 'range-limit-bounded-v2', 'recycle-default', 'recycle-bounded',
              'h2-control', 'h1-reset-control', 'h2-reset-20', 'lavf-short', 'lavf-long',
              'app-consumed', 'app-consumed-h2', 'app-consumed-early', 'seek-consumed-exact',
              'seek-consumed-keyframe', 'seek-consumed-relative', 'lavf-fault-pause']:
        progress(data[n])
    for n, count in [('default', 6), ('retry20', 21), ('retry100', 101)]:
        r = requests(data[n])
        assert len(r) == count and r[-1]['since_first_video'] < 5, (n, 'retry timing/count')
        assert len({x['first'] for x in r[1:]}) == 1, (n, 'retry offset')
    for n in ['range-limit-bounded-v2', 'recycle-bounded', 'range-normal']:
        r = requests(data[n])
        assert all(x['last'] - x['first'] + 1 <= 131072 and not x['truncated'] for x in r)
        assert sum(x['sent'] for x in r) == 2751298, (n, 'complete known synthetic video')
    for n in ['h2-control', 'h2-reset', 'h2-reset-20', 'h2-outage', 'app-consumed-h2']:
        assert all(x['protocol'] == '2.0' and x['alpn'] == 'h2' for x in requests(data[n])), n
    for n in ['h1-reset-control', 'h1-outage', 'h1-normal']:
        assert all(x['protocol'] == '1.1' for x in requests(data[n])), n
    for n in ['app-within', 'app-beyond', 'seek-exact', 'seek-keyframe', 'seek-relative',
              'app-consumed-beyond', 'app-consumed-within']:
        assert max(r['since_first_video'] for r in requests(data[n])) < 1, (n, 'unexpected reconnect')
    for n in ['app-consumed', 'app-consumed-h2', 'app-consumed-early', 'seek-consumed-exact', 'seek-consumed-keyframe']:
        d = data[n]
        for action in d['actions']:
            assert action['cache_before']['seekable-ranges'][0]['start'] > float(action['value'])
        assert any(r['since_first_video'] > 12 and r['first'] > 0 and not r['truncated'] for r in requests(d)), n
        assert d['samples'][-1]['eof-reached'] == 'yes', (n, 'natural finish after seek')
    early = data['app-consumed-early']
    assert any(9 < r['since_first_video'] < 12 and r['truncated'] for r in requests(early))
    assert not any(12 < r['since_first_video'] < 19 for r in requests(early))
    assert not any(12 < t < 19 and pts > 8 for t, pts, _ in frames(early))
    pre = data['app-before-exhaust']
    assert not pre['actions'][0]['cache_before']['eof-cached']
    assert any(r['first'] > 1000000 for r in requests(pre)), 'Pre-exhaustion seek did not reach a new offset'
    assert max(f[1] for f in frames(pre)) < 9
    for label in ['retry20', 'range', 'h1', 'lavf']:
        normal = data[label + '-normal']
        assert len(frames(normal)) == 720 and normal['samples'][-1]['eof-reached'] == 'yes'
        assert max(r['since_first_video'] for r in requests(normal)) < 2, (label, 'EOF reconnect')
        paused = data[label + '-pause']
        positions = [s['time-pos'] for s in paused['samples'] if 4.5 < s['t'] < 6.8]
        assert max(positions) - min(positions) < .02, (label, 'pause drift')
        assert paused['samples'][-1]['time-pos'] > 8, (label, 'resume')
        stop = data[label + '-stop']
        assert stop['samples'][-1]['idle-active'] == 'yes'
        assert stop['actions'][0]['call_seconds'] < 1
        assert max(r['since_first_video'] for r in requests(stop)) < 3.5, (label, 'requests after cancel')
        permanent = data[label + '-permanent']
        assert max(f[1] for f in frames(permanent)) < 9
        assert max(r['since_first_video'] for r in requests(permanent)) < (29 if label == 'lavf' else 3)
        assert len(requests(permanent)) <= (21 if label == 'retry20' else 12)
    fault_pause = data['lavf-fault-pause']
    paused_positions = [s['time-pos'] for s in fault_pause['samples'] if 5 < s['t'] < 29]
    assert max(paused_positions) - min(paused_positions) < .02
    assert all(s['pause'] == 'yes' for s in fault_pause['samples'] if 5 < s['t'] < 29)
    assert 25 < requests(data['lavf-long'])[-1]['since_first_video'] < 29
    assert summarize(data['lavf-long'])['max_frame_gap'] > 15, 'Do not call this seamless'
    # Total-delay option is consulted between waits; measured behavior can overshoot it.
    assert len(requests(data['lavf-small-total'])) < len(requests(data['lavf-long']))
    summary = dict(versions=data['default']['versions'], hashes=data['default']['hashes'],
                   cases={n: compact(d) for n, d in data.items()})
    if a.output:
        # One case per line avoids thousands of lines for repeated retry tuples.
        header = json.dumps({k: v for k, v in summary.items() if k != 'cases'}, indent=2)[:-2]
        body = ',\n'.join('    ' + json.dumps(n) + ': ' + json.dumps(v, separators=(',', ':'))
                          for n, v in summary['cases'].items())
        Path(a.output).write_text(header + ',\n  "cases": {\n' + body + '\n  }\n}\n', encoding='utf8')
    print(f'PASS: {len(data)} real-DLL cases; transport, TLS negotiation, decoded frames, seek, pause/cancel and finite no-progress retries')


if __name__ == '__main__':
    main()
