// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Live;
using Richasy.MpvKernel;

namespace BiliCopilot.UI.PlaybackRouting;

internal static class LiveNativePlaybackFailure
{
    public static LivePlaybackFailureKind Classify(MpvError error)
        => error switch
        {
            MpvError.Http403 or MpvError.Http404 or MpvError.Http500 or MpvError.TlsError
                or MpvError.LoadingFailed => LivePlaybackFailureKind.Transport,
            MpvError.VoInitFailed or MpvError.AoInitFailed or MpvError.Unsupported
                or MpvError.UnknownFormat or MpvError.NothingToPlay => LivePlaybackFailureKind.DecoderInit,
            _ => LivePlaybackFailureKind.Unknown,
        };
}
