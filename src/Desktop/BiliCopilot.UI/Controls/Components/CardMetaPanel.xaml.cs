// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Controls.Components;

/// <summary>
/// Shared card author header. Cards either fill the named label, info and
/// action slots, or pass a whole row through <see cref="Meta"/> when the card
/// needs its own arrangement.
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

    public static readonly DependencyProperty MetaProperty = DependencyProperty.Register(
        nameof(Meta), typeof(object), typeof(CardMetaPanel), new PropertyMetadata(null, OnMetaChanged));

    /// <summary>
    /// Initializes a new instance of the <see cref="CardMetaPanel"/> class.
    /// </summary>
    public CardMetaPanel()
    {
        InitializeComponent();
        UpdateMetaMode();
    }

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
    /// Gets or sets the label slot used by the default row.
    /// </summary>
    public object? Badges
    {
        get => GetValue(BadgesProperty);
        set => SetValue(BadgesProperty, value);
    }

    /// <summary>
    /// Gets or sets the trailing info slot used by the default row.
    /// </summary>
    public object? Info
    {
        get => GetValue(InfoProperty);
        set => SetValue(InfoProperty, value);
    }

    /// <summary>
    /// Gets or sets the trailing action slot used by the default row.
    /// </summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>
    /// Gets or sets a free metadata row that replaces the default layout of
    /// <see cref="Badges"/>, <see cref="Info"/> and <see cref="Actions"/>.
    /// Use it when the card needs its own row arrangement.
    /// </summary>
    public object? Meta
    {
        get => GetValue(MetaProperty);
        set => SetValue(MetaProperty, value);
    }

    private static void OnMetaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CardMetaPanel)d).UpdateMetaMode();

    private void UpdateMetaMode()
    {
        var hasMeta = Meta is not null;
        MetaPresenter.Visibility = hasMeta ? Visibility.Visible : Visibility.Collapsed;
        DefaultMetaRow.Visibility = hasMeta ? Visibility.Collapsed : Visibility.Visible;
    }
}
