// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Controls.Users;

/// <summary>
/// 关注页面主标题.
/// </summary>
public sealed partial class FollowsMainHeader : FollowsPageControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FollowsMainHeader"/> class.
    /// </summary>
    public FollowsMainHeader()
    {
        InitializeComponent();
    }

    private void OnSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        => ViewModel.SearchCommand.Execute(args.QueryText);

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && sender.Text.Length == 0)
        {
            ViewModel.SearchCommand.Execute(string.Empty);
        }
    }
}
