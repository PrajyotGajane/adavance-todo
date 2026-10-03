using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DesktopCompanion;

public partial class ToastWindow : Window
{
    const int GWL_EXSTYLE = -20, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x80;
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);

    ToastWindow(string heading, string sub)
    {
        InitializeComponent();
        Heading.Text = heading;
        Sub.Text = sub;
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        };
        Loaded += (_, _) =>
        {
            var a = SystemParameters.WorkArea;
            Left = a.Right - ActualWidth - 8;
            Top = a.Bottom - ActualHeight - 8;
        };
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        t.Tick += (_, _) => { t.Stop(); Close(); };
        t.Start();
    }

    public static void Popup(string heading, string sub) => new ToastWindow(heading, sub).Show();
}
