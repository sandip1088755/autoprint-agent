namespace AutoPrintAgent;

public class SetupForm : Form
{
    readonly AppConfig _cfg;
    readonly TextBox _code = new();
    readonly ComboBox _printer = new();
    readonly Label _status = new();
    readonly Button _ok = new();
    readonly Button _test = new();
    readonly PairingFile? _pf;

    public SetupForm(AppConfig cfg, PairingFile? pf = null)
    {
        _cfg = cfg;
        _pf = pf;
        Text = "AutoPrint Agent — Setup";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 350);
        Font = new Font("Segoe UI", 10f);

        Add(Lbl("Step 1 — Pairing Code", 16, 24, bold: true));
        Add(Lbl(pf != null
            ? $"Config file mil gayi — shop: {pf.Shop}. Bas printer chuno aur Connect dabao."
            : cfg.IsPaired
            ? $"Connected shop: {cfg.ShopName}. Code sirf tab daalo jab naya pair karna ho, warna khali chhodo."
            : "Shop panel → Print Agent page pe jo 8 letter ka code dikhta hai wo yahan daalo.",
            42, 44));

        _code.SetBounds(16, 90, 388, 36);
        _code.Font = new Font("Consolas", 16f, FontStyle.Bold);
        _code.CharacterCasing = CharacterCasing.Upper;
        _code.MaxLength = 9;
        if (pf != null) _code.Text = pf.Code;
        Controls.Add(_code);

        Add(Lbl("Step 2 — Printer chuno", 142, 24, bold: true));
        _printer.SetBounds(16, 170, 388, 30);
        _printer.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var p in PrinterService.InstalledPrinters()) _printer.Items.Add(p);
        var pick = !string.IsNullOrEmpty(cfg.PrinterName) ? cfg.PrinterName : new System.Drawing.Printing.PrinterSettings().PrinterName;
        var idx = _printer.Items.IndexOf(pick);
        if (idx >= 0) _printer.SelectedIndex = idx; else if (_printer.Items.Count > 0) _printer.SelectedIndex = 0;
        Controls.Add(_printer);

        _test.Text = "Test Page Print Karo";
        _test.SetBounds(16, 212, 388, 34);
        _test.Click += (_, _) =>
        {
            if (_printer.SelectedItem is not string p) return;
            try { PrinterService.PrintTestPage(p); SetStatus("Test page bhej diya.", Color.DarkGreen); }
            catch (Exception ex) { SetStatus("Test fail: " + ex.Message, Color.Firebrick); }
        };
        Controls.Add(_test);

        _status.SetBounds(16, 256, 388, 44);
        Controls.Add(_status);

        _ok.Text = "Connect && Save";
        _ok.SetBounds(16, 304, 388, 38);
        _ok.BackColor = Color.FromArgb(22, 163, 74);
        _ok.ForeColor = Color.White;
        _ok.FlatStyle = FlatStyle.Flat;
        _ok.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        _ok.Click += OnSave;
        Controls.Add(_ok);
        AcceptButton = _ok;
    }

    Label Lbl(string text, int y, int h, bool bold = false) => new()
    {
        Text = text,
        Bounds = new Rectangle(16, y, 388, h),
        Font = bold ? new Font("Segoe UI", 10.5f, FontStyle.Bold) : Font,
    };

    void Add(Control c) => Controls.Add(c);

    void SetStatus(string msg, Color c) { _status.ForeColor = c; _status.Text = msg; }

    async void OnSave(object? sender, EventArgs e)
    {
        var code = new string(_code.Text.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        if (_printer.SelectedItem is not string printer)
        { SetStatus("Printer chuno (Windows me printer install hona chahiye).", Color.Firebrick); return; }
        if (code.Length == 0 && !_cfg.IsPaired)
        { SetStatus("Pairing code daalo.", Color.Firebrick); return; }

        _ok.Enabled = false;
        SetStatus("Connect ho raha hai...", Color.Gray);
        try
        {
            if (code.Length > 0)
            {
                var r = await ApiClient.PairAsync(_cfg.ApiBase, code, CancellationToken.None);
                if (string.IsNullOrEmpty(r.Token))
                { SetStatus(r.Error ?? "Pair nahi hua.", Color.Firebrick); return; }
                _cfg.Token = r.Token;
                _cfg.ShopName = r.ShopName ?? "";
            }
            _cfg.PrinterName = printer;
            _cfg.Save();
            _pf?.Delete();   // code use ho gaya, file hata do
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            Log.Error("Pair: " + ex);
            SetStatus("Error: " + ex.Message, Color.Firebrick);
        }
        finally { _ok.Enabled = true; }
    }
}
