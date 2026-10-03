using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DesktopCompanion;

public sealed class Tray : IDisposable
{
    readonly Forms.NotifyIcon _icon;

    public Tray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show / hide widget", null, (_, _) => App.Widget.ToggleWidget());
        menu.Items.Add("Add task", null, (_, _) => App.QuickAdd.ShowOverlay());
        menu.Items.Add("Add note", null, (_, _) => App.Scratchpad.ShowPad());
        menu.Items.Add("Notes", null, (_, _) => App.ShowNotes());
        menu.Items.Add("Settings", null, (_, _) => App.ShowSettings(0));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => App.Quit());

        _icon = new Forms.NotifyIcon { Icon = MakeIcon(), Text = "Desktop Companion", Visible = true, ContextMenuStrip = menu };
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) App.Widget.ToggleWidget(); };
    }

    static Drawing.Icon MakeIcon()
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var bg = new Drawing.SolidBrush(Drawing.Color.FromArgb(0x3C, 0x76, 0xFF));
            using var path = new Drawing.Drawing2D.GraphicsPath();
            const int r = 8, s = 30;
            path.AddArc(1, 1, r, r, 180, 90); path.AddArc(s - r, 1, r, r, 270, 90);
            path.AddArc(s - r, s - r, r, r, 0, 90); path.AddArc(1, s - r, r, r, 90, 90);
            path.CloseFigure();
            g.FillPath(bg, path);
            using var pen = new Drawing.Pen(Drawing.Color.White, 3.5f) { StartCap = Drawing.Drawing2D.LineCap.Round, EndCap = Drawing.Drawing2D.LineCap.Round, LineJoin = Drawing.Drawing2D.LineJoin.Round };
            g.DrawLines(pen, new[] { new Drawing.PointF(8, 16.5f), new Drawing.PointF(13.5f, 22), new Drawing.PointF(24, 10) });
        }
        return Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}

