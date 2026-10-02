using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace AutoPrintAgent;

public class AppConfig
{
    // Server ka address. Badalna ho to exe ke saath server.txt rakh do (ek line: https://...)
    const string DefaultServer = "https://autoprint.freedev.app";

    public string TokenProtected { get; set; } = "";   // Windows DPAPI se encrypted
    public string PrinterName { get; set; } = "";
    public string ShopName { get; set; } = "";
    public int PollSeconds { get; set; } = 10;
    public bool AutoStart { get; set; } = true;

    [JsonIgnore] public bool IsPaired => !string.IsNullOrEmpty(TokenProtected);
    [JsonIgnore] public string ApiBase => ServerUrl + "/api/agent";

    [JsonIgnore]
    public string Token
    {
        get
        {
            if (!IsPaired) return "";
            try
            {
                var raw = ProtectedData.Unprotect(Convert.FromBase64String(TokenProtected), null,
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(raw);
            }
            catch { return ""; }
        }
        set
        {
            var raw = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null,
                DataProtectionScope.CurrentUser);
            TokenProtected = Convert.ToBase64String(raw);
        }
    }

    public static string ServerUrl
    {
        get
        {
            try
            {
                var f = Path.Combine(AppDir, "server.txt");
                if (File.Exists(f))
                {
                    var t = File.ReadAllText(f).Trim().TrimEnd('/');
                    if (t.StartsWith("https://") || t.StartsWith("http://")) return t;
                }
            }
            catch { }
            return DefaultServer;
        }
    }

    public static string AppDir =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoPrintAgent");

    static string FilePath => Path.Combine(DataDir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath)) ?? new AppConfig();
        }
        catch (Exception ex) { Log.Error("Config load: " + ex.Message); }
        return new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    // Windows ke saath auto start
    public static void ApplyAutoStart(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (on) key.SetValue("AutoPrintAgent", "\"" + Environment.ProcessPath + "\"");
            else key.DeleteValue("AutoPrintAgent", false);
        }
        catch (Exception ex) { Log.Error("AutoStart: " + ex.Message); }
    }
}

public static class Log
{
    static readonly object L = new();
    public static string LogFile => Path.Combine(AppConfig.DataDir, "agent.log");

    public static void Info(string m) => Write("INFO", m);
    public static void Error(string m) => Write("ERR ", m);

    static void Write(string lvl, string m)
    {
        try
        {
            lock (L)
            {
                Directory.CreateDirectory(AppConfig.DataDir);
                var fi = new FileInfo(LogFile);
                if (fi.Exists && fi.Length > 1_000_000) File.Move(LogFile, LogFile + ".old", true);
                File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{lvl}] {m}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
