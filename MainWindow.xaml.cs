using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32; // for OpenFileDialog trick
using IWshRuntimeLibrary; // COM reference for desktop shortcut

namespace WpfInstaller
{
    public partial class MainWindow : Window
    {
        private string defaultInstall = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "BrowserApp");

        // list all files to download from GitHub raw URLs
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
            "https://raw.githubusercontent.com/ireallyamadumbass9-arch/stuff/refs/heads/main/WpfApp1.runtimeconfig.json",

        };

        public MainWindow()
        {
            InitializeComponent();
            InstallPathBox.Text = defaultInstall;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select folder"
            };

            if (dlg.ShowDialog() == true)
            {
                string folder = System.IO.Path.GetDirectoryName(dlg.FileName);
                InstallPathBox.Text = folder;
            }
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            string installDir = InstallPathBox.Text;
            System.IO.Directory.CreateDirectory(installDir);

            InstallProgress.Value = 0;
            InstallButton.IsEnabled = false;

            try
            {
                double progressStep = 100.0 / filesToDownload.Length;

                using var httpClient = new HttpClient();

                foreach (string fileUrl in filesToDownload)
                {
                    string fileName = System.IO.Path.GetFileName(fileUrl);
                    string destFile = System.IO.Path.Combine(installDir, fileName);

                    using var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    var total = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = total != -1;

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var fs = new System.IO.FileStream(destFile, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);

                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        if (canReportProgress)
                        {
                            double fileProgress = (totalRead * progressStep) / total;
                            InstallProgress.Value += fileProgress;
                        }
                    }

                    // if content-length unknown, just increment progress by step
                    if (!canReportProgress) InstallProgress.Value += progressStep;
                }

                InstallProgress.Value = 100;

                string exePath = System.IO.Path.Combine(installDir, "WpfApp1.exe"); // original exe name
                string renamedExe = System.IO.Path.Combine(installDir, "browser.exe");

                if (System.IO.File.Exists(renamedExe)) System.IO.File.Delete(renamedExe);
                System.IO.File.Move(exePath, renamedExe);

                CreateDesktopShortcut(renamedExe);

                MessageBox.Show("Installation complete!", "Done", MessageBoxButton.OK, MessageBoxImage.Information);

                FinishButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error during installation: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                InstallButton.IsEnabled = true;
            }
        }

        private void CreateDesktopShortcut(string targetPath)
        {
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = System.IO.Path.Combine(desk, "Browser.lnk");

            var shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
            shortcut.Description = "My Browser";
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(targetPath);
            shortcut.IconLocation = targetPath + ",0"; // first icon from exe
            shortcut.Save();
        }

        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}