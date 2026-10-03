using System.Windows;

namespace DesktopCompanion;

public partial class QuickAddWindow : Window
{
    public QuickAddWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        View.Finished += Hide;
        IsVisibleChanged += (_, _) => { if (!IsVisible) App.TrimMemory(); };
    }

    public void ShowOverlay(TaskItem? edit = null)
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - 544) / 2;
        Top = area.Top + area.Height * 0.28;
        Show();
        Activate();
        View.Begin(edit);
    }

    void Window_Deactivated(object? s, EventArgs e) { if (IsVisible) { View.Cancel(); Hide(); } }
}
