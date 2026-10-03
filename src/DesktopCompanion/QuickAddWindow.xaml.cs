using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopCompanion;

public partial class QuickAddWindow : Window
{
    static readonly Brush Accent = (Brush)new BrushConverter().ConvertFromString("#3C76FF")!;

    public QuickAddWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        IsVisibleChanged += (_, _) => { if (!IsVisible) App.TrimMemory(); };
    }

    public void ShowOverlay()
    {
        Input.Clear();
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - 544) / 2;
        Top = area.Top + area.Height * 0.28;
        Show();
        Activate();
        Input.Focus();
    }

    void Input_TextChanged(object s, TextChangedEventArgs e) =>
        Hint.Visibility = Input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    void Submit_Click(object s, RoutedEventArgs e) => Submit();

    void Submit()
    {
        var text = Input.Text.Trim();
        if (text.Length > 0)
            SampleData.Tasks["Today"].Add(new TaskItem { Title = text, Icon = "\uE8A5", Accent = Accent });
        Hide();
    }

    void Window_PreviewKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Submit(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
        else if (e.Key == Key.Tab) e.Handled = true;
    }

    void Window_Deactivated(object? s, EventArgs e) { if (IsVisible) Hide(); }
}




