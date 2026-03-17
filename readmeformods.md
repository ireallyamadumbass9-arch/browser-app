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

1. Download [APPApi.dll](https://github.com/ireallyamadumbass9-arch/f/raw/refs/heads/main/APPApi.dll).  
2. In your mod project (Class Library), add a reference to [APPApi.dll](https://github.com/ireallyamadumbass9-arch/f/raw/refs/heads/main/APPApi.dll).  

---

### 2. Create a Mod Class

Your mod must implement the `IBrowserMod` interface:

```csharp
using APPApi;

public class MyMod : IBrowserMod
{
    public void OnLoad(AppAPI api)
    {
        // literally one line to patch any file
        api.Edit("MainWindow.xaml", "<Window x:Class=\"CustomBrowserWPF.MainWindow\"> ... </Window>");

        // you can also edit code-behind
        api.Edit("MainWindow.xaml.cs", "// new code here");

        // access GUI if loader has started it
        if (api.GuiInstance != null)
        {
            var mw = (CustomBrowserWPF.MainWindow)api.GuiInstance;
            // do stuff with the window
        }
    }
}
