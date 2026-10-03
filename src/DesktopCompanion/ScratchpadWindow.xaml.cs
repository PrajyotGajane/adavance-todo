using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopCompanion;

public partial class ScratchpadWindow : Window
{
    static readonly Brush Purple = (Brush)new BrushConverter().ConvertFromString("#A032FF")!;

    public ScratchpadWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        IsVisibleChanged += (_, _) => { if (!IsVisible) App.TrimMemory(); };
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - 324) / 2;
        Top = area.Top + area.Height * 0.25;
    }

    public void ShowPad()
    {
        Show();
        Activate();
        Editor.Focus();
        Editor.CaretIndex = Editor.Text.Length;
    }

    void Editor_TextChanged(object s, TextChangedEventArgs e) =>
        Hint.Visibility = Editor.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    void Bar_MouseDown(object s, MouseButtonEventArgs e) { if (e.OriginalSource is not Button) DragMove(); }
    void Pin_Click(object s, RoutedEventArgs e) => Topmost = PinToggle.IsChecked == true;
    void Hide_Click(object s, RoutedEventArgs e) => Hide();
    void Save_Click(object s, RoutedEventArgs e) => Save();
    void OpenNotes_Click(object s, RoutedEventArgs e) { App.Notes.Show(); App.Notes.Activate(); }

    // Phase 1: a "saved" note only lives in memory; the next note starts empty.
    void Save()
    {
        var text = Editor.Text.Trim();
        if (text.Length == 0) return;
        var lines = text.Split('\n', 2);
        var title = lines[0].Trim();
        var preview = lines.Length > 1 ? lines[1].Trim().Replace("\r", "").Replace("\n", " ") : title;
        SampleData.Notes.Insert(0, new NoteItem(title, preview, "Today", Purple));
        Editor.Clear();
        Status.Text = "Saved to Notes (in memory only)";
    }

    void Window_PreviewKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { Save(); e.Handled = true; }
    }
}


