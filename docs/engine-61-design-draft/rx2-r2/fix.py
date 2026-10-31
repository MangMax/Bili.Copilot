"""Keep each scene-control receipt attached to its admitted operation."""
def apply(source):
    f = source/'src/Danmaku.Core/DanmakuScheduler.cs'
    s = f.read_text()
    start = s.index('    public void Stop()')
    end = s.index('    private long BeginCollection()', start)
    block = s[start:end].replace('public void Stop()', 'public void Stop() => StopTracked();\n    internal (long Scene, long Intent) StopTracked()')
    block = block.replace('if (_isClosing || _fault != null) return;', 'if (_isClosing || _fault != null) return (_scene, _intentRevision);')
    block = block.replace('try { Pause(); StopRequested?.Invoke(); }', 'try\n        {\n            long intent = PauseTracked();\n            StopRequested?.Invoke();\n            return (scene, intent);\n        }')
    s = s[:start]+block+s[end:]
    s = s.replace('private void Replace(List<DanmakuItem> list, bool replace)', 'internal long ClearTracked() => Replace(null, false);\n    internal long SetTracked(List<DanmakuItem> list) => Replace(list, true);\n    private long Replace(List<DanmakuItem> list, bool replace)')
    s = s.replace('if (scene < 0) return;', 'if (scene < 0) return scene;')
    s = s.replace('finally { EndCollection(scene, error); }', 'finally { EndCollection(scene, error); }\n        return scene;')
    f.write_text(s)
    f = source/'src/Danmaku.Core/CoreExecutionBridge.cs'
    s = f.read_text()
    for method, call, declaration, receipt in [
        ('StopAsync', 'scheduler.StopTracked()', '(long Scene, long Intent) request', 'request.Intent, request.Scene'),
        ('SetListAsync', 'scheduler.SetTracked(items)', 'long request', 'null, request'),
        ('ClearAsync', 'scheduler.ClearTracked()', 'long request', 'null, request')]:
        start = s.index('    internal Task<CoreResourceOutcome> '+method)
        end = s.index('\n    internal ', start+1)
        block = s[start:end]
        old = block[block.index('        try {'):block.index('\n        catch')]
        block = block.replace(old, '        '+declaration+';\n        try { request = '+call+'; }')
        block = block.replace('return FrameAsync();', 'return RenderAsync(null, null, '+receipt+');')
        s = s[:start]+block+s[end:]
    s = s.replace('long? requestedIntent = null)', 'long? requestedIntent = null, long? requestedScene = null)')
    guard = 'if (requestedIntent.HasValue && state.Intent != requestedIntent.Value) return CoreResourceOutcome.Superseded;'
    assert s.count(guard) == 1
    s = s.replace(guard, guard+'\n            if (requestedScene.HasValue && state.Scene != requestedScene.Value) return CoreResourceOutcome.Superseded;')
    f.write_text(s)
