using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BlackboardDesktop;

internal sealed class SchoolBrowser : IDisposable
{
    public Form LoginWindow { get; }
    public WebView2 LoginView { get; } = new() { Dock = DockStyle.Fill };
    public WebView2 DetailView { get; } = new() { Dock = DockStyle.Fill };
    private readonly Form detailHost = new() { Size = new Size(800, 600), ShowInTaskbar = false };
    private readonly Store store;
    private readonly Action<string, bool> changed;
    private bool disposed, busy, initialized;
    private Task? initialization;
    private readonly bool testing;
    private CancellationTokenSource? operation;
    public bool Busy => busy;
    public SchoolBrowser(Store store, Action<string, bool> changed, bool testing = false)
    {
        this.store = store; this.changed = changed; this.testing = testing;
        LoginWindow = new Form { Text = "连接 Blackboard — 请使用自己的账号登录", Size = new Size(1040, 760), StartPosition = FormStartPosition.CenterScreen };
        LoginWindow.Controls.Add(LoginView);
        detailHost.Controls.Add(DetailView);
        LoginWindow.FormClosing += (_, e) => { if (!disposed) { e.Cancel = true; LoginWindow.Hide(); } };
    }
    public Task InitializeAsync() => initialization ??= InitializeCoreAsync();
    private async Task InitializeCoreAsync()
    {
        var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(store.Folder, "WebView2"));
        _ = LoginWindow.Handle; _ = LoginView.Handle;
        _ = detailHost.Handle; _ = DetailView.Handle;
        await LoginView.EnsureCoreWebView2Async(environment);
        await DetailView.EnsureCoreWebView2Async(environment);
        Configure(LoginView.CoreWebView2); Configure(DetailView.CoreWebView2);
        LoginView.CoreWebView2.NavigationCompleted += async (_, e) =>
        {
            if (testing || disposed || busy || !e.IsSuccess) return;
            var url = LoginView.Source?.ToString() ?? "";
            if (School.Allowed(url) && !School.Login(url)) await SyncAsync(false);
        };
        initialized = true;
    }
    private void Configure(CoreWebView2 core)
    {
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.IsWebMessageEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.AreDevToolsEnabled = false;
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.NavigationStarting += (_, e) =>
        {
            if (!testing && !School.Allowed(e.Uri))
            { e.Cancel = true; changed("已阻止非学校站点跳转，请在浏览器中核对", busy); }
        };
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (School.Allowed(e.Uri)) core.Navigate(e.Uri);
        };
        core.ProcessFailed += (_, _) => changed("页面进程中断，请退出并重新打开应用 · 保留上次数据", false);
    }
    public async Task ConnectAsync()
    {
        LoginWindow.Show(); LoginWindow.Activate();
        try
        {
            await InitializeAsync();
            if (!busy && !School.Login(LoginView.Source?.ToString() ?? "")) await SyncAsync();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            changed("需要安装 Microsoft Edge WebView2 运行时", false);
            initialization = null;
            if (MessageBox.Show("此电脑缺少 Microsoft Edge WebView2。打开微软官方下载页？安装后请重新启动本应用。", "安装登录组件", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Program.Open("https://developer.microsoft.com/microsoft-edge/webview2/");
        }
        catch { initialization = null; changed("登录组件无法启动，请重新打开应用", false); }
    }
    public async Task SyncAsync(bool navigate = true)
    {
        if (disposed || !initialized || busy) return;
        if (School.Login(LoginView.Source?.ToString() ?? ""))
        { changed("需要登录 · 点击“连接账号”", false); return; }
        busy = true; operation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = operation.Token;
        changed("正在同步 Blackboard…", true);
        try
        {
            string url = LoginView.Source?.ToString() ?? "";
            if (navigate || !url.Contains("/execute/mybb")) LoginView.CoreWebView2.Navigate(School.Home);
            bool success = false;
            for (int i = 0; i < 90; i++)
            {
                await Task.Delay(1000, ct);
                url = LoginView.Source?.ToString() ?? "";
                if (School.Login(url)) { changed("需要登录 · 点击“连接账号”", false); return; }
                if (!School.Allowed(url) || !url.Contains("/execute/mybb")) continue;
                var result = await ReadAsync<Extraction>(LoginView, School.Script("extract"));
                if (result is not null && store.Apply(result)) { success = true; break; }
            }
            if (!success) { changed("日程暂无法读取 · 保留上次数据", false); return; }
            LoginWindow.Hide(); changed("日程已同步 · 正在核对截止时刻", true);
            int missing = 0;
            foreach (var row in store.Items.ToList())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await NavigateAsync(DetailView, row.Url, ct);
                    if (School.Login(DetailView.Source?.ToString() ?? "")) { changed("登录已失效 · 请重新连接账号", false); return; }
                    Detail? result = null;
                    for (int attempt = 0; attempt < 4; attempt++)
                    {
                        await Task.Delay(500, ct);
                        result = await ReadAsync<Detail>(DetailView, School.Script("detail"));
                        if (result?.DueAt is not null) break;
                    }
                    if (result is not null) store.UpdateDetail(row.Id, result);
                    if (result?.DueAt is null) missing++;
                }
                catch (TimeoutException) { missing++; }
                catch (InvalidOperationException) { missing++; }
                changed("正在核对截止时刻…", true);
            }
            changed(missing == 0 ? "已同步 · 每 15 分钟自动刷新" : $"已同步 · {missing} 项时刻未核实，请打开原作业", false);
        }
        catch (OperationCanceledException) { if (!disposed) changed("同步超时 · 已保留读取到的记录", false); }
        catch { if (!disposed) changed("连接失败 · 保留上次数据，稍后重试", false); }
        finally { busy = false; operation?.Dispose(); operation = null; }
    }
    internal static async Task<T?> ReadAsync<T>(WebView2 view, string script)
    {
        var raw = await view.ExecuteScriptAsync(script);
        // Scripts return a JSON string; WebView2 JSON-encodes its JavaScript result again.
        var json = JsonSerializer.Deserialize<string>(raw);
        return json is null ? default : JsonSerializer.Deserialize<T>(json, Store.Json);
    }
    internal static async Task NavigateAsync(WebView2 view, string url, CancellationToken ct, bool html = false)
    {
        var done = new TaskCompletionSource<bool>();
        void Completed(object? s, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess) done.TrySetResult(true);
            else done.TrySetException(new InvalidOperationException($"页面加载失败：{e.WebErrorStatus}，URL：{view.Source}"));
        }
        view.CoreWebView2.NavigationCompleted += Completed;
        try
        {
            if (html) view.CoreWebView2.NavigateToString(url); else view.CoreWebView2.Navigate(url);
            await done.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
        }
        finally { view.CoreWebView2.NavigationCompleted -= Completed; }
    }
    public void Dispose()
    {
        disposed = true; operation?.Cancel();
        LoginWindow.Dispose(); detailHost.Dispose();
    }
}
