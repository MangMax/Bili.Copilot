"""Assert native-boundary counterexamples, not a production recovery detector.

Input is the private real-DLL matrix described in spec-184-native-boundary.md.
Output contains only synthetic aggregate evidence, never URLs or bulk logs.
"""
import argparse
import json
from pathlib import Path
import re
import struct


def packet_durations(path):
    """Read generated MP4 mdhd/stts; independently verify held-frame durations."""
    values = {}

    def visit(data):
        offset = 0
        while offset + 8 <= len(data):
            size, name = struct.unpack_from(">I4s", data, offset)
            assert size >= 8 and offset + size <= len(data), "invalid generated MP4 atom"
            payload = data[offset + 8:offset + size]
            if name in (b"moov", b"trak", b"mdia", b"minf", b"stbl"):
                visit(payload)
            elif name == b"mdhd":
                assert payload[0] == 0, "fixture requires mdhd version 0"
                values["timescale"] = struct.unpack_from(">I", payload, 12)[0]
            elif name == b"stts":
                count = struct.unpack_from(">I", payload, 4)[0]
                values["runs"] = [struct.unpack_from(">II", payload, 8 + i * 8) for i in range(count)]
            offset += size

    visit(path.read_bytes())
    return [duration / values["timescale"] for count, duration in values["runs"] for _ in range(count)]


def rejected_cache_predicate(sample):
    # Exactly the previously rejected predicate, used only to demonstrate that
    # adding an unattributed terminal error does not repair its false positive.
    cache = sample.get("demuxer-cache-state") or {}
    end = max((r["end"] for r in cache.get("seekable-ranges", [])), default=None)
    pos, duration = sample["time-pos"], sample["duration"]
    return (sample["pause"] == "no" and sample["paused-for-cache"] == "no"
            and sample["eof-reached"] == "no" and end is not None
            and pos is not None and duration is not None and cache.get("eof-cached")
            and cache.get("fw-bytes") == 0 and duration - end > 2
            and pos > end + 0.5 and pos < duration - 2)


def assess(root):
    names = ["fault-debug", "sparse", "variable", "short", "natural", "pause",
             "unavailable", "audio-sparse-late-debug", "padded"]
    results, raw = {}, {}
    for name in names:
        data = json.loads((root / name / "result.json").read_text(encoding="utf-8"))
        raw[name] = data
        assert not any(e["event"] == 24 for e in data["events"]), "native event queue overflow"
        frames = []
        for event in data["events"]:
            match = re.search(r"pts_time:([0-9.]+).*?checksum:([0-9A-F]+)", event.get("text", ""))
            if match:
                frames.append((float(match[1]), match[2]))
        failures = [e for e in data["events"] if "transfer failed:" in e.get("text", "")]
        eof = [e for e in data["events"] if e.get("prefix") == "cplayer"
               and e.get("text") == "video EOF (status=4)"]
        candidates = [s for s in data["samples"] if rejected_cache_predicate(s)]
        corroborated = [s for s in candidates if any(e["t"] <= s["t"] for e in failures)]
        last = data["samples"][-1]
        props = data["metadata"]["property-list"]
        props = props.split(",") if isinstance(props, str) else props
        assert "video-status" not in props and "video-eof" not in props
        for sample in data["samples"]:
            assert all(sample[k] is None for k in ["video-status", "video-eof",
                       "video-frame-info/duration", "current-tracks/video/eof"])
        assert not any(a["action"] == "automatic reopen" for a in data["actions"])
        results[name] = {
            "frames": len(frames), "last_frame_pts": frames[-1][0],
            "distinct_frames": len({checksum for _, checksum in frames}),
            "max_audio_pts": max((s["audio-pts"] for s in data["samples"] if s["audio-pts"] is not None), default=None),
            "duration": last["duration"], "cache_end": last["demuxer-cache-state"].get("cache-end"),
            "file_size": last["file-size"], "stream_pos": last["stream-pos"],
            "global_eof": last["eof-reached"],
            "terminal_failure_prefixes": sorted({e["prefix"] for e in failures}),
            "first_native_video_eof_wall": eof[0]["t"] if eof else None,
            "first_generic_error_plus_cache_match": corroborated[0]["t"] if corroborated else None,
            "first_match_position": corroborated[0]["time-pos"] if corroborated else None,
            "video_truncations": sum(r["truncated"] for r in data["requests"] if r["path"] == "/video.mp4"),
            "audio_truncations": sum(r["truncated"] for r in data["requests"] if r["path"] == "/audio.m4a"),
        }
    for name in ["fault-debug", "unavailable"]:
        r = results[name]
        assert r["last_frame_pts"] < 9 and r["max_audio_pts"] > 20
        assert r["video_truncations"] > 0 and r["audio_truncations"] == 0
        assert r["first_generic_error_plus_cache_match"] is not None
    for name, count, pts in [("sparse", 2, 10), ("variable", 4, 13), ("short", 240, 7.966667),
                             ("natural", 720, 23.966667), ("padded", 2, 10), ("audio-sparse-late-debug", 2, 10)]:
        r = results[name]
        assert r["frames"] == count and abs(r["last_frame_pts"] - pts) < 0.00001
        assert r["video_truncations"] == 0 and r["global_eof"] == "yes"
    assert packet_durations(root / "sparse" / "video.mp4") == [10, 10]
    assert packet_durations(root / "variable" / "video.mp4") == [1, 2, 10, 1]
    audio = results["audio-sparse-late-debug"]
    assert audio["audio_truncations"] > 0 and audio["first_generic_error_plus_cache_match"] is not None
    assert audio["terminal_failure_prefixes"] == results["fault-debug"]["terminal_failure_prefixes"] == ["curl"]
    assert audio["first_native_video_eof_wall"] is not None
    assert audio["first_native_video_eof_wall"] < audio["first_generic_error_plus_cache_match"]
    assert results["padded"]["stream_pos"] == results["sparse"]["stream_pos"]
    assert results["padded"]["file_size"] - results["sparse"]["file_size"] == 1048584
    pause = [s for s in raw["pause"]["samples"] if s["t"] > 6]
    assert all(s["pause"] == "yes" for s in pause)
    assert max(s["time-pos"] for s in pause) - min(s["time-pos"] for s in pause) < 0.01
    assert results["pause"]["first_generic_error_plus_cache_match"] is None
    unfiltered = json.loads((root / "audio-sparse-no-filter" / "result.json").read_text(encoding="utf-8"))
    assert unfiltered["metadata"]["options"]["vf"] == ""
    assert unfiltered["metadata"]["media"]["/video.mp4"] == raw["sparse"]["metadata"]["media"]["/video.mp4"]
    assert all(not r["truncated"] for r in unfiltered["requests"] if r["path"] == "/video.mp4")
    eof = next(e for e in unfiltered["events"] if e.get("prefix") == "cplayer"
               and e.get("text") == "video EOF (status=4)")
    at_eof = min(unfiltered["samples"], key=lambda s: abs(s["t"] - eof["t"]))
    assert at_eof["time-pos"] < 10 and at_eof["duration"] == 20
    assert any(rejected_cache_predicate(s) for s in unfiltered["samples"])
    assert any(e.get("prefix") == "curl" and "transfer failed:" in e.get("text", "") for e in unfiltered["events"])
    return {"verdict": "observability gate not passed; no production recovery", "cases": results,
            "without_diagnostic_filter": {"native_video_eof_wall": eof["t"],
                "position_at_native_eof": at_eof["time-pos"], "duration": at_eof["duration"],
                "same_complete_sparse_video": True},
            "binary": {k: raw["fault-debug"]["metadata"][k] for k in ["mpv_sha256", "ffmpeg_sha256", "versions"]},
            "verified_sample_durations": {"sparse": [10, 10], "variable": [1, 2, 10, 1]}}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    text = json.dumps(assess(args.root), indent=2) + "\n"
    if args.output:
        args.output.write_text(text, encoding="utf-8")
    print(text)
