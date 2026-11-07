"""Summarize real decoder output; optionally assert progression after recovery.

The progressing oracle deliberately fails for the unmodified exhausted-transfer
case. It does not equate global playback time with moving video.
"""
import argparse
import json
from pathlib import Path
import re


def summarize(path):
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    frames = []
    for event in data["events"]:
        match = re.search(r"pts_time:([0-9.]+).*?checksum:([0-9A-F]+)", event.get("text", ""))
        if match:
            frames.append((event["t"], float(match[1]), match[2]))
    late = [frame for frame in frames if frame[0] >= 18 and frame[1] >= 18]
    audio = [sample["audio-pts"] for sample in data["samples"] if isinstance(sample["audio-pts"], (float, int))]
    last = data["samples"][-1]
    cache = last.get("demuxer-cache-state") or {}
    tail = [sample for sample in data["samples"] if sample["t"] >= 6]
    return {
        "case": Path(path).parent.name,
        "frame_count": len(frames),
        "last_frame_pts": frames[-1][1] if frames else None,
        "last_frame_wall_seconds": frames[-1][0] if frames else None,
        "late_unique_frame_checksums": len({frame[2] for frame in late}),
        "max_audio_pts": max(audio, default=None),
        "final_eof_reached": data["samples"][-1]["eof-reached"],
        "final_duration": last["duration"],
        "final_cache_end": cache.get("cache-end"),
        "final_pause": last["pause"],
        "tail_time_pos_span": max((s["time-pos"] for s in tail if s["time-pos"] is not None), default=0) - min((s["time-pos"] for s in tail if s["time-pos"] is not None), default=0),
        "video_requests": [{k: r[k] for k in ("t", "first", "sent", "truncated")} for r in data["requests"] if r["path"] == "/video.mp4"],
        "late_video_requests": sum(r["t"] >= 12 for r in data["requests"] if r["path"] == "/video.mp4"),
        "actions": data["actions"],
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("results", nargs="+")
    parser.add_argument("--expect", choices=["progressing", "stalled", "normal-end", "paused", "short-video"])
    parser.add_argument("--output")
    args = parser.parse_args()
    summaries = [summarize(path) for path in args.results]
    text = json.dumps(summaries, indent=2)
    print(text)
    if args.output:
        Path(args.output).write_text(text + "\n", encoding="utf-8")
    for summary in summaries:
        if args.expect == "progressing":
            assert summary["late_unique_frame_checksums"] >= 30, "FAIL: video did not produce 30 distinct late frames"
            assert summary["max_audio_pts"] >= 20, "FAIL: audio did not progress"
        elif args.expect == "stalled":
            assert summary["last_frame_pts"] < 9 and summary["max_audio_pts"] >= 20, "FAIL: split-track stall absent"
            assert summary["late_unique_frame_checksums"] == 0, "FAIL: unexpected late video frames"
        elif args.expect == "normal-end":
            assert summary["frame_count"] == 720 and summary["final_eof_reached"] == "yes"
            assert abs(summary["final_duration"] - summary["final_cache_end"]) < 0.1
        elif args.expect == "paused":
            assert summary["final_pause"] == "yes" and summary["tail_time_pos_span"] < 0.01
            assert abs(summary["final_duration"] - summary["final_cache_end"]) < 0.1
        elif args.expect == "short-video":
            assert summary["frame_count"] == 240 and summary["max_audio_pts"] >= 20
            assert summary["final_duration"] == 8 and abs(summary["final_duration"] - summary["final_cache_end"]) < 0.1
