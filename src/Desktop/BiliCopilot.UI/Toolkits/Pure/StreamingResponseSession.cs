// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Toolkits;

/// <summary>A single response's ordered text and scheduled UI publications.</summary>
internal sealed class StreamingResponseSession(Action<Action> schedule, Action<string, bool> publish)
{
    private readonly object _gate = new();
    private readonly System.Text.StringBuilder _text = new();
    private bool _pending;
    private bool _finished;

    public void Cancel()
    {
        lock (_gate)
        {
            _finished = true;
            _text.Clear();
        }
    }

    public void Append(string text)
    {
        lock (_gate)
        {
            if (_finished || string.IsNullOrEmpty(text))
            {
                return;
            }

            _text.Append(text);
            if (!_pending)
            {
                _pending = true;
                schedule(Flush);
            }
        }
    }

    // Complete and scheduled callbacks run on the UI dispatcher.
    public void Complete()
    {
        lock (_gate)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            publish(_text.ToString(), true);
            _text.Clear();
        }
    }

    private void Flush()
    {
        lock (_gate)
        {
            if (_finished)
            {
                return;
            }

            _pending = false;
            publish(_text.ToString(), false);
        }
    }
}
