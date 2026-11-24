namespace Crt240pSwitch;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var cfg = AppConfig.Load();

        if (args.Length >= 2 && args[0].Equals("--export-monitors", StringComparison.OrdinalIgnoreCase))
        {
            var monitors = DisplayManager.EnumerateMonitors();
            var lines = monitors.Select(m =>
                $"{m.GdiName}\t{(string.IsNullOrWhiteSpace(m.FriendlyName) ? "Monitor" : m.FriendlyName)}\t{m.DeviceInstanceId}\t{(m.IsPrimary ? "1" : "0")}\t{m.Width}\t{m.Height}\t{m.RefreshHz}\t{m.DisplayIndex}");
            File.WriteAllLines(args[1], lines, System.Text.Encoding.UTF8);
            return;
        }

        if (args.Length >= 4 && args[0].Equals("--save-config", StringComparison.OrdinalIgnoreCase))
        {
            cfg.ProtectedMonitorInstanceId = args[1];
            cfg.TargetCrtInstanceId = args[2];
            if (int.TryParse(args[3], out int idx) && idx >= 1)
                cfg.TargetCrtMonitorIndex = idx;
            cfg.Save();
            return;
        }

        if (args.Any(a => a.Equals("--auto-install-modes", StringComparison.OrdinalIgnoreCase)))
        {
            DisplayManager.Log("=== Ejecutando --auto-install-modes desde el instalador ===");
            var edidRes = DisplayManager.InstallEdidOverride(cfg);
            if (!edidRes.ok)
            {
                DisplayManager.Log($"--auto-install-modes FALLO en EDID: {edidRes.message}");
                Environment.ExitCode = 1;
                return;
            }
            var modeRes = DisplayManager.SetCrtMode(cfg, 2560, 240, 60, persistInRegistry: true);
            if (!modeRes.ok)
            {
                DisplayManager.Log($"--auto-install-modes FALLO al aplicar 2560x240: {modeRes.message}. Revirtiendo EDID...");
                DisplayManager.RollbackEdidOverride(cfg);
                Environment.ExitCode = 2;
                return;
            }
            DisplayManager.Log($"--auto-install-modes COMPLETADO: {modeRes.message}");
            Environment.ExitCode = 0;
            return;
        }

        if (args.Any(a => a.Equals("--apply-224p", StringComparison.OrdinalIgnoreCase)))
        {
            DisplayManager.SetCrtMode(cfg, 2560, 224, 60, persistInRegistry: false);
            return;
        }

        if (args.Any(a => a.Equals("--apply-240p", StringComparison.OrdinalIgnoreCase)))
        {
            DisplayManager.SetCrtMode(cfg, 2560, 240, 60, persistInRegistry: true);
            return;
        }

        if (args.Any(a => a.Equals("--rollback", StringComparison.OrdinalIgnoreCase)))
        {
            DisplayManager.RollbackEdidOverride(cfg);
            return;
        }

        bool startMinimized = args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        Application.Run(new MainForm(startMinimized));
    }
}
