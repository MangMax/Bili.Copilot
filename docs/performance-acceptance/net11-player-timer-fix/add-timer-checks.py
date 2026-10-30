import pathlib, shutil, sys
fixture = pathlib.Path(sys.argv[1])
shutil.copyfile(pathlib.Path(__file__).with_name('TimerChecks.cs'), fixture / 'TimerChecks.cs')
p = fixture / 'Fixture.cs'
s = p.read_text(encoding='utf-8-sig')
s = s.replace('Host.AppWindow.Resize(', 'Host.DispatcherQueue.TryEnqueue(async () => { await Task.Delay(1000); await TimerChecks.Run((Panel)Host.Content); });\n        Host.AppWindow.Resize(')
p.write_text(s, encoding='utf-8')
p = fixture / 'Controls/Core/PlayerInteractivePanel.xaml.cs'
s = p.read_text(encoding='utf-8-sig')
# The observation sits immediately before the first dependency-property getter;
# a future lifetime guard must precede it. No VM inspection in instrumentation.
needle = '        if (ViewModel == null)'
assert s.count(needle) == 1
s = s.replace(needle, '        TimerChecks.BeforeRead(this);\n' + needle)
p.write_text(s, encoding='utf-8')
