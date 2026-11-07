// The player model adapter exposes only the existing notification/command contract.
using System.ComponentModel;
using BiliCopilot.UI.Controls.Components;
using BiliCopilot.UI.ViewModels.Core;
using BiliCopilot.UI.ViewModels.Items;

namespace BiliCopilot.UI.ViewModels.Items
{
    public interface IPlayerSectionDetailViewModel { string? Title { get; } }
    public sealed record Section(string? Title) : IPlayerSectionDetailViewModel;
}

namespace BiliCopilot.UI.ViewModels.Core
{
    public sealed class VideoConnectorViewModel : INotifyPropertyChanged
    {
        private List<IPlayerSectionDetailViewModel>? _sections;
        private IPlayerSectionDetailViewModel? _selected;
        public VideoConnectorViewModel() => SelectSectionCommand = new(this);
        public event PropertyChangedEventHandler? PropertyChanged;
        public List<IPlayerSectionDetailViewModel>? Sections
        {
            get => _sections;
            set { _sections = value; PropertyChanged?.Invoke(this, new(nameof(Sections))); }
        }
        public IPlayerSectionDetailViewModel? SelectedSection
        {
            get => _selected;
            set { _selected = value; PropertyChanged?.Invoke(this, new(nameof(SelectedSection))); }
        }
        public SelectionCommand SelectSectionCommand { get; }
        public sealed class SelectionCommand(VideoConnectorViewModel model)
        {
            public int Calls { get; private set; }
            public void Execute(object? value) { Calls++; model.SelectedSection = (IPlayerSectionDetailViewModel?)value; }
        }
    }
}

namespace BiliCopilot.UI.Controls.Player
{
    public abstract class VideoPlayerControlBase
    {
        private VideoConnectorViewModel? _viewModel;
        public VideoConnectorViewModel? ViewModel
        {
            get => _viewModel;
            set { var old = _viewModel; _viewModel = value; OnViewModelChanged(old, value); }
        }
        public void Mount() { OnControlLoaded(); ((VideoPlayerSideHeader)this).TestTabs.RaiseLoaded(); }
        public void Unmount() { OnControlUnloaded(); ((VideoPlayerSideHeader)this).TestTabs.RaiseUnloaded(); }
        protected virtual void OnControlLoaded() { }
        protected virtual void OnControlUnloaded() { }
        protected virtual void OnViewModelChanged(VideoConnectorViewModel? oldValue, VideoConnectorViewModel? newValue) { }
    }
    public sealed partial class VideoPlayerSideHeader
    {
        private SlidingTabBar Tabs = new();
        public SlidingTabBar TestTabs => Tabs;
        private void InitializeComponent() { }
    }
}
