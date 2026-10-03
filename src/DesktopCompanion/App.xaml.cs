using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace DesktopCompanion;

public partial class App : Application
{
    static QuickAddWindow? _quick;
    static ScratchpadWindow? _pad;
    static SettingsWindow? _settings;
    Mutex? _single;
    Tray? _tray;

    public static MainWindow Widget { get; private set; } = null!;
    public static bool Quitting { get; private set; }

    // Created on first use to keep idle memory low.
    public static QuickAddWindow QuickAdd => _quick ??= new QuickAddWindow();
    public static ScratchpadWindow Scratchpad => _pad ??= new ScratchpadWindow();

    public static void ShowNotes() { Widget.ShowWidget(); Widget.ShowNotesView(); }
    public static void ShowSettings(int row) { (_settings ??= new SettingsWindow()).ShowAt(row); }

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
        if (Environment.GetEnvironmentVariable("DC_ALLOW_MULTI") == null)
        {
            _single = new Mutex(true, @"Local\DesktopCompanion.SingleInstance", out var first);
            if (!first) { Shutdown(); return; }
        }
        Store.Init();
        Widget = new MainWindow();
        Widget.Show();
        _tray = new Tray();
        TrimMemory();
    }

    public static void Quit()
    {
        if (Quitting) return;
        Quitting = true;
        Widget.SavePosition();
        _pad?.SaveDraft();
        Widget?.CommitNotes();
        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        Db.Close();
        _single?.Dispose();
        base.OnExit(e);
    }
}

