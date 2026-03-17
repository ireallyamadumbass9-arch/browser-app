using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace CustomBrowserWPF
{
    public partial class MainWindow : Window
    {
        private bool isFullscreen = false;
        private bool isUpdatingAddressBar = false;
        private Rect restoreBounds;

        public MainWindow()
        {
            InitializeComponent();

            // create first tab
            CreateNewTab("https://www.google.com");

            this.KeyDown += MainWindow_KeyDown;

            AddressBar.PreviewMouseLeftButtonDown += (s, e) => AddressBar.SelectAll();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
                ToggleFullscreen();
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

            Image favicon = new Image
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(2, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock titleBlock = new TextBlock
            {
                Text = "New Tab",
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 5, 0)
            };

            Button closeButton = new Button
            {
                Content = "X",
                Width = 20,
                Height = 20,
                Margin = new Thickness(5, 0, 5, 0)
            };

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
                if (BrowserTabs.Items.Contains(tab))
                {
                    if (tab.Content is WebView2 wv)
                    {
                        wv.CoreWebView2?.Stop();
                        wv.Dispose();
                    }

                    BrowserTabs.Items.Remove(tab);
                }
            };

            BrowserTabs.SelectionChanged += (s, e) => UpdateTabXButtons();

            _ = InitializeWebView(webview, url, favicon, titleBlock);
        }

        private void UpdateTabXButtons()
        {
            foreach (TabItem t in BrowserTabs.Items)
            {
                if (t.Header is Grid g && g.Children.Count == 3 && g.Children[2] is Button b)
                    b.Visibility = t.IsSelected ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private async Task InitializeWebView(WebView2 webview, string url, Image favicon, TextBlock titleBlock)
        {
            // FORCE all WebView2 data to go in saves/webview2userdata
            var env = await Saves.CreateWebView2EnvironmentAsync();
            await webview.EnsureCoreWebView2Async(env);

            webview.CoreWebView2.Settings.AreDevToolsEnabled = true;
            webview.CoreWebView2.Settings.IsZoomControlEnabled = true;

            webview.CoreWebView2.NavigationStarting += (s, e) =>
            {
                titleBlock.Text = "Loading...";
            };

            void UpdateTabHeader()
            {
                try
                {
                    string currentUrl = webview.Source?.AbsoluteUri ?? webview.CoreWebView2.Source;

                    isUpdatingAddressBar = true;
                    AddressBar.Text = currentUrl;
                    isUpdatingAddressBar = false;

                    AddressPlaceholder.Visibility =
                        string.IsNullOrEmpty(AddressBar.Text) ? Visibility.Visible : Visibility.Collapsed;

                    string title = webview.CoreWebView2.DocumentTitle;
                    titleBlock.Text =
                        title.Length > 25 ? title.Substring(0, 22) + "..." : title;

                    Uri uri = new Uri(currentUrl);
                    favicon.Source = new BitmapImage(new Uri($"{uri.Scheme}://{uri.Host}/favicon.ico"));
                }
                catch { }
            }

            webview.CoreWebView2.NavigationCompleted += (s, e) =>
            {
                UpdateTabHeader();

                // LOG HISTORY using Saves.cs (all data stays in saves folder)
                try
                {
                    string currentUrl = webview.Source?.AbsoluteUri ?? webview.CoreWebView2.Source;
                    string title = webview.CoreWebView2.DocumentTitle ?? "";
                    Saves.AddHistory(currentUrl, title);
                }
                catch { }
            };

            webview.CoreWebView2.SourceChanged += (s, e) => UpdateTabHeader();
            webview.CoreWebView2.HistoryChanged += (s, e) => UpdateTabHeader();
            webview.CoreWebView2.DocumentTitleChanged += (s, e) => UpdateTabHeader();

            // fullscreen support
            webview.CoreWebView2.ContainsFullScreenElementChanged += (s, e) =>
            {
                if (webview.CoreWebView2.ContainsFullScreenElement)
                {
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
            };

            webview.CoreWebView2.Navigate(url);
        }

        public WebView2? GetCurrentWebView()
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

            string url;

            if (input.StartsWith("http://") || input.StartsWith("https://") || input.Contains("."))
                url = input.StartsWith("http") ? input : "https://" + input;
            else
                url = "https://www.google.com/search?q=" + Uri.EscapeDataString(input);

            webview.CoreWebView2.Navigate(url);

            SuggestionPopup.IsOpen = false;
        }

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Navigate();
        }

        private async void AddressBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingAddressBar)
                return;

            string query = AddressBar.Text.Trim();

            AddressPlaceholder.Visibility =
                string.IsNullOrWhiteSpace(query)
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(query))
            {
                SuggestionPopup.IsOpen = false;
                return;
            }

            await LoadGoogleSuggestions(query);
        }

        private async Task LoadGoogleSuggestions(string query)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();

                string url =
                    "https://suggestqueries.google.com/complete/search?client=firefox&q=" +
                    Uri.EscapeDataString(query);

                string json = await client.GetStringAsync(url);

                var data = System.Text.Json.JsonSerializer.Deserialize<object[]>(json);

                if (data == null || data.Length < 2)
                    return;

                var suggestions =
                    System.Text.Json.JsonSerializer.Deserialize<string[]>(data[1].ToString());

                SuggestionList.Items.Clear();

                if (suggestions != null)
                {
                    foreach (var s in suggestions)
                        SuggestionList.Items.Add(s);
                }

                SuggestionPopup.IsOpen = SuggestionList.Items.Count > 0;
            }
            catch
            {
                SuggestionPopup.IsOpen = false;
            }
        }

        private void Suggestion_Click(object sender, MouseButtonEventArgs e)
        {
            if (SuggestionList.SelectedItem == null)
                return;

            string selected = SuggestionList.SelectedItem.ToString();

            AddressBar.Text = selected;
            SuggestionPopup.IsOpen = false;

            Navigate();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Settings not implemented yet");
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            Navigate();
        }

        private void NewTab_Click(object sender, RoutedEventArgs e)
        {
            CreateNewTab("https://www.google.com");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentWebView()?.CoreWebView2.GoBack();
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentWebView()?.CoreWebView2.GoForward();
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentWebView()?.CoreWebView2.Reload();
        }
    }
}