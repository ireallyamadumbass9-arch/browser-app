using System;
using System.IO;
using System.Windows;

namespace CustomBrowserWPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();
            mainWindow.Show();

            // open html files passed as argument
            if (e.Args.Length > 0)
            {
                foreach (var arg in e.Args)
                {
                    if (File.Exists(arg) && Path.GetExtension(arg).ToLower() == ".html")
                    {
                        mainWindow.CreateNewTab(new Uri(arg).AbsoluteUri);
                    }
                }
            }
        }
    }
}