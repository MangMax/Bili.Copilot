// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// Shared card metadata layout. Each card supplies its own avatar, author,
/// labels, info and action elements through the named slots.
/// </summary>
public sealed partial class CardMetaPanel : UserControl
{
    public static readonly DependencyProperty AvatarProperty = DependencyProperty.Register(
        nameof(Avatar), typeof(object), typeof(CardMetaPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty AuthorProperty = DependencyProperty.Register(
        nameof(Author), typeof(object), typeof(CardMetaPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty BadgesProperty = DependencyProperty.Register(
        nameof(Badges), typeof(object), typeof(CardMetaPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty InfoProperty = DependencyProperty.Register(
        nameof(Info), typeof(object), typeof(CardMetaPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions), typeof(object), typeof(CardMetaPanel), new PropertyMetadata(null));

    /// <summary>
    /// Initializes a new instance of the <see cref="CardMetaPanel"/> class.
    /// </summary>
    public CardMetaPanel() => InitializeComponent();

    /// <summary>
    /// Gets or sets the avatar slot.
    /// </summary>
    public object? Avatar
    {
        get => GetValue(AvatarProperty);
        set => SetValue(AvatarProperty, value);
    }

    /// <summary>
    /// Gets or sets the author slot.
    /// </summary>
    public object? Author
    {
        get => GetValue(AuthorProperty);
        set => SetValue(AuthorProperty, value);
    }

    /// <summary>
    /// Gets or sets the label slot shown beside the author.
    /// </summary>
    public object? Badges
    {
        get => GetValue(BadgesProperty);
        set => SetValue(BadgesProperty, value);
    }

    /// <summary>
    /// Gets or sets the trailing info slot, usually a timestamp.
    /// </summary>
    public object? Info
    {
        get => GetValue(InfoProperty);
        set => SetValue(InfoProperty, value);
    }

    /// <summary>
    /// Gets or sets the trailing action slot, usually the more button.
    /// </summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
