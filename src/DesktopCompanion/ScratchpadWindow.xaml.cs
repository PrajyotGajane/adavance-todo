using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DesktopCompanion;

public partial class ScratchpadWindow : Window
{
    readonly DispatcherTimer _idle;

    public ScratchpadWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        IsVisibleChanged += (_, _) => { if (!IsVisible) App.TrimMemory(); };
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - 324) / 2;
        Top = area.Top + area.Height * 0.25;

        var secs = int.TryParse(Environment.GetEnvironmentVariable("DC_AUTOSAVE_SECONDS"), out var s) && s > 0 ? s : 300;
        _idle = new DispatcherTimer { Interval = TimeSpan.FromSeconds(secs) };
        _idle.Tick += (_, _) =>
        {
            _idle.Stop();
            if (Commit()) { Hide(); ToastWindow.Popup("Note saved", "Saved to Notes - 5 mins of inactivity"); }
        };
    }

    public void ShowPad()
    {
        Show();
        Activate();
        Editor.Focus();
        Editor.CaretIndex = Editor.Text.Length;
    }

    void Editor_TextChanged(object s, TextChangedEventArgs e)
    {
        bool has = Editor.Text.Trim().Length > 0;
        Hint.Visibility = Editor.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        _idle.Stop();
        if (has) _idle.Start();
    }

    void Bar_MouseDown(object s, MouseButtonEventArgs e) { if (e.OriginalSource is not Button) DragMove(); }
    void Pin_Click(object s, RoutedEventArgs e) => Topmost = PinToggle.IsChecked == true;
    void Hide_Click(object s, RoutedEventArgs e) { MorePopup.IsOpen = false; Hide(); }
    void More_Click(object s, RoutedEventArgs e) => MorePopup.IsOpen = !MorePopup.IsOpen;
    void Discard_Click(object s, RoutedEventArgs e) => Discard();
    void Save_Click(object s, RoutedEventArgs e) => Save();
    void OpenNotes_Click(object s, RoutedEventArgs e) => App.ShowNotes();

    // Writes the draft to the notes table and clears the editor.
    bool Commit()
    {
        var text = Editor.Text.Trim();
        if (text.Length == 0) return false;
        Store.AddNote(text);
        Editor.Clear();
        return true;
    }

    public void SaveDraft() => Commit();

    void Save()
    {
        if (!Commit()) return;
        Hide();
        ToastWindow.Popup("Note saved", "Saved to Notes");
    }

    void Discard()
    {
        MorePopup.IsOpen = false;
        Editor.Clear();
        Hide();
    }

    void Window_PreviewKeyDown(object s, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        if (e.Key == Key.Escape) { if (MorePopup.IsOpen) MorePopup.IsOpen = false; else Hide(); e.Handled = true; }
        else if (ctrl && (e.Key == Key.S || e.Key == Key.Enter)) { Save(); e.Handled = true; }
        else if (ctrl && e.Key == Key.D) { Discard(); e.Handled = true; }
        else if (ctrl && e.Key == Key.P) { PinToggle.IsChecked = PinToggle.IsChecked != true; Topmost = PinToggle.IsChecked == true; e.Handled = true; }
        else if (ctrl && e.Key == Key.O) { App.ShowNotes(); e.Handled = true; }
    }
}
