using System.Diagnostics;
using System.Drawing.Printing;
using System.Text.RegularExpressions;

namespace AutoPrintAgent;

public class PrinterService
{
    readonly AppConfig _cfg;
    public PrinterService(AppConfig cfg) => _cfg = cfg;

    public static IEnumerable<string> InstalledPrinters() =>
        PrinterSettings.InstalledPrinters.Cast<string>();

    public async Task PrintAsync(PrintJobDto job, string path, CancellationToken ct)
    {
        var sumatra = Path.Combine(AppConfig.AppDir, "sumatra", "SumatraPDF.exe");
        if (!File.Exists(sumatra))
            throw new FileNotFoundException("SumatraPDF nahi mila: " + sumatra);

        // Server wala printer naam sirf tab jab ye PC pe installed ho
        var printer = _cfg.PrinterName;
        if (!string.IsNullOrWhiteSpace(job.PrinterName) &&
            InstalledPrinters().Contains(job.PrinterName, StringComparer.OrdinalIgnoreCase))
            printer = job.PrinterName;
        if (string.IsNullOrWhiteSpace(printer))
            throw new InvalidOperationException("Printer select nahi hai — tray icon -> Settings");

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var isImage = ext is ".jpg" or ".jpeg" or ".png";

        var s = new List<string>();
        if (!isImage && !string.IsNullOrWhiteSpace(job.PageRange) &&
            Regex.IsMatch(job.PageRange, @"^[\d\s,\-]+$"))
            s.Add(job.PageRange.Replace(" ", ""));
        if (job.Copies > 1) s.Add(job.Copies + "x");
        s.Add(job.Duplex ? "duplexlong" : "simplex");
        s.Add(job.PrintMode == "COLOR" ? "color" : "monochrome");
        s.Add("paper=" + job.PaperSize);
        if (isImage) s.Add("fit");
        if (job.Orientation is "portrait" or "landscape") s.Add(job.Orientation);

        var psi = new ProcessStartInfo(sumatra)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-print-to");
        psi.ArgumentList.Add(printer);
        psi.ArgumentList.Add("-print-settings");
        psi.ArgumentList.Add(string.Join(",", s));
        psi.ArgumentList.Add("-silent");
        psi.ArgumentList.Add("-exit-when-done");
        psi.ArgumentList.Add(path);

        Log.Info($"Print: {printer} | {string.Join(",", s)} | {job.Id}");

        using var proc = Process.Start(psi) ?? throw new Exception("SumatraPDF start nahi hua");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(2));
        try { await proc.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { proc.Kill(true); } catch { }
            if (ct.IsCancellationRequested) throw;
            throw new TimeoutException("Print 2 minute me complete nahi hua");
        }
        if (proc.ExitCode != 0) throw new Exception("SumatraPDF exit code " + proc.ExitCode);
    }

    public static void PrintTestPage(string printerName)
    {
        using var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printerName;
        doc.PrintPage += (_, e) =>
        {
            using var big = new Font("Segoe UI", 22, FontStyle.Bold);
            using var small = new Font("Segoe UI", 11);
            e.Graphics!.DrawString("AutoPrint Agent — Test Page", big, Brushes.Black, 60, 80);
            e.Graphics.DrawString($"Printer: {printerName}\n{DateTime.Now:dd MMM yyyy HH:mm}\n\nAgar ye page nikla to setup sahi hai.",
                small, Brushes.Black, 60, 150);
        };
        doc.Print();
    }
}
