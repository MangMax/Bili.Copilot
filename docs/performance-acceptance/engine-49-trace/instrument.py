"""Trace only: apply once to a fresh scheduler-candidate source tree."""
from pathlib import Path
import sys

root = Path(sys.argv[1])
p = root / 'src/Danmaku.Legacy/DanmakuRender.cs'
s = p.read_text(encoding='utf-8-sig')
assert 'Trace49' not in s

def replace(a, b, count=1):
    global s
    assert s.count(a) == count, (a, s.count(a))
    s = s.replace(a, b)

helper = '''
        private readonly int _trace49Instance = System.Threading.Interlocked.Increment(ref _trace49NextInstance);
        private static int _trace49NextInstance;
        private int _trace49Generation;
        private void Trace49(string action, DanmakuRenderItem item = null, double totalMs = -1, string reason = "", int hr = 0)
        {
            LegacyTrace49.Add(new {
                action, instance = _trace49Instance,
                generation = System.Threading.Volatile.Read(ref _trace49Generation),
                paused = _isPaused, stopped = _isStopped,
                itemId = item?.Id, mode = item?.Mode.ToString(), text = item?.Text,
                x = item?.X, width = item?.Width, totalMs,
                ageMs = item == null || !item.IsFirstRenderTimeSet || totalMs < 0 ? (double?)null : totalMs - item.FirstRenderTime.TotalMilliseconds,
                firstMs = item?.FirstRenderTime.TotalMilliseconds,
                initialized = item?.IsFirstRenderTimeSet,
                target = item?.RenderTarget == null ? 0 : RuntimeHelpers.GetHashCode(item.RenderTarget),
                reason, hr
            });
        }
'''
replace('        private const float StandardCanvasWidth', helper + '\n        private const float StandardCanvasWidth')
replace('_renderLayerList[layerId].RenderList.Add(renderItem);', '_renderLayerList[layerId].RenderList.Add(renderItem);\n                    Trace49("Add", renderItem);')
replace('renderItem.Dispose();\n                            renderList.RemoveAt(i);', 'Trace49("Remove", renderItem, args.Timing.TotalTime.TotalMilliseconds, renderItem.Mode == DanmakuMode.Top || renderItem.Mode == DanmakuMode.Bottom ? "duration" : "offscreen");\n                            renderItem.Dispose();\n                            renderList.RemoveAt(i);')
replace('bool removeItem = false;', 'bool removeItem = false;\n                        Trace49("UpdateItem", renderItem, args.Timing.TotalTime.TotalMilliseconds);')
replace('                                    // Wait for Update event for this item before rendering', '                                    Trace49("DrawSkipUninitialized", renderItem, args.Timing.TotalTime.TotalMilliseconds);\n                                    // Wait for Update event for this item before rendering')
for line in ['spriteBatch.Draw(renderItem.RenderTarget, new Vector2(renderItem.X, renderItem.StartY));', 'spriteBatch.Draw(renderItem.RenderTarget, new Vector2(renderItem.X, y));']:
    replace(line, line + '\n                                            Trace49("DrawQueued", renderItem, args.Timing.TotalTime.TotalMilliseconds);', s.count(line))
replace('                }                if (totalCount == 0)', '                    Trace49("BatchDisposed", totalMs: args.Timing.TotalTime.TotalMilliseconds, reason: layerId.ToString());\n                }                if (totalCount == 0)')
replace('                Logger.Log(ex.Message);', '                Trace49("Exception", hr: ex.HResult);\n                Logger.Log(ex.Message);', 2)
replace('if (_device.IsDeviceLost(ex.HResult))\n                {', 'if (_device.IsDeviceLost(ex.HResult))\n                {\n                    Trace49("DeviceLost", hr: ex.HResult);', 2)
replace('Logger.Log($"RaiseDeviceLost() failed: {ex.Message}");', 'Trace49("ResourceException", hr: ex.HResult);\n                    Logger.Log($"RaiseDeviceLost() failed: {ex.Message}");')
replace('Logger.Log(args.Reason.ToString());', 'Trace49("CreateResources", reason: args.Reason.ToString());\n            Logger.Log(args.Reason.ToString());')
replace('_isPaused = true;\n                _canvas.Invalidate();', '_isPaused = true;\n                Trace49("PauseFlagWritten");\n                _canvas.Invalidate();')
replace('new RenderLayer(i, i ==', 'new RenderLayer(this, i, i ==')
replace('            private readonly uint LayerId;', '            private readonly DanmakuRender _trace49Owner;\n            private readonly uint LayerId;')
replace('public RenderLayer(uint layerId, bool requireStrictRenderOrder)', 'public RenderLayer(DanmakuRender trace49Owner, uint layerId, bool requireStrictRenderOrder)')
replace('                LayerId = layerId;', '                _trace49Owner = trace49Owner;\n                LayerId = layerId;')
replace('                        RenderList[i].Dispose();', '                        _trace49Owner.Trace49("Remove", RenderList[i], reason: "clear-layer");\n                        RenderList[i].Dispose();')

# Wrap original method bodies without changing branches, returns, exceptions or waits.
for signature, event, timed in [
    ('public void Pause()', 'Pause', False), ('public void Start()', 'Start', False),
    ('public void Clear()', 'Clear', False), ('public void Close()', 'Close', False),
    ('private void OnCanvasUpdate(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)', 'Update', True),
    ('private void OnCanvasDraw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)', 'Draw', True),
]:
    start = s.index('{', s.index(signature))
    depth = 1
    end = start + 1
    while depth:
        depth += (s[end] == '{') - (s[end] == '}')
        end += 1
    body = s[start+1:end-1]
    time = ', totalMs: args.Timing.TotalTime.TotalMilliseconds' if timed else ''
    gen = 'System.Threading.Interlocked.Increment(ref _trace49Generation);' if event in ('Clear', 'Close') else ''
    s = s[:start+1] + f'\n            {gen}\n            Trace49("{event}Enter"{time});\n            try\n            {{' + body + f'\n            }}\n            catch (Exception trace49Exception) {{ Trace49("{event}Unhandled", hr: trace49Exception.HResult); throw; }}\n            finally {{ Trace49("{event}Exit"{time}); }}\n        ' + s[end-1:]
p.write_bytes(b'\xef\xbb\xbf' + s.replace('\n', '\r\n').encode())
trace = Path(__file__).with_name('LegacyTrace49.cs').read_text()
(p.parent / 'LegacyTrace49.cs').write_bytes(trace.replace('\n', '\r\n').encode())
