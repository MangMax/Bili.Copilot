"""Bounded exit verdict. Event provider PID is deliberately never used."""
import datetime as dt
import ntpath
import xml.etree.ElementTree as ET


def path(value):
    return ntpath.normcase(ntpath.normpath(value.removeprefix('\\\\?\\')))


def timestamp(value):
    return dt.datetime.fromisoformat(value.replace('Z', '+00:00')).timestamp()


def event(xml):
    root = ET.fromstring(xml)
    ns = {'e': 'http://schemas.microsoft.com/win/2004/08/events/event'}
    def system(name):
        return root.find('e:System/e:' + name, ns)
    data = {x.attrib['Name']: x.text or '' for x in root.findall('e:EventData/e:Data', ns)}
    return dict(id=int(system('EventID').text), record=int(system('EventRecordID').text),
                utc=system('TimeCreated').attrib['SystemTime'], data=data)


def verdict(run, events):
    reasons = []
    if not run.get('handleRetained') or run.get('exitCode') is None:
        reasons.append('unknown-target-exit-code')
    elif run['exitCode'] != 0:
        reasons.append('nonzero-target-exit-code')
    for flag in ('terminated', 'queryComplete', 'observationComplete'):
        if not run.get(flag):
            reasons.append('missing-' + flag)
    if run.get('forced'):
        reasons.append('forced-termination')
    if run.get('managedUnhandled', 0):
        reasons.append('managed-unhandled')
    eligible = [e for e in events if e['record'] > run['bookmark'] and
                timestamp(run['startUtc']) <= timestamp(e['utc']) <= timestamp(run['collectionEndUtc'])]
    faults = []
    for e in eligible:
        d = e['data']
        if e['id'] != 1000 or path(d.get('AppPath', '')) != path(run['exe']):
            continue
        if int(d.get('ProcessId', '-1'), 0) != run['pid']:
            continue
        creation = d.get('ProcessCreationTime')
        if not creation:
            reasons.append('unresolved-process-creation')
        elif int(creation, 0) == int(run['creationFileTime']):
            faults.append(e)
    reports = {e['data'].get('IntegratorReportId') for e in faults} - {None, ''}
    unrelated_reports = {e['data'].get('IntegratorReportId') for e in eligible
                         if e['id'] == 1000 and e not in faults and
                         e['data'].get('ProcessCreationTime')} - reports - {None, ''}
    linked = []
    for e in eligible:
        if e['id'] != 1001:
            continue
        d = e['data']
        # WER has no fault PID: require an exact fault's report plus package/path.
        identity = d.get('P1') == run.get('package') or path(d.get('AppPath', '')) == path(run['exe'])
        if identity and d.get('ReportId') in reports:
            linked.append(e)
        elif identity and d.get('ReportId') not in unrelated_reports:
            reasons.append('unresolved-wer-report')
    if faults:
        reasons.append('exact-process-application-error')
    return dict(status='BLOCKED' if reasons else 'PASS_BOUNDED', reasons=sorted(set(reasons)),
                faultRecords=[e['record'] for e in faults], werRecords=[e['record'] for e in linked],
                reportIds=sorted(reports), futureExactEvidenceRevokes=True)


if __name__ == '__main__':
    import json
    import pathlib
    import sys
    folder = pathlib.Path(sys.argv[1])
    run = json.loads((folder / 'exit-observation.json').read_text(encoding='utf-8-sig'))
    records = json.loads((folder / 'event-xml.json').read_text(encoding='utf-8-sig'))
    result = verdict(run, [event(x) for x in records])
    (folder / 'exit-verdict.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))
    sys.exit(0 if result['status'] == 'PASS_BOUNDED' else 2)
