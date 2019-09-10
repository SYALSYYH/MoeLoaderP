﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using MoeLoader.Core;

namespace MoeLoader.UI
{
    /// <summary>
    /// 缩略图面板中的图片用户控件
    /// </summary>
    public partial class ImageControl
    {
        public ImageItem ImageItem { get; set; }
        public Settings Settings { get; set; }

        public event Action<ImageControl> ImageLoadEnd;

        public ImageControl(Settings settings , ImageItem item)
        {
            Settings = settings;
            ImageItem = item;
            DataContext = this;
            InitializeComponent();

            MouseEnter += (sender, args) => VisualStateManager.GoToState(this, nameof(MouseOverState), true);
            MouseLeave += (sender, args) => VisualStateManager.GoToState(this, nameof(NormalState), true);

            ScoreBorder.Visibility = item.Site.SurpportState.IsSupportScore ? Visibility.Visible : Visibility.Collapsed;
            ResolutionBorder.Visibility = item.Site.SurpportState.IsSupportResolution ? Visibility.Visible : Visibility.Collapsed;

            DetailPageLinkButton.Click += (sender, args) => ImageItem.DetailUrl.Go();

            RefreshButton.Click += RefreshButtonOnClick;
        }

        private async void RefreshButtonOnClick(object sender, RoutedEventArgs e)
        {
            await LoadImageAsync();
        }

        public async Task LoadImageAsync()
        {
            var loadingsb = this.Sb("LoadingSb");
            loadingsb.Begin();
            this.Sb("LoadingStartSb").Begin();
            
            // client
            var net = ImageItem.Net ?? new NetSwap(Settings);
            net.SetTimeOut(15);
            net.SetReferer(ImageItem.ThumbnailUrlInfo.Referer);
            Exception loadex = null;
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var getDetailTask = ImageItem.GetDetailAsync();
                var response = await net.Client.GetAsync(ImageItem.ThumbnailUrlInfo.Url, cts.Token);
                var stream = await response.Content.ReadAsStreamAsync();
                var source = await Task.Run(() =>
                {
                    try
                    {
                        var bitm = new BitmapImage
                        {
                            CacheOption = BitmapCacheOption.OnLoad,
                            CreateOptions = BitmapCreateOptions.IgnoreImageCache
                        };
                        bitm.BeginInit();
                        bitm.StreamSource = stream;
                        bitm.EndInit();
                        bitm.Freeze();
                        stream.Dispose();
                        return bitm;
                    }
                    catch (Exception ex)
                    {
                        App.Log(ex);
                        return null;
                    }
                }, cts.Token);
                if (source == null) loadex = new Exception("image source is null");
                else PreviewImage.Source = source;
                await getDetailTask;
            }
            catch (Exception ex)
            {
                loadex = ex;
            }

            if (loadex == null)
            {
                var loadSb = this.Sb("LoadedSb");
                loadSb.Completed += (sender, args) => loadingsb.Stop();
                loadSb.Begin();
                RefreshButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                App.Log(loadex);
                var loadfsb = this.Sb("LoadFailSb");
                loadfsb.Completed += (sender, args) => loadingsb.Stop();
                loadfsb.Begin();
                App.Log($"{ImageItem.ThumbnailUrlInfo.Url} 图片加载失败");
            }

            // Loaded
            ImageLoadEnd?.Invoke(this);
        }
    }
}