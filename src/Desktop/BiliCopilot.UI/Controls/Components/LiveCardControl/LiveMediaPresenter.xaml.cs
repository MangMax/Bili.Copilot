// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Items;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// Shared live cover: 16:10 image, gradient and viewer metrics.
/// </summary>
public sealed partial class LiveMediaPresenter : LiveMediaPresenterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveMediaPresenter"/> class.
    /// </summary>
    public LiveMediaPresenter()
    {
        InitializeComponent();
    }

}

/// <summary>
/// Base type for the shared live cover presenter.
/// </summary>
public abstract class LiveMediaPresenterBase : LayoutUserControlBase<LiveItemViewModel>;
