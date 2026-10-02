namespace AutoPrintAgent;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "AutoPrintAgent_SingleInstance", out bool created);
        if (!created)
        {
            MessageBox.Show("AutoPrint Agent pehle se chal raha hai (taskbar ke tray me dekho).",
                "AutoPrint Agent");
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
    }
}
