"""Deterministic proposal model, NOT production code, HTTP, GPU, or wall-clock timing.
Run: python schedule-model.py > model-results.json
"""
import heapq
import json


def batch(count, slow_segment, limit=6):
    events, now, cursor, started = [], 0, 1, []
    while cursor <= count or events:
        while len(events) < limit and cursor <= count:
            duration = 3000 if cursor == slow_segment else 30
            heapq.heappush(events, (now + duration, cursor))
            started.append(cursor)
            cursor += 1
        now, _ = heapq.heappop(events)
    return {"logical_publish_ms": now, "requests": len(started)}


class Window:
    def __init__(self):
        self.generation = 0
        self.current = 1
        self.closed = False
        self.cache = {}
        self.published = []
        self.requests = []

    def seek(self, segment):
        self.generation += 1
        self.current = segment
        window = {n for n in (segment-1, segment, segment+1) if n > 0}
        self.cache = {n: rows for n, rows in self.cache.items() if n in window}
        return [(self.generation, n) for n in (segment, segment+1, segment-1)
                if n > 0 and n not in self.cache]

    def complete(self, generation, segment, rows):
        if self.closed or generation != self.generation:
            return
        # Transport identity dedup is distinct from the app's 20-second text grouping.
        self.cache[segment] = sorted({row[0]: row for row in rows}.values(), key=lambda row: (row[1], row[0]))
        if segment == self.current:
            self.published.append((segment, list(self.cache[segment])))

    def close(self):
        self.closed = True
        self.generation += 1
        self.cache.clear()


results = []
for slow in (2, 120):
    result = {"case": f"slow-segment-{slow}", "all_barrier": batch(120, slow),
              "current_first": {"logical_publish_ms": 30, "initial_requests": 2},
              "note": "Current and next start together; only current gates publication. Assumed durations, not measurements."}
    results.append(result)

w = Window()
old = w.seek(1)
new = w.seek(119)
w.complete(*old[0], [("old", 1000)])
assert w.published == []
w.complete(*new[0], [("b", 42481000), ("a", 42481000), ("a", 42481000)])
assert w.published == [(119, [("a", 42481000), ("b", 42481000)])]
results.append({"case": "seek-stale-and-order-dedup", "status": "PASS", "published": w.published})
for segment in range(1, 121):
    targets = w.seek(segment)
    for generation, n in targets:
        w.complete(generation, n, [(str(n), n * 360000)])
    assert len(w.cache) <= 3
results.append({"case": "long-traversal-cache", "status": "PASS", "segments": 120, "max_cache_segments": 3,
                "note": "Only a segment count bound. Byte/item budget remains required for dense segments."})
before = len(w.published)
pending = w.seek(40)
w.close()
for generation, segment in pending:
    w.complete(generation, segment, [("late", 14041000)])
assert len(w.published) == before and not w.cache
results.append({"case": "close-late-completion", "status": "PASS"})
results.append({"case": "retry-budget", "status": "DESIGN_ONLY",
                "decision": "Use existing service's at-most-three transport attempts; no second three-attempt wrapper. Explicit user retry or re-entry resets failure state; cancellation never retries."})
print(json.dumps({"evidence_kind": "logical-time-model-only", "results": results}, indent=2))
