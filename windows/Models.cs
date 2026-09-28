using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlackboardDesktop;

internal static class School
{
    public const string Origin = "https://bb.sustech.edu.cn";
    public const string Home = Origin + "/webapps/bb-social-learning-BBLEARN/execute/mybb?cmd=display&toolId=BB-CORE_____overview-tool";
    public static readonly string[] Groups = ["过期", "今天截止", "本周截止", "以后截止"];
    public static bool Allowed(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u)
        && u.Scheme == "https" && u.IsDefaultPort && u.UserInfo.Length == 0
        && (u.Host == "bb.sustech.edu.cn" || u.Host == "cas.sustech.edu.cn");
    public static bool Login(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u)
        && (u.Host == "cas.sustech.edu.cn" || u.AbsolutePath.Contains("login", StringComparison.OrdinalIgnoreCase));
    public static bool ValidId(string id) => Regex.IsMatch(id, @"^_blackboard\.platform\.gradebook2\.GradableItem-_[0-9]+_1$");
    public static DateTimeOffset ChinaNow => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8));
    public static string Script(string name)
    {
        using var stream = typeof(School).Assembly.GetManifestResourceStream($"BlackboardDesktop.scripts.{name}.js")
            ?? throw new InvalidOperationException("缺少页面读取组件");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

internal sealed record Assignment
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Course { get; init; } = "";
    public string Due { get; init; } = "";
    public string Group { get; init; } = "";
    public string? Note { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public bool? Submitted { get; init; }
    public string Url => School.Origin + "/webapps/calendar/launch/attempt/" + Id;
    public string DisplayGroup(DateTimeOffset now)
    {
        if (DueAt is not { } at) return Group;
        if (at < now) return "过期";
        var today = now.ToOffset(TimeSpan.FromHours(8)).Date;
        var day = at.ToOffset(TimeSpan.FromHours(8)).Date;
        return day == today ? "今天截止" : day <= today.AddDays(7) ? "本周截止" : "以后截止";
    }
    public string Deadline => DueAt is { } at
        ? at.ToOffset(TimeSpan.FromHours(8)).ToString("M月d日 HH:mm") + " 截止 · 北京时间"
        : Due + " · 时刻待核实";
}

internal sealed record Extraction(bool Ready, List<Assignment> Items);
internal sealed record Detail(DateTimeOffset? DueAt, bool? Submitted);
internal sealed record Snapshot(List<Assignment> Items, DateTimeOffset? Updated, HashSet<string> Handled);

internal sealed class Store
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public List<Assignment> Items { get; private set; } = [];
    public HashSet<string> Handled { get; private set; } = [];
    public DateTimeOffset? Updated { get; private set; }
    public string Folder { get; }
    public bool CacheError { get; private set; }
    public Store(string folder)
    {
        Folder = folder;
        try
        {
            var file = Path.Combine(folder, "snapshot.json");
            if (!File.Exists(file)) return;
            var saved = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(file), Json);
            if (saved is null || saved.Items is null || saved.Handled is null || !Valid(saved.Items)) return;
            Items = saved.Items; Updated = saved.Updated; Handled = saved.Handled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { CacheError = true; }
    }
    public static bool Valid(List<Assignment> rows) => rows.Count <= 1000 && rows.All(a => a is not null
        && a.Id is not null && School.ValidId(a.Id) && !string.IsNullOrWhiteSpace(a.Title)
        && !string.IsNullOrWhiteSpace(a.Course) && School.Groups.Contains(a.Group))
        && rows.Select(a => a.Id).Distinct().Count() == rows.Count;
    public bool Apply(Extraction result)
    {
        if (!result.Ready || result.Items is null || !Valid(result.Items)) return false;
        // Submission status and deadlines must be re-read: never silently reuse stale details.
        Items = result.Items; Updated = DateTimeOffset.UtcNow;
        Handled.IntersectWith(Items.Select(a => a.Id));
        Save(); return true;
    }
    public void UpdateDetail(string id, Detail detail)
    {
        int index = Items.FindIndex(a => a.Id == id);
        if (index < 0) return;
        Items[index] = Items[index] with { DueAt = detail.DueAt, Submitted = detail.Submitted };
        Save();
    }
    public void Toggle(string id)
    {
        if (!Items.Any(a => a.Id == id)) return;
        if (!Handled.Remove(id)) Handled.Add(id);
        Save();
    }
    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var file = Path.Combine(Folder, "snapshot.json");
            File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(new Snapshot(Items, Updated, Handled), Json));
            File.Move(file + ".tmp", file, true); CacheError = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { CacheError = true; }
    }
}
