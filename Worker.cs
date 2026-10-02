namespace AutoPrintAgent;

public enum AgentState { Unpaired, Online, Printing, Offline, Error }

public class Worker
{
    public event Action<AgentState, string>? StateChanged;

    readonly AppConfig _cfg;
    readonly string _tmp;
    AgentState? _state;
    string _msg = "";

    public Worker(AppConfig cfg)
    {
        _cfg = cfg;
        _tmp = Path.Combine(Path.GetTempPath(), "AutoPrintAgent");
        Directory.CreateDirectory(_tmp);
    }

    void Set(AgentState s, string msg)
    {
        if (_state == s && _msg == msg) return;   // log spam nahi
        _state = s; _msg = msg;
        Log.Info($"{s}: {msg}");
        StateChanged?.Invoke(s, msg);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var api = new ApiClient(_cfg.ApiBase, _cfg.Token);
        var printer = new PrinterService(_cfg);
        var idle = TimeSpan.FromSeconds(Math.Clamp(_cfg.PollSeconds, 5, 60));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var job = await api.NextJobAsync(ct);
                if (job != null)
                {
                    await ProcessAsync(api, printer, job, ct);
                    continue;
                }
                Set(AgentState.Online, "Online — print jobs ka intezaar");
                await Task.Delay(idle, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (AuthException)
            {
                Set(AgentState.Unpaired, "Token invalid — dobara pair karo");
                return;
            }
            catch (HttpRequestException ex)
            {
                Set(AgentState.Offline, "Server se connect nahi ho raha: " + ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(20), ct);
            }
            catch (Exception ex)
            {
                Set(AgentState.Error, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(20), ct);
            }
        }
    }

    async Task ProcessAsync(ApiClient api, PrinterService printer, PrintJobDto job, CancellationToken ct)
    {
        if (!await api.ClaimAsync(job.Id, ct)) { Log.Info("Claim fail (kisi aur ne le li): " + job.Id); return; }

        string? local = null;
        try
        {
            var ext = Path.GetExtension(job.OriginalFilename).ToLowerInvariant();
            if (ext is not (".pdf" or ".jpg" or ".jpeg" or ".png"))
                throw new NotSupportedException("Unsupported file: " + ext);

            Set(AgentState.Printing, $"Print ho raha hai: {job.OriginalFilename}");
            local = Path.Combine(_tmp, Guid.NewGuid().ToString("N") + ext);
            await api.DownloadAsync(job.Id, local, ct);

            if (job.Booklet || job.Binding)
                Log.Info($"Note: booklet/binding ({job.Booklet}/{job.Binding}) abhi auto nahi hota — shopkeeper ko manual karna hoga");

            await printer.PrintAsync(job, local, ct);
            await api.ReportAsync(job.Id, "COMPLETED", null, ct);
            Log.Info("COMPLETED " + job.Id);
        }
        catch (AuthException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            Log.Error($"FAILED {job.Id}: {ex.Message}");
            try { await api.ReportAsync(job.Id, "FAILED", ex.Message, CancellationToken.None); } catch { }
        }
        finally
        {
            if (local != null) try { File.Delete(local); } catch { }
        }
    }
}
