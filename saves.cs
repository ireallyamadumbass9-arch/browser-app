using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CustomBrowserWPF
{
    public class HistoryItem
    {
        public string Url { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime Date { get; set; } = DateTime.Now;
    }

    public static class Saves
    {
        public static readonly string SaveFolder =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saves");

        public static readonly string CookiesFolder = Path.Combine(SaveFolder, "cookies");
        public static readonly string CacheFolder = Path.Combine(SaveFolder, "cache");
        public static readonly string WebViewUserDataFolder = Path.Combine(SaveFolder, "webview2userdata");

        private static readonly string HistoryFile = Path.Combine(SaveFolder, "history.json");
        private static List<HistoryItem> HistoryCache = new List<HistoryItem>();

        static Saves()
        {
            Directory.CreateDirectory(SaveFolder);
            Directory.CreateDirectory(CookiesFolder);
            Directory.CreateDirectory(CacheFolder);
            Directory.CreateDirectory(WebViewUserDataFolder);

            LoadHistory();
        }

        private static void LoadHistory()
        {
            if (!File.Exists(HistoryFile))
            {
                HistoryCache = new List<HistoryItem>();
                SaveHistory();
                return;
            }

            try
            {
                string json = File.ReadAllText(HistoryFile);
                HistoryCache = JsonSerializer.Deserialize<List<HistoryItem>>(json)
                               ?? new List<HistoryItem>();
            }
            catch
            {
                HistoryCache = new List<HistoryItem>();
            }
        }

        private static void SaveHistory()
        {
            try
            {
                string json = JsonSerializer.Serialize(HistoryCache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(HistoryFile, json);
            }
            catch { }
        }

        public static void AddHistory(string url, string title)
        {
            var item = new HistoryItem
            {
                Url = url,
                Title = title,
                Date = DateTime.Now
            };

            HistoryCache.Add(item);
            SaveHistory();
        }

        public static List<HistoryItem> GetHistory()
        {
            return new List<HistoryItem>(HistoryCache);
        }

        public static async System.Threading.Tasks.Task<CoreWebView2Environment> CreateWebView2EnvironmentAsync()
        {
            Directory.CreateDirectory(WebViewUserDataFolder);
            return await CoreWebView2Environment.CreateAsync(null, WebViewUserDataFolder);
        }
    }
}