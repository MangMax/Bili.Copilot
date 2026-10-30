"""Only applies metadata markers to a fresh, full-source offline fixture."""
import pathlib
import shutil
import sys

fixture = pathlib.Path(sys.argv[1])
def edit(name, old, new, count=1):
    p = fixture / name
    s = p.read_text(encoding='utf-8-sig')
    assert s.count(old) == count, (name, old, s.count(old))
    p.write_text(s.replace(old, new), encoding='utf-8')

shutil.copyfile(pathlib.Path(__file__).with_name('ExitTrace.cs'), fixture / 'ExitTrace.cs')
edit('App.xaml.cs', 'InitializeTrayLifecycle(); Offline48.Start();', 'ExitTrace.Initialize(this); InitializeTrayLifecycle(); Offline48.Start();')
name = 'Forms/MainWindow.xaml.cs'
edit(name, 'SaveCurrentWindowStats();\n                await this.Get<SettingsPageViewModel>().CheckSaveServicesAsync();',
     'ExitTrace.Mark("settings-save-start");\n                SaveCurrentWindowStats();\n                await this.Get<SettingsPageViewModel>().CheckSaveServicesAsync();\n                ExitTrace.Mark("settings-save-complete");')
edit(name, 'await Task.WhenAll(appVM.Players.ToArray().Select(player => player.CloseAsync()));',
     'ExitTrace.Mark("players-close-start", new { count = appVM.Players.Count });\n                await Task.WhenAll(appVM.Players.ToArray().Select(player => player.CloseAsync()));\n                ExitTrace.Mark("players-close-complete");')
edit(name, 'app.ReleaseTrayResources();', 'ExitTrace.Mark("tray-release-start"); app.ReleaseTrayResources(); ExitTrace.Mark("tray-release-complete");')
edit(name, '                Close();\n                app.Exit();', '                ExitTrace.Mark("main-close-start"); Close(); ExitTrace.Mark("main-close-complete");\n                ExitTrace.Mark("app-exit-start"); app.Exit(); ExitTrace.Mark("app-exit-return");')
name = 'Forms/PlayerWindow.cs'
edit(name, 'public void Close() => _rootWindow.Destroy();', 'public void Close() { ExitTrace.Mark("player-destroy-start", new { IsClosed, IsDisposed }); _rootWindow.Destroy(); ExitTrace.Mark("player-destroy-complete", new { IsClosed, IsDisposed }); }')
edit(name, '_xamlSource?.Dispose();', 'ExitTrace.Mark("xaml-source-dispose-start", new { IsClosed, IsDisposed }); _xamlSource?.Dispose(); ExitTrace.Mark("xaml-source-dispose-complete", new { IsClosed, IsDisposed });')
edit(name, '        IsClosed = true;', '        ExitTrace.Mark("player-destroying", new { IsClosed, IsDisposed });\n        IsClosed = true;')
edit(name, '        await Task.Delay(400);', '        await Task.Delay(400);\n        ExitTrace.Mark("activation-callback-resumed", new { IsClosed, IsDisposed });')
edit(name, '            if (IsClosed || IsDisposed || _xamlSource is null', '            ExitTrace.Mark("focus-callback", new { IsClosed, IsDisposed });\n            if (IsClosed || IsDisposed || _xamlSource is null')
edit(name, '                    await Task.Delay(800);', '                    await Task.Delay(800);\n                    ExitTrace.Mark("padding-callback-resumed", new { IsClosed, IsDisposed });', 2)
name = 'MainRuntimeChecks.cs'
edit(name, 'Offline48.Log("explicit-exit",new{players=vm.Players.Count});', 'ExitTrace.Mark("explicit-exit",new{players=vm.Players.Count});Offline48.Log("explicit-exit",new{players=vm.Players.Count});')
edit(name, '        controller.Activate();', '        controller.Closed += (_, _) => ExitTrace.Mark("fixture-controller-closed");\n        ExitTrace.Mark("fixture-controller-created");\n        controller.Activate();')
edit('Toolkits/Pure/TrayLifecycle.cs', '            tray.Dispose();', '            ExitTrace.Mark("tray-dispose-start"); tray.Dispose(); ExitTrace.Mark("tray-dispose-complete");')
