using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopCompanion;

public partial class MainWindow : Window
{
    Bucket _bucket = Bucket.Today;
    bool _expanded;
    HotkeyManager? _hotkeys;
    readonly RadioButton[] _tabs;
    List<Button> _menuRows = new();
    int _menuIdx = -1;
    bool _drawerOpen, _notesMode;
    Point _dragStart;
    TaskItem? _dragItem;
    DateTime _day = DateTime.Today;
    DispatcherTimer? _midnight;

    public MainWindow()
    {
        InitializeComponent();
        _tabs = new[] { TabToday, TabUpcoming, TabBacklog, TabLater };
        _menuRows = MenuStack.Children.OfType<Button>().ToList();

        Topmost = Prefs.OnTop;
        Opacity = Prefs.Opacity;
        RestorePosition();
        NotesPane.CloseRequested += ShowTasksView;
        SetExpanded(Prefs.Expanded);
        RefreshHints();
        TopCheck.Visibility = Topmost ? Visibility.Visible : Visibility.Hidden;

        Store.TasksChanged += Refresh;
        Store.CountsChanged += UpdateCounts;
        SourceInitialized += (_, _) =>
        {
            _hotkeys = new HotkeyManager(new WindowInteropHelper(this).Handle);
            var failed = ApplyHotkeys();
            if (failed.Count > 0)
                MessageBox.Show("These shortcuts are already used by another app:\n" + string.Join("\n", failed) +
                                "\n\nChange them in Settings.", "Desktop Companion");
        };
        Activated += (_, _) => { if (_day != DateTime.Today) { _day = DateTime.Today; Refresh(); UpdateCounts(); } };
        Refresh();
        UpdateCounts();
        ScheduleMidnight();
        ContentRendered += (_, _) => Focus();
    }

    // ---- window state ----

    void RestorePosition()
    {
        var area = SystemParameters.WorkArea;
        double left = area.Left + 12, top = area.Top + 12;
        if (double.TryParse(Prefs.Get("left", ""), out var l) && double.TryParse(Prefs.Get("top", ""), out var t))
        {
            left = Math.Clamp(l, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80);
            top = Math.Clamp(t, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60);
        }
        Left = left; Top = top;
    }

    public void SavePosition()
    {
        if (WindowState != WindowState.Normal) return;
        Prefs.Set("left", Left.ToString("0"));
        Prefs.Set("top", Top.ToString("0"));
    }

    void ScheduleMidnight()
    {
        _midnight?.Stop();
        var next = DateTime.Today.AddDays(1).AddSeconds(1);
        _midnight = new DispatcherTimer { Interval = next - DateTime.Now };
        _midnight.Tick += (_, _) => { _day = DateTime.Today; Refresh(); UpdateCounts(); ScheduleMidnight(); };
        _midnight.Start();
    }

    public void ShowWidget()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Topmost = true;
        Activate();
        Topmost = Prefs.OnTop;
        Focus();
    }

    public void HideToTray()
    {
        CloseDrawer();
        CommitNotes();
        SavePosition();
        Hide();
        App.TrimMemory();
    }

    public void ToggleWidget()
    {
        if (IsVisible && IsActive) HideToTray(); else ShowWidget();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!App.Quitting) { e.Cancel = true; HideToTray(); return; }
        base.OnClosing(e);
    }

    // ---- hotkeys / hints ----

    public List<string> ApplyHotkeys()
    {
        var failed = new List<string>();
        if (_hotkeys == null) return failed;
        _hotkeys.Clear();
        void Reg(Hotkey h, string name, Action a)
        {
            if (!_hotkeys.Register(h.Mods, h.Key, a)) failed.Add(h.Pretty + " (" + name + ")");
        }
        Reg(Prefs.Quick, "quick add", () => App.QuickAdd.ShowOverlay());
        Reg(Prefs.Pad, "scratchpad", ToggleScratchpad);
        Reg(Prefs.Toggle, "show/hide widget", ToggleWidget);
        RefreshHints();
        return failed;
    }

    public void RefreshHints()
    {
        HintQuick.Text = Prefs.Quick.Pretty;
        HintPad.Text = Prefs.Pad.Pretty;
    }

    public void ApplyOpacity() => Opacity = Prefs.Opacity;

    static void ToggleScratchpad()
    {
        if (App.Scratchpad.IsVisible && App.Scratchpad.IsActive) App.Scratchpad.Hide();
        else App.Scratchpad.ShowPad();
    }

    // ---- list ----

    TaskItem? Selected => TaskList.SelectedItem as TaskItem;

    void Refresh()
    {
        int idx = Math.Max(TaskList.SelectedIndex, 0);
        var items = Store.For(_bucket);
        TaskList.ItemsSource = items;
        EmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (items.Count > 0) TaskList.SelectedIndex = Math.Min(idx, items.Count - 1);
    }

    void UpdateCounts()
    {
        CountToday.Text = Store.Count(Bucket.Today).ToString();
        CountUpcoming.Text = Store.Count(Bucket.Upcoming).ToString();
        CountBacklog.Text = Store.Count(Bucket.Backlog).ToString();
    }

    void SelectTask(TaskItem t)
    {
        if (TaskList.ItemsSource is List<TaskItem> l)
        {
            int i = l.FindIndex(x => x.Id == t.Id);
            if (i >= 0) { TaskList.SelectedIndex = i; TaskList.ScrollIntoView(l[i]); }
        }
    }

    void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || TaskList == null) return;
        _bucket = (Bucket)Array.IndexOf(_tabs, rb);
        TaskList.SelectedIndex = -1;
        Refresh();
    }

    void SwitchTab(int delta)
    {
        int i = ((int)_bucket + delta + 4) % 4;
        _tabs[i].IsChecked = true;
    }

    void MoveSelected(int dir)
    {
        if (Selected is not { } t) return;
        int to = Math.Clamp((int)_bucket + dir, 0, 3);
        if (to == (int)_bucket) return;
        Store.Move(t, (Bucket)to);
        _tabs[to].IsChecked = true;
        SelectTask(t);
    }

    void MoveSelection(int delta, bool absolute = false)
    {
        int n = TaskList.Items.Count;
        if (n == 0) return;
        int i = absolute ? (delta < 0 ? n - 1 : 0) : Math.Clamp(TaskList.SelectedIndex + delta, 0, n - 1);
        TaskList.SelectedIndex = i;
        TaskList.ScrollIntoView(TaskList.SelectedItem);
    }

    void Delete_Click(object s, RoutedEventArgs e)
    {
        if (((FrameworkElement)s).DataContext is TaskItem t) Store.DeleteTask(t);
    }

    // ---- drag a task onto a tab ----

    void List_MouseDown(object s, MouseButtonEventArgs e)
    {
        _dragItem = null;
        if (Vis.Up<System.Windows.Controls.Primitives.ButtonBase>(e.OriginalSource as DependencyObject) != null) return;
        if (Vis.Up<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is TaskItem t)
        {
            _dragItem = t;
            _dragStart = e.GetPosition(null);
        }
    }

    void List_MouseMove(object s, MouseEventArgs e)
    {
        if (_dragItem == null || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(null);
        if (Math.Abs(p.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var item = _dragItem;
        _dragItem = null;
        DragDrop.DoDragDrop(TaskList, new DataObject(typeof(TaskItem), item), DragDropEffects.Move);
    }

    void Tab_DragOver(object s, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TaskItem)) ? DragDropEffects.Move : DragDropEffects.None;
        if (e.Effects != DragDropEffects.None) ((RadioButton)s).Background = (Brush)FindResource("HoverTabBrush");
        e.Handled = true;
    }

    void Tab_DragLeave(object s, DragEventArgs e) => ((RadioButton)s).Background = Brushes.Transparent;

    void Tab_Drop(object s, DragEventArgs e)
    {
        var tab = (RadioButton)s;
        tab.Background = Brushes.Transparent;
        if (e.Data.GetData(typeof(TaskItem)) is not TaskItem t) return;
        var to = (Bucket)Array.IndexOf(_tabs, tab);
        if (t.Bucket != to) Store.Move(t, to);
        e.Handled = true;
    }

    // ---- header / view ----

    void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2) { SetExpanded(!_expanded); Prefs.Expanded = _expanded; return; }
        DragMove();
        SavePosition();
    }

    void SetExpanded(bool on)
    {
        _expanded = on;
        Card.Width = on ? 500 : 380;
        ListScroll.MaxHeight = on ? double.PositiveInfinity : 124;
        ToggleViewText.Text = on ? "Compact view" : "Expanded view";
        UpdateCardHeight();
    }

    // Compact cards grow while the drawer or notes are showing so they have room.
    void UpdateCardHeight()
    {
        Card.Height = _expanded ? 500 : double.NaN;
        Card.MinHeight = !_expanded && (_drawerOpen || _notesMode) ? 400 : 0;
    }

    void ToggleExpanded() { SetExpanded(!_expanded); Prefs.Expanded = _expanded; }

    void ToggleTop()
    {
        Topmost = !Topmost;
        Prefs.OnTop = Topmost;
        TopCheck.Visibility = Topmost ? Visibility.Visible : Visibility.Hidden;
    }

    // ---- drawer ----

    void SetMenuIndex(int i)
    {
        _menuIdx = i;
        for (int k = 0; k < _menuRows.Count; k++) _menuRows[k].Tag = k == i ? "sel" : null;
    }

    void OpenMenu()
    {
        if (_drawerOpen) return;
        _drawerOpen = true;
        TopCheck.Visibility = Topmost ? Visibility.Visible : Visibility.Hidden;
        UpdateCardHeight();
        Scrim.Visibility = Visibility.Visible;
        Drawer.Visibility = Visibility.Visible;
        DrawerShift.BeginAnimation(TranslateTransform.XProperty, new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(140))
            { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
        SetMenuIndex(0);
    }

    void CloseDrawer()
    {
        if (!_drawerOpen) return;
        _drawerOpen = false;
        SetMenuIndex(-1);
        Scrim.Visibility = Visibility.Collapsed;
        DrawerShift.BeginAnimation(TranslateTransform.XProperty, null);
        DrawerShift.X = -224;
        Drawer.Visibility = Visibility.Collapsed;
        UpdateCardHeight();
        Focus();
    }

    void Menu_Click(object s, RoutedEventArgs e) { if (_drawerOpen) CloseDrawer(); else OpenMenu(); }
    void Scrim_Down(object s, MouseButtonEventArgs e) { CloseDrawer(); e.Handled = true; }

    // ---- notes pane ----

    public void ShowNotesView()
    {
        CloseDrawer();
        _notesMode = true;
        TasksBody.Visibility = Visibility.Collapsed;
        TabsPanel.Visibility = Visibility.Collapsed;
        NotesTitle.Visibility = Visibility.Visible;
        NotesPane.Visibility = Visibility.Visible;
        UpdateCardHeight();
        NotesPane.Open();
    }

    public void ShowTasksView()
    {
        CloseDrawer();
        if (_notesMode) NotesPane.Commit();
        _notesMode = false;
        NotesPane.Visibility = Visibility.Collapsed;
        NotesTitle.Visibility = Visibility.Collapsed;
        TabsPanel.Visibility = Visibility.Visible;
        TasksBody.Visibility = Visibility.Visible;
        UpdateCardHeight();
        TaskList.Focus();
        Focus();
    }

    public void CommitNotes() { if (_notesMode) NotesPane.Commit(); }

    void Tasks_Click(object s, RoutedEventArgs e) => ShowTasksView();
    void ToggleView_Click(object s, RoutedEventArgs e) { CloseDrawer(); ToggleExpanded(); }
    void Top_Click(object s, RoutedEventArgs e) { CloseDrawer(); ToggleTop(); }
    void Add_Click(object s, RoutedEventArgs e) { CloseDrawer(); if (_notesMode) App.Scratchpad.ShowPad(); else App.QuickAdd.ShowOverlay(); }
    void AddNote_Click(object s, RoutedEventArgs e) { CloseDrawer(); App.Scratchpad.ShowPad(); }
    void Notes_Click(object s, RoutedEventArgs e) => ShowNotesView();
    void Settings_Click(object s, RoutedEventArgs e) { CloseDrawer(); App.ShowSettings(0); }
    void Appearance_Click(object s, RoutedEventArgs e) { CloseDrawer(); App.ShowSettings(1); }
    void Shortcuts_Click(object s, RoutedEventArgs e) { CloseDrawer(); App.ShowSettings(2); }
    void Hide_Click(object s, RoutedEventArgs e) => HideToTray();
    void Quit_Click(object s, RoutedEventArgs e) => App.Quit();
    // ---- keyboard ----

    void Window_PreviewKeyDown(object s, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;
        bool ctrl = mods == ModifierKeys.Control, shift = mods == ModifierKeys.Shift, none = mods == ModifierKeys.None;

        if (_drawerOpen && (none || shift))
        {
            switch (key)
            {
                case Key.Down: SetMenuIndex((_menuIdx + 1) % _menuRows.Count); e.Handled = true; return;
                case Key.Up: SetMenuIndex((_menuIdx - 1 + _menuRows.Count) % _menuRows.Count); e.Handled = true; return;
                case Key.Home: SetMenuIndex(0); e.Handled = true; return;
                case Key.End: SetMenuIndex(_menuRows.Count - 1); e.Handled = true; return;
                case Key.Enter:
                case Key.Space:
                    if (_menuIdx >= 0) _menuRows[_menuIdx].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    e.Handled = true; return;
                case Key.Escape:
                case Key.M:
                case Key.F10: CloseDrawer(); e.Handled = true; return;
            }
        }

        if (_notesMode)
        {
            if (none && key == Key.F10) { OpenMenu(); e.Handled = true; return; }
            if (none && key == Key.F1) { App.ShowSettings(2); e.Handled = true; return; }
            if (NotesPane.HandleKey(key, mods)) { e.Handled = true; return; }
            if (!ctrl) return;
        }

        bool handled = true;
        if (ctrl && key == Key.H) HideToTray();
        else if (ctrl && key == Key.Q) App.Quit();
        else if (ctrl && key == Key.OemComma) App.ShowSettings(0);
        else if (none && key == Key.F1) App.ShowSettings(2);
        else if (none && (key == Key.F10 || key == Key.M)) OpenMenu();
        else if (none && key == Key.Escape) HideToTray();
        else if (none && key == Key.Down) MoveSelection(1);
        else if (none && key == Key.Up) MoveSelection(-1);
        else if (none && key == Key.Home) MoveSelection(-1, true);
        else if (none && key == Key.End) MoveSelection(1, true);
        else if (none && key == Key.PageDown) MoveSelection(4);
        else if (none && key == Key.PageUp) MoveSelection(-4);
        else if (none && key == Key.Right) SwitchTab(1);
        else if (none && key == Key.Left) SwitchTab(-1);
        else if (ctrl && key == Key.Tab) SwitchTab(1);
        else if (mods == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.Tab) SwitchTab(-1);
        else if (shift && key == Key.Right) MoveSelected(1);
        else if (shift && key == Key.Left) MoveSelected(-1);
        else if (none && key is >= Key.D1 and <= Key.D4) _tabs[key - Key.D1].IsChecked = true;
        else if (none && key is >= Key.NumPad1 and <= Key.NumPad4) _tabs[key - Key.NumPad1].IsChecked = true;
        else if (none && key == Key.Space) { if (Selected is { } t) t.IsDone = !t.IsDone; }
        else if (none && (key == Key.Enter || key == Key.F2)) { if (Selected is { } t) App.QuickAdd.ShowOverlay(t); }
        else if (none && key == Key.Delete) { if (Selected is { } t) Store.DeleteTask(t); }
        else if (none && key == Key.N) App.QuickAdd.ShowOverlay();
        else if (shift && key == Key.N) App.Scratchpad.ShowPad();
        else if (none && key == Key.O) ShowNotesView();
        else if (none && key == Key.E) ToggleExpanded();
        else if (none && key == Key.T) ToggleTop();
        else handled = false;
        if (handled) e.Handled = true;
    }
}
