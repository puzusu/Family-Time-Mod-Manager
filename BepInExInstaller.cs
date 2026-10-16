using System.IO.Compression;
using System.Security.Cryptography;
using System.Reflection;

namespace FamilyModManager;

internal static class BepInExInstaller
{
    private const string ArchiveName = "BepInEx_win_x64_5.4.23.5.zip";
    private const string ExpectedSha256 = "82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4";
    private const string SupportArchiveName = "FamilyTime-b108-unstripped-compat.zip";
    private const string SupportSha256 = "1A3F4F705DA33606FF98527905225DDC95DF7602FC045142D68524403C98A41B";

    internal static bool IsInstalled(string gameFolder) =>
        File.Exists(Path.Combine(gameFolder, "winhttp.dll")) &&
        File.Exists(Path.Combine(gameFolder, "doorstop_config.ini")) &&
        File.Exists(Path.Combine(gameFolder, "BepInEx", "core", "BepInEx.dll")) &&
        File.Exists(Path.Combine(gameFolder, "BepInEx", "unstripped", "mscorlib.dll"));

    internal static void Install(string gameFolder)
    {
        string root = GameFolders.Normalize(gameFolder);
        if (GameFolders.IsGameRunning())
            throw new InvalidOperationException("Close Family Time before installing BepInEx.");
        if (IsInstalled(root))
            throw new InvalidOperationException("BepInEx is already installed in this game folder.");
        if (Directory.Exists(Path.Combine(root, "BepInEx")) ||
            File.Exists(Path.Combine(root, "winhttp.dll")) ||
            File.Exists(Path.Combine(root, "doorstop_config.ini")))
            throw new InvalidOperationException("Loader files already exist here. Resolve the partial or different installation before installing.");

        VerifyArchive(ArchiveName, ExpectedSha256);
        VerifyArchive(SupportArchiveName, SupportSha256);

        string stage = Path.Combine(root, ".family-mod-manager-loader-" + Guid.NewGuid().ToString("N"));
        var installed = new List<string>();
        Directory.CreateDirectory(stage);
        try
        {
            ExtractArchive(ArchiveName, stage);
            ExtractArchive(SupportArchiveName, stage);
            // b108's UserInput type requires the game's own Input System assembly.
            // The older unstripped copy causes a TypeLoadException on world entry.
            string incompatibleInputSystem = Path.Combine(stage, "BepInEx", "unstripped", "Unity.InputSystem.dll");
            if (File.Exists(incompatibleInputSystem)) File.Delete(incompatibleInputSystem);
            if (!File.Exists(Path.Combine(stage, "winhttp.dll")) ||
                !File.Exists(Path.Combine(stage, "doorstop_config.ini")) ||
                !File.Exists(Path.Combine(stage, "BepInEx", "core", "BepInEx.dll")) ||
                !File.Exists(Path.Combine(stage, "BepInEx", "unstripped", "mscorlib.dll")))
                throw new InvalidDataException("The BepInEx installer files are incomplete.");
            string configPath = Path.Combine(stage, "doorstop_config.ini");
            string config = File.ReadAllText(configPath);
            string updatedConfig = System.Text.RegularExpressions.Regex.Replace(config,
                @"(?m)^dll_search_path_override\s*=.*$",
                @"dll_search_path_override = BepInEx\unstripped");
            if (updatedConfig == config)
                throw new InvalidDataException("The BepInEx configuration has an unexpected format.");
            File.WriteAllText(configPath, updatedConfig);

            string sourceCore = Path.Combine(stage, "BepInEx");
            string targetCore = Path.Combine(root, "BepInEx");
            Directory.Move(sourceCore, targetCore);
            installed.Add(targetCore);
            foreach (string name in new[] { ".doorstop_version", "doorstop_config.ini", "winhttp.dll", "changelog.txt" })
            {
                string source = Path.Combine(stage, name);
                if (!File.Exists(source)) continue;
                string target = Path.Combine(root, name);
                File.Move(source, target);
                installed.Add(target);
            }
            Directory.CreateDirectory(Path.Combine(targetCore, "plugins"));
            Directory.CreateDirectory(Path.Combine(targetCore, "config"));
        }
        catch
        {
            for (int i = installed.Count - 1; i >= 0; i--)
            {
                string path = installed[i];
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            throw;
        }
        finally
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
        }
    }

    private static Stream OpenArchive(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream("FamilyModManager.Assets." + name)
        ?? throw new FileNotFoundException("The bundled BepInEx archive is missing: " + name);

    private static void ExtractArchive(string name, string destination)
    {
        using Stream input = OpenArchive(name);
        ZipFile.ExtractToDirectory(input, destination);
    }

    private static void VerifyArchive(string name, string expectedHash)
    {
        using Stream input = OpenArchive(name);
        string hash = Convert.ToHexString(SHA256.HashData(input));
        if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A BepInEx installer archive failed its checksum. Reinstall the mod manager.");
    }
}
