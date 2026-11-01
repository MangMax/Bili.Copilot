"""Read-only audit of the supplied archive and locally replayed panel tests."""
from pathlib import Path
import hashlib, json, re, subprocess, xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[3]
work = root / 'artifacts/review-ef802ad6'
e = work / 'evidence'
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
sha = lambda b: hashlib.sha256(b).hexdigest()
manifest = read(e / 'manifest.json')
for item in manifest:
    b = (e / item['path']).read_bytes()
    assert len(b) == item['bytes'] and sha(b) == item['sha256'], item['path']

ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def counters(path):
    return ET.parse(path).find('.//t:Counters', ns).attrib

results = {'manifestEntriesVerified': len(manifest), 'tests': {}}
for name, path, count in [
    ('independentPanelReplay', work/'results/review-panel.trx', 12),
    ('archivedFresh577', e/'tests-and-audits/full/full.trx', 577),
    ('archivedFinalPanel', e/'tests-and-audits/final-fresh-panel-results/panel.trx', 12),
]:
    c = counters(path)
    assert int(c['passed']) == count and int(c['failed']) == 0 and int(c['total']) == count
    results['tests'][name] = c

panel = work/'source/src/Desktop/BiliCopilot.UI/Controls/Danmaku/VideoDanmakuPanel.xaml.cs'
assert sha(panel.read_bytes()) == read(e/'tests-and-audits/final-product-input.json')['sha256']
results['finalPanelSha256'] = sha(panel.read_bytes())
rsp = (e/'builds/final.ilc.rsp').read_text(encoding='utf-8-sig')
packages = []
for item in read(e/'tests-and-audits/dependency-audit.json')['ilcPackages']:
    assert item['dll'] in rsp
    # Validate the three committed packages against the archived dependency audit.
    name = Path(item['package']).name
    if name.startswith('Danmaku.'):
        b = subprocess.check_output(['git','show',f'ef802ad6:packages/danmaku/{name}'], cwd=root)
        assert sha(b) == item['packageSha256']
    packages.append({'dll': Path(item['dll']).name, 'dllSha256': item['dllSha256'], 'packageSha256': item['packageSha256']})
results['ilcDependencyReceipts'] = packages
for run in ('final', 'verified'):
    d = e/'runs'/run
    home = read(d/'controlled-results.json')
    seek = (d/'seek.tsv').read_text()
    logs = '\n'.join(p.read_text(encoding='utf-8-sig') for p in (d/'Logger').glob('*.txt'))
    marker = 'Danmaku renderer failed and was retired.'
    assert logs.count(marker) == 1
    assert logs[logs.index(marker):].count('Unhandled exception occurred.') == 0
    observation = read(d/'exit-observation.json')
    assert observation['handleRetained'] and observation['terminated'] and observation['exitCode'] == 0 and not observation['forced']
    assert observation['queries'][-1]['startedSecondsAfterExit'] >= 31
    assert read(d/'exit-verdict.json')['status'] == 'PASS_BOUNDED'
    cleanup = read(d/'cleanup.json')
    assert cleanup['identity'] == f'Mang.BiliCopilot.HomeRegression.T5df5.{run}'
    assert cleanup['registrationAbsent'] and cleanup['processAbsent']
    assert read(d/'yield.json') == {'released': False}
    pe = read(d/'native-inputs.json')
    assert all(x['clrRva'] == 0 for x in pe)
    exe = next(x for x in pe if x['path'].endswith('BiliCopilot.UI.exe'))
    loaded = read(d/'loaded-modules.json')
    assert next(x['sha256'] for x in loaded if x['path'].endswith('BiliCopilot.UI.exe')).lower() == exe['sha256'].lower()
    modules = read(d/'all-modules.json')
    assert not any(re.search(r'(coreclr|clrjit|hostfxr|hostpolicy)\.dll$', x, re.I) for x in modules)
    assert any(x.endswith('libmpv-2.dll') for x in modules)
    result = {'homePassed': sum(x['status']=='PASS' for x in home), 'seekPassed': seek.count('\tPASS\t'), 'seekFailed': seek.count('\tFAIL\t'), 'globalExceptions': logs.count('Unhandled exception occurred.'), 'afterRetirementGlobalExceptions': 0, 'terminalLogCount': 1, 'exeSha256': exe['sha256'].lower(), 'exitCode': observation['exitCode'], 'werSeconds': observation['queries'][-1]['startedSecondsAfterExit'], 'cleanup': cleanup}
    if run == 'verified':
        assert result['homePassed'] == 27 and result['seekPassed'] == 32 and result['seekFailed'] == 0
        assert '\tPASS\tRecoveredSeek position=28 progress=00:00:28' in seek
    else:
        assert '\tFAIL\tRecoveredSeek' in seek and (d/'failure.json').exists()
    results[run] = result
before = read(e/'tests-and-audits/protection-before-launch.json')
after = read(e/'tests-and-audits/protection-after.json')
assert before['processes'] == after['processes']
bp = {x['Name']: x for x in before['packages']}
ap = {x['Name']: x for x in after['packages']}
assert set(bp)-set(ap) == {'Mang.BiliCopilot.PerfAcceptance.Caa6a7be'}
assert all(bp[k] == v for k, v in ap.items())
results['protectionArchivedVerdict'] = read(e/'tests-and-audits/protection-verdict.json')
assert results['protectionArchivedVerdict']['packagesUnchangedDuringOwnedRuns'] is False
b = subprocess.check_output(['git','show','ef802ad6:docs/performance-acceptance/renderer-fault-isolation/README.md'], cwd=root)
try:
    b.decode('utf-8')
    results['readmeUtf8'] = True
except UnicodeDecodeError:
    results['readmeUtf8'] = False
    results['readmeFirstLineGb18030'] = b.splitlines()[0].decode('gb18030')
out = Path(__file__).with_name('audit-result.json')
out.write_text(json.dumps(results, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps(results, ensure_ascii=False, indent=2))
