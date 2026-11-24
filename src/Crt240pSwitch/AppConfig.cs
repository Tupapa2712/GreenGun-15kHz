using System.Text.Json;

namespace Crt240pSwitch;

internal sealed class AppConfig
{
    public string ProtectedMonitorInstanceId { get; set; } = "";
    public string TargetCrtInstanceId { get; set; } = "";
    public int TargetCrtMonitorIndex { get; set; } = 2;

    public static string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Crt240pSwitch");

    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");
    public static string BackupDir => Path.Combine(AppContext.BaseDirectory, "backup");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch { }
        return new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
