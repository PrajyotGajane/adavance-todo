using System.Windows.Input;
using Microsoft.Win32;

namespace DesktopCompanion;

public readonly record struct Hotkey(ModifierKeys Mods, Key Key)
{
    public static bool TryParse(string? s, out Hotkey h)
    {
        h = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var mods = ModifierKeys.None;
        Key key = Key.None;
        foreach (var raw in s.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= ModifierKeys.Control; break;
                case "alt": mods |= ModifierKeys.Alt; break;
                case "shift": mods |= ModifierKeys.Shift; break;
                default:
                    if (!Enum.TryParse(raw, true, out key)) return false;
                    break;
            }
        }
        if (key == Key.None || (mods & (ModifierKeys.Control | ModifierKeys.Alt)) == 0) return false;
        h = new Hotkey(mods, key);
        return true;
    }

    public override string ToString() => Format("+");
    public string Pretty => Format(" + ");

    string Format(string sep)
    {
        var parts = new List<string>();
        if (Mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        var k = Key.ToString();
        if (k.Length == 2 && k[0] == 'D' && char.IsDigit(k[1])) k = k[1].ToString();
        parts.Add(k);
        return string.Join(sep, parts);
    }
}

// Preferences are stored in the settings table and cached in memory.
public static class Prefs
{
    static readonly Dictionary<string, string> _d = new();

    public static readonly Hotkey DefaultQuick = new(ModifierKeys.Control | ModifierKeys.Alt, Key.T);
    public static readonly Hotkey DefaultPad = new(ModifierKeys.Control | ModifierKeys.Alt, Key.J);
    public static readonly Hotkey DefaultToggle = new(ModifierKeys.Control | ModifierKeys.Alt, Key.H);

    public static void Load()
    {
        _d.Clear();
        foreach (var r in Db.Query("SELECT key,value FROM settings")) _d[(string)r[0]!] = (string)r[1]!;
    }

    public static string Get(string k, string def) => _d.TryGetValue(k, out var v) ? v : def;

    public static void Set(string k, string v)
    {
        _d[k] = v;
        Db.Exec("INSERT INTO settings(key,value) VALUES(@k,@v) ON CONFLICT(key) DO UPDATE SET value=@v", ("@k", k), ("@v", v));
    }

    public static bool OnTop { get => Get("on_top", "1") == "1"; set => Set("on_top", value ? "1" : "0"); }
    public static bool Expanded { get => Get("expanded", "0") == "1"; set => Set("expanded", value ? "1" : "0"); }

    public static double Opacity
    {
        get => double.TryParse(Get("opacity", "1"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var o) ? Math.Clamp(o, 0.5, 1) : 1;
        set => Set("opacity", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public static Hotkey GetHotkey(string key, Hotkey def) => Hotkey.TryParse(Get(key, ""), out var h) ? h : def;
    public static void SetHotkey(string key, Hotkey h) => Set(key, h.ToString());

    public static Hotkey Quick => GetHotkey("hk_quick", DefaultQuick);
    public static Hotkey Pad => GetHotkey("hk_pad", DefaultPad);
    public static Hotkey Toggle => GetHotkey("hk_toggle", DefaultToggle);
}

public static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "DesktopCompanion";

    public static bool IsEnabled
    {
        get { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue(Name) != null; }
    }

    public static void Set(bool on)
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) k.SetValue(Name, "\"" + Environment.ProcessPath + "\"");
        else k.DeleteValue(Name, false);
    }
}
