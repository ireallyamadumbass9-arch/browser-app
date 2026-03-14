using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using IWshRuntimeLibrary;

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

            if (Directory.Exists(defaultInstall) && Directory.GetFiles(defaultInstall).Length > 0)
                InstallButton.Content = "Update";
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { CheckFileExists = false, CheckPathExists = true, FileName = "Select folder" };
            if (dlg.ShowDialog() == true)
                InstallPathBox.Text = Path.GetDirectoryName(dlg.FileName)!;
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
                    string fileName = Path.GetFileName(fileUrl);
                    string destFile = Path.Combine(installDir, fileName);

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

                // --- finalize ---
                string exePath = Path.Combine(installDir, "WpfApp1.exe");
                string renamedExe = Path.Combine(installDir, "browser.exe");
                if (System.IO.File.Exists(renamedExe)) System.IO.File.Delete(renamedExe);
                System.IO.File.Move(exePath, renamedExe);

                CreateDesktopShortcut(renamedExe);

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
            string shortcutPath = Path.Combine(desk, "browser.lnk");

            var shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
            shortcut.Description = "Browser app";
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath)!;
            shortcut.IconLocation = targetPath + ",0";
            shortcut.Save();
        }

        private void Finish_Click(object sender, RoutedEventArgs e) => Close();
    }
}
