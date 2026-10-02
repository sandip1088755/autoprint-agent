using System.Diagnostics;

namespace AutoPrintAgent;

public class TrayContext : ApplicationContext
{
    readonly NotifyIcon _tray;
    readonly ToolStripMenuItem _statusItem = new("Starting...") { Enabled = false };
    readonly ToolStripMenuItem _autoItem = new("Windows ke saath start") { CheckOnClick = true };
    readonly SynchronizationContext _ui;
    readonly Dictionary<AgentState, Icon> _icons;

    AppConfig _cfg = AppConfig.Load();
    CancellationTokenSource? _cts;
    Worker? _worker;
    bool _dialogOpen;

    public TrayContext()
    {
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        _icons = new()
        {
            [AgentState.Unpaired] = MakeIcon(Color.Gray),
            [AgentState.Online]   = MakeIcon(Color.FromArgb(22, 163, 74)),
            [AgentState.Printing] = MakeIcon(Color.FromArgb(37, 99, 235)),
            [AgentState.Offline]  = MakeIcon(Color.FromArgb(234, 88, 12)),
            [AgentState.Error]    = MakeIcon(Color.FromArgb(220, 38, 38)),
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings / Printer badlo", null, (_, _) => OpenSettings());
        menu.Items.Add("Test page print karo", null, (_, _) => TestPrint());
        _autoItem.Checked = _cfg.AutoStart;
        _autoItem.CheckedChanged += (_, _) =>
        {
            _cfg.AutoStart = _autoItem.Checked; _cfg.Save(); AppConfig.ApplyAutoStart(_cfg.AutoStart);
        };
        menu.Items.Add(_autoItem);
        menu.Items.Add("Log dekho", null, (_, _) => OpenLog());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        _tray = new NotifyIcon
        {
            Icon = _icons[AgentState.Unpaired],
            Text = "AutoPrint Agent",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => OpenSettings();

        // Message loop start hone ke baad startup chalao
        var t = new System.Windows.Forms.Timer { Interval = 300 };
        t.Tick += (_, _) => { t.Stop(); t.Dispose(); Startup(); };
        t.Start();
    }

    void Startup()
    {
        if (!_cfg.IsPaired)
        {
            if (!OpenSettings(PairingFile.Find())) { ExitApp(); return; }
        }
        else
        {
            AppConfig.ApplyAutoStart(_cfg.AutoStart);
            StartWorker();
        }
    }

    bool OpenSettings(PairingFile? pf = null)
    {
        if (_dialogOpen) return _cfg.IsPaired;
        _dialogOpen = true;
        try
        {
            StopWorker();
            using var f = new SetupForm(_cfg, pf);
            var res = f.ShowDialog();
            if (res == DialogResult.OK && _cfg.IsPaired)
            {
                AppConfig.ApplyAutoStart(_cfg.AutoStart);
                _autoItem.Checked = _cfg.AutoStart;
            }
            if (_cfg.IsPaired) StartWorker();
            return _cfg.IsPaired;
        }
        finally { _dialogOpen = false; }
    }

    void StartWorker()
    {
        StopWorker();
        _cts = new CancellationTokenSource();
        var w = new Worker(_cfg);
        _worker = w;
        w.StateChanged += (s, m) => _ui.Post(_ => { if (ReferenceEquals(w, _worker)) OnState(s, m); }, null);
        var ct = _cts.Token;
        _ = Task.Run(async () =>
        {
            try { await w.RunAsync(ct); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log.Error(ex.ToString()); }
        });
    }

    void StopWorker()
    {
        _cts?.Cancel();
        _cts = null;
        _worker = null;
    }

    void OnState(AgentState s, string msg)
    {
        _tray.Icon = _icons[s];
        _statusItem.Text = msg.Length > 60 ? msg[..60] + "…" : msg;
        var tip = "AutoPrint Agent — " + msg;
        _tray.Text = tip.Length > 63 ? tip[..63] : tip;

        if (s == AgentState.Unpaired)
        {
            _tray.ShowBalloonTip(5000, "AutoPrint Agent", "Dobara pair karna hoga.", ToolTipIcon.Warning);
            _cfg.TokenProtected = "";
            _cfg.Save();
            OpenSettings(PairingFile.Find());
        }
    }

    void TestPrint()
    {
        try { PrinterService.PrintTestPage(_cfg.PrinterName); }
        catch (Exception ex) { MessageBox.Show("Test fail: " + ex.Message, "AutoPrint Agent"); }
    }

    static void OpenLog()
    {
        try { Process.Start(new ProcessStartInfo("notepad.exe", "\"" + Log.LogFile + "\"") { UseShellExecute = true }); }
        catch { }
    }

    void ExitApp()
    {
        StopWorker();
        _tray.Visible = false;
        _tray.Dispose();
        ExitThread();
    }

    static Icon MakeIcon(Color c)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var b = new SolidBrush(c);
            g.FillEllipse(b, 1, 1, 30, 30);
            using var f = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
            g.DrawString("P", f, Brushes.White, 8, 6);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
