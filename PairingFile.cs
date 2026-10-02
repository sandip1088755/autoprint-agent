using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoPrintAgent;

// Shop panel se download hui autoprint-config.json — isme ek-baar-wala pairing code hota hai
public class PairingFile
{
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("shop")] public string Shop { get; set; } = "";
    [JsonIgnore] public string FilePath { get; set; } = "";

    public static PairingFile? Find()
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dirs = new[]
            {
                Path.Combine(home, "Downloads"),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                AppConfig.AppDir,
            };
            var files = dirs.Where(Directory.Exists)
                .SelectMany(d =>
                {
                    try { return new DirectoryInfo(d).GetFiles("autoprint-config*.json"); }
                    catch { return Array.Empty<FileInfo>(); }
                })
                .Where(f => f.LastWriteTime > DateTime.Now.AddDays(-2))
                .OrderByDescending(f => f.LastWriteTime);

            foreach (var f in files)
            {
                try
                {
                    var pf = JsonSerializer.Deserialize<PairingFile>(File.ReadAllText(f.FullName));
                    if (pf != null && pf.Code.Length == 8)
                    {
                        pf.FilePath = f.FullName;
                        return pf;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { Log.Error("PairingFile.Find: " + ex.Message); }
        return null;
    }

    public void Delete()
    {
        try { File.Delete(FilePath); } catch { }
    }
}
