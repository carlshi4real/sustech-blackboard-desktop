using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BlackboardDesktop;

internal sealed class WidgetForm : Form
{
    private static readonly Color Background = Color.FromArgb(24, 36, 41), Tile = Color.FromArgb(35, 49, 54);
    private static readonly Color Muted = Color.FromArgb(158, 177, 181), Mint = Color.FromArgb(130, 219, 187);
    private static readonly Color Coral = Color.FromArgb(255, 137, 118), Amber = Color.FromArgb(255, 206, 125);
    internal Store Store { get; }
    internal NotifyIcon Tray { get; }
    internal SchoolBrowser Browser { get; }
    private readonly Label status, updated;
    private readonly Label[] metrics = new Label[3];
    private readonly FlowLayoutPanel list;
    private readonly Button refresh;
    private readonly ToolTip tips = new();
    private readonly System.Windows.Forms.Timer syncTimer = new() { Interval = 900_000 };
    private readonly System.Windows.Forms.Timer clockTimer = new() { Interval = 60_000 };
    private readonly ContextMenuStrip menu = new();
    private bool quitting, showHandled;
    private readonly string? testReport;

    public WidgetForm(Store store, string? testReport = null)
    {
        Store = store; this.testReport = testReport;
        Text = "Blackboard 作业提醒"; Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "app.ico"));
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
        BackColor = Background; ForeColor = Color.White;
        ClientSize = new Size(350, 520); AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.Manual;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 5 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        Controls.Add(root);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
        var heading = new Label { Text = "作业提醒\nBLACKBOARD · 南方科技大学", Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold) };
        heading.MouseDown += DragWindow;
        header.Controls.Add(heading);
        refresh = Button("↻", "刷新作业", async () => await Browser!.SyncAsync());
        header.Controls.Add(refresh);
        var more = Button("···", "更多选项", () => menu.Show(Cursor.Position)); header.Controls.Add(more);
        header.Controls.Add(Button("×", "隐藏卡片，保留右下角托盘图标", Hide));
        root.Controls.Add(header, 0, 0);
        var summary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0, 0, 0, 10) };
        string[] labels = ["逾期", "今天待交", "即将截止"];
        for (int i = 0; i < 3; i++)
        {
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            metrics[i] = new Label { Text = "0 " + labels[i], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BackColor = Tile, ForeColor = i == 0 ? Coral : i == 1 ? Amber : Mint, Font = new Font(Font.FontFamily, 10, FontStyle.Bold), Margin = new Padding(2) };
            summary.Controls.Add(metrics[i]);
        }
        root.Controls.Add(summary, 0, 1);
        list = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
        list.SizeChanged += (_, _) => ResizeRows();
        root.Controls.Add(list, 0, 2);
        status = new Label { Dock = DockStyle.Fill, ForeColor = Muted, Font = new Font(Font.FontFamily, 8), TextAlign = ContentAlignment.MiddleLeft };
        root.Controls.Add(status, 0, 3);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        updated = new Label { Dock = DockStyle.Fill, ForeColor = Muted, Font = new Font(Font.FontFamily, 8), TextAlign = ContentAlignment.MiddleLeft };
        footer.Controls.Add(updated);
        footer.Controls.Add(Button("连接账号", "在学校官方页面登录自己的账号", async () => await Browser!.ConnectAsync()));
        root.Controls.Add(footer, 0, 4);
        Browser = new SchoolBrowser(store, SetStatus, testReport is not null);
        menu.Items.Add("显示作业提醒", null, (_, _) => Restore());
        menu.Items.Add("刷新作业", null, async (_, _) => await Browser.SyncAsync());
        menu.Items.Add("连接 / 登录 Blackboard", null, async (_, _) => await Browser.ConnectAsync());
        var handledItem = new ToolStripMenuItem("显示已处理") { CheckOnClick = true };
        handledItem.CheckedChanged += (_, _) => { showHandled = handledItem.Checked; RenderRows(); }; menu.Items.Add(handledItem);
        var pin = new ToolStripMenuItem("始终置顶") { CheckOnClick = true };
        pin.CheckedChanged += (_, _) => TopMost = pin.Checked; menu.Items.Add(pin);
        menu.Items.Add("移到屏幕右侧", null, (_, _) => { PositionRight(); Restore(); });
        menu.Items.Add("打开 Blackboard", null, (_, _) => Program.Open(School.Home));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出作业提醒", null, (_, _) => Quit());
        Tray = new NotifyIcon { Icon = Icon, Text = "Blackboard 作业提醒", ContextMenuStrip = menu, Visible = true };
        Tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) Restore(); };
        syncTimer.Tick += async (_, _) => await Browser.SyncAsync();
        clockTimer.Tick += (_, _) => RenderRows();
        SetStatus(store.Updated is null ? "请连接账号 · 在学校官方页面登录" : "显示上次同步的记录 · 正在连接", false);
        Shown += async (_, _) =>
        {
            PositionRight();
            if (testReport is not null) { await SmokeTests.RunAsync(this, testReport); return; }
            syncTimer.Start(); clockTimer.Start(); SystemEvents.PowerModeChanged += Wake;
            await Browser.ConnectAsync();
        };
    }
    private Button Button(string label, string hint, Action action)
    {
        var b = new Button { Text = label, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, ForeColor = Mint, BackColor = Background, Cursor = Cursors.Hand, Margin = Padding.Empty, AccessibleName = hint };
        b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => action(); tips.SetToolTip(b, hint); return b;
    }
    private void SetStatus(string text, bool busy)
    {
        if (IsDisposed) return;
        status.Text = Store.CacheError ? text + " · 本地缓存保存失败" : text;
        refresh.Enabled = !busy; RenderRows();
    }
    internal void RenderRows()
    {
        int scroll = -list.AutoScrollPosition.Y;
        list.SuspendLayout();
        while (list.Controls.Count > 0) { var c = list.Controls[0]; list.Controls.Remove(c); c.Dispose(); }
        var now = School.ChinaNow;
        var rows = Store.Items.Where(a => showHandled || !Store.Handled.Contains(a.Id)).ToList();
        var pending = rows.Where(a => !Store.Handled.Contains(a.Id) && a.Submitted != true).ToList();
        metrics[0].Text = $"{pending.Count(a => a.DisplayGroup(now) == "过期")} 逾期";
        metrics[1].Text = $"{pending.Count(a => a.DisplayGroup(now) == "今天截止")} 今天待交";
        metrics[2].Text = $"{pending.Count(a => a.DisplayGroup(now) is "本周截止" or "以后截止")} 即将截止";
        foreach (string group in School.Groups)
        {
            var items = rows.Where(a => a.DisplayGroup(now) == group).OrderBy(a => a.DueAt ?? DateTimeOffset.MaxValue).ToList();
            if (items.Count == 0) continue;
            Color color = group == "过期" ? Coral : group == "今天截止" ? Amber : Mint;
            list.Controls.Add(new Label { Text = group == "过期" ? "● 逾期 · 请核对提交状态" : "● " + group, Height = 25, ForeColor = color, Padding = new Padding(0, 5, 0, 0), Margin = new Padding(0, 7, 0, 2), Font = new Font(Font.FontFamily, 8) });
            foreach (var a in items)
            {
                var row = new Panel { Height = 105, BackColor = Tile, Margin = new Padding(0, 0, 0, 6), Padding = new Padding(9) };
                var mark = new CheckBox { Width = 22, Dock = DockStyle.Left, Checked = Store.Handled.Contains(a.Id), AccessibleName = "标记已处理：" + a.Title, Cursor = Cursors.Hand };
                tips.SetToolTip(mark, "只在本机标记已处理，不会提交作业");
                mark.Click += (_, _) => { Store.Toggle(a.Id); BeginInvoke(new Action(RenderRows)); };
                var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
                content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 19)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
                var title = new LinkLabel { Text = a.Title, Dock = DockStyle.Fill, LinkColor = Color.White, ActiveLinkColor = Mint, VisitedLinkColor = Color.White, Font = new Font(Font.FontFamily, 9, FontStyle.Bold), Margin = Padding.Empty, AutoEllipsis = true };
                title.LinkClicked += (_, _) => Program.Open(a.Url); tips.SetToolTip(title, a.Title + "\n点击打开原作业");
                content.Controls.Add(title);
                var course = new Label { Text = a.Course + (a.Submitted == true ? " · 已提交" : a.Note is not null ? " · 请核对成绩" : ""), Dock = DockStyle.Fill, ForeColor = a.Submitted == true ? Mint : Muted, AutoEllipsis = true, Font = new Font(Font.FontFamily, 8), Margin = Padding.Empty };
                tips.SetToolTip(course, course.Text); content.Controls.Add(course);
                content.Controls.Add(new Label { Text = a.Deadline, Dock = DockStyle.Fill, ForeColor = color, Font = new Font(Font.FontFamily, 8), Margin = Padding.Empty });
                row.Controls.Add(content); row.Controls.Add(mark); list.Controls.Add(row);
            }
        }
        if (rows.Count == 0) list.Controls.Add(new Label { Text = Store.Updated is null ? "连接你的 Blackboard\n\n登录后，在这里查看待交与逾期作业。\n账号密码只在学校登录页输入。" : "当前列表没有待办作业", Height = 140, Padding = new Padding(8, 24, 0, 0), ForeColor = Muted });
        updated.Text = Store.Updated is { } at ? "更新于 " + at.ToLocalTime().ToString("M/d HH:mm") : "尚未同步";
        ResizeRows(); list.ResumeLayout(); list.AutoScrollPosition = new Point(0, scroll);
    }
    private void ResizeRows() { foreach (Control c in list.Controls) c.Width = Math.Max(200, list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2); }
    private void PositionRight()
    {
        var area = Screen.FromControl(this).WorkingArea;
        Location = new Point(Math.Max(area.Left, area.Right - Width - 20), area.Top + 24);
    }
    internal void Restore() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    internal void Quit() { quitting = true; Close(); }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        base.OnFormClosing(e);
    }
    private void Wake(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume && !IsDisposed) BeginInvoke(new Action(async () => await Browser.SyncAsync()));
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.PowerModeChanged -= Wake;
            syncTimer.Dispose(); clockTimer.Dispose(); Browser.Dispose(); Tray.Visible = false;
            Tray.Dispose(); menu.Dispose(); tips.Dispose();
        }
        base.Dispose(disposing);
    }
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam);
    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture(); SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
    }
}
