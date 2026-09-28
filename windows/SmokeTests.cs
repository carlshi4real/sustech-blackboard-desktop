using System.Drawing.Imaging;
using System.Text.Json;

namespace BlackboardDesktop;

internal static class SmokeTests
{
    internal static async Task RunAsync(WidgetForm form, string report)
    {
        var checks = new List<string>();
        Directory.CreateDirectory(report);
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            checks.Add("PASS: " + name);
        }
        try
        {
            Check(School.Allowed(School.Home), "School HTTPS allowed");
            Check(!School.Allowed("https://bb.sustech.edu.cn.evil.example/login"), "Lookalike domain blocked");
            Check(!School.Allowed("http://bb.sustech.edu.cn"), "Plain HTTP blocked");
            Check(!School.Allowed("file:///C:/Windows/win.ini"), "Local file navigation blocked");
            Check(!School.Allowed("https://user@bb.sustech.edu.cn:8443/"), "Credential URLs and unusual ports blocked");
            Check(form.Store.Items.Count == 0, "Clean install contains no account or assignment data");
            await form.Browser.InitializeAsync();
            Check(!form.Browser.LoginView.CoreWebView2.Settings.IsPasswordAutosaveEnabled, "Password autosave disabled");
            Check(!form.Browser.LoginView.CoreWebView2.Settings.AreHostObjectsAllowed, "No native bridge in school page");
            var web = form.Browser.LoginView;
            async Task<Extraction?> Read(string html)
            {
                await SchoolBrowser.NavigateAsync(web, "<html><body>" + html + "</body></html>", CancellationToken.None, true);
                return await SchoolBrowser.ReadAsync<Extraction>(web, School.Script("extract"));
            }
            const string first = "_blackboard.platform.gradebook2.GradableItem-_900001_1";
            const string second = "_blackboard.platform.gradebook2.GradableItem-_900002_1";
            var valid = await Read($"<h2>日程表</h2><h3>今天截止</h3><a href='/webapps/calendar/launch/attempt/{first}'>示例作业</a><p>今天截止</p><p>示例课程 A</p><a href='/webapps/calendar/launch/attempt/{second}'>示例作业</a><p>今天截止</p><p>示例课程 B</p><footer>查看更多日程表</footer>");
            Check(valid is { Ready: true } && valid.Items.Count == 2 && valid.Items[1].Course == "示例课程 B", "Duplicate titles in different courses remain distinct");
            Check(form.Store.Apply(valid!), "Complete schedule accepted");
            Check(new Store(form.Store.Folder).Items.Count == 2, "Snapshot roundtrip");
            var incomplete = await Read("<h2>日程表</h2><p>正在加载</p><footer>查看更多日程表</footer>");
            Check(!form.Store.Apply(incomplete!) && form.Store.Items.Count == 2, "Partial page cannot erase cache");
            var malicious = await Read($"<h2>日程表</h2><h3>今天截止</h3><a href='https://example.com/calendar/launch/attempt/{first}'>示例</a><p>今天截止</p><p>课程</p><footer>查看更多日程表</footer>");
            Check(malicious is { Ready: false }, "External assignment links rejected");
            var empty = await Read("<h2>日程表</h2><p>没有作业事件</p><footer>查看更多日程表</footer>");
            Check(empty is { Ready: true } && empty.Items.Count == 0, "Explicit empty schedule recognized");
            foreach (var pair in new[] { ("上午12:00", "00:00"), ("下午12:00", "12:00"), ("下午11:59", "23:59"), ("上午11:59", "11:59"), ("23:05", "23:05") })
            {
                await SchoolBrowser.NavigateAsync(web, $"<p>到期日期</p><p>2030年9月28日 {pair.Item1}</p>", CancellationToken.None, true);
                var detail = await SchoolBrowser.ReadAsync<Detail>(web, School.Script("detail"));
                Check(detail?.DueAt?.ToOffset(TimeSpan.FromHours(8)).ToString("HH:mm") == pair.Item2, "Deadline parses " + pair.Item1);
                Check(detail?.Submitted is null, "No inferred submission on upload page");
            }
            await SchoolBrowser.NavigateAsync(web, "<p>复查提交历史记录</p><p>尝试 1</p>", CancellationToken.None, true);
            var submitted = await SchoolBrowser.ReadAsync<Detail>(web, School.Script("detail"));
            Check(submitted?.Submitted == true && submitted.DueAt is null, "Explicit submission recognized without invented deadline");
            var now = School.ChinaNow;
            var sample = valid!.Items[0] with { DueAt = now.AddMinutes(-1) };
            Check(sample.DisplayGroup(now) == "过期", "Deadline moves to overdue after its exact time");
            sample = sample with { DueAt = new DateTimeOffset(now.Date.AddDays(1).AddHours(1), TimeSpan.FromHours(8)) };
            Check(sample.DisplayGroup(now) == "本周截止", "Midnight uses school timezone");
            form.Store.Toggle(first);
            Check(new Store(form.Store.Folder).Handled.Contains(first), "Handled status persists locally");
            form.Store.Toggle(first);
            form.Store.UpdateDetail(first, new Detail(new DateTimeOffset(now.Date.AddHours(23).AddMinutes(59), TimeSpan.FromHours(8)), null));
            form.Store.UpdateDetail(second, new Detail(now.AddDays(-1), true));
            form.RenderRows();
            form.Close();
            Check(!form.Visible && form.Tray.Visible && !form.IsDisposed, "Closing hides card and retains tray");
            form.Restore();
            Check(form.Visible, "Tray restore shows card");
            using (var image = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(image, form.ClientRectangle);
                image.Save(Path.Combine(report, "windows-widget.png"), ImageFormat.Png);
            }
            File.WriteAllText(Path.Combine(report, "checks.txt"), string.Join(Environment.NewLine, checks));
            File.WriteAllText(Path.Combine(report, "result.json"), JsonSerializer.Serialize(new { passed = true, count = checks.Count }));
            Environment.ExitCode = 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(report, "checks.txt"), string.Join(Environment.NewLine, checks) + "\nFAIL: " + ex);
            File.WriteAllText(Path.Combine(report, "result.json"), JsonSerializer.Serialize(new { passed = false, error = ex.ToString() }));
            Environment.ExitCode = 1;
        }
        finally { form.Quit(); }
    }
}
