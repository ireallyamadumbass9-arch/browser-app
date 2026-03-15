using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using IWshRuntimeLibrary;
using System.Text.Json;

namespace WpfInstaller
{
    public partial class MainWindow : Window
    {
        private string defaultInstall = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "BrowserApp");
        private readonly string[] filesToDownload = new string[]
        {
            "https://github.com/ireallyamadumbass9-arch/stuff/raw/main/WpfApp1.exe",
            "https://github.com/ireallyamadumbass9-arch/stuff/raw/refs/heads/main/Microsoft.Web.WebView2.Core.dll",
            "https://raw.githubusercontent.com/ireallyamadumbass9-arch/stuff/refs/heads/main/Microsoft.Web.WebView2.Core.xml",
            "https://raw.githubusercontent.com/ireallyamadumbass9-arch/stuff/refs/heads/main/Microsoft.Web.WebView2.WinForms.dll",
            "https://github.com/ireallyamadumbass9-arch/stuff/raw/refs/heads/main/Microsoft.Web.WebView2.Wpf.dll",
            "https://raw.githubusercontent.com/ireallyamadumbass9-arch/stuff/refs/heads/main/Microsoft.Web.WebView2.Wpf.xml",
            "https://github.com/ireallyamadumbass9-arch/stuff/raw/refs/heads/main/WebView2Loader.dll",
            "https://github.com/ireallyamadumbass9-arch/stuff/raw/refs/heads/main/WpfApp1.deps.json",
            "https://github.com/ireallyamadumbass9-arch/stuff/raw/refs/heads/main/WpfApp1.dll",
            "https://github.com/ireallyamadumbass9-arch/stuff/raw/refs/heads/main/WpfApp1.pdb",
            "https://raw.githubusercontent.com/ireallyamadumbass9-arch/stuff/refs/heads/main/WpfApp1.runtimeconfig.json"
        };

        private CancellationTokenSource? cts = null;

        public MainWindow()
        {
            InitializeComponent();
            InstallPathBox.Text = defaultInstall;
            AddonCodeBox.IsEnabled = false; // disabled by default

            if (Directory.Exists(defaultInstall) && Directory.GetFiles(defaultInstall).Length > 0)
                InstallButton.Content = "Update";
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { CheckFileExists = false, CheckPathExists = true, FileName = "Select folder" };
            if (dlg.ShowDialog() == true)
                InstallPathBox.Text = System.IO.Path.GetDirectoryName(dlg.FileName)!;
        }

        private void AllowAddons_Checked(object sender, RoutedEventArgs e)
        {
            AddonCodeBox.IsEnabled = true;
        }

        private void AllowAddons_Unchecked(object sender, RoutedEventArgs e)
        {
            AddonCodeBox.IsEnabled = false;
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            InstallButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            cts = new CancellationTokenSource();
            var token = cts.Token;

            string installDir = InstallPathBox.Text;
            Directory.CreateDirectory(installDir);

            InstallProgress.Value = 0;
            int totalFiles = filesToDownload.Length;
            int currentFileIndex = 0;

            try
            {
                using var httpClient = new HttpClient();

                foreach (string fileUrl in filesToDownload)
                {
                    currentFileIndex++;
                    string fileName = System.IO.Path.GetFileName(fileUrl);
                    string destFile = System.IO.Path.Combine(installDir, fileName);

                    using var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, token);
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    using var stream = await response.Content.ReadAsStreamAsync(token);
                    using var fs = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None);

                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int read;
                    while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, read), token);
                        totalRead += read;

                        if (totalBytes > 0)
                        {
                            double percent = ((currentFileIndex - 1 + (double)totalRead / totalBytes) / totalFiles) * 100;
                            InstallProgress.Value = percent;
                            InstallStatusLabel.Content = $"Downloading {fileName} ({currentFileIndex}/{totalFiles}) - {((double)totalRead / totalBytes * 100):F1}%";
                        }

                        if (token.IsCancellationRequested)
                            throw new OperationCanceledException();
                    }
                }

                // rename exe
                string exePath = System.IO.Path.Combine(installDir, "WpfApp1.exe");
                string renamedExe = System.IO.Path.Combine(installDir, "browser.exe");
                if (System.IO.File.Exists(renamedExe)) System.IO.File.Delete(renamedExe);
                System.IO.File.Move(exePath, renamedExe);

                CreateDesktopShortcut(renamedExe);

                // --- handle addons ---
                if (AllowAddonsCheckbox.IsChecked == true && !string.IsNullOrWhiteSpace(AddonCodeBox.Text))
                {
                    string modsDir = System.IO.Path.Combine(installDir, "mods");
                    Directory.CreateDirectory(modsDir);

                    string codeOrLink = AddonCodeBox.Text.Trim();

                    // resolve code to URL from Codes.json
                    string codesJsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Codes.json");
                    string addonUrl = codeOrLink;

                    if (System.IO.File.Exists(codesJsonPath))
                    {
                        var json = JsonDocument.Parse(System.IO.File.ReadAllText(codesJsonPath));
                        if (json.RootElement.TryGetProperty(codeOrLink, out var urlElem))
                            addonUrl = urlElem.GetString() ?? codeOrLink;
                    }

                    string addonName = System.IO.Path.GetFileNameWithoutExtension(addonUrl);
                    string addonFolder = System.IO.Path.Combine(modsDir, addonName);
                    Directory.CreateDirectory(addonFolder);

                    string dllPath = System.IO.Path.Combine(addonFolder, System.IO.Path.GetFileName(addonUrl));
                    var bytes = await httpClient.GetByteArrayAsync(addonUrl);
                    await System.IO.File.WriteAllBytesAsync(dllPath, bytes);

                    // write mod.json
                    var modConfig = new { Disabled = false };
                    string configJson = JsonSerializer.Serialize(modConfig, new JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(System.IO.Path.Combine(addonFolder, "mod.json"), configJson);
                }

                InstallProgress.Value = 100;
                InstallStatusLabel.Content = "Installation complete!";
                MessageBox.Show("Installation complete!", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                FinishButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
            }
            catch (OperationCanceledException)
            {
                InstallStatusLabel.Content = "Installation cancelled!";
                InstallProgress.Value = 0;
                InstallButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
                InstallButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
        }

        private void CreateDesktopShortcut(string targetPath)
        {
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = System.IO.Path.Combine(desk, "browser.lnk");

            var shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
            shortcut.Description = "Browser app";
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(targetPath)!;
            shortcut.IconLocation = targetPath + ",0";
            shortcut.Save();
        }

        private void Finish_Click(object sender, RoutedEventArgs e) => Close();
    }
}