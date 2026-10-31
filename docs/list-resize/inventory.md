# Production list and image paths

Generated from production XAML. This is a call-site inventory, not a claim of native test coverage.

| Container file | Container | Layout | Template/card descendants |
| --- | --- | --- | --- |
| Controls/AI/AIBody.xaml | AppRepeater | inline/default | AppRepeater.Layout, StackLayout |
| Controls/AI/AIFooter.xaml | ItemsRepeater | inline/default | ItemsRepeater.Layout, StackLayout |
| Controls/AI/AIHeader.xaml | ItemsRepeater | inline/default | ItemsRepeater.Layout, StackLayout |
| Controls/Article/ArticlePartitionMainBody.xaml | AppRepeater | {StaticResource ArticleLayout} | ArticleCardControl |
| Controls/Article/ArticlePartitionSideBody.xaml | ItemsView | inline/default | ItemsView.Layout, StackLayout |
| Controls/Comment/CommentDetailPanel.xaml | AppRepeater | inline/default |  |
| Controls/Comment/CommentMainPanel.xaml | AppRepeater | inline/default |  |
| Controls/Components/AppSearchBox.xaml | ItemsView | inline/default |  |
| Controls/Components/EmotePanel.xaml | ItemsView | inline/default | ItemsView.Layout, StackLayout |
| Controls/Components/EmotePanel.xaml | ItemsView | inline/default | ItemsView.Layout, UniformGridLayout |
| Controls/Components/EmoteTextBlock.xaml | ItemsRepeater | inline/default | ColumnMajorUniformToLargestGridLayout, ItemsRepeater.Layout |
| Controls/Core/OverlayTransportControls.xaml | ItemsControl | inline/default |  |
| Controls/Core/OverlayTransportControls.xaml | ItemsRepeater | inline/default | AppToggleCard, ItemsRepeater.Layout, StackLayout |
| Controls/Core/PlayerOverlay.xaml | ItemsRepeater | inline/default | ItemsRepeater.Layout, StackLayout |
| Controls/Danmaku/DanmakuSendDialog.xaml | ItemsRepeater | inline/default | ItemsRepeater.Layout, UniformGridLayout |
| Controls/Favorites/FavoritesPageSideBody.xaml | ItemsView | inline/default | ItemsView.Layout, StackLayout |
| Controls/Favorites/PgcFavoriteBody.xaml | AppRepeater | {StaticResource PgcLayout} | SeasonCardControl |
| Controls/Favorites/UgcFavoriteBody.xaml | AppRepeater | {StaticResource VideoLayout} | VideoCardControl |
| Controls/Favorites/VideoFavoriteBody.xaml | ItemsRepeater | {StaticResource VideoLayout} | VideoCardControl |
| Controls/History/ArticleHistorySection.xaml | AppRepeater | {StaticResource VideoLayout} | ArticleCardControl |
| Controls/History/HistoryVideoSearchSection.xaml | AppRepeater | {StaticResource VideoLayout} | VideoCardControl |
| Controls/History/LiveHistorySection.xaml | AppRepeater | {StaticResource VideoLayout} | LiveCardControl |
| Controls/History/VideoHistorySection.xaml | AppRepeater | {StaticResource VideoLayout} | VideoCardControl |
| Controls/LivePartition/LivePartitionMainHeader.xaml | AppRepeater | inline/default | AppRepeater.Layout, LiveCardControl, StackLayout |
| Controls/LivePartition/LivePartitionMainSideBody.xaml | ItemsView | inline/default | ItemsView.Layout, StackLayout |
| Controls/LivePartition/LivePartitionSubSideBody.xaml | ItemsView | inline/default | ItemsView.Layout, StackLayout |
| Controls/LivePartition/LiveRecommendMainBody.xaml | AppRepeater | {StaticResource LiveLayout} | LiveCardControl |
| Controls/LivePartition/LiveSubPartitionMainBody.xaml | AppRepeater | {StaticResource LiveLayout} | LiveCardControl |
| Controls/Message/ChatSessionBody.xaml | ItemsRepeater | inline/default | ItemsRepeater.Layout, StackLayout |
| Controls/Message/MessagePageSideBody.xaml | ItemsView | inline/default | ItemsView.Layout, StackLayout |
| Controls/Message/NotifyMessageBody.xaml | AppRepeater | inline/default | AppRepeater.Layout, NotifyMessageCardControl, StackLayout |
| Controls/Moment/ComprehensiveMainBody.xaml | AppRepeater | inline/default | MomentCardControl |
| Controls/Moment/ComprehensiveMomentSpaceControl.xaml | AppRepeater | inline/default | MomentCardControl |
| Controls/Moment/ComprehensiveSideBody.xaml | ItemsView | inline/default | ItemsView.Layout, StackLayout |
| Controls/Moment/MomentImageGallery.xaml | ItemsRepeater | inline/default | ItemsRepeater.Layout, UniformGridLayout |
| Controls/Moment/UserSpaceVideoSearchSection.xaml | AppRepeater | {StaticResource VideoLayout} | VideoCardControl |
| Controls/Moment/VideoMomentSectionDetailControl.xaml | AppRepeater | {StaticResource MomentLayout} | MomentCardControl |
| Controls/Moment/VideoMomentSpaceControl.xaml | AppRepeater | {StaticResource MomentLayout} | MomentCardControl |
| Controls/Pgc/AnimeTimelineMainControl.xaml | AppRepeater | {StaticResource PgcLayout} | SeasonCardControl |
| Controls/Pgc/AnimeTimelineSideControl.xaml | ItemsView | inline/default | ItemsView.Layout, StackLayout |
| Controls/Pgc/EntertainmentIndexMainControl.xaml | AppRepeater | {StaticResource PgcLayout} | SeasonCardControl |
| Controls/Pgc/EntertainmentIndexSideControl.xaml | AppRepeater | inline/default | AppRepeater.Layout, StackLayout |
| Controls/Pinner.xaml | ItemsRepeater | inline/default | ItemsRepeater.Layout, StackLayout |
| Controls/Player/FavoriteButton.xaml | AppRepeater | inline/default | AppRepeater.Layout, StackLayout |
| Controls/Player/PgcEpisodesSection.xaml | AppRepeater | {StaticResource DefaultLayout} |  |
| Controls/Player/PgcEpisodesSection.xaml | AppRepeater | {StaticResource IndexLayout} |  |
| Controls/Player/PgcSeasonsSection.xaml | AppRepeater | inline/default | AppRepeater.Layout, SeasonCardControl, UniformGridLayout |
| Controls/Player/VideoDescriptorControl.xaml | AppRepeater | inline/default | AppRepeater.Layout, WrapLayout |
| Controls/Player/VideoPartsSection.xaml | ItemsView | inline/default |  |
| Controls/Player/VideoPlaylistSection.xaml | AppRepeater | inline/default | AppRepeater.Layout, StackLayout, VideoCardControl |
| Controls/Player/VideoRecommendSection.xaml | AppRepeater | inline/default | AppRepeater.Layout, StackLayout, VideoCardControl |
| Controls/Player/VideoSeasonSection.xaml | AppRepeater | inline/default | AppRepeater.Layout, StackLayout, VideoCardControl |
| Controls/Popular/PopularMainBody.xaml | AppRepeater | {StaticResource VideoLayout} |  |
| Controls/Popular/PopularSideBody.xaml | ItemsView | inline/default | ItemsView.Layout, StackLayout |
| Controls/Search/ArticleSectionDetailControl.xaml | AppRepeater | {StaticResource ArticleLayout} | ArticleCardControl |
| Controls/Search/LiveSectionDetailControl.xaml | AppRepeater | {StaticResource LiveLayout} | LiveCardControl |
| Controls/Search/PgcSectionDetailControl.xaml | AppRepeater | {StaticResource PgcLayout} | SeasonCardControl |
| Controls/Search/SearchSideBody.xaml | ItemsView | inline/default | ItemsView.Layout, StackLayout |
| Controls/Search/UserSectionDetailControl.xaml | AppRepeater | {StaticResource UserLayout} | UserCardControl |
| Controls/Search/VideoSectionDetailControl.xaml | AppRepeater | {StaticResource VideoLayout} | VideoCardControl |
| Controls/Settings/SponsorBlockSettingControl.xaml | ItemsControl | inline/default | SettingsCard |
| Controls/Users/FansBody.xaml | AppRepeater | {StaticResource UserLayout} | UserCardControl |
| Controls/Users/FollowsMainBody.xaml | AppRepeater | {StaticResource UserLayout} | UserCardControl |
| Controls/Users/FollowsSideBody.xaml | ItemsView | inline/default | ItemsView.Layout, StackLayout |
| Controls/VideoPartition/VideoPartitionMainBody.xaml | AppRepeater | {StaticResource VideoLayout} | VideoCardControl |
| Controls/VideoPartition/VideoPartitionSideBody.xaml | ItemsView | inline/default | ItemsView.Layout, StackLayout |
| Controls/ViewLater/ViewLaterBody.xaml | AppRepeater | {StaticResource VideoLayout} | VideoCardControl |
| Pages/GalleryPage.xaml | ItemsView | inline/default | ItemsView.Layout, StackLayout |

## Image semantics at each call site

| File | Image | Source | DecodeWidth | Size / corner radius |
| --- | --- | --- | --- | --- |
| Controls/AI/AIBody.xaml | BasicCoverImage | {x:Bind ViewModel.SourceCover, Mode=OneWay} | 200 | 100 × 76 / {StaticResource ControlCornerRadius} |
| Controls/Article/ArticleReaderToolBar.xaml | BasicCoverImage | {x:Bind ViewModel.Avatar, Mode=OneWay} | 100 | 48 × 48 / 24 |
| Controls/Article/ArticleReaderToolBar.xaml | Image | ms-appx:///Assets/vip.png | default | 16 × auto / 0 |
| Controls/Comment/CommentItemControl.xaml | BasicCoverImage | {x:Bind ViewModel.Avatar, Mode=OneWay} | 80 | 40 × 40 / 20 |
| Controls/Comment/CommentItemControl.xaml | Image | ms-appx:///Assets/vip.png | default | 16 × auto / 0 |
| Controls/Comment/CommentItemControl.xaml | Image | {x:Bind ViewModel.Level, Converter={StaticResource LevelImageConverter}, Mode=OneWay} | default | auto × 8 / 0 |
| Controls/Components/AccountCard.xaml | BasicCoverImage | {x:Bind ViewModel.MyProfile.User.Avatar.Uri, Mode=OneWay} | 100 | 48 × 48 / 24 |
| Controls/Components/AccountCard.xaml | Image | ms-appx:///Assets/vip.png | default | 16 × auto / 0 |
| Controls/Components/AccountCard.xaml | Image | {x:Bind ViewModel.MyProfile.Level, Mode=OneWay, Converter={StaticResource LevelImageConverter}} | default | auto × 10 / 0 |
| Controls/Components/AppSearchBox.xaml | Image | {x:Bind Icon.Uri, Converter={StaticResource BitmapConverter}} | default | 16 × auto / 0 |
| Controls/Components/ArticleCardControl/HistoryArticlePresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/ArticleCardControl/PartitionArticlePresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/ArticleCardControl/PartitionArticlePresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Avatar} | 64 | 32 × 32 / 16 |
| Controls/Components/ArticleCardControl/SearchArticlePresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/EmotePanel.xaml | BasicCoverImage | {x:Bind Icon.Uri} | 60 | 28 × 28 / {StaticResource ControlCornerRadius} |
| Controls/Components/EmotePanel.xaml | BasicCoverImage | {x:Bind Image.Uri} | 100 | 32 × 32 / {StaticResource ControlCornerRadius} |
| Controls/Components/EmoteTextBlock.xaml | BasicCoverImage | {x:Bind Uri} | 200 | 120 × 120 / {StaticResource ControlCornerRadius} |
| Controls/Components/EpisodeCardControl/MomentEpisodePresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | 600 | auto × auto / {StaticResource ControlCornerRadius} |
| Controls/Components/EpisodeCardControl/PlayerEpisodePresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | 600 | auto × auto / 4 |
| Controls/Components/HeaderAccountControl.xaml | BasicCoverImage | {x:Bind ViewModel.MyProfile.User.Avatar.Uri, Mode=OneWay} | 56 | 28 × 28 / 14 |
| Controls/Components/LiveCardControl/FollowLivePresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Avatar, Mode=OneWay} | 100 | 48 × 48 / 24 |
| Controls/Components/LiveCardControl/HistoryLivePresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/LiveCardControl/PartitionLivePresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/LiveCardControl/PartitionLivePresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Avatar, Mode=OneWay} | 100 | 32 × 32 / 16 |
| Controls/Components/LiveCardControl/RecommendLivePresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/LiveCardControl/SearchLivePresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/MomentCardControl/FullMomentPresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Avatar, Mode=OneWay} | 100 | 36 × 36 / 18 |
| Controls/Components/MomentCardControl/PersonalVideoMomentPresenter.xaml | BasicCoverImage | {x:Bind ViewModel.VideoCover, Mode=OneWay} | 600 | auto × auto / {StaticResource ControlCornerRadius} |
| Controls/Components/MomentCardControl/VideoMomentPresenter.xaml | BasicCoverImage | {x:Bind ViewModel.Avatar, Mode=OneWay} | 100 | 36 × 36 / 18 |
| Controls/Components/MomentCardControl/VideoMomentPresenter.xaml | BasicCoverImage | {x:Bind ViewModel.VideoCover, Mode=OneWay} | 600 | auto × auto / {StaticResource ControlCornerRadius} |
| Controls/Components/PartitionItemControl.xaml | BasicCoverImage | {x:Bind ViewModel.Logo, Mode=OneWay} | 60 | 20 × 20 / 0 |
| Controls/Components/SeasonCardControl/FavoriteSeasonPresenter.xaml | BlurCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | default | auto × auto / {StaticResource ControlCornerRadius} |
| Controls/Components/SeasonCardControl/IndexSeasonPresenter.xaml | BlurCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | default | auto × auto / {StaticResource ControlCornerRadius} |
| Controls/Components/SeasonCardControl/PlayerSeasonPresenter.xaml | BlurCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | default | auto × auto / {StaticResource ControlCornerRadius} |
| Controls/Components/SeasonCardControl/TimelineSeasonPresenter.xaml | BlurCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | default | auto × auto / {StaticResource ControlCornerRadius} |
| Controls/Components/UserCardControl.xaml | BasicCoverImage | {x:Bind ViewModel.Avatar, Mode=OneWay} | 100 | 48 × 48 / 24 |
| Controls/Components/UserCardControl.xaml | Image | ms-appx:///Assets/vip.png | default | 16 × auto / 0 |
| Controls/Components/UserCardControl.xaml | Image | {x:Bind ViewModel.Level, Converter={StaticResource LevelImageConverter}, Mode=OneWay} | default | auto × 9 / 0 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | RecommendationCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Cover} | default | auto × auto / 4,4,0,0 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Cover} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Cover} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Avatar} | 64 | 32 × 32 / 16 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Cover} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Cover} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Cover} | 600 | auto × auto / {StaticResource ControlCornerRadius} |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Cover} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Avatar} | 64 | 32 × 32 / 16 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Cover} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Cover} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Avatar} | 64 | 32 × 32 / 16 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Cover} | 600 | auto × auto / 4,4,0,0 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Cover} | 600 | auto × auto / 4 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Cover} | 600 | auto × auto / 4 |
| Controls/Components/VideoCardControl/VideoCardControl.xaml | BasicCoverImage | {Binding RelativeSource={RelativeSource Mode=TemplatedParent}, Path=ViewModel.Cover} | 600 | auto × auto / 4 |
| Controls/Favorites/UgcFavoriteSideItem.xaml | BasicCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | 100 | 40 × 40 / 20 |
| Controls/Message/ChatMessageHeader.xaml | BasicCoverImage | {x:Bind ViewModel.Avatar, Mode=OneWay} | 100 | 36 × 36 / 18 |
| Controls/Message/ChatSessionSideItem.xaml | BasicCoverImage | {x:Bind ViewModel.Avatar, Mode=OneWay} | 100 | 36 × 36 / 18 |
| Controls/Message/NotifyMessageCardControl.xaml | BasicCoverImage | {x:Bind ViewModel.FirstUserAvatar, Mode=OneWay} | 100 | 32 × 32 / 16 |
| Controls/Moment/MomentImageGallery.xaml | BasicCoverImage | {x:Bind Uri} | 300 | 120 × 120 / {StaticResource ControlCornerRadius} |
| Controls/Moment/MomentUperItemControl.xaml | BasicCoverImage | {x:Bind ViewModel.Avatar, Mode=OneWay} | 100 | 28 × 28 / 14 |
| Controls/Moment/UserSpacePageHeader.xaml | BasicCoverImage | {x:Bind ViewModel.Card.Profile.User.Avatar.SourceUri, Mode=OneWay} | 100 | 48 × 48 / 24 |
| Controls/Moment/UserSpacePageHeader.xaml | Image | ms-appx:///Assets/vip.png | default | 16 × auto / 0 |
| Controls/Moment/UserSpacePageHeader.xaml | Image | {x:Bind ViewModel.Card.Profile.Level, Mode=OneWay, Converter={StaticResource LevelImageConverter}} | default | auto × 12 / 0 |
| Controls/Pinner.xaml | BasicCoverImage | {x:Bind Cover} | 100 | 42 × 42 / 21 |
| Controls/Player/PgcPlayerWidget.xaml | BasicCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | 320 | 100 × 68 / {StaticResource ControlCornerRadius} |
| Controls/Player/UpSectionControl.xaml | BasicCoverImage | {x:Bind Avatar, Mode=OneWay} | 100 | 44 × 44 / 22 |
| Controls/Player/VideoPlayerWidget.xaml | BasicCoverImage | {x:Bind ViewModel.Cover, Mode=OneWay} | 320 | 100 × 68 / {StaticResource ControlCornerRadius} |
| Controls/Popular/PopularRankParitionItemControl.xaml | BasicCoverImage | {x:Bind ViewModel.Logo, Mode=OneWay} | 50 | 18 × 18 / 0 |
| Pages/GalleryPage.xaml | Image | {x:Bind ViewModel.SelectedImage.SourceUri, Mode=OneWay, Converter={StaticResource BitmapConverter}} | default | auto × auto / 0 |
| Pages/GalleryPage.xaml | BasicCoverImage | {x:Bind Uri} | 600 | 100 × 80 / 0 |
| Pages/SettingsPage.xaml | Image | {ThemeResource QQGroup} | default | auto × auto / 0 |
| Pages/StartupPage.xaml | Image | ms-appx:///Assets/StoreLogo.png | default | 148 × auto / 0 |
| Pages/StartupPage.xaml | Image |  | default | auto × auto / 0 |
