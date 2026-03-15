using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Web;

namespace CustomBrowserWPF
{
    public class HistoryItem
    {
        public string Url { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime Date { get; set; }
    }

    public class SettingsData
    {
        public string DownloadFolder { get; set; } = "";
        public string ImportBrowser { get; set; } = "edge";
    }

    public partial class MainWindow : Window
    {
        private string saveFolder;
        private string historyFile;
        private string settingsFile;
        private SettingsData settings = new SettingsData();
        private List<HistoryItem> browserHistory = new List<HistoryItem>();
        private List<string> suggestions = new List<string>();
        private bool isUpdatingAddressBar = false;
        private bool isUserTyping = false;

        private List<string> defaultSuggestions = new List<string>
        {
            "youtube", "facebook", "twitter", "gmail", "wikipedia", "amazon", "reddit", "instagram", "netflix"
        };

        private CancellationTokenSource suggestionCts = new CancellationTokenSource();

        // fullscreen/maximized
        private bool isFullscreen = false;
        private Rect restoreBounds;

        public MainWindow()
        {
            InitializeComponent();

            saveFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saves");
            Directory.CreateDirectory(saveFolder);

            historyFile = Path.Combine(saveFolder, "history.json");
            settingsFile = Path.Combine(saveFolder, "settings.json");

            LoadOrCreateSettings();

            CreateNewTab("https://www.google.com");

            this.KeyDown += MainWindow_KeyDown;

            AddressBar.PreviewMouseLeftButtonDown += (s, e) => AddressBar.SelectAll();
            AddressBar.PreviewTextInput += (s, e) => { isUserTyping = true; };
            AddressBar.PreviewKeyDown += AddressBar_KeyDown;
            AddressBar.TextChanged += AddressBar_TextChanged;
        }

        private void LoadOrCreateSettings()
        {
            if (!File.Exists(settingsFile))
            {
                settings = new SettingsData
                {
                    DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Downloads"),
                    ImportBrowser = "edge"
                };
                SaveSettings();
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(settingsFile);
                    settings = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
                }
                catch { settings = new SettingsData(); }
            }
        }

        private void SaveSettings()
        {
            File.WriteAllText(settingsFile, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                ToggleFullscreen();
            }
        }

        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                restoreBounds = new Rect(this.Left, this.Top, this.Width, this.Height);
                TopBar.Visibility = Visibility.Collapsed;
                WindowState = WindowState.Maximized;
                isFullscreen = true;
            }
            else
            {
                TopBar.Visibility = Visibility.Visible;
                WindowState = WindowState.Normal;
                this.Left = restoreBounds.Left;
                this.Top = restoreBounds.Top;
                this.Width = restoreBounds.Width;
                this.Height = restoreBounds.Height;
                isFullscreen = false;
            }
        }

        public void CreateNewTab(string url)
        {
            var webview = new WebView2();
            var tab = new TabItem();

            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            Image favicon = new Image { Width = 16, Height = 16, Margin = new Thickness(2, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock titleBlock = new TextBlock { Text = "New Tab", VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 5, 0) };
            Button closeButton = new Button { Content = "X", Width = 20, Height = 20, Margin = new Thickness(5, 0, 5, 0) };

            Grid.SetColumn(favicon, 0);
            Grid.SetColumn(titleBlock, 1);
            Grid.SetColumn(closeButton, 2);

            headerGrid.Children.Add(favicon);
            headerGrid.Children.Add(titleBlock);
            headerGrid.Children.Add(closeButton);

            tab.Header = headerGrid;
            tab.Content = webview;

            BrowserTabs.Items.Add(tab);
            BrowserTabs.SelectedItem = tab;

            closeButton.Click += (s, e) =>
            {
                if (BrowserTabs.Items.Count > 1)
                    BrowserTabs.Items.Remove(tab);
                else
                    this.Close();
            };

            BrowserTabs.SelectionChanged += (s, e) => UpdateTabXButtons();

            _ = InitializeWebView(webview, url, favicon, titleBlock, tab);
        }

        private void UpdateTabXButtons()
        {
            foreach (TabItem t in BrowserTabs.Items)
            {
                if (t.Header is Grid g && g.Children.Count == 3 && g.Children[2] is Button b)
                {
                    b.Visibility = t.IsSelected ? Visibility.Visible : Visibility.Hidden;
                }
            }
        }

        private async Task InitializeWebView(WebView2 webview, string url, Image favicon, TextBlock titleBlock, TabItem tab)
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(saveFolder, "webview2userdata"));
            await webview.EnsureCoreWebView2Async(env);

            webview.CoreWebView2.Settings.AreDevToolsEnabled = true;
            webview.CoreWebView2.Settings.IsZoomControlEnabled = true;

            void UpdateTabHeader()
            {
                try
                {
                    string currentUrl = webview.Source?.AbsoluteUri ?? webview.CoreWebView2.Source;

                    isUpdatingAddressBar = true;
                    AddressBar.Text = currentUrl;
                    AddressPlaceholder.Visibility = string.IsNullOrEmpty(AddressBar.Text) && isUserTyping ? Visibility.Visible : Visibility.Collapsed;
                    isUpdatingAddressBar = false;

                    string title = webview.CoreWebView2.DocumentTitle;
                    titleBlock.Text = title.Length > 25 ? title.Substring(0, 22) + "..." : title;

                    Uri uri = new Uri(currentUrl);
                    favicon.Source = new BitmapImage(new Uri($"{uri.Scheme}://{uri.Host}/favicon.ico"));

                    if (!suggestions.Contains(currentUrl))
                        suggestions.Add(currentUrl);
                }
                catch { }
            }

            webview.CoreWebView2.NavigationStarting += (s, e) => { titleBlock.Text = "Loading..."; };
            webview.CoreWebView2.NavigationCompleted += (s, e) => UpdateTabHeader();
            webview.CoreWebView2.SourceChanged += (s, e) => UpdateTabHeader();
            webview.CoreWebView2.HistoryChanged += (s, e) => UpdateTabHeader();
            webview.CoreWebView2.DocumentTitleChanged += (s, e) => UpdateTabHeader();

            webview.CoreWebView2.ContainsFullScreenElementChanged += (s, e) =>
            {
                if (webview.CoreWebView2.ContainsFullScreenElement)
                {
                    if (!isFullscreen)
                    {
                        TopBar.Visibility = Visibility.Collapsed;
                        WindowState = WindowState.Maximized;
                        isFullscreen = true;
                    }
                }
                else
                {
                    if (isFullscreen)
                    {
                        TopBar.Visibility = Visibility.Visible;
                        WindowState = WindowState.Normal;
                        this.Left = restoreBounds.Left;
                        this.Top = restoreBounds.Top;
                        this.Width = restoreBounds.Width;
                        this.Height = restoreBounds.Height;
                        isFullscreen = false;
                    }
                }
            };

            webview.CoreWebView2.Navigate(url);
        }

        private WebView2? GetCurrentWebView()
        {
            if (BrowserTabs.SelectedItem is TabItem tab && tab.Content is WebView2 webview)
                return webview;
            return null;
        }

        private void Navigate()
        {
            var webview = GetCurrentWebView();
            if (webview == null) return;

            string input = AddressBar.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            string url = input.Contains(".") || input.StartsWith("http") ? (input.StartsWith("http") ? input : "https://" + input) : "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
            webview.CoreWebView2.Navigate(url);

            isUserTyping = false;
        }

        private void GoButton_Click(object sender, RoutedEventArgs e) => Navigate();
        private void NewTab_Click(object sender, RoutedEventArgs e) => CreateNewTab("https://www.google.com");
        private void BackButton_Click(object sender, RoutedEventArgs e) => GetCurrentWebView()?.CoreWebView2.GoBack();
        private void ForwardButton_Click(object sender, RoutedEventArgs e) => GetCurrentWebView()?.CoreWebView2.GoForward();
        private void ReloadButton_Click(object sender, RoutedEventArgs e) => GetCurrentWebView()?.CoreWebView2.Reload();

        private async void AddressBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingAddressBar || !isUserTyping)
                return;

            AddressPlaceholder.Visibility = string.IsNullOrEmpty(AddressBar.Text) && isUserTyping
                ? Visibility.Visible
                : Visibility.Collapsed;

            string input = AddressBar.Text.Trim();
            suggestionCts?.Cancel();
            suggestionCts = new CancellationTokenSource();
            var token = suggestionCts.Token;

            if (string.IsNullOrEmpty(input))
            {
                SuggestionPopup.IsOpen = false;
                return;
            }

            try
            {
                await Task.Delay(200, token); // debounce
                var liveSuggestions = await GetGoogleSuggestions(input);

                var combined = new List<string>();

                // history matches
                foreach (var h in suggestions)
                {
                    if (h.ToLower().Contains(input.ToLower()))
                    {
                        string suggestionText = h;
                        if (h.Contains("google.com/search") && h.Contains("q="))
                        {
                            try
                            {
                                var uri = new Uri(h);
                                var query = HttpUtility.ParseQueryString(uri.Query).Get("q");
                                if (!string.IsNullOrEmpty(query))
                                    suggestionText = query;
                            }
                            catch { }
                        }
                        if (!combined.Contains(suggestionText))
                            combined.Add(suggestionText);
                    }
                }

                // live google suggestions
                foreach (var s in liveSuggestions)
                {
                    if (!combined.Contains(s))
                        combined.Add(s);
                }

                if (combined.Count == 0)
                    combined.AddRange(defaultSuggestions);

                SuggestionList.Items.Clear();
                foreach (var s in combined)
                    SuggestionList.Items.Add(s);

                SuggestionPopup.IsOpen = SuggestionList.Items.Count > 0;
            }
            catch (OperationCanceledException) { }
        }

        private async Task<List<string>> GetGoogleSuggestions(string query)
        {
            try
            {
                using var client = new HttpClient();
                string url = $"https://suggestqueries.google.com/complete/search?client=firefox&q={Uri.EscapeDataString(query)}";
                var response = await client.GetStringAsync(url);
                var suggestions = JsonSerializer.Deserialize<List<object>>(response) ?? new List<object>();
                if (suggestions.Count >= 2 && suggestions[1] is JsonElement arr && arr.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var item in arr.EnumerateArray())
                        list.Add(item.GetString() ?? "");
                    return list;
                }
            }
            catch { }
            return new List<string>();
        }

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Enter || e.Key == Key.Tab) && SuggestionList.Items.Count > 0)
            {
                if (SuggestionList.Items[0] is string first)
                {
                    isUpdatingAddressBar = true;
                    AddressBar.Text = first;
                    AddressBar.CaretIndex = first.Length;
                    isUpdatingAddressBar = false;

                    if (e.Key == Key.Enter) Navigate();
                }

                e.Handled = true;
                SuggestionPopup.IsOpen = false;
            }

            if (e.Key == Key.Back || e.Key == Key.Delete)
                isUserTyping = true;
            else
                isUserTyping = false;
        }

        private void Suggestion_Click(object sender, MouseButtonEventArgs e)
        {
            if (SuggestionList.SelectedItem is string selected)
            {
                isUpdatingAddressBar = true;
                AddressBar.Text = selected;
                AddressBar.CaretIndex = selected.Length;
                isUpdatingAddressBar = false;
                Navigate();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            string settingsPath = Path.Combine(saveFolder, "settings.html");
            if (!File.Exists(settingsPath))
            {
                File.WriteAllText(settingsPath, "<!DOCTYPE html><html><body><h1>Settings</h1></body></html>");
            }
            CreateNewTab(settingsPath);
        }
    }
}