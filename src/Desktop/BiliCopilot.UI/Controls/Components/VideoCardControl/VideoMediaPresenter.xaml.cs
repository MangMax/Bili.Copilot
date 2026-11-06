// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using BiliCopilot.UI.Models.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>Shared video cover, badges and community metrics.</summary>
public sealed partial class VideoMediaPresenter : VideoMediaPresenterBase
{
    private CancellationTokenSource? _metadataRequest;

    public VideoMediaPresenter()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadMomentMetadata();
        Unloaded += OnUnloaded;
    }

    public CompositeTransform CoverZoom => CoverTransform;

    protected override void OnViewModelChanged(VideoItemViewModel? oldValue, VideoItemViewModel? newValue)
    {
        if (IsLoaded) LoadMomentMetadata();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _metadataRequest?.Cancel();
        _metadataRequest?.Dispose();
        _metadataRequest = null;
    }

    private void LoadMomentMetadata()
    {
        _metadataRequest?.Cancel();
        _metadataRequest?.Dispose();
        _metadataRequest = null;
        if (ViewModel is not { Style: VideoCardStyle.Moment } model
            || (model.PlayCount is >= 0 && !string.IsNullOrWhiteSpace(model.Description) && !string.IsNullOrWhiteSpace(model.Title))) return;
        _metadataRequest = new CancellationTokenSource();
        _ = CompleteMomentMetadataAsync(model, _metadataRequest.Token);
    }

    private async Task CompleteMomentMetadataAsync(VideoItemViewModel model, CancellationToken token)
    {
        try { await model.LoadPopularMetadataAsync(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            this.Get<ILogger<VideoMediaPresenter>>().LogWarning(ex, "Unable to complete dynamic video metadata.");
        }
    }
}

public abstract class VideoMediaPresenterBase : LayoutUserControlBase<VideoItemViewModel>;
