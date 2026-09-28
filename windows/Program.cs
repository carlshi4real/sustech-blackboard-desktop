using System.Diagnostics;

namespace BlackboardDesktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        bool test = args.Length == 2 && args[0] == "--self-test";
        using var mutex = new Mutex(true, test ? "BlackboardDesktop.Test." + Guid.NewGuid() : "Local\\SUSTechBlackboardDesktop", out bool first);
        if (!first)
        {
            MessageBox.Show("作业提醒已经在运行，请点击任务栏右下角的作业提醒图标。", "Blackboard 作业提醒");
            return;
        }
        var folder = test ? Path.Combine(Path.GetTempPath(), "BlackboardTest-" + Guid.NewGuid())
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SUSTechBlackboardDesktop");
        Application.Run(new WidgetForm(new Store(folder), test ? Path.GetFullPath(args[1]) : null));
    }
    public static void Open(string url)
    {
        if (!School.Allowed(url) && url != "https://developer.microsoft.com/microsoft-edge/webview2/") return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { MessageBox.Show("无法打开默认浏览器，请手动打开 Blackboard。", "作业提醒"); }
    }
}
