using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using APPApi;

namespace CustomBrowserWPF
{
    public static class Loader
    {
        // call this before showing MainWindow
        public static async Task InitializeAsync()
        {
            // ensure folders exist
            Directory.CreateDirectory(Saves.SaveFolder);
            Directory.CreateDirectory(Saves.CookiesFolder);
            Directory.CreateDirectory(Saves.CacheFolder);
            Directory.CreateDirectory(Saves.WebViewUserDataFolder);
            Directory.CreateDirectory("Mods");

            // preload history
            Saves.GetHistory();

            // load mods (pre-GUI)
            await LoadModsAsync();
        }

        private static async Task LoadModsAsync()
        {
            string modsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods");
            if (!Directory.Exists(modsPath)) return;

            var modFiles = Directory.GetFiles(modsPath, "*.dll");

            foreach (var file in modFiles)
            {
                try
                {
                    var asm = Assembly.LoadFrom(file);

                    var modTypes = asm.GetTypes()
                        .Where(t => typeof(IBrowserMod).IsAssignableFrom(t) && !t.IsInterface);

                    foreach (var type in modTypes)
                    {
                        var mod = (IBrowserMod)Activator.CreateInstance(type);

                        // pre-GUI API (no window yet)
                        var api = new AppAPI();

                        mod.OnLoad(api);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load mod {file}: {ex.Message}");
                }
            }

            await Task.CompletedTask;
        }

        // optional: apply edits before GUI starts
        public static void ApplyPreloadEdits(string filePath, string newContent)
        {
            if (!File.Exists(filePath)) return;

            string backup = filePath + ".bak";
            File.Copy(filePath, backup, true);

            File.WriteAllText(filePath, newContent);
        }

        // start GUI and re-run mods with GUI access
        public static void StartGUI()
        {
            var mainWindow = new MainWindow();

            foreach (var mod in AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IBrowserMod).IsAssignableFrom(t) && !t.IsInterface)
                .Select(t => (IBrowserMod)Activator.CreateInstance(t)))
            {
                var api = new AppAPI
                {
                    GuiInstance = mainWindow
                };

                mod.OnLoad(api);
            }

            mainWindow.Show();
        }
    }
}