// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Live;

/// <summary>Projects the retained chat window without recreating surviving rows.</summary>
public static class LiveChatProjection
{
    /// <summary>Applies retention, deletion and merges using stable message sequences.</summary>
    public static void Synchronize<T>(LiveChatItemCollection<T> rows, IReadOnlyList<LiveChatMessage> messages,
        Func<LiveChatMessage, T> create) where T : LiveChatMessagePresentation
    {
        if (messages.Count == 0)
        {
            rows.ClearAll();
            return;
        }

        var prefix = 0;
        while (prefix < rows.Count && rows[prefix].Message.Sequence < messages[0].Sequence)
        {
            prefix++;
        }

        rows.RemoveFromStart(prefix);
        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];
            while (index < rows.Count && rows[index].Message.Sequence < message.Sequence)
            {
                rows.RemoveAt(index);
            }

            if (index == rows.Count || rows[index].Message.Sequence != message.Sequence)
            {
                rows.Insert(index, create(message));
            }

            rows[index].UpdateMergeCount();
        }

        while (rows.Count > messages.Count)
        {
            rows.RemoveAt(rows.Count - 1);
        }
    }
}
