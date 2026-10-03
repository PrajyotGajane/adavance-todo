using System.ComponentModel;
using System.Globalization;
using System.Windows.Media;

namespace DesktopCompanion;

public enum Bucket { Today, Upcoming, Backlog, Later }

public static class Look
{
    static Brush B(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; b.Freeze(); return b; }
    static readonly Brush[] Palette = { B("#FF525B"), B("#80CB89"), B("#3C76FF"), B("#A032FF"), B("#FFD02D") };
    static readonly string[] Icons = { "\uE8A5", "\uE715", "\uE006", "\uE736", "\uE707", "\uE717", "\uE719", "\uE722" };

    static int Hash(string s) { int h = 7; foreach (var c in s) h = unchecked(h * 31 + c); return Math.Abs(h % 100000); }
    public static Brush Accent(string key) => Palette[Hash(key) % Palette.Length];
    public static string Icon(string key) => Icons[Hash(key) % Icons.Length];
}

public class TaskItem : INotifyPropertyChanged
{
    bool _done;
    public long Id { get; init; }
    public string Title { get; set; } = "";
    public DateTime? DueDate { get; set; }
    public TimeSpan? DueTime { get; set; }
    public string? Tag { get; set; }
    public DateTime Created { get; init; } = DateTime.Now;

    string Key => string.IsNullOrEmpty(Tag) ? Id.ToString() : Tag!;
    public Brush Accent => Look.Accent(Key);
    public string Icon => Look.Icon(Key);

    public Bucket Bucket
    {
        get
        {
            if (DueDate is not { } d) return Bucket.Later;
            var today = DateTime.Today;
            return d.Date == today ? Bucket.Today : d.Date > today ? Bucket.Upcoming : Bucket.Backlog;
        }
    }

    public string When
    {
        get
        {
            if (DueDate is not { } d) return "";
            if (d.Date == DateTime.Today)
                return DueTime is { } t ? (DateTime.Today + t).ToString("h:mm tt", CultureInfo.InvariantCulture) : "";
            return d.ToString("MMM d", CultureInfo.InvariantCulture);
        }
    }

    public bool IsDone
    {
        get => _done;
        set
        {
            if (_done == value) return;
            _done = value;
            PropertyChanged?.Invoke(this, new(nameof(IsDone)));
            Store.SetDone(this, value);
        }
    }

    public void LoadDone(bool v) => _done = v;
    public event PropertyChangedEventHandler? PropertyChanged;
}

public class NoteItem
{
    public long Id { get; init; }
    public string Body { get; set; } = "";
    public DateTime Created { get; init; }
    public DateTime Updated { get; set; }

    public string Title
    {
        get { var l = Body.Split('\n', 2)[0].Trim(); return l.Length == 0 ? "Untitled note" : l; }
    }

    public string Preview
    {
        get
        {
            var parts = Body.Split('\n', 2);
            return parts.Length > 1 ? parts[1].Replace("\r", "").Replace("\n", " ").Trim() : "";
        }
    }

    public string When => Updated.Date == DateTime.Today ? "Today" : Updated.ToString("MMM d", CultureInfo.InvariantCulture);
    public Brush Accent => Look.Accent(Id.ToString());
}
