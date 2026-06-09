using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace IpRevealer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new OverlayForm());
    }
}

/// <summary>
/// A small, borderless, always-on-top window that shows the machine's
/// external (public) IP address. Drag it anywhere; right-click for options.
/// </summary>
internal sealed partial class OverlayForm : Form
{
    private static readonly string[] IpServices =
    {
        "https://ipreveal.cc/ip",
        "https://ifconfig.io/ip",
        "https://api.ipify.org",
        "https://ifconfig.me/ip",
        "https://icanhazip.com",
        "https://ipinfo.io/ip",
    };

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IpRevealer", "settings.json");

    private readonly Label _label;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private string _currentIp = "…";

    /// <summary>Index into <see cref="IpServices"/> to try first; -1 = auto (try all in order).</summary>
    private int _serviceIndex = -1;

    public OverlayForm()
    {
        // ---- Window chrome ----
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(20, 20, 20);
        Opacity = 0.85;
        Padding = new Padding(10, 6, 10, 6);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // ---- IP label ----
        _label = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(0, 220, 120),
            Font = new Font("Consolas", 12f, FontStyle.Bold),
            Text = "IP: …",
        };
        Controls.Add(_label);

        // ---- Context menu ----
        var menu = new ContextMenuStrip();
        menu.Items.Add("Refresh now", null, async (_, _) => await UpdateIpAsync());
        menu.Items.Add("Copy IP", null, (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_currentIp) && _currentIp != "…")
                Clipboard.SetText(_currentIp);
        });
        var startup = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = IsStartupEnabled(),
        };
        startup.Click += (_, _) => SetStartup(startup.Checked);
        menu.Items.Add(startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Close());
        ContextMenuStrip = menu;

        // Let drags on the label move the window too.
        MakeDraggable(this);
        MakeDraggable(_label);

        // ---- Refresh timer ----
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _refreshTimer.Tick += async (_, _) => await UpdateIpAsync();

        Load += async (_, _) =>
        {
            RestoreSettings();
            EnsureOnScreen();
            _refreshTimer.Start();
            await UpdateIpAsync();
        };

        FormClosing += (_, _) => SaveSettings();
    }

    private async Task UpdateIpAsync()
    {
        var ip = await FetchExternalIpAsync(_serviceIndex);
        _currentIp = ip ?? _currentIp;
        var lan = GetLocalIp() ?? "n/a";

        _label.Text = ip is null
            ? $"WAN: offline\nLAN: {lan}"
            : $"WAN: {ip}\nLAN: {lan}";
        _label.ForeColor = ip is null
            ? Color.FromArgb(230, 90, 90)
            : Color.FromArgb(0, 220, 120);
        EnsureOnScreen();
    }

    /// <summary>
    /// Returns the LAN IP of the network interface that would actually be
    /// used to reach the internet (the "preferred outbound" address).
    /// No packets are sent — a connected UDP socket just resolves routing.
    /// </summary>
    private static string? GetLocalIp()
    {
        try
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram,
                System.Net.Sockets.ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            return (socket.LocalEndPoint as System.Net.IPEndPoint)?.Address.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> FetchExternalIpAsync(int preferredIndex)
    {
        foreach (var url in OrderedServices(preferredIndex))
        {
            try
            {
                var text = (await Http.GetStringAsync(url)).Trim();
                if (System.Net.IPAddress.TryParse(text, out var addr))
                    return addr.ToString();
            }
            catch
            {
                // Try the next provider.
            }
        }
        return null;
    }

    /// <summary>The preferred service first (if any), then the rest as fallbacks.</summary>
    private static IEnumerable<string> OrderedServices(int preferredIndex)
    {
        if (preferredIndex >= 0 && preferredIndex < IpServices.Length)
        {
            yield return IpServices[preferredIndex];
            for (int i = 0; i < IpServices.Length; i++)
                if (i != preferredIndex) yield return IpServices[i];
        }
        else
        {
            foreach (var url in IpServices) yield return url;
        }
    }

    // ---------- Dragging (move a borderless window) ----------
    // Done manually (rather than the WM_NCLBUTTONDOWN "begin move" trick) so
    // that DoubleClick still fires — that trick enters a modal move loop on
    // mouse-down and swallows the double-click.
    private bool _dragging;
    private Point _dragStartCursor;
    private Point _dragStartForm;

    private void MakeDraggable(Control c)
    {
        c.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragStartCursor = Cursor.Position;
                _dragStartForm = Location;
            }
        };
        c.MouseMove += (_, _) =>
        {
            if (_dragging)
            {
                var dx = Cursor.Position.X - _dragStartCursor.X;
                var dy = Cursor.Position.Y - _dragStartCursor.Y;
                Location = new Point(_dragStartForm.X + dx, _dragStartForm.Y + dy);
            }
        };
        c.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) _dragging = false;
        };
        c.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowServicePicker();
        };
    }

    /// <summary>Modal popup to choose which IP service supplies the WAN address.</summary>
    private void ShowServicePicker()
    {
        using var dlg = new Form
        {
            Text = "Choose IP service",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            TopMost = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };

        var layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14),
            Dock = DockStyle.Fill,
        };

        var radios = new List<RadioButton>();
        void AddOption(int index, string text)
        {
            var rb = new RadioButton
            {
                Text = text,
                AutoSize = true,
                Checked = _serviceIndex == index,
                Tag = index,
                Margin = new Padding(3, 3, 3, 3),
            };
            radios.Add(rb);
            layout.Controls.Add(rb);
        }

        AddOption(-1, "Auto (try all, in order)");
        for (int i = 0; i < IpServices.Length; i++)
            AddOption(i, IpServices[i]);

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(3, 10, 3, 3),
        };
        layout.Controls.Add(ok);

        dlg.Controls.Add(layout);
        dlg.AcceptButton = ok;

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _serviceIndex = (int)radios.First(r => r.Checked).Tag!;
            SaveSettings();
            _ = UpdateIpAsync();
        }
    }

    // ---------- Keep the window visible on the current desktop ----------
    private void EnsureOnScreen()
    {
        var wa = Screen.FromControl(this).WorkingArea;
        int x = Math.Min(Math.Max(Location.X, wa.Left), wa.Right - Width);
        int y = Math.Min(Math.Max(Location.Y, wa.Top), wa.Bottom - Height);
        Location = new Point(x, y);
    }

    // ---------- Settings persistence ----------
    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(new Settings
            {
                X = Location.X,
                Y = Location.Y,
                ServiceIndex = _serviceIndex,
            });
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* not critical */ }
    }

    private void RestoreSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
                if (s is not null)
                {
                    Location = new Point(s.X, s.Y);
                    _serviceIndex = s.ServiceIndex;
                    return;
                }
            }
        }
        catch { /* fall through to default */ }

        // Default: top-right corner.
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(wa.Right - 220, wa.Top + 20);
    }

    private sealed class Settings
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int ServiceIndex { get; set; } = -1;
    }

    // ---------- Run at login (per-user, no admin needed) ----------
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "IpRevealer";

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) is not null;
    }

    private static void SetStartup(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;
            if (enabled)
                key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch { /* ignore */ }
    }
}
