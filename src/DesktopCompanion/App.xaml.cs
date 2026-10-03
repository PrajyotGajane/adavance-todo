using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace DesktopCompanion;

public partial class App : Application
{
    static QuickAddWindow? _quick;
    static ScratchpadWindow? _pad;
    static NotesWindow? _notes;

    // Created on first use to keep idle memory low.
    public static QuickAddWindow QuickAdd => _quick ??= new QuickAddWindow();
    public static ScratchpadWindow Scratchpad => _pad ??= new ScratchpadWindow();
    public static NotesWindow Notes => _notes ??= new NotesWindow();

    [DllImport("psapi.dll")]
    static extern bool EmptyWorkingSet(IntPtr process);

    // Releases pages not needed right now; run once the UI has gone idle.
    public static void TrimMemory() =>
        Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        });

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TrimMemory();
    }
}
