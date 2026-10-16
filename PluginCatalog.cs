using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace FamilyModManager;

internal enum PluginState { Enabled, Disabled }

internal sealed record PluginRecord(
    string Name, string Version, string Guids, PluginState State,
    string RelativePath, string AbsolutePath);

internal static class PluginCatalog
{
    internal static IReadOnlyList<PluginRecord> Scan(string gameFolder)
    {
        string root = GameFolders.Normalize(gameFolder);
        string bepinex = Path.Combine(root, "BepInEx");
        var records = new List<PluginRecord>();
        ScanFolder(Path.Combine(bepinex, "plugins"), PluginState.Enabled, records);
        ScanFolder(Path.Combine(bepinex, "disabled_plugins"), PluginState.Disabled, records);
        return records.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void ScanFolder(string folder, PluginState state, List<PluginRecord> records)
    {
        if (!Directory.Exists(folder)) return;
        foreach (string path in DllFiles(folder))
        {
            if (!TryReadPlugin(path, out string name, out string version, out string guids)) continue;
            records.Add(new PluginRecord(name, version, guids, state,
                Path.GetRelativePath(folder, path), path));
        }
    }

    private static IEnumerable<string> DllFiles(string folder)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(folder));
        while (pending.Count > 0)
        {
            DirectoryInfo current = pending.Pop();
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            foreach (FileInfo file in current.EnumerateFiles("*.dll"))
                if ((file.Attributes & FileAttributes.ReparsePoint) == 0) yield return file.FullName;
            foreach (DirectoryInfo child in current.EnumerateDirectories())
                if ((child.Attributes & FileAttributes.ReparsePoint) == 0) pending.Push(child);
        }
    }

    internal static bool TryReadPlugin(string path, out string name, out string version, out string guids)
    {
        name = version = guids = string.Empty;
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata) return false;
            MetadataReader md = pe.GetMetadataReader();
            var names = new List<string>();
            var versions = new List<string>();
            var ids = new List<string>();
            foreach (TypeDefinitionHandle handle in md.TypeDefinitions)
            {
                TypeDefinition type = md.GetTypeDefinition(handle);
                foreach (CustomAttributeHandle attributeHandle in type.GetCustomAttributes())
                {
                    CustomAttribute attribute = md.GetCustomAttribute(attributeHandle);
                    if (AttributeType(md, attribute.Constructor) != "BepInEx.BepInPlugin") continue;
                    BlobReader blob = md.GetBlobReader(attribute.Value);
                    if (blob.ReadUInt16() != 1) continue;
                    string id = blob.ReadSerializedString() ?? string.Empty;
                    string title = blob.ReadSerializedString() ?? id;
                    string release = blob.ReadSerializedString() ?? string.Empty;
                    names.Add(title);
                    versions.Add(release);
                    ids.Add(id);
                }
            }
            if (names.Count == 0) return false;
            name = string.Join(" / ", names);
            version = string.Join(" / ", versions);
            guids = string.Join(" / ", ids);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            BadImageFormatException or ArgumentException or InvalidOperationException or InvalidDataException)
        {
            return false;
        }
    }

    private static string AttributeType(MetadataReader md, EntityHandle constructor)
    {
        EntityHandle type = constructor.Kind switch
        {
            HandleKind.MemberReference => md.GetMemberReference((MemberReferenceHandle)constructor).Parent,
            HandleKind.MethodDefinition => md.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
            _ => default
        };
        return type.Kind switch
        {
            HandleKind.TypeReference => FullName(md,
                md.GetTypeReference((TypeReferenceHandle)type).Namespace,
                md.GetTypeReference((TypeReferenceHandle)type).Name),
            HandleKind.TypeDefinition => FullName(md,
                md.GetTypeDefinition((TypeDefinitionHandle)type).Namespace,
                md.GetTypeDefinition((TypeDefinitionHandle)type).Name),
            _ => string.Empty
        };
    }

    private static string FullName(MetadataReader md, StringHandle ns, StringHandle name)
    {
        string prefix = md.GetString(ns);
        return prefix.Length == 0 ? md.GetString(name) : prefix + "." + md.GetString(name);
    }

    internal static void SetEnabled(string gameFolder, PluginRecord plugin, bool enabled)
    {
        if (GameFolders.IsGameRunning())
            throw new InvalidOperationException("Close Family Time before changing plugins.");
        string root = GameFolders.Normalize(gameFolder);
        string bepinex = Path.Combine(root, "BepInEx");
        string plugins = Path.Combine(bepinex, "plugins");
        string disabled = Path.Combine(bepinex, "disabled_plugins");
        string sourceRoot = plugin.State == PluginState.Enabled ? plugins : disabled;
        string targetRoot = enabled ? plugins : disabled;
        string source = Within(sourceRoot, plugin.AbsolutePath);
        string target = Within(targetRoot, Path.Combine(targetRoot, plugin.RelativePath));
        if (!File.Exists(source)) throw new FileNotFoundException("The plugin changed on disk. Refresh the list.", source);
        if (File.Exists(target)) throw new IOException("A DLL already exists at the destination: " + target);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(source, target);
    }

    internal static string Import(string gameFolder, string packagePath)
    {
        if (GameFolders.IsGameRunning())
            throw new InvalidOperationException("Close Family Time before importing plugins.");
        string root = GameFolders.Normalize(gameFolder);
        string bepinex = Path.Combine(root, "BepInEx");
        string pluginRoot = Path.Combine(bepinex, "plugins");
        if (!Directory.Exists(pluginRoot))
            throw new DirectoryNotFoundException("BepInEx is not installed in this game folder yet.");

        string stageRoot = Path.Combine(bepinex, "manager_staging");
        string stage = Within(stageRoot, Path.Combine(stageRoot, Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(stage);
        try
        {
            string extension = Path.GetExtension(packagePath);
            if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(packagePath, Path.Combine(stage, Path.GetFileName(packagePath)));
            }
            else if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ExtractPluginZip(packagePath, stage);
            }
            else throw new InvalidOperationException("Select a plugin DLL or ZIP package.");

            var importedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool foundPlugin = false;
            foreach (string path in DllFiles(stage))
            {
                if (!TryReadPlugin(path, out _, out _, out string guids)) continue;
                foundPlugin = true;
                foreach (string id in SplitIds(guids))
                    if (!importedIds.Add(id))
                        throw new InvalidOperationException("This package contains the same plugin ID twice: " + id);
            }
            if (!foundPlugin)
                throw new InvalidOperationException("No BepInEx plugin DLL was found in this package.");
            var installedIds = new HashSet<string>(
                Scan(root).SelectMany(plugin => SplitIds(plugin.Guids)),
                StringComparer.OrdinalIgnoreCase);
            foreach (string id in importedIds)
                if (installedIds.Contains(id))
                    throw new InvalidOperationException("This plugin ID is already installed: " + id);

            string folderName = SafeFolderName(Path.GetFileNameWithoutExtension(packagePath));
            string managedRoot = Path.Combine(pluginRoot, "FamilyModManager");
            Directory.CreateDirectory(managedRoot);
            string destination = Path.Combine(managedRoot, folderName);
            for (int number = 2; Directory.Exists(destination); number++)
                destination = Path.Combine(managedRoot, folderName + "-" + number);
            Directory.Move(stage, destination);
            return destination;
        }
        catch
        {
            if (Directory.Exists(stage)) Directory.Delete(Within(stageRoot, stage), true);
            throw;
        }
    }

    private static void ExtractPluginZip(string zipPath, string stage)
    {
        const long maxFileBytes = 128L * 1024 * 1024;
        const long maxPackageBytes = 512L * 1024 * 1024;
        long total = 0;
        using ZipArchive zip = ZipFile.OpenRead(zipPath);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            if (entry.Name.Length == 0) continue;
            string relative = entry.FullName.Replace('\\', '/');
            if (relative.StartsWith("BepInEx/plugins/", StringComparison.OrdinalIgnoreCase))
                relative = relative["BepInEx/plugins/".Length..];
            else if (relative.StartsWith("plugins/", StringComparison.OrdinalIgnoreCase))
                relative = relative["plugins/".Length..];
            else if (relative.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase) ||
                relative.Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase) ||
                relative.Equals("doorstop_config.ini", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("This ZIP includes loader files. Select a plugin-only ZIP.");
            if (relative.Length == 0 || relative.StartsWith('/') || relative.Contains(':') ||
                relative.Split('/').Any(part => part is "" or "." or ".."))
                throw new InvalidDataException("Unsafe path in ZIP: " + entry.FullName);
            total += entry.Length;
            if (entry.Length > maxFileBytes || total > maxPackageBytes)
                throw new InvalidDataException("Plugin package is too large.");
            string target = Within(stage, Path.Combine(stage, relative.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using Stream input = entry.Open();
            using FileStream output = new(target, FileMode.CreateNew, FileAccess.Write);
            input.CopyTo(output);
        }
    }

    private static string SafeFolderName(string name)
    {
        string clean = new(name.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        return clean.Length == 0 ? "plugin" : clean[..Math.Min(clean.Length, 60)];
    }

    private static IEnumerable<string> SplitIds(string guids) =>
        guids.Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Within(string root, string path)
    {
        string basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A plugin path escaped its managed folder.");
        return fullPath;
    }
}
