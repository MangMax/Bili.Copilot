// Copyright (c) Bili Copilot. All rights reserved.

// Only Windows presentation dependencies; search commands and item models are linked production code.
namespace Richasy.WinUIKernel.Share.ViewModels
{
    public abstract class ViewModelBase : CommunityToolkit.Mvvm.ComponentModel.ObservableObject { }
}

namespace BiliCopilot.UI.ViewModels.Core
{
    public sealed class NavigationViewModel
    {
        public void Search(string keyword) { }
        public void SearchInRegion(string keyword) { }
    }
}
