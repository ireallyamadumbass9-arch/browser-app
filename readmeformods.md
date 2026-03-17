# APPApi - Modding API for CustomBrowserWPF

This is the official API for creating mods for **CustomBrowserWPF**.  
It allows mods to:

- Edit XAML files (`MainWindow.xaml`)  
- Edit code-behind (`MainWindow.xaml.cs`)  
- Access folders for saves, cache, cookies  
- Interact with the live GUI (optional)

---

## Getting Started

### 1. Reference the DLL

1. Download [APPApi.dll](https://github.com/YourRepo/APPApi/releases/latest).  
2. In your mod project (Class Library), add a reference to [APPApi.dll](https://github.com/YourRepo/APPApi/releases/latest).  

---

### 2. Create a Mod Class

Your mod must implement the `IBrowserMod` interface:

```csharp
using APPApi;

public class MyMod : IBrowserMod
{
    public void OnLoad(AppAPI api)
    {
        // Example: edit XAML
        api.EditXaml("MainWindow.xaml", "<Window x:Class=\"CustomBrowserWPF.MainWindow\"> ... </Window>");

        // Example: edit code-behind
        api.EditCodeBehind("MainWindow.xaml.cs", "using System; ... full code ...");

        // Example: live GUI manipulation (optional)
        if (api.GuiInstance != null)
        {
            dynamic gui = api.GuiInstance;
            gui.CreateNewTab("https://www.google.com");
        }
    }
}
