"""Reduce owned-PID samples, keeping external/wait samples separate from work.

Usage: python summarize.py <run-dir> [<run-dir> ...]
No sample-count-to-CPU-ms conversion is made.
"""
import collections
import datetime as dt
import hashlib
import json
import pathlib
import statistics
import sys

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def timestamp(value):
    return dt.datetime.fromisoformat(value.replace('Z', '+00:00'))

for argument in sys.argv[1:]:
    root = pathlib.Path(argument)
    baseline = read(root / 'idle-baseline.json')
    result = {'run': root.name, 'phases': {}}
    for phase in ('visible', 'hidden', 'restored'):
        rows = [r for r in baseline['rows'] if r['phase'] == phase]
        assert len(rows) == 61 and all(r['scale'] == 1.5 for r in rows)
        rates = []
        for start, end in ((0, 60), (0, 30), (30, 60)):
            seconds = (timestamp(rows[end]['utc']) - timestamp(rows[start]['utc'])).total_seconds()
            rates.append((rows[end]['cpuMilliseconds']-rows[start]['cpuMilliseconds'])/seconds)
        result['phases'][phase] = {'cpuMsPerSecond_full_first_last': rates, 'start': rows[0]['utc'], 'end': rows[-1]['utc']}
    before, after = [read(root / 'profile' / f'{name}.json') for name in ('before', 'after')]
    result['counterWindow'] = {'start':before['utc'], 'end':after['utc'], 'processCpuMs':after['cpuMs']-before['cpuMs']}
    initial = {t['tid']:t for t in before['threads']}
    result['threads'] = sorted([
        {'tid':t['tid'], **{field:t[field]-initial[t['tid']][field] for field in ('cpuMs','userMs','kernelMs')}}
        for t in after['threads'] if t['tid'] in initial
    ], key=lambda t:-t['cpuMs'])
    result['threadCoverageNote']='Only threads surviving both endpoint snapshots; new/exited threads are not assigned CPU.'
    result['threadsAppearingAfterStart'] = [t for t in after['threads'] if t['tid'] not in initial]
    samplefile = root / 'profile' / 'cpu.nettrace.samples.jsonl'
    if samplefile.exists():
        result['eventSummary'] = read(root/'profile'/'cpu.nettrace.summary.json')
        event_start=timestamp(result['eventSummary']['SessionStartTime'])
        event_end=timestamp(result['eventSummary']['SessionEndTime'])
        aligned=[r for r in baseline['rows'] if r['phase']=='restored' and event_start<=timestamp(r['utc'])<=event_end]
        result['alignedCounterRows']={'first':aligned[0]['sample'],'last':aligned[-1]['sample'],
            'start':aligned[0]['utc'],'end':aligned[-1]['utc'],
            'cpuMs':aligned[-1]['cpuMilliseconds']-aligned[0]['cpuMilliseconds']}
        rows = [json.loads(line) for line in samplefile.read_text().splitlines()]
        start = timestamp(aligned[0]['utc'])
        end = timestamp(aligned[-1]['utc'])
        rows = [r for r in rows if start <= timestamp(r['utc']) <= end]
        kinds = collections.Counter()
        stacks = collections.Counter()
        leaves = collections.Counter()
        by_thread = collections.Counter()
        thread_leaves = collections.defaultdict(collections.Counter)
        for r in rows:
            kind = ';'.join(f"{p['name']}={p['value']}" for p in r['payload'])
            kinds[kind] += 1
            frames = tuple(r['frames'])
            stacks[(kind, frames)] += 1
            leaves[(kind, frames[0] if frames else '<no stack>')] += 1
            by_thread[(kind, r['tid'])] += 1
            thread_leaves[r['tid']][frames[0] if frames else '<no stack>'] += 1
        result['samples'] = {'count':len(rows), 'types':dict(kinds),
            'threadCounts':[{'kind':k,'tid':t,'count':n} for (k,t),n in by_thread.most_common()],
            'topLeaves':[{'kind':k,'leaf':f,'count':n} for (k,f),n in leaves.most_common(40)],
            'topStacks':[{'kind':k,'frames':f,'count':n} for (k,f),n in stacks.most_common(40)],
            'noStack':sum(not r['frames'] for r in rows)}
        result['samples']['threadTopLeaves']=[{'tid':tid,'leaves':counter.most_common(3)} for tid,counter in thread_leaves.items()]
        per_thread=collections.defaultdict(list)
        for r in rows:
            per_thread[r['tid']].append(r['ms'])
        intervals=[b-a for times in per_thread.values() for a,b in zip(times,times[1:]) if b>a]
        intervals.sort()
        result['samples']['observedSpacingMs']={'median':statistics.median(intervals),
            'p95':intervals[int(len(intervals)*.95)],'max':max(intervals)}
        result['samples']['blankFrameSamples']=sum(any(not f for f in r['frames']) for r in rows)
        result['samples']['depth100Samples']=sum(len(r['frames'])==100 for r in rows)
        result['threadsWithoutEventPipeSamples']=[t for t in result['threads'] if t['tid'] not in per_thread and t['cpuMs']>0]
        trace = root / 'profile' / 'cpu.nettrace'
        result['trace'] = {'sha256':hashlib.file_digest(trace.open('rb'),'sha256').hexdigest(), 'bytes':trace.stat().st_size}
    (root/'summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(root.name, result['phases']['restored'], result.get('samples',{}).get('types'))
