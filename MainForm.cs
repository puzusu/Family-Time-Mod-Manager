using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Reflection;

namespace FamilyModManager;

internal sealed class MainForm : Form
{
    private readonly Panel heading = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };
    private readonly TextBox gamePath = new() { Dock = DockStyle.Fill };
    private readonly TextBox search = new() { Width = 220, PlaceholderText = "Find a plugin" };
    private readonly ListView list = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
        MultiSelect = false, HideSelection = false
    };
    private readonly Label status = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Label details = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Button enable = new GameButton("Enable", 96);
    private readonly Button disable = new GameButton("Disable", 96);
    private readonly Button installLoader = new GameButton("Install BepInEx", 148);
    private IReadOnlyList<PluginRecord> plugins = Array.Empty<PluginRecord>();

    internal MainForm(string? initialFolder = null)
    {
        Text = "Family Time Mod Manager";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        Opacity = 0.88;
        BackColor = Color.Black;
        TransparencyKey = Color.Black;
        MinimumSize = new Size(920, 620);
        Size = new Size(1130, 755);
        ForeColor = GameSkin.Ivory;
        Font = GameSkin.Body;
        DoubleBuffered = true;
        Padding = new Padding(48, 40, 48, 38);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7, ColumnCount = 1, BackColor = Color.Transparent };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 73));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(layout);

        heading.Paint += (_, e) => {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using var ink = new SolidBrush(GameSkin.Ivory);
            using var alignment = new StringFormat {
                Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center
            };
            e.Graphics.DrawString("Mod Manager", GameSkin.Heading, ink,
                heading.ClientRectangle, alignment);
        };
        var close = Button("X", (_, _) => Close(), 40);
        heading.Controls.Add(close);
        var minimize = Button("_", (_, _) => WindowState = FormWindowState.Minimized, 40);
        heading.Controls.Add(minimize);
        var credits = Button("?", ShowCredits, 40);
        heading.Controls.Add(credits);
        void PositionTitleButtons() {
            close.Left = heading.Width - close.Width;
            minimize.Left = close.Left - minimize.Width - 4;
            credits.Left = minimize.Left - credits.Width - 4;
        }
        heading.Resize += (_, _) => PositionTitleButtons();
        PositionTitleButtons();
        heading.MouseDown += DragWindow;
        layout.Controls.Add(heading, 0, 0);

        var folderRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        folderRow.Controls.Add(new Label { Text = "Game folder", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        gamePath.Text = initialFolder ?? GameFolders.FindDefaultGame();
        gamePath.BackColor = GameSkin.Field;
        gamePath.ForeColor = ForeColor;
        gamePath.BorderStyle = BorderStyle.FixedSingle;
        gamePath.Font = GameSkin.Body;
        gamePath.Margin = new Padding(4, 8, 6, 8);
        gamePath.KeyDown += (_, eventArgs) => { if (eventArgs.KeyCode == Keys.Enter) { eventArgs.SuppressKeyPress = true; RefreshPlugins(); } };
        folderRow.Controls.Add(gamePath, 1, 0);
        folderRow.Controls.Add(Button("Browse…", Browse, 105), 2, 0);
        layout.Controls.Add(folderRow, 0, 1);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        toolbar.Controls.Add(Button("Refresh", (_, _) => RefreshPlugins(), 95));
        toolbar.Controls.Add(Button("Add mod…", Import, 110));
        toolbar.Controls.Add(installLoader);
        toolbar.Controls.Add(enable);
        toolbar.Controls.Add(disable);
        toolbar.Controls.Add(Button("Mods folder", (_, _) => OpenFolder("plugins"), 120));
        toolbar.Controls.Add(Button("Configs", (_, _) => OpenFolder("config"), 98));
        toolbar.Controls.Add(Button("Log", OpenLog, 70));
        toolbar.Controls.Add(Button("Play", LaunchGame, 85));
        enable.Click += (_, _) => Toggle(true);
        disable.Click += (_, _) => Toggle(false);
        installLoader.Click += InstallLoader;
        layout.Controls.Add(toolbar, 0, 2);

        list.Columns.Add("Plugin", 260);
        list.Columns.Add("Version", 110);
        list.Columns.Add("Status", 100);
        list.Columns.Add("DLL location", 440);
        list.BackColor = GameSkin.Field;
        list.ForeColor = ForeColor;
        list.BorderStyle = BorderStyle.None;
        list.Font = GameSkin.Body;
        list.OwnerDraw = true;
        list.HeaderStyle = ColumnHeaderStyle.None;
        list.DrawItem += (_, _) => { };
        list.DrawSubItem += DrawCell;
        list.SelectedIndexChanged += (_, _) => ShowSelection();
        list.DoubleClick += (_, _) =>
        {
            if (Selected is PluginRecord item) Toggle(item.State == PluginState.Disabled);
        };
        var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = Color.FromArgb(26, 26, 26) };
        listHost.Paint += (_, e) => GameSkin.DrawFrame(e.Graphics, GameSkin.ControlFrame,
            new Rectangle(0, 0, listHost.Width, listHost.Height), 13);
        var header = new Panel { Dock = DockStyle.Top, Height = 33, BackColor = Color.FromArgb(37, 37, 37) };
        header.Paint += (_, e) => PaintListHeader(e.Graphics, header.ClientRectangle);
        list.Resize += (_, _) =>
        {
            list.Columns[3].Width = Math.Max(250,
                list.ClientSize.Width - list.Columns[0].Width - list.Columns[1].Width - list.Columns[2].Width - 2);
            header.Invalidate();
        };
        listHost.Controls.Add(list);
        listHost.Controls.Add(header);
        layout.Controls.Add(listHost, 0, 4);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        bottom.Controls.Add(details, 0, 0);
        bottom.Controls.Add(new Label
        {
            Text = "Changes take effect the next time the game starts. DLLs are inspected, never executed, by this app.",
            Dock = DockStyle.Fill, ForeColor = GameSkin.Muted
        }, 0, 1);
        layout.Controls.Add(bottom, 0, 5);

        status.ForeColor = GameSkin.Muted;
        layout.Controls.Add(status, 0, 6);
        search.TextChanged += (_, _) => RenderList();
        var searchHost = new Panel { Dock = DockStyle.Fill };
        search.Dock = DockStyle.Right;
        search.BackColor = GameSkin.Field;
        search.ForeColor = ForeColor;
        search.Font = GameSkin.Body;
        search.BorderStyle = BorderStyle.FixedSingle;
        search.Margin = new Padding(4, 9, 0, 6);
        searchHost.Controls.Add(search);
        searchHost.Controls.Add(new Label { Text = "Search", Dock = DockStyle.Right, Width = 72, TextAlign = ContentAlignment.MiddleRight });
        layout.Controls.Add(searchHost, 0, 3);
        RefreshPlugins();
    }

    private static Button Button(string caption, EventHandler click, int width)
    {
        var button = new GameButton(caption, width);
        button.Click += click;
        return button;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var glass = new GraphicsPath();
        glass.AddPolygon(new Point[] {
            new(56, 34), new(Width - 57, 34), new(Width - 37, 53),
            new(Width - 37, Height - 54), new(Width - 57, Height - 35),
            new(56, Height - 35), new(37, Height - 54), new(37, 53)
        });
        e.Graphics.SetClip(glass);
        using (var surface = new LinearGradientBrush(ClientRectangle,
            Color.FromArgb(42, 42, 42), Color.FromArgb(7, 7, 7), LinearGradientMode.Vertical))
            e.Graphics.FillRectangle(surface, ClientRectangle);
        using (var gloss = new LinearGradientBrush(new Rectangle(45, 43, Width - 90, 190),
            Color.FromArgb(58, 220, 220, 220), Color.FromArgb(0, 220, 220, 220),
            LinearGradientMode.Vertical))
            e.Graphics.FillRectangle(gloss, new Rectangle(45, 43, Width - 90, 190));
        e.Graphics.ResetClip();
        GameSkin.DrawFrame(e.Graphics, GameSkin.DialogFrame,
            new Rectangle(13, 10, Width - 26, Height - 20), 47);
    }

    private void PaintListHeader(Graphics graphics, Rectangle bounds)
    {
        using var background = new SolidBrush(Color.FromArgb(37, 37, 37));
        graphics.FillRectangle(background, bounds);
        int left = 0;
        foreach (ColumnHeader column in list.Columns)
        {
            TextRenderer.DrawText(graphics, column.Text, GameSkin.Body,
                new Rectangle(left + 6, 0, column.Width - 8, bounds.Height),
                GameSkin.Ivory, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            left += column.Width;
        }
        using var line = new Pen(Color.FromArgb(140, 140, 140));
        graphics.DrawLine(line, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
    }

    private static void DrawCell(object? sender, DrawListViewSubItemEventArgs e)
    {
        Color fill = e.Item?.Selected == true ? Color.FromArgb(88, 88, 88)
            : e.ItemIndex % 2 == 0 ? GameSkin.Field : Color.FromArgb(37, 37, 37);
        using var background = new SolidBrush(fill);
        e.Graphics.FillRectangle(background, e.Bounds);
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", GameSkin.Body,
            new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height),
            e.Item?.ForeColor ?? GameSkin.Ivory, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, 0x2, 0);
    }

    internal void ValidateTitleButtons()
    {
        Button[] buttons = heading.Controls.OfType<GameButton>().ToArray();
        if (buttons.Length != 3 || buttons.Any(button =>
            button.Left < 0 || button.Right > heading.ClientSize.Width ||
            heading.GetChildAtPoint(new Point(button.Left + button.Width / 2,
                button.Top + button.Height / 2)) != button))
            throw new InvalidOperationException("The title buttons are covered or outside the window.");
    }

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, int parameter, int argument);

    private PluginRecord? Selected => list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as PluginRecord;

    private void Browse(object? sender, EventArgs eventArgs)
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose the Family Time game folder", ShowNewFolderButton = false };
        if (Directory.Exists(gamePath.Text)) dialog.SelectedPath = gamePath.Text;
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            gamePath.Text = dialog.SelectedPath;
            RefreshPlugins();
        }
    }

    private void ShowCredits(object? sender, EventArgs eventArgs)
    {
        using Stream source = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("FamilyModManager.Assets.OFL.txt")
            ?? throw new InvalidOperationException("Bundled font license is missing.");
        using var reader = new StreamReader(source);
        using var dialog = new Form { Text = "Credits and licenses", Width = 720, Height = 580,
            StartPosition = FormStartPosition.CenterParent };
        var text = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill, Font = SystemFonts.MessageBoxFont,
            Text = "Family Mod Manager 0.8.1\r\n\r\nBepInEx 5.4.23.5: github.com/BepInEx/BepInEx\r\n" +
                "Unity support assemblies: supplied Family Time compatibility pack.\r\n\r\n" +
                "Neuton Cursive font license:\r\n\r\n" + reader.ReadToEnd() };
        dialog.Controls.Add(text);
        dialog.ShowDialog(this);
    }

    private void RefreshPlugins()
    {
        try
        {
            string root = GameFolders.Normalize(gamePath.Text);
            plugins = PluginCatalog.Scan(root);
            RenderList();
            bool loaderFound = BepInExInstaller.IsInstalled(root);
            installLoader.Enabled = !loaderFound;
            status.Text = loaderFound
                ? $"{plugins.Count} plugin DLLs found. {plugins.Count(x => x.State == PluginState.Disabled)} disabled."
                : "BepInEx is missing. Choose Install BepInEx to set it up automatically.";
        }
        catch (Exception error)
        {
            plugins = Array.Empty<PluginRecord>();
            list.Items.Clear();
            installLoader.Enabled = false;
            status.Text = error.Message;
        }
        ShowSelection();
    }

    private void InstallLoader(object? sender, EventArgs eventArgs)
    {
        Attempt(() =>
        {
            string root = GameFolders.Normalize(gamePath.Text);
            BepInExInstaller.Install(root);
            RefreshPlugins();
            status.Text = "BepInEx installed. Launch Family Time once, then add your mods.";
        });
    }

    private void RenderList()
    {
        string query = search.Text.Trim();
        list.BeginUpdate();
        list.Items.Clear();
        foreach (PluginRecord plugin in plugins)
        {
            if (query.Length > 0 &&
                !plugin.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !plugin.Guids.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !plugin.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            var item = new ListViewItem(plugin.Name) { Tag = plugin };
            item.SubItems.Add(plugin.Version);
            item.SubItems.Add(plugin.State.ToString());
            item.SubItems.Add(plugin.RelativePath);
            if (plugin.State == PluginState.Disabled) item.ForeColor = Color.FromArgb(175, 175, 175);
            list.Items.Add(item);
        }
        list.EndUpdate();
        ShowSelection();
    }

    private void ShowSelection()
    {
        PluginRecord? item = Selected;
        enable.Enabled = item?.State == PluginState.Disabled;
        disable.Enabled = item?.State == PluginState.Enabled;
        details.Text = item == null ? "Select a plugin to see its ID and location."
            : $"ID: {item.Guids}    File: {item.AbsolutePath}";
    }

    private void Toggle(bool turnOn)
    {
        PluginRecord? item = Selected;
        if (item == null || (item.State == PluginState.Enabled) == turnOn) return;
        Attempt(() =>
        {
            PluginCatalog.SetEnabled(gamePath.Text, item, turnOn);
            RefreshPlugins();
            status.Text = $"{item.Name} {(turnOn ? "enabled" : "disabled")}. Restart Family Time to apply.";
        });
    }

    private void Import(object? sender, EventArgs eventArgs)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "BepInEx plugins (*.dll;*.zip)|*.dll;*.zip|All files (*.*)|*.*",
            Title = "Add a BepInEx plugin"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        Attempt(() =>
        {
            string installed = PluginCatalog.Import(gamePath.Text, dialog.FileName);
            RefreshPlugins();
            status.Text = "Installed in " + installed + ". Restart Family Time to apply.";
        });
    }

    private void OpenFolder(string child)
    {
        Attempt(() =>
        {
            string folder = Path.Combine(GameFolders.Normalize(gamePath.Text), "BepInEx", child);
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder + " does not exist yet.");
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        });
    }

    private void OpenLog(object? sender, EventArgs eventArgs)
    {
        Attempt(() =>
        {
            string log = Path.Combine(GameFolders.Normalize(gamePath.Text), "BepInEx", "LogOutput.log");
            if (!File.Exists(log)) throw new FileNotFoundException("No BepInEx log exists yet. Launch the game once.");
            Process.Start(new ProcessStartInfo(log) { UseShellExecute = true });
        });
    }

    private void LaunchGame(object? sender, EventArgs eventArgs)
    {
        Attempt(() =>
        {
            string root = GameFolders.Normalize(gamePath.Text);
            Process.Start(new ProcessStartInfo(Path.Combine(root, "FamilyTime.exe"))
            {
                WorkingDirectory = root, UseShellExecute = true
            });
        });
    }

    private void Attempt(Action action)
    {
        try { action(); }
        catch (Exception error)
        {
            status.Text = error.Message;
            MessageBox.Show(this, error.Message, "Family Time Mod Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
