// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;
using Richasy.BiliKernel.Models.Appearance;
using Richasy.WinUIKernel.Share.Base;
using Richasy.WinUIKernel.Share.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.UserProfile;
using WinUIEx;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 图片库页面视图模型.
/// </summary>
public sealed partial class GalleryPageViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GalleryPageViewModel"/> class.
    /// </summary>
    public GalleryPageViewModel(
        BiliImage image,
        List<BiliImage>? images = default)
    {
        SelectedImage = image;
        Images = images ?? new List<BiliImage> { image };
        IsGroup = Images?.Count > 1;
        IsMenuHide = false;
    }

    [RelayCommand]
    private void ChangeImage(BiliImage image)
    {
        if (image == null || SelectedImage == image)
        {
            return;
        }

        SelectedImage = image;
    }

    [RelayCommand]
    private void NextImage()
    {
        var index = Images.ToList().IndexOf(SelectedImage);
        if (index == Images.Count - 1)
        {
            return;
        }

        index++;
        ChangeImage(Images.ElementAt(index));
    }

    [RelayCommand]
    private void PreviousImage()
    {
        var index = Images.ToList().IndexOf(SelectedImage);
        if (index == 0)
        {
            return;
        }

        index--;
        ChangeImage(Images.ElementAt(index));
    }

    [RelayCommand]
    private void CopyImage()
    {
        if (SelectedImage is null)
        {
            return;
        }

        var dp = new DataPackage();
        dp.SetWebLink(SelectedImage.SourceUri);
        dp.SetBitmap(RandomAccessStreamReference.CreateFromUri(SelectedImage.SourceUri));
        Clipboard.SetContent(dp);
        this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(Models.Constants.StringNames.Copied), InfoType.Success));
    }

    [RelayCommand]
    private async Task SaveImageAsync()
    {
        if (SelectedImage is null)
        {
            return;
        }

        var fileName = Path.GetFileName(SelectedImage.SourceUri.ToString());
        var extension = Path.GetExtension(fileName);

        // 新选择器在构造时就绑定窗口标识，不再需要 InitializeWithWindow 把 UWP 选择器挂到 HWND 上.
        // 默认文件名带扩展名、类型过滤只留图片扩展名，和旧版行为一致；不额外设 DefaultFileExtension，
        // 免得用户改名时不带扩展名的情况出现两套补全规则.
        var hwnd = this.Get<AppViewModel>().ActivatedWindow.GetWindowHandle();
        var savePicker = new FileSavePicker(Win32Interop.GetWindowIdFromWindow(hwnd))
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = fileName,
        };

        savePicker.FileTypeChoices.Add($"{extension.TrimStart('.').ToUpper()} 图片", [extension]);
        var result = await savePicker.PickSaveFileAsync();

        // 取消时返回 null：不建文件、不提示错误，保持旧版语义.
        if (result is null)
        {
            return;
        }

        // 新接口只给路径，没有 StorageFile；用异步 FileStream 维持原有的非阻塞写入.
        using var stream = new FileStream(result.Path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        using var client = new HttpClient();
        using var response = await client.GetAsync(SelectedImage.SourceUri);
        await response.Content.CopyToAsync(stream);
        this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.Saved), InfoType.Success));
    }

    [RelayCommand]
    private async Task SetWallPaperOrLockScreenAsync(bool isWallPaper)
    {
        if (SelectedImage is null)
        {
            return;
        }

        var profileSettings = UserProfilePersonalizationSettings.Current;
        var fileName = Path.GetFileName(SelectedImage.SourceUri.ToString());
        var folder = await Microsoft.Windows.Storage.ApplicationData.GetDefault().LocalFolder.CreateFolderAsync("TempImages", CreationCollisionOption.OpenIfExists);
        var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        using var stream = await file.OpenStreamForWriteAsync();
        using var client = new HttpClient();
        using var response = await client.GetAsync(SelectedImage.SourceUri);
        await response.Content.CopyToAsync(stream);
        var result = isWallPaper
            ? await profileSettings.TrySetWallpaperImageAsync(file)
            : await profileSettings.TrySetLockScreenImageAsync(file);
        if (result)
        {
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.SetSuccess), InfoType.Success));
        }
        else
        {
            this.Get<AppViewModel>().ShowTipCommand.Execute((ResourceToolkit.GetLocalizedString(StringNames.SetFailed), InfoType.Error));
        }
    }
}
