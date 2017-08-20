using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

using Shadowsocks.Model;
using Shadowsocks.Util;

namespace Shadowsocks.Controller
{
    public class UpdateChecker
    {
        private const string UpdateURL = "https://www.baidu.com";
        private const string UserAgent = "Mozilla/5.0 (Windows NT 5.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/35.0.3319.102 Safari/537.36";

        private Configuration config;
        public bool NewVersionFound;
        public string LatestVersionNumber;
        public string LatestVersionSuffix;
        public string LatestVersionName;
        public string LatestVersionURL;
        public string LatestVersionLocalName;
        public event EventHandler CheckUpdateCompleted;

        public const string Version = "4.0.5";

        private class CheckUpdateTimer : System.Timers.Timer
        {
            public Configuration config;

            public CheckUpdateTimer(int p) : base(p)
            {
            }
        }

        public void CheckUpdate(Configuration config, int delay)
        {
            CheckUpdateTimer timer = new CheckUpdateTimer(delay);
            timer.AutoReset = false;
            timer.Elapsed += Timer_Elapsed;
            timer.config = config;
            timer.Enabled = true;
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            CheckUpdateTimer timer = (CheckUpdateTimer)sender;
            Configuration config = timer.config;
            timer.Elapsed -= Timer_Elapsed;
            timer.Enabled = false;
            timer.Dispose();
            CheckUpdate(config);
        }

        public void CheckUpdate(Configuration config)
        {
            this.config = config;

            try
            {
                Logging.Debug("Checking updates...");
                WebClient http = CreateWebClient();
                http.DownloadStringCompleted += http_DownloadStringCompleted;
                http.DownloadStringAsync(new Uri(UpdateURL));
            }
            catch (Exception ex)
            {
                Logging.LogUsefulException(ex);
            }
        }

        private void http_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                Logging.Debug("Do nothing, auto update is turned off!");
            }
            catch (Exception ex)
            {
                Logging.LogUsefulException(ex);
            }
        }

        private void startDownload()
        {
            try
            {
                LatestVersionLocalName = Utils.GetTempPath(LatestVersionName);
                WebClient http = CreateWebClient();
                http.DownloadFileCompleted += Http_DownloadFileCompleted;
                http.DownloadFileAsync(new Uri(LatestVersionURL), LatestVersionLocalName);
            }
            catch (Exception ex)
            {
                Logging.LogUsefulException(ex);
            }
        }

        private void Http_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null)
                {
                    Logging.LogUsefulException(e.Error);
                    return;
                }
                Logging.Debug($"New version {LatestVersionNumber}{LatestVersionSuffix} found: {LatestVersionLocalName}");
                if (CheckUpdateCompleted != null)
                {
                    CheckUpdateCompleted(this, new EventArgs());
                }
            }
            catch (Exception ex)
            {
                Logging.LogUsefulException(ex);
            }
        }

        private WebClient CreateWebClient()
        {
            WebClient http = new WebClient();
            http.Headers.Add("User-Agent", UserAgent);
            http.Proxy = new WebProxy(IPAddress.Loopback.ToString(), config.localPort);
            return http;
        }

        private void SortByVersions(List<Asset> asserts)
        {
            asserts.Sort();
        }

        public class Asset : IComparable<Asset>
        {
            public bool prerelease;
            public string name;
            public string version;
            public string browser_download_url;
            public string suffix;

            public static Asset ParseAsset(JObject assertJObject)
            {
                var name = (string) assertJObject["name"];
                Match match = Regex.Match(name, @"^Shadowsocks-(?<version>\d+(?:\.\d+)*)(?:|-(?<suffix>.+))\.\w+$",
                    RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string version = match.Groups["version"].Value;

                    var asset = new Asset
                    {
                        browser_download_url = (string) assertJObject["browser_download_url"],
                        name = name,
                        version = version
                    };

                    if (match.Groups["suffix"].Success)
                    {
                        asset.suffix = match.Groups["suffix"].Value;
                    }

                    return asset;
                }

                return null;
            }

            public bool IsNewVersion(string currentVersion, bool checkPreRelease)
            {
                if (prerelease && !checkPreRelease)
                {
                    return false;
                }
                if (version == null)
                {
                    return false;
                }
                var cmp = CompareVersion(version, currentVersion);
                return cmp > 0;
            }

            public static int CompareVersion(string l, string r)
            {
                var ls = l.Split('.');
                var rs = r.Split('.');
                for (int i = 0; i < Math.Max(ls.Length, rs.Length); i++)
                {
                    int lp = (i < ls.Length) ? int.Parse(ls[i]) : 0;
                    int rp = (i < rs.Length) ? int.Parse(rs[i]) : 0;
                    if (lp != rp)
                    {
                        return lp - rp;
                    }
                }
                return 0;
            }

            public int CompareTo(Asset other)
            {
                return CompareVersion(version, other.version);
            }
        }
    }
}
