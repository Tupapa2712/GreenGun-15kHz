using Microsoft.Win32;

namespace Crt240pSwitch;

internal sealed class MainForm : Form
{
    private const string RunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Crt240pSwitch";

    private readonly AppConfig _cfg;
    private readonly ComboBox _cmbProtected = new();
    private readonly ComboBox _cmbCrt = new();
    private readonly Label _lblLive = new();
    private readonly Label _lblLog = new();
    private readonly CheckBox _chkAutoStart = new();
    private readonly NotifyIcon _trayIcon = new();
    private readonly bool _startMinimized;
    private List<MonitorEntry> _monitors = new();
    private bool _reallyExit;

    public MainForm(bool startMinimized = false)
    {
        _startMinimized = startMinimized;
        _cfg = AppConfig.Load();

        Text = "GTX 1650 / NVIDIA 15 kHz CRT Switcher";
        ClientSize = new Size(490, 560);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(24, 26, 32);
        ForeColor = Color.FromArgb(236, 239, 244);
        Font = new Font("Segoe UI", 9.5f);

        BuildUi();
        BuildTray();
        LoadMonitors();
    }

    private void BuildUi()
    {
        var title = new Label
        {
            Text = "NVIDIA 15 kHz CRT Switcher (240p / 224p)",
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(18, 12),
            AutoSize = true
        };
        Controls.Add(title);

        // Panel de seleccion dinamica de pantallas (Hard-Lock + Target CRT)
        var pnlSel = new Panel { Location = new Point(18, 44), Size = new Size(454, 126), BackColor = Color.FromArgb(34, 38, 46) };
        Controls.Add(pnlSel);

        pnlSel.Controls.Add(new Label { Text = "Monitor Principal Protegido (Hard-Lock — NUNCA se modifica):", Location = new Point(10, 8), AutoSize = true, ForeColor = Color.FromArgb(163, 190, 140), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) });
        _cmbProtected.Location = new Point(10, 26);
        _cmbProtected.Size = new Size(434, 26);
        _cmbProtected.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbProtected.SelectedIndexChanged += (_, _) => SaveSelections();
        pnlSel.Controls.Add(_cmbProtected);

        pnlSel.Controls.Add(new Label { Text = "Monitor CRT Destino (15 kHz — Recibe 2560×240 / 2560×224):", Location = new Point(10, 58), AutoSize = true, ForeColor = Color.FromArgb(136, 192, 208), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) });
        _cmbCrt.Location = new Point(10, 76);
        _cmbCrt.Size = new Size(434, 26);
        _cmbCrt.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbCrt.SelectedIndexChanged += (_, _) => SaveSelections();
        pnlSel.Controls.Add(_cmbCrt);

        _lblLive.Location = new Point(10, 104);
        _lblLive.Size = new Size(434, 18);
        _lblLive.Font = new Font("Consolas", 8.5f, FontStyle.Bold);
        pnlSel.Controls.Add(_lblLive);

        // Botones de conmutacion rapida
        var btn240 = MakeButton("Modo CRT 240p (2560×240 @ 60 Hz — Arranque)", new Point(18, 182), new Size(454, 42), Color.FromArgb(46, 139, 87));
        btn240.Click += (_, _) => ApplyMode(2560, 240, 60, persist: true);
        Controls.Add(btn240);

        var btn224 = MakeButton("Modo CRT 224p (2560×224 @ 60 Hz — SNES/MD/NeoGeo)", new Point(18, 230), new Size(454, 38), Color.FromArgb(39, 120, 110));
        btn224.Click += (_, _) => ApplyMode(2560, 224, 60, persist: false);
        Controls.Add(btn224);

        var btn1920 = MakeButton("CRT 240p Alt (1920×240)", new Point(18, 276), new Size(222, 34), Color.FromArgb(59, 66, 82));
        btn1920.Click += (_, _) => ApplyMode(1920, 240, 60, persist: false);
        Controls.Add(btn1920);

        var btn720 = MakeButton("Emergencia: 1280×720", new Point(250, 276), new Size(222, 34), Color.FromArgb(76, 86, 106));
        btn720.Click += (_, _) => ApplyMode(1280, 720, 60, persist: false);
        Controls.Add(btn720);

        // Seccion de Calibracion / Instalacion EDID + NVAPI (con Watchdog de 15s)
        var lblCal = new Label { Text = "Calibración e Instalación de Modos (requiere Administrador):", Location = new Point(18, 322), AutoSize = true, ForeColor = Color.FromArgb(216, 222, 233), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        Controls.Add(lblCal);

        var btnEdid = MakeButton("1. Instalar EDID_OVERRIDE (2560×240 + 2560×224 + Backup)", new Point(18, 344), new Size(454, 36), Color.FromArgb(67, 76, 130));
        btnEdid.Click += (_, _) => RunInstallEdid();
        Controls.Add(btnEdid);

        var btnNv224 = MakeButton("2. Registrar 2560×224 vía NVAPI (Prueba 15s)", new Point(18, 386), new Size(222, 36), Color.FromArgb(94, 129, 172));
        btnNv224.Click += (_, _) => RunNvApiRegister(is224p: true);
        Controls.Add(btnNv224);

        var btnRollback = MakeButton("Restaurar EDID Original", new Point(250, 386), new Size(222, 36), Color.FromArgb(140, 64, 68));
        btnRollback.Click += (_, _) => RunRollback();
        Controls.Add(btnRollback);

        _chkAutoStart.Text = "Iniciar minimizado en bandeja con Windows";
        _chkAutoStart.Checked = IsAutoStartEnabled();
        _chkAutoStart.Location = new Point(18, 434);
        _chkAutoStart.AutoSize = true;
        _chkAutoStart.CheckedChanged += (_, _) => SetAutoStart(_chkAutoStart.Checked);
        Controls.Add(_chkAutoStart);

        var btnRefresh = MakeButton("Recargar Pantallas", new Point(332, 430), new Size(140, 28), Color.FromArgb(59, 66, 82));
        btnRefresh.Click += (_, _) => LoadMonitors();
        Controls.Add(btnRefresh);

        _lblLog.Location = new Point(18, 470);
        _lblLog.Size = new Size(454, 78);
        _lblLog.Font = new Font("Segoe UI", 8.5f);
        _lblLog.ForeColor = Color.FromArgb(180, 188, 200);
        _lblLog.Text = "Listo. Selecciona tu Monitor Protegido y tu Monitor CRT Destino.";
        Controls.Add(_lblLog);
    }

    private static Button MakeButton(string text, Point loc, Size size, Color bg)
    {
        var b = new Button { Text = text, Location = loc, Size = size, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private void BuildTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Modo CRT 240p (2560×240 @ 60 Hz)", null, (_, _) => ApplyMode(2560, 240, 60, persist: true));
        menu.Items.Add("Modo CRT 224p (2560×224 @ 60 Hz)", null, (_, _) => ApplyMode(2560, 224, 60, persist: false));
        menu.Items.Add("Modo CRT 240p Alt (1920×240 @ 60 Hz)", null, (_, _) => ApplyMode(1920, 240, 60, persist: false));
        menu.Items.Add("Emergencia: 1280×720 @ 60 Hz", null, (_, _) => ApplyMode(1280, 720, 60, persist: false));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Mostrar ventana", null, (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); LoadMonitors(); });
        menu.Items.Add("Salir", null, (_, _) => { _reallyExit = true; _trayIcon.Visible = false; Close(); });

        _trayIcon.Icon = SystemIcons.Application;
        _trayIcon.Text = "NVIDIA 15 kHz CRT Switcher";
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); LoadMonitors(); };
    }

    private void LoadMonitors()
    {
        _monitors = DisplayManager.EnumerateMonitors();
        _cmbProtected.Items.Clear();
        _cmbCrt.Items.Clear();
        foreach (var m in _monitors) { _cmbProtected.Items.Add(m); _cmbCrt.Items.Add(m); }

        int pIdx = _monitors.FindIndex(m => !string.IsNullOrEmpty(_cfg.ProtectedMonitorInstanceId) && m.DeviceInstanceId.Equals(_cfg.ProtectedMonitorInstanceId, StringComparison.OrdinalIgnoreCase));
        if (pIdx < 0) pIdx = _monitors.FindIndex(m => m.IsPrimary);
        if (pIdx >= 0) _cmbProtected.SelectedIndex = pIdx;

        int cIdx = _monitors.FindIndex(m => !string.IsNullOrEmpty(_cfg.TargetCrtInstanceId) && m.DeviceInstanceId.Equals(_cfg.TargetCrtInstanceId, StringComparison.OrdinalIgnoreCase));
        if (cIdx < 0) cIdx = _monitors.FindIndex(m => !m.IsPrimary);
        if (cIdx >= 0) _cmbCrt.SelectedIndex = cIdx;

        UpdateLiveStatus();
    }

    private void SaveSelections()
    {
        if (_cmbProtected.SelectedItem is MonitorEntry p) _cfg.ProtectedMonitorInstanceId = p.DeviceInstanceId;
        if (_cmbCrt.SelectedItem is MonitorEntry c)
        {
            _cfg.TargetCrtInstanceId = c.DeviceInstanceId;
            _cfg.TargetCrtMonitorIndex = c.DisplayIndex;
        }
        _cfg.Save();
        UpdateLiveStatus();
    }

    private void UpdateLiveStatus()
    {
        var crt = DisplayManager.ResolveTargetCrt(_cfg, out var err);
        if (crt == null)
        {
            _lblLive.Text = err ?? "Selecciona un monitor secundario como CRT.";
            _lblLive.ForeColor = Color.FromArgb(235, 203, 139);
            return;
        }
        _lblLive.Text = $"CRT Activo: {crt.GdiName} | {crt.Width}×{crt.Height} @ {crt.RefreshHz}Hz | fH: {crt.HFreqKHz:F2} kHz | {crt.Scanline}";
        _lblLive.ForeColor = (crt.Height == 240 || crt.Height == 224) ? Color.FromArgb(143, 224, 150) : Color.FromArgb(129, 161, 193);
    }

    private void ApplyMode(uint w, uint h, uint f, bool persist)
    {
        var (ok, msg) = DisplayManager.SetCrtMode(_cfg, w, h, f, persist);
        SetLog(msg, ok);
        LoadMonitors();
    }

    private void RunInstallEdid()
    {
        var (ok, msg) = DisplayManager.InstallEdidOverride(_cfg);
        SetLog(msg, ok);
        if (ok)
        {
            var (swOk, swMsg) = DisplayManager.SetCrtMode(_cfg, 2560, 240, 60, persistInRegistry: true);
            if (swOk && !Show15sConfirmDialog("2560×240 @ 60 Hz"))
            {
                DisplayManager.RollbackEdidOverride(_cfg);
                SetLog("No confirmado en 15s. EDID_OVERRIDE revertido automáticamente.", false);
            }
            else if (swOk)
            {
                SetLog("EDID_OVERRIDE y 2560×240 @ 60 Hz confirmados permanentemente.", true);
            }
            else SetLog(msg + "\n" + swMsg, false);
        }
        LoadMonitors();
    }

    private void RunNvApiRegister(bool is224p)
    {
        var crt = DisplayManager.ResolveTargetCrt(_cfg, out var err);
        if (crt == null) { SetLog(err ?? "CRT no disponible.", false); return; }

        var (ok, msg) = NvApiBridge.TryAndConfirmMode(crt.GdiName, is224p, () => Show15sConfirmDialog(is224p ? "2560×224 @ 60 Hz (NVAPI)" : "2560×240 @ 60 Hz (NVAPI)"));
        SetLog(msg, ok);
        LoadMonitors();
    }

    private void RunRollback()
    {
        var (ok, msg) = DisplayManager.RollbackEdidOverride(_cfg);
        SetLog(msg, ok);
        LoadMonitors();
    }

    private static bool Show15sConfirmDialog(string modeLabel)
    {
        using var dlg = new Form
        {
            Text = "Confirmar Modo CRT (Watchdog 15s)",
            ClientSize = new Size(380, 140),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            TopMost = true,
            BackColor = Color.FromArgb(30, 34, 42),
            ForeColor = Color.White
        };

        int seconds = 15;
        var lbl = new Label { Text = $"¿Se ve estable {modeLabel} en el CRT?\nRevirtiendo automáticamente en {seconds}s...", Location = new Point(16, 16), Size = new Size(348, 52), Font = new Font("Segoe UI", 9.5f) };
        var btnOk = MakeButton("Mantener Modo", new Point(16, 82), new Size(166, 38), Color.FromArgb(46, 139, 87));
        var btnCancel = MakeButton("Revertir Ahora", new Point(198, 82), new Size(166, 38), Color.FromArgb(140, 64, 68));
        btnOk.DialogResult = DialogResult.OK;
        btnCancel.DialogResult = DialogResult.Cancel;
        dlg.Controls.AddRange(new Control[] { lbl, btnOk, btnCancel });

        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) =>
        {
            seconds--;
            lbl.Text = $"¿Se ve estable {modeLabel} en el CRT?\nRevirtiendo automáticamente en {seconds}s...";
            if (seconds <= 0) { timer.Stop(); dlg.DialogResult = DialogResult.Cancel; dlg.Close(); }
        };
        dlg.Shown += (_, _) => timer.Start();
        dlg.FormClosed += (_, _) => { timer.Stop(); timer.Dispose(); };
        return dlg.ShowDialog() == DialogResult.OK;
    }

    private void SetLog(string text, bool ok)
    {
        _lblLog.Text = text;
        _lblLog.ForeColor = ok ? Color.FromArgb(163, 190, 140) : Color.FromArgb(235, 110, 110);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_startMinimized) { WindowState = FormWindowState.Minimized; Hide(); }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized) Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_reallyExit && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.OnFormClosing(e);
    }

    private static bool IsAutoStartEnabled()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunRegPath, false);
        return k?.GetValue(RunValueName) is string s && !string.IsNullOrWhiteSpace(s);
    }

    private void SetAutoStart(bool enable)
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunRegPath, true);
        if (k == null) return;
        if (enable) k.SetValue(RunValueName, $"\"{Environment.ProcessPath ?? Application.ExecutablePath}\" --minimized");
        else k.DeleteValue(RunValueName, false);
    }
}
