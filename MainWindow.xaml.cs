using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.IO.Compression;
using System.Text.Json;

// alias to fix File conflict
using IOFile = System.IO.File;

namespace WpfInstaller
{
    public partial class MainWindow : Window
    {
        private string defaultInstall = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "BrowserApp");

        private readonly string[] filesToDownload = new string[]
        {
            "https://github.com/ireallyamadumbass9-arch/stuff/releases/download/y/installer.zip"
        };

        private CancellationTokenSource? cts = null;

        public MainWindow()
        {
            InitializeComponent();

            InstallPathBox.Text = defaultInstall;
            AddonCodeBox.IsEnabled = false;

            if (Directory.Exists(defaultInstall) && Directory.GetFiles(defaultInstall).Length > 0)
                InstallButton.Content = "Update";
        }

        // ===== BROWSE =====
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select folder"
            };

            if (dlg.ShowDialog() == true)
                InstallPathBox.Text = Path.GetDirectoryName(dlg.FileName)!;
        }

        // ===== ADDONS =====
        private void AllowAddons_Checked(object sender, RoutedEventArgs e)
        {
            AddonCodeBox.IsEnabled = true;
        }

        private void AllowAddons_Unchecked(object sender, RoutedEventArgs e)
        {
            AddonCodeBox.IsEnabled = false;
        }

        // ===== INSTALL =====
        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            InstallButton.IsEnabled = false;
            CancelButton.IsEnabled = true;

            cts = new CancellationTokenSource();
            var token = cts.Token;

            string installDir = InstallPathBox.Text;
            Directory.CreateDirectory(installDir);

            InstallProgress.Value = 0;

            try
            {
                using var httpClient = new HttpClient();

                string fileUrl = filesToDownload[0];
                string zipPath = Path.Combine(installDir, "installer.zip");

                // ===== DOWNLOAD =====
                using var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using var stream = await response.Content.ReadAsStreamAsync(token);
                using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read), token);
                    totalRead += read;

                    if (totalBytes > 0)
                    {
                        double percent = (double)totalRead / totalBytes * 50;
                        InstallProgress.Value = percent;
                        InstallStatusLabel.Content = $"Downloading... {(percent * 2):F1}%";
                    }

                    if (token.IsCancellationRequested)
                        throw new OperationCanceledException();
                }

                // ===== EXTRACT =====
                string extractPath = Path.Combine(installDir, "temp_extract");

                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);

                Directory.CreateDirectory(extractPath);

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    int totalEntries = archive.Entries.Count;
                    int current = 0;

                    foreach (var entry in archive.Entries)
                    {
                        if (token.IsCancellationRequested)
                            throw new OperationCanceledException();

                        string fullPath = Path.Combine(extractPath, entry.FullName);

                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(fullPath);
                        }
                        else
                        {
                            string? dir = Path.GetDirectoryName(fullPath);
                            if (dir == null)
                                throw new Exception("Invalid path during extraction");

                            Directory.CreateDirectory(dir);
                            entry.ExtractToFile(fullPath, true);
                        }

                        current++;

                        double percent = 50 + ((double)current / totalEntries * 40);
                        InstallProgress.Value = percent;
                        InstallStatusLabel.Content = $"Extracting... {percent:F1}%";
                    }
                }

                IOFile.Delete(zipPath);

                // ===== MOVE FILES =====
                string installerFolder = Path.Combine(extractPath, "installer");

                if (!Directory.Exists(installerFolder))
                    throw new Exception("installer folder not found in zip");

                foreach (var dir in Directory.GetDirectories(installerFolder))
                {
                    string dest = Path.Combine(installDir, Path.GetFileName(dir));
                    if (Directory.Exists(dest)) Directory.Delete(dest, true);
                    Directory.Move(dir, dest);
                }

                foreach (var file in Directory.GetFiles(installerFolder))
                {
                    string dest = Path.Combine(installDir, Path.GetFileName(file));
                    if (IOFile.Exists(dest)) IOFile.Delete(dest);
                    IOFile.Move(file, dest);
                }

                Directory.Delete(extractPath, true);

                // ===== RENAME EXE =====
                string oldExe = Path.Combine(installDir, "WpfApp1.exe");
                string newExe = Path.Combine(installDir, "browser.exe");

                if (!IOFile.Exists(oldExe))
                    throw new Exception("WpfApp1.exe not found");

                if (IOFile.Exists(newExe))
                    IOFile.Delete(newExe);

                IOFile.Move(oldExe, newExe);

                // ===== ADDON SAVE =====
                if (AddonCodeBox.IsEnabled && !string.IsNullOrWhiteSpace(AddonCodeBox.Text))
                {
                    string addonsDir = Path.Combine(installDir, "addons");
                    Directory.CreateDirectory(addonsDir);

                    string addonPath = Path.Combine(addonsDir, "useraddon.txt");
                    IOFile.WriteAllText(addonPath, AddonCodeBox.Text);
                }

                // ===== SHORTCUT =====
                CreateDesktopShortcut(newExe);

                InstallProgress.Value = 100;
                InstallStatusLabel.Content = "Installation complete!";

                MessageBox.Show("Installation complete!", "Done");

                FinishButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
            }
            catch (OperationCanceledException)
            {
                InstallStatusLabel.Content = "Cancelled!";
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
            string name = Path.GetFileNameWithoutExtension(targetPath);
            string shortcutPath = Path.Combine(desk, name + ".lnk");

            var shell = new IWshRuntimeLibrary.WshShell();
            IWshRuntimeLibrary.IWshShortcut shortcut =
                (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);

            string? dir = Path.GetDirectoryName(targetPath);
            if (dir == null)
                throw new Exception("Invalid exe path");

            shortcut.Description = name;
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = dir;
            shortcut.IconLocation = targetPath + ",0";
            shortcut.Save();
        }

        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}