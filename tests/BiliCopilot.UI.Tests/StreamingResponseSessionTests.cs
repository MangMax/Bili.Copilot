using BiliCopilot.UI.Toolkits;
using System.Diagnostics;
using Xunit.Abstractions;

namespace BiliCopilot.UI.Tests;

public class StreamingResponseSessionTests(ITestOutputHelper output)
{
    [Fact]
    public void FixedReplayMeasuresPublicationAndAllocationCosts()
    {
        var chunks = Enumerable.Range(0, 1024).Select(i => $"{i:D4}|正文|```code```|[link](url)\n").ToArray();
        var expected = string.Concat(chunks);
        // CPU/allocation micro-replay only: no XAML, Markdown parsing or real input latency.
        // Virtual arrival every 5ms; drain one pending refresh every 75ms.
        (long Bytes, double Milliseconds, int Commits) Replay(bool batched)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var commits = 0;
            var visible = string.Empty;
            var response = string.Empty;
            if (batched)
            {
                var queue = new Queue<Action>();
                var session = new StreamingResponseSession(queue.Enqueue, (text, _) =>
                {
                    visible = text;
                    commits++;
                });
                for (var i = 0; i < chunks.Length; i++)
                {
                    session.Append(chunks[i]);
                    if ((i + 1) % 15 == 0) queue.Dequeue()();
                }

                session.Complete();
                while (queue.TryDequeue(out var callback)) callback();
            }
            else
            {
                // Baseline's two cumulative strings, without the additional Markdown cost.
                foreach (var chunk in chunks)
                {
                    response += chunk;
                    visible += chunk;
                    commits++;
                }
            }

            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(expected, visible);
            if (!batched) Assert.Equal(expected, response);
            return (allocated, elapsed, commits);
        }

        Replay(false);
        Replay(true);
        output.WriteLine("mode,allocated_bytes,cpu_wall_ms,publications");
        for (var iteration = 0; iteration < 5; iteration++)
        {
            var baseline = Replay(false);
            var batched = Replay(true);
            Assert.Equal(1024, baseline.Commits);
            Assert.Equal(69, batched.Commits);
            output.WriteLine($"baseline,{baseline.Bytes},{baseline.Milliseconds:F3},{baseline.Commits}");
            output.WriteLine($"batched,{batched.Bytes},{batched.Milliseconds:F3},{batched.Commits}");
        }
    }

    [Fact]
    public async Task LateProducerCannotChangeReplacementTextOrGeneratingState()
    {
        var queue = new Queue<Action>();
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var text = string.Empty;
        var generating = true;
        void Publish(string value, bool final)
        {
            text = value;
            generating = !final;
        }

        var old = new StreamingResponseSession(queue.Enqueue, Publish);
        old.Append("old");
        async Task ProduceLateAsync()
        {
            await resume.Task;
            old.Append("late");
            old.Complete();
        }

        var producer = ProduceLateAsync();
        old.Cancel();
        var current = new StreamingResponseSession(queue.Enqueue, Publish);
        current.Append("new");
        queue.Dequeue()();
        queue.Dequeue()();
        resume.SetResult();
        await producer;
        Assert.Equal("new", text);
        Assert.True(generating);
        current.Complete();
        Assert.Equal("new", text);
        Assert.False(generating);
    }

    [Fact]
    public void MarkdownAndSplitUnicodeSurviveCompletionBeforeQueuedRefresh()
    {
        const string answer = "| A | B |\n|---|---|\n| 中 | 😀 |\n```csharp\nvar x = 1;\n```\n[link](https://example.com)";
        var queue = new Queue<Action>();
        var visible = new List<(string Text, bool Final)>();
        var session = new StreamingResponseSession(queue.Enqueue, (text, final) => visible.Add((text, final)));
        foreach (var character in answer) session.Append(character.ToString());
        session.Complete();
        session.Append("ignored");
        session.Complete();
        while (queue.TryDequeue(out var callback)) callback();
        Assert.Equal((answer, true), Assert.Single(visible));
    }

    [Fact]
    public void EmptyChunksDoNotScheduleWork()
    {
        var queue = new Queue<Action>();
        var session = new StreamingResponseSession(queue.Enqueue, (_, _) => { });
        session.Append(string.Empty);
        Assert.Empty(queue);
    }

    [Fact]
    public void CancelledSessionCannotPublishOrCompleteOverReplacement()
    {
        var queue = new Queue<Action>();
        var visible = new List<(string Text, bool Final)>();
        var old = new StreamingResponseSession(queue.Enqueue, (text, final) => visible.Add((text, final)));
        old.Append("old");
        old.Cancel();
        var current = new StreamingResponseSession(queue.Enqueue, (text, final) => visible.Add((text, final)));
        current.Append("new");
        current.Complete();
        old.Append("late");
        old.Complete();
        while (queue.TryDequeue(out var callback)) callback();
        Assert.Equal(("new", true), Assert.Single(visible));
    }

    [Fact]
    public void BurstPublishesInOrderAndCompletionFlushesTailImmediately()
    {
        var queue = new Queue<Action>();
        var visible = new List<(string Text, bool Final)>();
        var session = new StreamingResponseSession(queue.Enqueue, (text, final) => visible.Add((text, final)));
        session.Append("first ");
        session.Append("second");
        Assert.Empty(visible);
        Assert.Single(queue);
        queue.Dequeue()();
        Assert.Equal(("first second", false), Assert.Single(visible));
        session.Append(" tail");
        session.Complete();
        Assert.Equal(("first second tail", true), visible.Last());
        queue.Dequeue()();
        Assert.Equal(2, visible.Count);
    }
}
