using System.IO.Compression;

namespace FamilyModManager;

internal static class SelfTest
{
    internal static void Run(string fixture, string gameExe, string pluginDll,
        string secondPluginDll, string thirdPluginDll)
    {
        if (Directory.Exists(fixture)) throw new InvalidOperationException("Use a new, empty test folder.");
        Directory.CreateDirectory(Path.Combine(fixture, "BepInEx", "plugins"));
        File.Copy(gameExe, Path.Combine(fixture, "FamilyTime.exe"));
        string plugin = Path.Combine(fixture, "BepInEx", "plugins", Path.GetFileName(pluginDll));
        File.Copy(pluginDll, plugin);

        PluginRecord original = PluginCatalog.Scan(fixture).Single();
        Check(original.State == PluginState.Enabled, "initial scan");
        PluginCatalog.SetEnabled(fixture, original, false);
        PluginRecord disabled = PluginCatalog.Scan(fixture).Single();
        Check(disabled.State == PluginState.Disabled, "disable");
        PluginCatalog.SetEnabled(fixture, disabled, true);
        Check(PluginCatalog.Scan(fixture).Single().State == PluginState.Enabled, "re-enable");

        string imported = PluginCatalog.Import(fixture, secondPluginDll);
        Check(Directory.Exists(imported) && PluginCatalog.Scan(fixture).Count == 2, "DLL import");

        string zip = Path.Combine(fixture, "plugin-package.zip");
        using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            archive.CreateEntryFromFile(thirdPluginDll, "BepInEx/plugins/nested/" + Path.GetFileName(thirdPluginDll));
        string zipImported = PluginCatalog.Import(fixture, zip);
        Check(Directory.Exists(zipImported) && PluginCatalog.Scan(fixture).Count == 3, "ZIP import");
        bool blockedDuplicate = false;
        try { PluginCatalog.Import(fixture, pluginDll); }
        catch (InvalidOperationException error) when (error.Message.Contains("already installed"))
        { blockedDuplicate = true; }
        Check(blockedDuplicate, "duplicate ID blocked");
        Console.WriteLine("PASS: scan, disable, re-enable, DLL import, ZIP import, duplicate ID blocked");
    }

    private static void Check(bool success, string step)
    {
        if (!success) throw new InvalidOperationException("Manager self-test failed: " + step);
    }
}
