﻿using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace MoeLoaderP.Core.Sites
{
    /// <summary>
    /// zerochan.net fixed 20200323
    /// </summary>
    public class ZeroChanSite : MoeSite
    {
        public override string HomeUrl => "http://www.zerochan.net";

        public override string DisplayName => "Zerochan";

        public override string ShortName => "zerochan";
        
        private readonly string[] _user = { "zerouser1" };
        private readonly string[] _pass = { "zeropass" };
        private string _beforeWord = "", _beforeUrl = "";

        public ZeroChanSite()
        {
            SupportState.IsSupportRating = false;
            DownloadTypes.Add("原图", 4);
            DownloadTypes.Add("预览图", 2);
        }

        private bool IsLogon { get; set; }

        public override async Task<MoeItems> GetRealPageImagesAsync(SearchPara para, CancellationToken token)
        {
            // logon
            if (!IsLogon)
            {
                Net = new NetDocker(Settings,HomeUrl);
                var index = new Random().Next(0, _user.Length);
                var loginurl = "https://www.zerochan.net/login";

                var response = await Net.Client.PostAsync(loginurl, 
                    new StringContent($"ref=%2F&login=Login&name={_user[index]}&password={_pass[index]}"), token);

                if (response.IsSuccessStatusCode) IsLogon = true;
            }
            if(!IsLogon) return new MoeItems();

            // get page source
            string pageString;
            var url = $"{HomeUrl}{(para.Keyword.Length > 0 ? $"/search?q={para.Keyword}&" : "/?")}p={para.PageIndex}";

            if (!_beforeWord.Equals(para.Keyword, StringComparison.CurrentCultureIgnoreCase))
            {
                // 301
                var respose = await Net.Client.GetAsync(url, token);
                if (respose.IsSuccessStatusCode)
                    _beforeUrl = respose.Headers.Location.AbsoluteUri;
                else
                {
                    Extend.ShowMessage("搜索失败，请检查您输入的关键词");
                    return new MoeItems();
                }
                
                pageString = await respose.Content.ReadAsStringAsync();

                _beforeWord = para.Keyword;
            }
            else
            {
                url = string.IsNullOrWhiteSpace(para.Keyword) ? url : $"{_beforeUrl}?p={para.PageIndex}";
                var res = await Net.Client.GetAsync(url, token);

                pageString = await res.Content.ReadAsStringAsync();
            }

            // images
            var imgs = new MoeItems();
            var doc = new HtmlDocument();
            doc.LoadHtml(pageString);
            HtmlNodeCollection nodes;
            try
            {
                nodes = doc.DocumentNode.SelectSingleNode("//ul[@id='thumbs2']").SelectNodes(".//li");
            }
            catch
            {
                Extend.ShowMessage("没有搜索到图片");
                return new MoeItems();
            }

            foreach (var imgNode in nodes)
            {
                var img = new MoeItem(this,para);
                var strId = imgNode.SelectSingleNode("a").Attributes["href"].Value;
                int.TryParse(strId.Substring(1),out var id);
                var fav = imgNode.SelectSingleNode("a/span")?.InnerText;
                if (!string.IsNullOrWhiteSpace(fav)) img.Score = Regex.Replace(fav, @"[^0-9]+", "")?.ToInt() ?? 0;
                var imgHref = imgNode.SelectSingleNode(".//img");
                var previewUrl = imgHref?.Attributes["src"]?.Value;
                //http://s3.zerochan.net/Morgiana.240.1355397.jpg   preview
                //http://s3.zerochan.net/Morgiana.600.1355397.jpg    sample
                //http://static.zerochan.net/Morgiana.full.1355397.jpg   full
                //先加前一个，再加后一个  范围都是00-49
                //string folder = (id % 2500 % 50).ToString("00") + "/" + (id % 2500 / 50).ToString("00");
                var sampleUrl = "";
                var fileUrl = "";
                if (!string.IsNullOrWhiteSpace(previewUrl))
                {
                    sampleUrl = previewUrl?.Replace("240", "600");
                    fileUrl = Regex.Replace(previewUrl, "^(.+?)zerochan.net/", "https://static.zerochan.net/").Replace("240", "full");
                }
                
                var resandfilesize = imgHref?.Attributes["title"].Value;
                var dimension = resandfilesize?.Substring(0, resandfilesize.IndexOf(' '));
                var title = imgHref?.Attributes["alt"].Value;

                int width = 0, height = 0;
                try
                {
                    width = dimension.Substring(0, dimension.IndexOf('x')).ToInt();
                    height = dimension.Substring(dimension.IndexOf('x') + 1).ToInt();
                }
                catch
                {
                    // ignored
                }

                //convert relative url to absolute
                if (!string.IsNullOrWhiteSpace(fileUrl) && fileUrl.StartsWith("/")) fileUrl = $"{HomeUrl}{fileUrl}";
                if (sampleUrl != null && sampleUrl.StartsWith("/")) sampleUrl = HomeUrl + sampleUrl;

                img.Description = title;
                img.Title = title;
                img.Id = id;

                img.Urls.Add(new UrlInfo("缩略图", 1, previewUrl, HomeUrl));
                img.Urls.Add(new UrlInfo("预览图", 2, sampleUrl, HomeUrl));
                img.Urls.Add(new UrlInfo("原图", 4, fileUrl, img.DetailUrl));
                img.Width = width;
                img.Height = height;
                img.DetailUrl = $"{HomeUrl}/{id}";
                
                img.OriginString = imgNode.OuterHtml;
                imgs.Add(img);
            }
            token.ThrowIfCancellationRequested();
            return imgs;
        }

        public override async Task<AutoHintItems> GetAutoHintItemsAsync(SearchPara para, CancellationToken token)
        {
            //http://www.zerochan.net/suggest?q=tony&limit=8
            var re = new AutoHintItems();

            var url = $"{HomeUrl}/suggest?limit=8&q={para.Keyword}";

            Net.Client.DefaultRequestHeaders.Referrer =  new Uri(HomeUrl);
            var res = await Net.Client.GetAsync(url, token);

            var txt = await res.Content.ReadAsStringAsync();

            var lines = txt.Split('\n');
            for (var i = 0; i < lines.Length && i < 8; i++)
            {
                //Tony Taka|Mangaka|
                if (lines[i].Trim().Length > 0) re.Add(new AutoHintItem { Word = lines[i].Substring(0, lines[i].IndexOf('|')).Trim() });
            }

            return re;
        }

    }
}
