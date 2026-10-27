using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DragonBreak;

internal sealed class PetWindow : Window
{
    public bool Dragging { get; private set; }
    public IPetVisual Model { get; }
    public void ApplySize(int percent)
    {
        double scale = Math.Clamp(percent, 50, 200) / 100d;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var area = hwnd == IntPtr.Zero ? System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea
            : System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea;
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice;
        double dpiX = transform?.M11 ?? 1, dpiY = transform?.M22 ?? 1;
        scale = Math.Min(scale, Math.Min(area.Width / (380d * dpiX), area.Height / (380d * dpiY)));
        Width = 380 * scale; Height = 380 * scale;
    }

    public PetWindow(Action settings, Func<ContextMenu> menu, IPetDefinition definition)
    {
        Title = "DragonBreak pet"; Width = 380; Height = 380;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = false; ShowActivated = false;
        var presentation = definition.CreatePresentation(370, false); var art = presentation.View;
        Model = presentation.Visual;
        var body = new Border { Background = Brushes.Transparent, Child = new Viewbox { Child = art, Stretch = Stretch.Uniform }, Cursor = Cursors.Hand,
            ToolTip = "Drag to move • right-click for controls • double-click for settings" };
        Content = body;
        body.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { settings(); return; }
            Dragging = true;
            try { DragMove(); } catch (InvalidOperationException) { }
            finally { Dragging = false; }
        };
        body.MouseRightButtonUp += (_, _) => { var context = menu(); context.PlacementTarget = body; context.IsOpen = true; };
    }
}
