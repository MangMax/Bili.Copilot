import copy
import gzip
import hashlib
import json
import subprocess
import unittest
from pathlib import Path
from exit_gate import event, verdict

PREFIX = 'docs/performance-acceptance/net11-primary/evidence/'
def archived(name):
    return gzip.decompress(subprocess.check_output(['git', 'show', 'e55180:' + PREFIX + name + '.gz']))


class ExitGateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.events = [event(archived('native-crash/' + name).decode('utf-8-sig')) for name in
                      ('event-1000-6512.xml', 'event-1001-6513.xml')]
        d = cls.events[0]['data']
        cls.record = dict(pid=11436, exe=d['AppPath'], creationFileTime=int(d['ProcessCreationTime'], 0),
                       package=d['PackageFullName'], bookmark=6511, startUtc='2026-10-30T16:20:00Z',
                       collectionEndUtc='2026-10-30T16:28:00Z', handleRetained=True, exitCode=0,
                       terminated=True, queryComplete=True, observationComplete=True)

    def test_sealed_false_green_is_blocked_and_bytes_unchanged(self):
        before = archived('final-rc/accepted.json')
        self.assertTrue(json.loads(before)['explicitExit'])
        result = verdict(self.record, self.events)
        self.assertEqual('BLOCKED', result['status'])
        self.assertEqual([6512], result['faultRecords'])
        self.assertEqual([6513], result['werRecords'])
        self.assertEqual(1, len(result['reportIds']))
        self.assertEqual(hashlib.sha256(before).digest(), hashlib.sha256(archived('final-rc/accepted.json')).digest())

    def test_normal_zero_complete_observation(self):
        self.assertEqual('PASS_BOUNDED', verdict(self.record, [])['status'])

    def test_historical_normal_record_without_exit_code_is_not_certified(self):
        p = Path(__file__).parent.parent / 'net10-adaptation/evidence/final-net10/main/exit.json.gz'
        historical = json.loads(gzip.decompress(p.read_bytes()))
        self.assertTrue(historical['explicitExit'])
        self.assertEqual(0, historical['unhandledCount'])
        self.assertEqual('BLOCKED', verdict(dict(self.record, exitCode=historical.get('exitCode')), [])['status'])

    def test_owned_live_shutdown_crash_is_blocked_despite_zero_managed_unhandled(self):
        evidence = json.loads(Path(__file__).with_name('evidence.json').read_text(encoding='utf-8'))
        run = evidence['observation']
        self.assertEqual(0, run['managedUnhandled'])
        self.assertEqual(0xC000027B, run['exitCode'])
        result = verdict(run, evidence['events'])
        self.assertEqual('BLOCKED', result['status'])
        self.assertIn('exact-process-application-error', result['reasons'])
        self.assertIn('nonzero-target-exit-code', result['reasons'])

    def test_unknown_and_nonzero_are_never_pass(self):
        for code in (None, 3221226107):
            with self.subTest(code=code):
                self.assertEqual('BLOCKED', verdict(dict(self.record, exitCode=code), [])['status'])

    def test_pid_reuse_unrelated_pid_path_and_old_event(self):
        for field, value in [('ProcessCreationTime', '0x1'), ('ProcessId', '0x1234'), ('AppPath', 'C:\\unrelated\\app.exe')]:
            e = copy.deepcopy(self.events[0]); e['data'][field] = value
            self.assertEqual('PASS_BOUNDED', verdict(self.record, [e])['status'])
            self.assertEqual('PASS_BOUNDED', verdict(self.record, [e, self.events[1]])['status'])
        e = copy.deepcopy(self.events[0]); e['utc'] = '2026-10-30T16:19:00Z'
        self.assertEqual('PASS_BOUNDED', verdict(self.record, [e])['status'])

    def test_delayed_publication_revokes_provisional_pass(self):
        self.assertEqual('PASS_BOUNDED', verdict(self.record, [])['status'])
        self.assertEqual('BLOCKED', verdict(self.record, self.events)['status'])
        self.assertEqual('BLOCKED', verdict(self.record, self.events[1:])['status'])

    def test_provider_pid_is_not_fault_pid(self):
        self.assertEqual('BLOCKED', verdict(self.record, self.events)['status'])
        other = dict(self.record, pid=40840)
        self.assertEqual('PASS_BOUNDED', verdict(other, self.events[:1])['status'])

    def test_missing_handle_query_timeout_forced_and_managed_failure(self):
        for change in [dict(handleRetained=False), dict(queryComplete=False), dict(terminated=False),
                       dict(observationComplete=False), dict(forced=True), dict(managedUnhandled=1)]:
            self.assertEqual('BLOCKED', verdict(dict(self.record, **change), [])['status'])

    def test_canonical_path_and_bookmark(self):
        r = dict(self.record, exe=self.record['exe'].upper().replace('\\', '/'))
        self.assertEqual('BLOCKED', verdict(r, self.events)['status'])
        self.assertEqual('PASS_BOUNDED', verdict(dict(self.record, bookmark=6513), self.events)['status'])

if __name__ == '__main__':
    unittest.main(verbosity=2)
