using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DesktopCompanion;

public partial class NotesWindow : Window
{
    public NotesWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        var area = SystemParameters.WorkArea;
        Left = area.Left + 400;
        Top = area.Top + 80;
        NoteList.ItemsSource = SampleData.Notes;
    }

    void Drag(object s, MouseButtonEventArgs e) { if (e.OriginalSource is not TextBox && e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    void Close_Click(object s, RoutedEventArgs e) => Hide();
    void Tasks_Click(object s, RoutedEventArgs e) => Hide();

    void Search_TextChanged(object s, TextChangedEventArgs e)
    {
        var q = Search.Text.Trim();
        SearchHint.Visibility = q.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoteList.ItemsSource = q.Length == 0
            ? SampleData.Notes
            : SampleData.Notes.Where(n => n.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                                       || n.Preview.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    void Window_PreviewKeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Escape) { Hide(); e.Handled = true; } }
}
