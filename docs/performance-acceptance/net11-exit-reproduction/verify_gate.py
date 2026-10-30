"""Mutation red/green proof; does not alter gate or archived records on disk."""
import io
import json
from pathlib import Path
import unittest
import test_exit_gate as tests

stream = io.StringIO()
real = tests.verdict
tests.verdict = lambda run, events: dict(status='PASS_BOUNDED', faultRecords=[], werRecords=[], reportIds=[])
red = unittest.TextTestRunner(stream=stream).run(unittest.defaultTestLoader.loadTestsFromTestCase(tests.ExitGateTests))
tests.verdict = real
green = unittest.TextTestRunner(stream=stream).run(unittest.defaultTestLoader.loadTestsFromTestCase(tests.ExitGateTests))
result = dict(red=dict(run=red.testsRun, failures=len(red.failures), errors=len(red.errors)),
              green=dict(run=green.testsRun, failures=len(green.failures), errors=len(green.errors)),
              mutation='legacy disappearance/zero-managed acceptance ignores target exit and events')
Path(__file__).with_name('gate-tests.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
Path('artifacts/exit-reproduction/gate-tests.log').write_text(stream.getvalue(), encoding='utf-8')
print(json.dumps(result))
assert not red.wasSuccessful() and green.wasSuccessful()
