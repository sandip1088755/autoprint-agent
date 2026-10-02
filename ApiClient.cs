using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AutoPrintAgent;

public class AuthException : Exception
{
    public AuthException() : base("Token invalid ya agent delete ho gaya") { }
}

public class PairResult
{
    [JsonPropertyName("token")] public string? Token { get; set; }
    [JsonPropertyName("shop_name")] public string? ShopName { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
}

public class PrintJobDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("original_filename")] public string OriginalFilename { get; set; } = "";
    [JsonPropertyName("page_count")] public int PageCount { get; set; }
    [JsonPropertyName("copies")] public int Copies { get; set; } = 1;
    [JsonPropertyName("paper_size")] public string PaperSize { get; set; } = "A4";
    [JsonPropertyName("print_mode")] public string PrintMode { get; set; } = "BW";
    [JsonPropertyName("duplex")] public bool Duplex { get; set; }
    [JsonPropertyName("printer_name")] public string? PrinterName { get; set; }
    [JsonPropertyName("page_range")] public string? PageRange { get; set; }
    [JsonPropertyName("orientation")] public string? Orientation { get; set; }
    [JsonPropertyName("booklet")] public bool Booklet { get; set; }
    [JsonPropertyName("binding")] public bool Binding { get; set; }
}

public sealed class ApiClient : IDisposable
{
    public const string Version = "1.0.0";
    readonly HttpClient _http;

    public ApiClient(string apiBase, string? token)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(apiBase.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(60),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AutoPrintAgent/" + Version);
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Add("X-Agent-Token", token);
    }

    public static async Task<PairResult> PairAsync(string apiBase, string code, CancellationToken ct)
    {
        using var c = new ApiClient(apiBase, null);
        using var resp = await c._http.PostAsJsonAsync("pair.php",
            new { code, machine_name = Environment.MachineName, agent_version = Version }, ct);
        PairResult? body = null;
        try { body = await resp.Content.ReadFromJsonAsync<PairResult>(cancellationToken: ct); } catch { }
        body ??= new PairResult();
        if (!resp.IsSuccessStatusCode && string.IsNullOrEmpty(body.Error))
            body.Error = "Server error " + (int)resp.StatusCode;
        return body;
    }

    public async Task<PrintJobDto?> NextJobAsync(CancellationToken ct)
    {
        using var resp = await _http.GetAsync("next-job.php", ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) throw new AuthException();
        if (resp.StatusCode == HttpStatusCode.NoContent) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PrintJobDto>(cancellationToken: ct);
    }

    public async Task<bool> ClaimAsync(string jobId, CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync("claim.php", new { job_id = jobId }, ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) throw new AuthException();
        return resp.IsSuccessStatusCode;
    }

    public async Task DownloadAsync(string jobId, string savePath, CancellationToken ct)
    {
        using var resp = await _http.GetAsync("download.php?id=" + Uri.EscapeDataString(jobId),
            HttpCompletionOption.ResponseHeadersRead, ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) throw new AuthException();
        resp.EnsureSuccessStatusCode();
        await using var fs = File.Create(savePath);
        await resp.Content.CopyToAsync(fs, ct);
    }

    public async Task ReportAsync(string jobId, string status, string? error, CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync("report.php",
            new { job_id = jobId, status, error_message = error }, ct);
        if (!resp.IsSuccessStatusCode)
            Log.Error($"Report {status} failed: {(int)resp.StatusCode}");
    }

    public void Dispose() => _http.Dispose();
}
