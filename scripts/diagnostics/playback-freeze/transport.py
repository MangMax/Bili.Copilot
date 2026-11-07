"""Existing DLL transport/seek experiments; no app or native source changes.

Requires Python + Node, existing libmpv/ffmpeg, and optional scratch TLS cert.
All artifacts go to --out. Fault timing is relative to first video request.
"""
import argparse
import ctypes as C
import hashlib
import json
from pathlib import Path
import re
import subprocess
import time
from repro import Mpv, generate


def run(a):
    out = Path(a.out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    media = Path(a.media).resolve()
    media.mkdir(parents=True, exist_ok=True)
    generate(a.ffmpeg, media, 24)
    ledger = out / 'requests.jsonl'
    ledger.write_text('', encoding='utf8')
    fixture = dict(media=str(media), ledger=str(ledger), fault=a.fault, release=a.release,
                   failures=a.failures, limit=a.limit, tls=bool(a.cert), cert=a.cert, key=a.key)
    config = out / 'fixture.json'
    config.write_text(json.dumps(fixture), encoding='utf8')
    errors = (out / 'server-stderr.txt').open('w', encoding='utf8')
    server = subprocess.Popen(['node', str(Path(__file__).with_name('transport-server.cjs')), str(config)],
                              stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=errors, text=True)
    player = None
    try:
        address = json.loads(server.stdout.readline())
        base = ('https' if a.cert else 'http') + f"://127.0.0.1:{address['port']}"
        options = {'config': 'no', 'load-scripts': 'no', 'vo': 'null', 'ao': 'null', 'hwdec': 'no',
                   'idle': 'yes', 'keep-open': 'yes', 'cache': 'yes', 'demuxer-max-bytes': '300MiB',
                   'demuxer-readahead-secs': '360', 'audio-files': base + '/audio.m4a',
                   'terminal': 'no', 'vf': 'lavfi=[showinfo]'}
        if a.cert:
            options['tls-verify'] = 'no'  # Only this loopback diagnostic player.
        options.update(x.split('=', 1) for x in a.option)
        player = Mpv(a.mpv, options)
        player.lib.mpv_set_property.argtypes = [C.c_void_p, C.c_char_p, C.c_int, C.c_void_p]
        player.lib.mpv_set_property.restype = C.c_int
        effective = {k: player.get('options/' + k) for k in [
            'curl-enabled', 'curl-max-retries', 'curl-max-request-size', 'curl-http-version',
            'demuxer-max-back-bytes', 'stream-lavf-o', 'hr-seek']}
        versions = {k: player.get(k) for k in ['mpv-version', 'ffmpeg-version']}
        samples, events, actions = [], [], []
        # action = wall:kind:destination; app = exact pinned wrapper native call.
        pending = [x.split(':') for x in a.action]
        started = time.monotonic()
        player.command('loadfile', base + '/video.mp4')
        while time.monotonic() - started < a.seconds:
            t = round(time.monotonic() - started, 4)
            while pending and t >= float(pending[0][0]):
                _, kind, value = pending.pop(0)
                before = player.get('demuxer-cache-state')
                action_start = time.monotonic()
                if kind == 'app':
                    position = C.c_double(float(value))
                    # MPV_FORMAT_DOUBLE = 5; SetCurrentPositionAsync uses SetProperty.
                    player.check(player.lib.mpv_set_property(player.handle, b'time-pos', 5, C.byref(position)), 'app seek')
                elif kind in ['absolute', 'absolute+exact', 'absolute+keyframes', 'relative', 'relative+exact']:
                    player.command('seek', value, kind)
                elif kind == 'pause':
                    player.set('pause', value)
                elif kind == 'stop':
                    player.command('stop')
                else:
                    raise ValueError(kind)
                actions.append(dict(t=t, kind=kind, value=value, cache_before=before,
                                    call_seconds=time.monotonic() - action_start))
            events.extend(dict(t=t, **e) for e in player.events())
            samples.append(dict(t=t, **{k: player.get(k) for k in [
                'time-pos', 'video-pts', 'audio-pts', 'pause', 'paused-for-cache', 'eof-reached',
                'idle-active', 'demuxer-cache-state']}))
            time.sleep(.05)
        close_start = time.monotonic()
        player.close()
        player = None
        close_seconds = time.monotonic() - close_start
        requests = [json.loads(x) for x in ledger.read_text(encoding='utf8').splitlines()]
        # Align server times with player load: first video request is shared epoch.
        first = next(r['t'] for r in requests if r.get('path') == '/video.mp4')
        for r in requests:
            r['since_first_video'] = round(r['t'] - first, 6)
        result = dict(fixture=fixture, options=options, effective=effective, versions=versions,
                      hashes={k: hashlib.sha256(Path(v).read_bytes()).hexdigest() for k, v in
                              [('mpv', a.mpv), ('ffmpeg', a.ffmpeg), ('video', media / 'video.mp4'), ('audio', media / 'audio.m4a')]},
                      requests=requests, actions=actions, samples=samples, events=events, close_seconds=close_seconds)
        (out / 'result.json').write_text(json.dumps(result, indent=2), encoding='utf8')
        (out / 'mpv.log').write_text('\n'.join(f"{e['t']} [{e.get('prefix', 'event')}] {e.get('text', e['event'])}" for e in events), encoding='utf8')
        print(json.dumps(summarize(result)))
    finally:
        if player:
            player.close()
        server.terminate()
        server.wait(timeout=5)
        errors.close()


def summarize(d):
    frames = []
    for e in d['events']:
        m = re.search(r'pts_time:([0-9.]+).*?checksum:([0-9A-F]+)', e.get('text', ''))
        if m:
            frames.append((e['t'], float(m[1]), m[2]))
    req = [r for r in d['requests'] if r.get('path') == '/video.mp4']
    times = [r['since_first_video'] for r in req]
    return dict(frames=len(frames), last_frame=frames[-1][:2] if frames else None,
                unique_late=len({f[2] for f in frames if f[1] >= 18}),
                max_frame_gap=max((b[0]-a[0] for a,b in zip(frames, frames[1:])), default=0),
                video_requests=len(req), request_span=max(times, default=0),
                attempt_spacing=[round(b-a, 6) for a,b in zip(times, times[1:])],
                protocols=sorted(set(r['protocol'] for r in req)),
                last=d['samples'][-1], actions=d['actions'], close_seconds=d['close_seconds'])


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ['mpv', 'ffmpeg', 'out', 'media']:
        p.add_argument('--' + name, required=True)
    p.add_argument('--fault', choices=['none', 'outage', 'attempts', 'request-limit', 'recycle', 'h2-reset'], default='outage')
    p.add_argument('--release', type=float, default=12)
    p.add_argument('--failures', type=int, default=10)
    p.add_argument('--limit', type=int, default=262144)
    p.add_argument('--seconds', type=float, default=26)
    p.add_argument('--cert')
    p.add_argument('--key')
    p.add_argument('--option', action='append', default=[])
    p.add_argument('--action', action='append', default=[])
    run(p.parse_args())
