using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DesktopCompanion;

public partial class MainWindow : Window
{
    bool _expanded;

    public MainWindow()
    {
        InitializeComponent();
        var area = SystemParameters.WorkArea;
        Left = area.Left + 12;
        Top = area.Top + 12;
        TaskItem.Changed += UpdateCounts;
        foreach (var list in SampleData.Tasks.Values)
            list.CollectionChanged += (_, _) => UpdateCounts();
        ShowTab("Today");
        UpdateCounts();
    }

    void UpdateCounts()
    {
        static int N(string k) => SampleData.Tasks[k].Count(t => !t.IsDone);
        CountToday.Text = N("Today").ToString();
        CountUpcoming.Text = N("Upcoming").ToString();
        CountBacklog.Text = N("Backlog").ToString();
    }

    void ShowTab(string name) => TaskList.ItemsSource = SampleData.Tasks[name];

    void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string name && TaskList != null) ShowTab(name);
    }

    void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2) { ToggleView(); return; }
        DragMove();
    }

    void ToggleView()
    {
        _expanded = !_expanded;
        Card.Width = _expanded ? 400 : 360;
        ListScroll.MaxHeight = _expanded ? 186 : 124;
        ToggleViewText.Text = _expanded ? "Compact view" : "Expanded view";
    }

    void Menu_Click(object s, RoutedEventArgs e) => MenuPopup.IsOpen = !MenuPopup.IsOpen;
    void ToggleView_Click(object s, RoutedEventArgs e) { MenuPopup.IsOpen = false; ToggleView(); }
    void Add_Click(object s, RoutedEventArgs e) { MenuPopup.IsOpen = false; App.QuickAdd.ShowOverlay(); }
    void AddNote_Click(object s, RoutedEventArgs e) { MenuPopup.IsOpen = false; App.Scratchpad.ShowPad(); }
    void Notes_Click(object s, RoutedEventArgs e) { MenuPopup.IsOpen = false; App.Notes.Show(); App.Notes.Activate(); }
    void Minimize_Click(object s, RoutedEventArgs e) { MenuPopup.IsOpen = false; WindowState = WindowState.Minimized; }
    void Quit_Click(object s, RoutedEventArgs e) => Application.Current.Shutdown();

    void Stub_Click(object s, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        MessageBox.Show(((Button)s).Tag + " is not available in the Phase 1 prototype.", "Desktop Companion");
    }

    void Window_KeyDown(object s, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        if (ctrl && e.Key == Key.N && !alt) App.QuickAdd.ShowOverlay();
        else if (ctrl && alt && e.SystemKey == Key.N) App.Scratchpad.ShowPad();
        else if (ctrl && e.Key == Key.H) WindowState = WindowState.Minimized;
    }
}

