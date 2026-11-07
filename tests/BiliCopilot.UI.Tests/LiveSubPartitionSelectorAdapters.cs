// Copyright (c) Bili Copilot. All rights reserved.

global using Microsoft.UI.Xaml;

// Headless adapters only for the live sub-partition selector's XAML surface.
// The selector code-behind, detail view model and page view model are production code.
using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.ViewModels.Components;

namespace Microsoft.UI.Xaml.Controls
{
    /// <summary>
    /// 选择器条目的无 WinUI 替身：生产控件只透传文本与标签.
    /// </summary>
    public sealed class SelectorBarItem
    {
        public string? Text { get; set; }

        public object? Tag { get; set; }
    }
}

namespace BiliCopilot.UI.Controls.Components
{
    /// <summary>
    /// SlidingTabBar 的测试替身：记录调用方交付的条目与选中项.
    /// </summary>
    public sealed class SlidingTabBar
    {
        public Visibility Visibility { get; set; } = Visibility.Visible;

        public object? SelectedItem { get; set; }

        public event EventHandler? SelectionChanged;

        public List<(IReadOnlyList<SelectorBarItem> Items, object? Selected)> Deliveries { get; } = new();

        public void SetItems(IEnumerable<SelectorBarItem> items, object? selectedItem)
        {
            Deliveries.Add((items.ToList(), selectedItem));
            SelectedItem = selectedItem;
        }

        public void Select(object? selectedItem) => SelectedItem = selectedItem;

        public void RaiseSelectionChanged() => SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}

namespace BiliCopilot.UI.Controls.LivePartition
{
    /// <summary>
    /// 直播子分区控件基类的无 WinUI 替身.
    /// </summary>
    public abstract class LiveSubPartitionControlBase
    {
        private LivePartitionDetailViewModel? _viewModel;

        public LivePartitionDetailViewModel? ViewModel
        {
            get => _viewModel;
            set
            {
                var previous = _viewModel;
                _viewModel = value;
                OnViewModelChanged(previous, value);
            }
        }

        public Visibility Visibility { get; set; } = Visibility.Visible;

        public void LoadForTest() => OnControlLoaded();

        public void UnloadForTest() => OnControlUnloaded();

        protected virtual void OnControlLoaded()
        {
        }

        protected virtual void OnControlUnloaded()
        {
        }

        protected virtual void OnViewModelChanged(LivePartitionDetailViewModel? oldValue, LivePartitionDetailViewModel? newValue)
        {
        }
    }

    public sealed partial class LiveSubPartitionSelector
    {
        /// <summary>
        /// Gets the fake tab bar the production code delivers its items to.
        /// </summary>
        public SlidingTabBar TabBar { get; } = new();

        private SlidingTabBar Selector => TabBar;

        private void InitializeComponent()
        {
        }
    }
}
