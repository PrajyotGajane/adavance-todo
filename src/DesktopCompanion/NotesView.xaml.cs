using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DesktopCompanion;

public partial class NotesView : UserControl
{
    NoteItem? _editing;
    bool _isNew;

    public event Action? CloseRequested;

    public NotesView()
    {
        InitializeComponent();
        Store.NotesChanged += Rebuild;
        Rebuild();
    }

    public void Open()
    {
        CommitEdit();
        ShowListView();
        Search.Focus();
    }

    public void Commit() => CommitEdit();

    void ShowListView()
    {
        _editing = null;
        EditView.Visibility = Visibility.Collapsed;
        ListView.Visibility = Visibility.Visible;
        Rebuild();
    }

    public void NewNote()
    {
        CommitEdit();
        OpenNote(new NoteItem(), true);
    }

    void OpenNote(NoteItem n, bool isNew = false)
    {
        _editing = n;
        _isNew = isNew;
        EditTitle.Text = isNew ? "New note" : "Edit note";
        Editor.Text = n.Body;
        ListView.Visibility = Visibility.Collapsed;
        EditView.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            Keyboard.Focus(Editor);
            Editor.CaretIndex = Editor.Text.Length;
        });
    }

    // Saves the open note (or removes it if emptied).
    void CommitEdit()
    {
        if (_editing is not { } n) return;
        var text = Editor.Text.Trim();
        if (_isNew)
        {
            if (text.Length > 0) { _editing = Store.AddNote(text); _isNew = false; }
        }
        else if (text.Length == 0) Store.DeleteNote(n);
        else Store.UpdateNote(n, text);
    }

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

    void Back_Click(object s, RoutedEventArgs e) { CommitEdit(); ShowListView(); Search.Focus(); }
    void Delete_Click(object s, RoutedEventArgs e) { if (_editing is { } n) { _editing = null; if (!_isNew) Store.DeleteNote(n); ShowListView(); Search.Focus(); } }

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

    // Returns true when the key was consumed by the notes pane.
    public bool HandleKey(Key key, ModifierKeys mods)
    {
        bool ctrl = mods == ModifierKeys.Control;
        if (_editing != null)
        {
            if (key == Key.Escape) { Back_Click(this, new RoutedEventArgs()); return true; }
            if (ctrl && key == Key.S) { CommitEdit(); return true; }
            if (ctrl && key == Key.D) { Delete_Click(this, new RoutedEventArgs()); return true; }
            return false;
        }
        if (key == Key.Escape)
        {
            if (Search.Text.Length > 0) Search.Clear(); else CloseRequested?.Invoke();
            return true;
        }
        if (mods != ModifierKeys.None && !ctrl) return false;
        switch (key)
        {
            case Key.Down when !ctrl: Move(1); return true;
            case Key.Up when !ctrl: Move(-1); return true;
            case Key.PageDown when !ctrl: Move(4); return true;
            case Key.PageUp when !ctrl: Move(-4); return true;
            case Key.Enter when !ctrl: if (NoteList.SelectedItem is NoteItem n) OpenNote(n); return true;
            case Key.N when ctrl: NewNote(); return true;
        }
        if ((ctrl && key == Key.D) || (key == Key.Delete && Search.Text.Length == 0))
        {
            if (NoteList.SelectedItem is NoteItem n) Store.DeleteNote(n);
            return true;
        }
        return false;
    }
}
