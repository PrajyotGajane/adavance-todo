using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace DesktopCompanion;

public static class Vis
{
    public static T? Up<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = d is Visual or Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return null;
    }
}
