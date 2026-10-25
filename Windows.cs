using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DragonBreak;

internal static class Ui
{
    public static Brush Ink => new SolidColorBrush(Color.FromRgb(10, 20, 24));
    public static Brush Mint => new SolidColorBrush(Color.FromRgb(143, 235, 183));
    public static Brush Muted => new SolidColorBrush(Color.FromRgb(170, 187, 185));
    public static TextBlock Text(string text, double size = 16, Brush? color = null) => new()
    {
        Text = text, FontSize = size, Foreground = color ?? Brushes.White,
        FontFamily = new FontFamily("Segoe UI"), TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 12)
    };
    public static Button Button(string title, Action action, bool primary = false)
    {
        var b = new Button
        {
            Content = title, FontSize = 15, Padding = new Thickness(18, 11, 18, 11),
            Margin = new Thickness(0, 0, 10, 8), Cursor = Cursors.Hand,
            Background = primary ? Mint : new SolidColorBrush(Color.FromRgb(35, 52, 55)),
            Foreground = primary ? Ink : Brushes.White,
            BorderThickness = new Thickness(0), MinHeight = 42
        };
        b.Click += (_, _) => action();
        return b;
    }
    public static Grid Dragon(double width, bool fire)
    {
        var grid = new Grid { Width = width, Height = width, IsHitTestVisible = false };
        var image = new DragonModel { RenderTransformOrigin = new Point(.5, .5) };
        var movement = new TransformGroup();
        var rotate = new RotateTransform();
        var translate = new TranslateTransform();
        movement.Children.Add(rotate); movement.Children.Add(translate); image.RenderTransform = movement;
        rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-2, 2, TimeSpan.FromSeconds(2.4)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-4, 4, TimeSpan.FromSeconds(1.8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        grid.Children.Add(image);
        if (fire)
        {
            var fireLayer = new Canvas { Width = width, Height = width, IsHitTestVisible = false };
            var flame = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 0,50 C 25,30 40,15 65,28 L 100,0 L 90,35 L 150,20 L 120,60 L 160,80 L 90,74 L 110,110 C 50,95 40,65 0,50 Z"),
                Fill = new LinearGradientBrush(Color.FromRgb(255, 243, 137), Color.FromRgb(255, 79, 12), 0),
                Width = width * .23, Height = width * .15, Stretch = Stretch.Fill,
                RenderTransformOrigin = new Point(0, .5)
            };
            Canvas.SetLeft(flame, width * .80); Canvas.SetTop(flame, width * .53);
            var scale = new ScaleTransform(1, 1);
            flame.RenderTransform = scale;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.6, 1.15, TimeSpan.FromSeconds(.27)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.7, 1.2, TimeSpan.FromSeconds(.36)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            flame.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.75, 1, TimeSpan.FromSeconds(.23)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            fireLayer.Children.Add(flame); grid.Children.Add(fireLayer);
        }
        return grid;
    }
}

internal sealed class PetWindow : Window
{
    public bool Dragging { get; private set; }
    public DragonModel Model { get; }
    public PetWindow(Action settings, Func<ContextMenu> menu)
    {
        Title = "DragonBreak pet"; Width = 380; Height = 270;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = false; ShowActivated = false;
        var art = Ui.Dragon(370, false); art.Height = 250;
        Model = (DragonModel)art.Children[0];
        var body = new Border { Background = Brushes.Transparent, Child = art, Cursor = Cursors.Hand,
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

internal sealed class BreakWindow : Window
{
    private readonly TextBlock _countdown;
    public BreakWindow(BreakKind kind, Action<bool> finish, Action mute)
    {
        bool water = kind == BreakKind.Water;
        Title = water ? "DragonBreak — water break" : "DragonBreak — movement break";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; Topmost = true;
        Background = Ui.Ink; ShowInTaskbar = true;
        var root = new Grid { Background = new RadialGradientBrush(Color.FromRgb(21, 54, 44), Color.FromRgb(8, 17, 22)) };
        var content = new Grid { Width = 1050, Height = 650, Margin = new Thickness(35) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(520) });
        content.ColumnDefinitions.Add(new ColumnDefinition());
        content.Children.Add(Ui.Dragon(500, true));
        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(30, 0, 0, 0) };
        Grid.SetColumn(copy, 1);
        copy.Children.Add(Ui.Text("D R A G O N B R E A K", 14, Ui.Mint));
        copy.Children.Add(Ui.Text(water ? "A moment\nfor water." : "Rise. Stretch.\nReturn stronger.", 54));
        copy.Children.Add(Ui.Text(water ? "Your next task can wait.\nTake a sip and breathe." : "Stand up, loosen your shoulders,\nand take a short walk.", 21, Ui.Muted));
        _countdown = Ui.Text("00:30", 38, Ui.Mint); _countdown.Margin = new Thickness(0, 24, 0, 26);
        copy.Children.Add(_countdown);
        var actions = new WrapPanel();
        actions.Children.Add(Ui.Button("Done", () => finish(false), true));
        actions.Children.Add(Ui.Button("Snooze 5 min", () => finish(true)));
        actions.Children.Add(Ui.Button("Skip", () => finish(false)));
        copy.Children.Add(actions);
        copy.Children.Add(Ui.Button("Mute sound", mute));
        copy.Children.Add(Ui.Text("Esc always dismisses this reminder.", 13, Ui.Muted));
        content.Children.Add(copy);
        root.Children.Add(new Viewbox { Child = content, Stretch = Stretch.Uniform, Margin = new Thickness(12) });
        Content = root;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; finish(false); } };
    }
    public void Update(int seconds) => _countdown.Text = TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"mm\:ss");
}

internal sealed class SettingsWindow : Window
{
    public SettingsWindow(Settings current, Action<Settings> save, Action<BreakKind> preview, Action quit)
    {
        Title = "DragonBreak settings"; Width = 510; Height = 740; MinWidth = 450; MinHeight = 600;
        Background = Ui.Ink; Foreground = Brushes.White; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new StackPanel { Margin = new Thickness(30) };
        panel.Children.Add(Ui.Text("Your little dragon.\nYour time to recharge.", 29, Ui.Mint));
        panel.Children.Add(Ui.Text("Right-click the pet or tray icon any time.", 14, Ui.Muted));
        TextBox Field(string label, int value)
        {
            panel.Children.Add(Ui.Text(label, 14));
            var box = new TextBox { Text = value.ToString(), FontSize = 17, Padding = new Thickness(9), Margin = new Thickness(0, 0, 0, 14) };
            panel.Children.Add(box); return box;
        }
        var water = Field("Water reminder · minutes (1–240)", current.WaterMinutes);
        var stretch = Field("Movement reminder · minutes (1–240)", current.StretchMinutes);
        var duration = Field("Break countdown · seconds (5–300)", current.BreakSeconds);
        panel.Children.Add(Ui.Text("Movement", 14));
        var movement = new ComboBox { ItemsSource = Enum.GetValues<Movement>(), SelectedItem = current.Movement, FontSize = 16, Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(movement);
        panel.Children.Add(Ui.Text("Roar + voice volume", 14));
        var volume = new Slider { Minimum = 0, Maximum = 100, Value = current.Volume, TickFrequency = 10, Margin = new Thickness(0, 0, 0, 18) };
        panel.Children.Add(volume);
        var muted = new CheckBox { Content = "Mute all reminder audio", IsChecked = current.Muted, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 14) };
        var quiet = new CheckBox { Content = "Quiet hours: 10 pm–8 am (no automatic reminders)", IsChecked = current.QuietHours, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20) };
        panel.Children.Add(muted); panel.Children.Add(quiet);
        var error = Ui.Text("", 13, Brushes.Salmon); panel.Children.Add(error);
        bool Apply()
        {
            if (!int.TryParse(water.Text, out int w) || w < 1 || w > 240 || !int.TryParse(stretch.Text, out int s) || s < 1 || s > 240 || !int.TryParse(duration.Text, out int d) || d < 5 || d > 300)
            { error.Text = "Enter intervals from 1–240 and a countdown from 5–300."; return false; }
            try
            {
                save(new Settings { WaterMinutes = w, StretchMinutes = s, BreakSeconds = d, Volume = (int)volume.Value,
                    Muted = muted.IsChecked == true, QuietHours = quiet.IsChecked == true, Movement = (Movement)movement.SelectedItem });
                error.Text = "Saved. Reminder timers restarted."; return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { error.Text = "Could not save settings: " + ex.Message; return false; }
        }
        var buttons = new WrapPanel();
        buttons.Children.Add(Ui.Button("Save", () => { Apply(); }, true));
        buttons.Children.Add(Ui.Button("Test water", () => { if (Apply()) preview(BreakKind.Water); }));
        buttons.Children.Add(Ui.Button("Test stretch", () => { if (Apply()) preview(BreakKind.Stretch); }));
        buttons.Children.Add(Ui.Button("Quit pet", quit));
        panel.Children.Add(buttons);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
