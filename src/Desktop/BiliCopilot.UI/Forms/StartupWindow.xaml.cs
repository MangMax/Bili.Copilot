// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Pages;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Animation;
using Richasy.WinUIKernel.Share.Base;
using WinUIEx;

namespace BiliCopilot.UI.Forms;

/// <summary>
/// 初始窗口.
/// </summary>
public sealed partial class StartupWindow : WindowBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StartupWindow"/> class.
    /// </summary>
    public StartupWindow()
        : this(null)
    {
    }

    internal StartupWindow(long? expectedUid)
    {
        ExpectedUid = expectedUid;
        InitializeComponent();
        IsMaximizable = false;
        IsMinimizable = false;
        IsResizable = false;
        AppWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);

        Width = 720;
        Height = 460;

        Title = ResourceToolkit.GetLocalizedString(StringNames.AppName);

        this.CenterOnScreen();
        this.SetIcon("Assets/logo.ico");
        this.SetTitleBar(TitleBar);
        this.Get<AppViewModel>().Windows.Add(this);

        RootFrame.Navigate(typeof(StartupPage), this, new SuppressNavigationTransitionInfo());

        Closed += OnClosed;
    }

    internal long? ExpectedUid { get; }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        this.Get<AppViewModel>().Windows.Remove(this);
    }

}
