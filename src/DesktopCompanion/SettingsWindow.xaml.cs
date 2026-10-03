using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopCompanion;

public partial class SettingsWindow : Window
{
    static readonly double[] Opacities = { 1.0, 0.9, 0.8, 0.7 };
    static readonly string[] HkKeys = { "hk_quick", "hk_pad", "hk_toggle" };
    static readonly Hotkey[] HkDefaults = { Prefs.DefaultQuick, Prefs.DefaultPad, Prefs.DefaultToggle };

    readonly Border[] _rows;
    readonly TextBlock[] _vals;
    int _sel;
    bool _capturing;

    public SettingsWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        _rows = new[] { Row0, Row1, Row2, Row3, Row4 };
        _vals = new[] { Val0, Val1, Val2, Val3, Val4 };
        var a = SystemParameters.WorkArea;
        Left = a.Left + (a.Width - 444) / 2;
        Top = a.Top + a.Height * 0.2;
        IsVisibleChanged += (_, _) => { if (!IsVisible) App.TrimMemory(); };
    }

    public void ShowAt(int row)
    {
        _capturing = false;
        Status.Text = "";
        Select(row);
        Render();
        Show();
        Activate();
        Focus();
    }

    void Select(int i)
    {
        _sel = Math.Clamp(i, 0, _rows.Length - 1);
        for (int k = 0; k < _rows.Length; k++)
            _rows[k].Background = k == _sel ? (Brush)FindResource("SelectedRowBrush") : Brushes.Transparent;
    }

    void Render()
    {
        Val0.Text = Startup.IsEnabled ? "On" : "Off";
        Val1.Text = (int)Math.Round(Prefs.Opacity * 100) + "%";
        Val2.Text = Prefs.Quick.Pretty;
        Val3.Text = Prefs.Pad.Pretty;
        Val4.Text = Prefs.Toggle.Pretty;
        if (_capturing) _vals[_sel].Text = "Press new shortcut...";
    }

    void Drag(object s, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not Border { Tag: string }) DragMove(); }
    void Close_Click(object s, RoutedEventArgs e) => Hide();

    void Row_Click(object s, MouseButtonEventArgs e)
    {
        Select(int.Parse((string)((Border)s).Tag));
        Activate_();
        e.Handled = true;
    }

    // Enter / Space / click on the selected row.
    void Activate_()
    {
        if (_sel == 0) { Toggle(); }
        else if (_sel == 1) CycleOpacity(1);
        else BeginCapture();
    }

    void Toggle()
    {
        try { Startup.Set(!Startup.IsEnabled); Status.Text = ""; }
        catch (Exception ex) { Status.Text = "Could not change startup setting: " + ex.Message; }
        Render();
    }

    void CycleOpacity(int dir)
    {
        int i = Array.FindIndex(Opacities, o => Math.Abs(o - Prefs.Opacity) < 0.01);
        if (i < 0) i = 0;
        i = (i + dir + Opacities.Length) % Opacities.Length;
        Prefs.Opacity = Opacities[i];
        App.Widget.ApplyOpacity();
        Render();
    }

    void BeginCapture()
    {
        _capturing = true;
        Status.Text = "Press the new shortcut (needs Ctrl or Alt). Esc cancels.";
        Render();
    }

    void EndCapture(string message)
    {
        _capturing = false;
        Status.Text = message;
        Render();
    }

    void Capture(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { EndCapture(""); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.None or Key.ImeProcessed or Key.DeadCharProcessed) return;
        var mods = Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift);
        if ((mods & (ModifierKeys.Control | ModifierKeys.Alt)) == 0) { Status.Text = "Shortcut must include Ctrl or Alt."; return; }
        var h = new Hotkey(mods, key);

        int slot = _sel - 2;
        for (int i = 0; i < HkKeys.Length; i++)
            if (i != slot && Prefs.GetHotkey(HkKeys[i], HkDefaults[i]) == h) { Status.Text = h.Pretty + " is already used by another action."; return; }

        var old = Prefs.GetHotkey(HkKeys[slot], HkDefaults[slot]);
        Prefs.SetHotkey(HkKeys[slot], h);
        var failed = App.Widget.ApplyHotkeys();
        if (failed.Count > 0)
        {
            Prefs.SetHotkey(HkKeys[slot], old);
            App.Widget.ApplyHotkeys();
            EndCapture(h.Pretty + " is taken by another app. Kept " + old.Pretty + ".");
        }
        else EndCapture("Saved " + h.Pretty + ".");
    }

    void Window_PreviewKeyDown(object s, KeyEventArgs e)
    {
        if (_capturing) { Capture(e); e.Handled = true; return; }
        switch (e.Key)
        {
            case Key.Escape: Hide(); break;
            case Key.Down: Select(_sel + 1); break;
            case Key.Up: Select(_sel - 1); break;
            case Key.Home: Select(0); break;
            case Key.End: Select(_rows.Length - 1); break;
            case Key.Left: if (_sel == 1) CycleOpacity(-1); else if (_sel == 0) Toggle(); break;
            case Key.Right: if (_sel == 1) CycleOpacity(1); else if (_sel == 0) Toggle(); break;
            case Key.Space:
            case Key.Enter: Activate_(); break;
            default: return;
        }
        e.Handled = true;
    }
}

