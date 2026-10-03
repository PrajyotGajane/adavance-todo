using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DesktopCompanion;

public partial class QuickAddView : UserControl
{
    TaskItem? _editing;
    bool _forceChips;

    // Raised after the task is saved or the edit is cancelled.
    public event Action? Finished;

    public QuickAddView() { InitializeComponent(); }

    public void Begin(TaskItem? edit = null)
    {
        _editing = edit;
        _forceChips = edit != null;
        Hint.Text = edit == null ? "Add a task, e.g. Call mom tomorrow 10am" : "Edit task";
        Input.Text = edit == null ? "" : edit.Title + (string.IsNullOrEmpty(edit.Tag) ? "" : " #" + edit.Tag);
        UpdateChips();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            Keyboard.Focus(Input);
            Input.CaretIndex = Input.Text.Length;
        });
    }

    public void Cancel() { _editing = null; }

    void Input_TextChanged(object s, TextChangedEventArgs e)
    {
        Hint.Visibility = Input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateChips();
    }

    void UpdateChips()
    {
        if (Chips == null) return;
        var p = QuickParser.Parse(Input.Text);
        bool show = _forceChips || (Input.Text.Trim().Length > 0 && p.HasAny);
        Chips.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        TabHint.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        if (!show) return;
        DateChip.Text = _editing != null && !p.HasDate && Input.Text.Trim().Length > 0 ? DateText(_editing) : p.DateChip;
        TimeChip.Text = _editing != null && !p.HasTime ? (_editing.DueTime is { } t ? (DateTime.Today + t).ToString("h:mm tt") : "No time") : p.TimeChip;
        TagChip.Text = p.Tag != null ? "#" + p.Tag : _editing?.Tag is { Length: > 0 } g ? "#" + g : "No tag";
    }

    static string DateText(TaskItem t) => new Parsed("", t.DueDate, null, null, true, false).DateChip;

    void Submit_Click(object s, RoutedEventArgs e) => Submit();

    void Submit()
    {
        var text = Input.Text.Trim();
        if (text.Length > 0)
        {
            var p = QuickParser.Parse(text);
            if (_editing is { } t)
                Store.UpdateTask(t, p.Title, p.HasDate ? p.Date : t.DueDate, p.HasTime ? p.Time : t.DueTime, p.Tag ?? t.Tag);
            else
                Store.AddTask(p.Title, p.Date, p.Time, p.Tag);
        }
        _editing = null;
        Finished?.Invoke();
    }

    void View_PreviewKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Submit(); e.Handled = true; }
        else if (e.Key == Key.Escape) { _editing = null; Finished?.Invoke(); e.Handled = true; }
        else if (e.Key == Key.Tab)
        {
            _forceChips = !_forceChips;
            UpdateChips();
            e.Handled = true;
        }
    }
}
