using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Net.Http;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CustomBrowserWPF
{
    public partial class MainWindow : Window
    {
        private string userDataFolder;
        private HttpClient httpClient = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();

            userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CustomBrowserWPFData"
            );

            CreateNewTab("https://www.google.com");
        }

        // --- Trim long tab titles ---
        private string TrimTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "New Tab";
            return title.Length > 15 ? title.Substring(0, 12) + "..." : title;
        }

        // --- Create a new tab ---
        private async void CreateNewTab(string url)
        {
            var webview = new WebView2();
            var tab = new TabItem();

            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            TextBlock title = new TextBlock { Text = "New Tab", Margin = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            Button closeButton = new Button { Content = "X", Width = 18, Height = 18, Margin = new Thickness(5, 0, 5, 0), Opacity = 0 };

            closeButton.Click += (s, e) =>
            {
                if (BrowserTabs.Items.Count > 1)
                    BrowserTabs.Items.Remove(tab);
            };

            Grid.SetColumn(title, 0);
            Grid.SetColumn(closeButton, 1);

            headerGrid.Children.Add(title);
            headerGrid.Children.Add(closeButton);

            tab.Header = headerGrid;
            tab.Content = webview;

            tab.MouseEnter += (s, e) => closeButton.Opacity = 1;
            tab.MouseLeave += (s, e) => closeButton.Opacity = 0;

            BrowserTabs.Items.Add(tab);
            BrowserTabs.SelectedItem = tab;

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await webview.EnsureCoreWebView2Async(env);

            webview.CoreWebView2.Settings.AreDevToolsEnabled = true;
            webview.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            webview.CoreWebView2.Settings.IsStatusBarEnabled = true;
            webview.CoreWebView2.Settings.IsZoomControlEnabled = true;

            webview.CoreWebView2.DownloadStarting += DownloadStarting;
            webview.CoreWebView2.NewWindowRequested += NewWindowRequested;

            webview.CoreWebView2.DocumentTitleChanged += (s, e) =>
            {
                Dispatcher.Invoke(() => title.Text = TrimTitle(webview.CoreWebView2.DocumentTitle));
            };

            webview.CoreWebView2.Navigate(url);
        }

        // --- Open new tabs instead of new windows ---
        private void NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            Dispatcher.Invoke(() => CreateNewTab(e.Uri));
        }

        // --- Force downloads folder ---
        private void DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
        {
            string forcedPath = @"D:\downloads";
            Directory.CreateDirectory(forcedPath);

            string fileName = Path.GetFileName(e.ResultFilePath);
            string finalPath = Path.Combine(forcedPath, fileName);

            e.ResultFilePath = finalPath;
            e.Handled = false;
        }

        // --- Navigate ---
        private void Navigate()
        {
            if (BrowserTabs.SelectedItem is TabItem tab && tab.Content is WebView2 webview)
            {
                string input = AddressBar.Text.Trim();
                if (string.IsNullOrEmpty(input)) return;

                string url;
                bool looksLikeUrl = input.Contains(".") || input.StartsWith("http") || input.StartsWith("localhost");
                url = looksLikeUrl ? (input.StartsWith("http") ? input : "https://" + input) : "https://www.google.com/search?q=" + Uri.EscapeDataString(input);

                webview.CoreWebView2.Navigate(url);
            }

            SuggestionPopup.IsOpen = false;
        }

        private void GoButton_Click(object sender, RoutedEventArgs e) => Navigate();

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Navigate();
                e.Handled = true;
            }
        }

        private void NewTab_Click(object sender, RoutedEventArgs e) => CreateNewTab("https://www.google.com");

        // --- Placeholder & suggestions ---
        private async void AddressBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            AddressPlaceholder.Visibility = string.IsNullOrWhiteSpace(AddressBar.Text) ? Visibility.Visible : Visibility.Hidden;

            if (AddressBar.Text.Length < 2)
            {
                SuggestionPopup.IsOpen = false;
                return;
            }

            var suggestions = await GetSuggestions(AddressBar.Text);
            SuggestionList.ItemsSource = suggestions;
            SuggestionPopup.IsOpen = suggestions.Count > 0;
        }

        private async Task<List<string>> GetSuggestions(string query)
        {
            var list = new List<string>();
            try
            {
                string url = "https://suggestqueries.google.com/complete/search?client=firefox&q=" + Uri.EscapeDataString(query);
                string json = await httpClient.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(json);
                foreach (var item in doc.RootElement[1].EnumerateArray()) list.Add(item.GetString());
            }
            catch { }
            return list;
        }

        private void Suggestion_Click(object sender, MouseButtonEventArgs e)
        {
            if (SuggestionList.SelectedItem == null) return;

            AddressBar.Text = SuggestionList.SelectedItem.ToString();
            SuggestionPopup.IsOpen = false;
            Navigate();
        }
    }
}