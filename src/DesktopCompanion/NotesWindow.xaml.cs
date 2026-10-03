using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DesktopCompanion;

public partial class NotesWindow : Window
{
    NoteItem? _editing;

    public NotesWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        var area = SystemParameters.WorkArea;
        Left = area.Left + 400;
        Top = area.Top + 80;
        Store.NotesChanged += Rebuild;
        IsVisibleChanged += (_, _) => { if (!IsVisible) App.TrimMemory(); };
        Rebuild();
    }

    public void ShowList()
    {
        CommitEdit();
        ShowListView();
        Show();
        Activate();
        Search.Focus();
    }

    void ShowListView()
    {
        _editing = null;
        EditView.Visibility = Visibility.Collapsed;
        ListView.Visibility = Visibility.Visible;
        Rebuild();
    }

    void OpenNote(NoteItem n)
    {
        _editing = n;
        Editor.Text = n.Body;
        ListView.Visibility = Visibility.Collapsed;
        EditView.Visibility = Visibility.Visible;
        Editor.Focus();
        Editor.CaretIndex = Editor.Text.Length;
    }

    // Saves the open note (or removes it if emptied).
    void CommitEdit()
    {
        if (_editing is not { } n) return;
        var text = Editor.Text.Trim();
        if (text.Length == 0) Store.DeleteNote(n);
        else Store.UpdateNote(n, text);
    }

    void CloseWindow() { CommitEdit(); _editing = null; Hide(); }

    void Rebuild()
    {
        if (NoteList == null) return;
        var q = Search.Text.Trim();
        SearchHint.Visibility = q.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var items = Store.Notes.Where(n => q.Length == 0 || n.Body.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        int idx = Math.Max(NoteList.SelectedIndex, 0);
        NoteList.ItemsSource = items;
        EmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (items.Count > 0) NoteList.SelectedIndex = Math.Min(idx, items.Count - 1);
    }

    void Drag(object s, MouseButtonEventArgs e) { if (e.OriginalSource is not TextBox && e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    void Close_Click(object s, RoutedEventArgs e) => CloseWindow();
    void Tasks_Click(object s, RoutedEventArgs e) { CloseWindow(); App.Widget.ShowWidget(); }
    void Settings_Click(object s, RoutedEventArgs e) => App.ShowSettings(0);
    void New_Click(object s, RoutedEventArgs e) => App.Scratchpad.ShowPad();
    void Back_Click(object s, RoutedEventArgs e) { CommitEdit(); ShowListView(); Search.Focus(); }
    void Delete_Click(object s, RoutedEventArgs e) { if (_editing is { } n) { _editing = null; Store.DeleteNote(n); ShowListView(); Search.Focus(); } }

    void NoteList_Click(object s, MouseButtonEventArgs e)
    {
        if (Vis.Up<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is NoteItem n) OpenNote(n);
    }

    void Search_TextChanged(object s, TextChangedEventArgs e)
    {
        if (NoteList == null) return;
        NoteList.SelectedIndex = -1;
        Rebuild();
    }

    void Move(int d)
    {
        int n = NoteList.Items.Count;
        if (n == 0) return;
        NoteList.SelectedIndex = Math.Clamp(NoteList.SelectedIndex + d, 0, n - 1);
        NoteList.ScrollIntoView(NoteList.SelectedItem);
    }

    void Window_PreviewKeyDown(object s, KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;
        bool ctrl = mods == ModifierKeys.Control;
        if (_editing != null)
        {
            if (e.Key == Key.Escape) { Back_Click(s, e); e.Handled = true; }
            else if (ctrl && e.Key == Key.S) { CommitEdit(); e.Handled = true; }
            else if (ctrl && e.Key == Key.D) { Delete_Click(s, e); e.Handled = true; }
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (Search.Text.Length > 0) Search.Clear(); else CloseWindow();
            e.Handled = true;
        }
        else if (e.Key == Key.Down) { Move(1); e.Handled = true; }
        else if (e.Key == Key.Up) { Move(-1); e.Handled = true; }
        else if (e.Key == Key.PageDown) { Move(4); e.Handled = true; }
        else if (e.Key == Key.PageUp) { Move(-4); e.Handled = true; }
        else if (e.Key == Key.Enter) { if (NoteList.SelectedItem is NoteItem n) OpenNote(n); e.Handled = true; }
        else if (ctrl && e.Key == Key.N) { App.Scratchpad.ShowPad(); e.Handled = true; }
        else if ((ctrl && e.Key == Key.D) || (e.Key == Key.Delete && Search.Text.Length == 0))
        {
            if (NoteList.SelectedItem is NoteItem n) Store.DeleteNote(n);
            e.Handled = true;
        }
    }
}

