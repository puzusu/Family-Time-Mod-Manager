using System.Diagnostics;

namespace FamilyModManager;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--install-loader")
        {
            if (args.Length < 2) throw new ArgumentException("Supply a Family Time game folder.");
            BepInExInstaller.Install(args[1]);
            return;
        }
        if (args.Length > 0 && args[0] == "--inspect")
        {
            string folder = args.Length > 1 ? args[1] : GameFolders.DefaultGame;
            foreach (PluginRecord plugin in PluginCatalog.Scan(folder))
                Console.WriteLine($"{plugin.State}\t{plugin.Name}\t{plugin.Version}\t{plugin.RelativePath}");
            return;
        }
        if (args.Length > 0 && args[0] == "--self-test")
        {
            SelfTest.Run(args[1], args[2], args[3], args[4], args[5]);
            return;
        }
        if (args.Length > 0 && args[0] == "--preview")
        {
            ApplicationConfiguration.Initialize();
            using var form = new MainForm(args.Length > 2 ? args[2] : null)
            { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
            form.Show();
            Application.DoEvents();
            form.ValidateTitleButtons();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.MakeTransparent(form.TransparencyKey);
            bitmap.Save(args[1]);
            form.Close();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal static class GameFolders
{
    internal const string DefaultGame = @"F:\Family Time\Family_Time_Ep_3_b108";

    internal static string FindDefaultGame()
    {
        string here = AppContext.BaseDirectory;
        string? parent = Directory.GetParent(here)?.FullName;
        foreach (string? candidate in new[] { here, parent, DefaultGame })
            if (candidate != null && File.Exists(Path.Combine(candidate, "FamilyTime.exe")))
                return candidate;
        return string.Empty;
    }

    internal static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new DirectoryNotFoundException("Choose the folder containing FamilyTime.exe with Browse.");
        string root = Path.GetFullPath(path.Trim().Trim('"'));
        if (!File.Exists(Path.Combine(root, "FamilyTime.exe")))
            throw new DirectoryNotFoundException("Choose a folder containing FamilyTime.exe.");
        return root;
    }

    internal static bool IsGameRunning()
    {
        Process[] processes = Process.GetProcessesByName("FamilyTime");
        try { return processes.Length > 0; }
        finally { foreach (Process process in processes) process.Dispose(); }
    }
}
