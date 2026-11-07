"""Controlled split-track truncation against real libmpv (Python stdlib only).

No remote media, credentials, application settings, or production changes.
Outputs generated media and diagnostic JSON/logs to the explicit --out directory.
See docs/research/spec-182-playback-freeze.md for interpretation and limitations.
"""
import argparse
import ctypes as C
import hashlib
import http.server
import json
from pathlib import Path
import re
import socket
import subprocess
import threading
import time


class Event(C.Structure):
    _fields_ = [("event_id", C.c_int), ("error", C.c_int),
                ("reply_userdata", C.c_uint64), ("data", C.c_void_p)]


class LogMessage(C.Structure):
    _fields_ = [("prefix", C.c_char_p), ("level", C.c_char_p),
                ("text", C.c_char_p), ("log_level", C.c_int)]


class Mpv:
    def __init__(self, dll, options):
        self.lib = C.CDLL(str(Path(dll).resolve()))
        signatures = {
            "mpv_create": (C.c_void_p, []),
            "mpv_initialize": (C.c_int, [C.c_void_p]),
            "mpv_set_option_string": (C.c_int, [C.c_void_p, C.c_char_p, C.c_char_p]),
            "mpv_set_property_string": (C.c_int, [C.c_void_p, C.c_char_p, C.c_char_p]),
            "mpv_get_property_string": (C.c_void_p, [C.c_void_p, C.c_char_p]),
            "mpv_free": (None, [C.c_void_p]),
            "mpv_command": (C.c_int, [C.c_void_p, C.POINTER(C.c_char_p)]),
            "mpv_request_log_messages": (C.c_int, [C.c_void_p, C.c_char_p]),
            "mpv_wait_event": (C.POINTER(Event), [C.c_void_p, C.c_double]),
            "mpv_terminate_destroy": (None, [C.c_void_p]),
        }
        for name, (restype, argtypes) in signatures.items():
            fn = getattr(self.lib, name)
            fn.restype, fn.argtypes = restype, argtypes
        self.handle = self.lib.mpv_create()
        if not self.handle:
            raise RuntimeError("mpv_create failed")
        for name, value in options.items():
            self.check(self.lib.mpv_set_option_string(self.handle, name.encode(), str(value).encode()), name)
        self.check(self.lib.mpv_initialize(self.handle), "initialize")
        self.check(self.lib.mpv_request_log_messages(self.handle, b"v"), "logging")

    @staticmethod
    def check(result, operation):
        if result < 0:
            raise RuntimeError(f"mpv {operation}: {result}")

    def get(self, name):
        ptr = self.lib.mpv_get_property_string(self.handle, name.encode())
        if not ptr:
            return None
        try:
            value = C.string_at(ptr).decode("utf-8", "replace")
            try:
                return json.loads(value)
            except ValueError:
                return value
        finally:
            self.lib.mpv_free(ptr)

    def set(self, name, value):
        self.check(self.lib.mpv_set_property_string(self.handle, name.encode(), str(value).encode()), name)

    def command(self, *args):
        values = (C.c_char_p * (len(args) + 1))(*(str(x).encode() for x in args), None)
        self.check(self.lib.mpv_command(self.handle, values), str(args))

    def events(self):
        result = []
        while True:
            e = self.lib.mpv_wait_event(self.handle, 0).contents
            if not e.event_id:
                return result
            item = {"event": e.event_id, "error": e.error}
            if e.event_id == 2:
                msg = C.cast(e.data, C.POINTER(LogMessage)).contents
                item.update(prefix=msg.prefix.decode(), level=msg.level.decode(), text=msg.text.decode("utf-8", "replace").strip())
            result.append(item)

    def close(self):
        self.lib.mpv_terminate_destroy(self.handle)


def generate(ffmpeg, directory, video_seconds, video_rate=30):
    for name, source, codec in [
        ("video.mp4", f"testsrc2=size=320x180:rate={video_rate}", ["-an", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "20", "-g", "30", "-bf", "0"]),
        ("audio.m4a", "sine=frequency=440:sample_rate=48000", ["-vn", "-c:a", "aac", "-b:a", "128k"]),
    ]:
        path = directory / name
        if not path.exists():
            subprocess.run([ffmpeg, "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", source,
                            "-t", str(video_seconds if name == "video.mp4" else 24), *codec, "-movflags", "+faststart", "-y", str(path)], check=True)


def run(args):
    out = Path(args.out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    generate(args.ffmpeg, out, args.video_seconds, args.video_rate)
    media = {"/video.mp4": (out / "video.mp4").read_bytes(), "/audio.m4a": (out / "audio.m4a").read_bytes()}
    cutoff = len(media["/video.mp4"]) // 3 + 17  # deliberately inside a packet
    started = time.monotonic()
    requests = []
    state = {"released": False, "failures": 0}

    class Handler(http.server.BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def log_message(self, *_):
            pass

        def do_GET(self):
            if self.path not in media:
                self.send_error(404)
                return
            data = media[self.path]
            match = re.fullmatch(r"bytes=(\d+)-(\d*)", self.headers.get("Range", ""))
            first = int(match[1]) if match else 0
            last = min(int(match[2]), len(data) - 1) if match and match[2] else len(data) - 1
            if first >= len(data):
                self.send_error(416)
                return
            broken = (self.path == "/video.mp4" and args.scenario != "control" and
                      not state["released"] and (args.scenario != "transient" or state["failures"] < 1))
            stop = max(first, min(last + 1, cutoff)) if broken else last + 1
            if broken:
                state["failures"] += 1
            entry = {"t": round(time.monotonic() - started, 3), "path": self.path,
                     "range": self.headers.get("Range"), "first": first, "last": last,
                     "sent": stop - first, "truncated": broken}
            requests.append(entry)
            self.send_response(206 if match else 200)
            self.send_header("Content-Type", "video/mp4" if self.path == "/video.mp4" else "audio/mp4")
            self.send_header("Accept-Ranges", "bytes")
            self.send_header("Content-Length", str(last - first + 1))
            if match:
                self.send_header("Content-Range", f"bytes {first}-{last}/{len(data)}")
            self.send_header("Connection", "close")
            self.end_headers()
            try:
                self.wfile.write(data[first:stop])
                self.wfile.flush()
                self.connection.shutdown(socket.SHUT_WR)
            except (ConnectionError, OSError) as exc:
                entry["client_disconnect"] = type(exc).__name__
            self.close_connection = True

    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    base = f"http://127.0.0.1:{server.server_port}"
    options = {"config": "no", "load-scripts": "no", "vo": "null", "ao": "null", "hwdec": "no",
               "idle": "yes", "keep-open": "yes", "cache": "yes", "demuxer-max-bytes": "300MiB",
               "demuxer-readahead-secs": "360", "audio-files": base + "/audio.m4a",
               "terminal": "no", "vf": "lavfi=[showinfo]"}
    for option in args.option:
        key, value = option.split("=", 1)
        options[key] = value
    player = Mpv(args.mpv, options)
    metadata = {"mpv_sha256": hashlib.sha256(Path(args.mpv).read_bytes()).hexdigest(),
                "ffmpeg_sha256": hashlib.sha256(Path(args.ffmpeg).read_bytes()).hexdigest(),
                "media": {key: {"bytes": len(value), "sha256": hashlib.sha256(value).hexdigest()} for key, value in media.items()},
                "cutoff": cutoff, "options": options,
                "versions": {key: player.get(key) for key in ["mpv-version", "ffmpeg-version"]},
                "effective": {key: player.get("options/" + key) for key in ["curl-enabled", "curl-max-retries", "curl-http-version", "demuxer-seekable-cache", "demuxer-max-bytes", "demuxer-readahead-secs", "stream-lavf-o"]}}
    samples, events, actions = [], [], []
    did_action = False
    attempts = 0
    next_attempt = None
    recovery_position = None
    failed = False
    try:
        player.command("loadfile", base + "/video.mp4")
        while time.monotonic() - started < args.seconds:
            now = time.monotonic() - started
            if now >= args.release_at and not state["released"]:
                state["released"] = True
                actions.append({"t": round(now, 3), "action": "server healthy"})
            if now >= args.action_at and args.action != "none" and not did_action:
                if args.action == "seek":
                    player.command("seek", args.seek_to, "absolute+exact")
                elif args.action == "seek-no-cache":
                    player.set("demuxer-seekable-cache", "no")
                    player.command("seek", args.seek_to, "absolute+exact")
                elif args.action == "reload":
                    player.command("loadfile", base + "/video.mp4", "replace", "-1", f"start={args.seek_to}")
                elif args.action == "pause":
                    player.set("pause", "yes")
                actions.append({"t": round(now, 3), "action": args.action, "target": args.seek_to})
                did_action = True
            for event in player.events():
                events.append({"t": round(now, 3), **event})
            sample = {"t": round(now, 3), **{key: player.get(key) for key in
                ["time-pos", "video-pts", "audio-pts", "eof-reached", "pause", "paused-for-cache", "duration", "demuxer-cache-state", "estimated-vf-fps"]}}
            samples.append(sample)
            if args.auto_reopen and not failed:
                cache = sample.get("demuxer-cache-state") or {}
                ranges = cache.get("seekable-ranges") or []
                end = max((r["end"] for r in ranges), default=None)
                position, duration = sample["time-pos"], sample["duration"]
                playing = sample["pause"] == "no" and sample["paused-for-cache"] == "no" and sample["eof-reached"] == "no"
                premature = (playing and end is not None and position is not None and duration is not None
                             and cache.get("eof-cached") and cache.get("fw-bytes") == 0
                             and duration - end > 2 and position > end + 0.5 and position < duration - 2)
                healthy = end is not None and duration is not None and end >= duration - 2
                if healthy:
                    next_attempt = None
                # A failed reopen can have no ranges at all. A deadline belongs only
                # to an already admitted recovery, never to ordinary playback.
                due = next_attempt is not None and now >= next_attempt and sample["pause"] == "no"
                if (premature and next_attempt is None) or due:
                    if attempts == 2:
                        failed = True
                        actions.append({"t": round(now, 3), "action": "automatic recovery exhausted"})
                    else:
                        recovery_position = position if position is not None else recovery_position
                        attempts += 1
                        actions.append({"t": round(now, 3), "action": "automatic reopen", "attempt": attempts,
                                        "target": recovery_position, "cache_end": end, "duration": duration})
                        player.command("loadfile", base + "/video.mp4", "replace", "-1", f"start={recovery_position}")
                        next_attempt = now + 5
            time.sleep(0.1)
    finally:
        player.close()
        server.shutdown()
        server.server_close()
    result = {"scenario": args.scenario, "metadata": metadata, "requests": requests, "actions": actions, "samples": samples, "events": events}
    (out / "result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    (out / "mpv.log").write_text("\n".join(f"{e['t']} [{e.get('prefix', 'event')}] {e.get('text', e['event'])}" for e in events), encoding="utf-8")
    print(json.dumps({"out": str(out), "requests": requests, "actions": actions, "last": samples[-1], "versions": metadata["versions"]}, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mpv", required=True, help="Path to libmpv DLL/shared library")
    parser.add_argument("--ffmpeg", required=True)
    parser.add_argument("--out", required=True, help="Use an untracked scratch directory")
    parser.add_argument("--scenario", choices=["control", "transient", "exhaust"], default="exhaust")
    parser.add_argument("--seconds", type=float, default=22)
    parser.add_argument("--video-seconds", type=float, default=24, help="Use a fresh output directory when changing media duration")
    parser.add_argument("--video-rate", default="30", help="Generated video rate; use a fresh output directory")
    parser.add_argument("--auto-reopen", action="store_true", help="Experimental duration/cache predicate, not production policy")
    parser.add_argument("--release-at", type=float, default=12)
    parser.add_argument("--action-at", type=float, default=14)
    parser.add_argument("--action", choices=["none", "seek", "seek-no-cache", "reload", "pause"], default="none")
    parser.add_argument("--seek-to", type=float, default=14)
    parser.add_argument("--option", action="append", default=[], help="One mpv option override: name=value")
    run(parser.parse_args())
