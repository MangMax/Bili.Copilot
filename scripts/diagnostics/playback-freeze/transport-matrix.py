"""Reproduce selected #184 transport cases with existing binaries; no downloads.

Use --case repeatedly for adaptive subsets. TLS cases need a loopback cert/key.
Each case has a separate process/DLL handle and a 65-second outer safety limit.
"""
import argparse
import concurrent.futures
from pathlib import Path
import subprocess
import sys
from repro import generate


LAVF = ['--option', 'curl-enabled=no', '--option',
        'stream-lavf-o=reconnect=1,reconnect_on_network_error=1,reconnect_delay_max=16,reconnect_max_retries=6,reconnect_delay_total_max=30']
EVICT = ['--option', 'demuxer-max-back-bytes=256KiB', '--option', 'demuxer-donate-buffer=no']


def cases(tls):
    result = {
        'default': [],
        'retry20': ['--option', 'curl-max-retries=20'],
        'retry100': ['--option', 'curl-max-retries=100'],
        'attempts-default': ['--fault', 'attempts'],
        'attempts-20': ['--fault', 'attempts', '--option', 'curl-max-retries=20'],
        'range-outage': ['--option', 'curl-max-request-size=128KiB'],
        'range-limit-default-v2': ['--fault', 'request-limit'],
        'range-limit-bounded-v2': ['--fault', 'request-limit', '--option', 'curl-max-request-size=128KiB'],
        'recycle-default': ['--fault', 'recycle'],
        'recycle-bounded': ['--fault', 'recycle', '--option', 'curl-max-request-size=128KiB'],
        'h2-control': tls + ['--fault', 'none'],
        'h2-reset': tls + ['--fault', 'h2-reset'],
        'h2-reset-20': tls + ['--fault', 'h2-reset', '--option', 'curl-max-retries=20'],
        'h1-reset-control': tls + ['--fault', 'h2-reset', '--option', 'curl-http-version=1.1'],
        'h2-outage': tls,
        'h1-outage': tls + ['--option', 'curl-http-version=1.1'],
        'lavf-default': ['--option', 'curl-enabled=no'],
        'lavf-short': ['--option', 'curl-enabled=no', '--release', '3'],
        'lavf-long': LAVF + ['--seconds', '38'],
        'lavf-small-total': ['--option', 'curl-enabled=no', '--option',
                            'stream-lavf-o=reconnect=1,reconnect_delay_max=16,reconnect_max_retries=6,reconnect_delay_total_max=2'],
        'app-within': ['--action', '14:app:4'],
        'app-beyond': ['--action', '14:app:14'],
        'app-before-exhaust': ['--option', 'curl-max-retries=100', '--action', '0.3:app:14'],
        'seek-exact': ['--action', '14:absolute+exact:14'],
        'seek-keyframe': ['--action', '14:absolute+keyframes:14'],
        'seek-relative': ['--action', '14:relative:2'],
        'app-consumed': EVICT + ['--action', '14:app:2', '--seconds', '38'],
        'app-consumed-h2': tls + EVICT + ['--action', '14:app:2', '--seconds', '38'],
        'app-consumed-early': EVICT + ['--action', '10:app:2', '--action', '20:app:2', '--seconds', '44'],
        'app-consumed-beyond': EVICT + ['--action', '14:app:14'],
        'app-consumed-within': EVICT + ['--action', '14:app:7'],
        'seek-consumed-exact': EVICT + ['--action', '14:absolute+exact:2', '--seconds', '38'],
        'seek-consumed-keyframe': EVICT + ['--action', '14:absolute+keyframes:2', '--seconds', '38'],
        'seek-consumed-relative': EVICT + ['--action', '14:relative:-12', '--seconds', '38'],
        'lavf-fault-pause': LAVF + ['--action', '4:pause:yes', '--action', '30:pause:no', '--seconds', '50'],
    }
    for label, options in {
        'retry20': ['--option', 'curl-max-retries=20'],
        'range': ['--option', 'curl-max-request-size=128KiB'],
        'h1': tls + ['--option', 'curl-http-version=1.1'],
        'lavf': LAVF,
    }.items():
        result[label + '-normal'] = options + ['--fault', 'none']
        result[label + '-pause'] = options + ['--fault', 'none', '--action', '4:pause:yes', '--action', '7:pause:no', '--seconds', '12']
        result[label + '-stop'] = options + ['--release', '999', '--action', '3:stop:0', '--seconds', '6']
        result[label + '-permanent'] = options + ['--release', '999', '--seconds', '40' if label == 'lavf' else '12']
    return result


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for name in ['mpv', 'ffmpeg', 'out', 'media']:
        p.add_argument('--' + name, required=True)
    p.add_argument('--cert')
    p.add_argument('--key')
    p.add_argument('--case', action='append')
    p.add_argument('--jobs', type=int, default=1)
    a = p.parse_args()
    root = Path(a.out).resolve()
    root.mkdir(parents=True, exist_ok=True)
    media = Path(a.media)
    media.mkdir(parents=True, exist_ok=True)
    generate(a.ffmpeg, media, 24)
    matrix = cases(['--cert', a.cert, '--key', a.key] if a.cert and a.key else [])
    selected = a.case or list(matrix)
    if any(n.startswith(('h1', 'h2')) or n.endswith('-h2') for n in selected) and not (a.cert and a.key):
        p.error('TLS comparison requires --cert and --key; HTTP/1-only is not an HTTP/2 test')
    common = [sys.executable, str(Path(__file__).with_name('transport.py')), '--mpv', a.mpv,
              '--ffmpeg', a.ffmpeg, '--media', a.media]

    def run(name):
        with (root / (name + '-runner.txt')).open('w', encoding='utf8') as f:
            run = subprocess.run(common + ['--out', str(root / name)] + matrix[name],
                                 stdout=f, stderr=subprocess.STDOUT, timeout=65)
        print(name, run.returncode, flush=True)
        return run.returncode

    with concurrent.futures.ThreadPoolExecutor(max_workers=a.jobs) as pool:
        codes = list(pool.map(run, selected))
    if any(codes):
        raise SystemExit(1)


if __name__ == '__main__':
    main()
