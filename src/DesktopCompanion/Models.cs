using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace DesktopCompanion;

public class TaskItem : INotifyPropertyChanged
{
    bool _done;
    public string Title { get; set; } = "";
    public string When { get; set; } = "";
    public string Icon { get; set; } = "\uE8A5";
    public Brush Accent { get; set; } = Brushes.Red;
    public bool IsDone
    {
        get => _done;
        set { _done = value; PropertyChanged?.Invoke(this, new(nameof(IsDone))); Changed?.Invoke(); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public static event Action? Changed;
}

public record NoteItem(string Title, string Preview, string When, Brush Accent);

public static class SampleData
{
    static Brush B(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; b.Freeze(); return b; }
    static readonly Brush Red = B("#FF525B"), Green = B("#80CB89"), Blue = B("#3C76FF"), Purple = B("#A032FF"), Yellow = B("#FFD02D");

    static TaskItem T(string title, string when, string icon, Brush c) => new() { Title = title, When = when, Icon = icon, Accent = c };

    public static readonly string[] Tabs = { "Today", "Upcoming", "Backlog", "Later" };

    public static readonly Dictionary<string, ObservableCollection<TaskItem>> Tasks = new()
    {
        ["Today"] = new()
        {
            T("Finish KPIs PR", "10:00 AM", "\uE8A5", Red),
            T("Reply to email", "11:30 AM", "\uE715", Yellow),
            T("Workout", "6:00 PM", "\uE006", Green),
            T("Read DSA (Arrays)", "", "\uE736", Blue),
            T("Book bike service", "", "\uE707", Purple),
            T("Plan weekend ride", "", "\uE707", Purple),
        },
        ["Upcoming"] = new()
        {
            T("Call dentist", "Oct 5", "\uE717", Yellow),
            T("Plan weekend ride", "Oct 11", "\uE707", Blue),
            T("Buy new laptop", "Oct 15", "\uE719", Blue),
        },
        ["Backlog"] = new()
        {
            T("Update resume", "Sep 28", "\uE8A5", Purple),
        },
        ["Later"] = new()
        {
            T("Learn Rust basics", "", "\uE736", Green),
            T("Organize photo library", "", "\uE722", Purple),
        },
    };

    public static readonly ObservableCollection<NoteItem> Notes = new()
    {
        new("API rate limits discussion", "Discuss API rate limits and caching strategi...", "Today", Purple),
        new("Ideas for side project", "Build a lightweight desktop app...", "Sep 30", Green),
        new("Meeting with Priya", "Discussed the new dashboard requirements...", "Sep 28", Yellow),
    };
}
