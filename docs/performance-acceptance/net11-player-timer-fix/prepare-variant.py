"""After prepare-stop.py, prepare one NEW green/final directory; never edits red."""
import pathlib, shutil, sys
root = pathlib.Path.cwd()
base = root / 'artifacts/timer-fix'
variant = sys.argv[1]
assert variant in ('green', 'final')
target = base / variant / 'fixture'
assert not target.exists()
shutil.copytree(base / 'stop-only/fixture', target,
               ignore=shutil.ignore_patterns('bin', 'obj', 'frozen-output', 'frozen-*.json'))
panel = target / 'Controls/Core/PlayerInteractivePanel.xaml.cs'
shutil.copyfile(root / 'src/Desktop/BiliCopilot.UI/Controls/Core/PlayerInteractivePanel.xaml.cs', panel)
if variant == 'green':
    s = panel.read_text(encoding='utf-8-sig')
    s = s.replace('        if (ViewModel == null)', '        TimerChecks.BeforeRead(this);\n        if (ViewModel == null)')
    for operation, stage in [('Start', 'start'), ('Stop', 'stop')]:
        statement = f'        _tempControlTimer.{operation}();'
        s = s.replace(statement, statement + '\n        ExitTrace.Mark("timer-' + stage + '", new { id = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this), running = _tempControlTimer.IsRunning });')
    panel.write_text(s)
else:
    for name in ['Forms/MainWindow.xaml.cs', 'Forms/PlayerWindow.cs', 'Toolkits/Pure/TrayLifecycle.cs']:
        shutil.copyfile(base / 'stop-only/source/src/Desktop/BiliCopilot.UI' / name, target / name)
    p = target / 'App.xaml.cs'
    p.write_text(p.read_text(encoding='utf-8-sig').replace('ExitTrace.Initialize(this); ', ''))
    p = target / 'MainRuntimeChecks.cs'
    s = p.read_text(encoding='utf-8-sig').replace('ExitTrace.Mark("explicit-exit",new{players=vm.Players.Count});', '')
    s = s.replace('        controller.Closed += (_, _) => ExitTrace.Mark("fixture-controller-closed");\n        ExitTrace.Mark("fixture-controller-created");\n', '')
    p.write_text(s)
    (target / 'ExitTrace.cs').unlink()
    shutil.copyfile(pathlib.Path(__file__).with_name('FinalTimerChecks.cs'), target / 'TimerChecks.cs')
(target / 'source-provenance.txt').write_text(f'RC3 + local PlayerInteractivePanel fix; variant={variant}; verify frozen source hashes.\n')
