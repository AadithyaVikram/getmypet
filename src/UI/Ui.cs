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

internal static class Ui
{
    public static BitmapImage Logo => new(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "app.ico")));
    // Shared app palette: warm paper, plum, and copper.
    public static Brush Background => new SolidColorBrush(Color.FromRgb(250, 244, 235));
    public static Brush Foreground => new SolidColorBrush(Color.FromRgb(56, 38, 56));
    public static Brush Accent => new SolidColorBrush(Color.FromRgb(155, 71, 46));
    public static Brush Muted => new SolidColorBrush(Color.FromRgb(116, 97, 107));
    public static Brush Surface => new SolidColorBrush(Color.FromRgb(239, 226, 221));
    public static Brush Outline => new SolidColorBrush(Color.FromRgb(204, 183, 187));
    public static Brush Error => new SolidColorBrush(Color.FromRgb(164, 34, 48));

    public static void ApplyTheme(Window window)
    {
        window.Background = Background; window.Foreground = Foreground;
        window.Resources[SystemColors.HighlightBrushKey] = Accent;
        window.Resources[SystemColors.HighlightTextBrushKey] = Brushes.White;
        var input = new Style(typeof(TextBox));
        input.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
        input.Setters.Add(new Setter(Control.ForegroundProperty, Foreground));
        input.Setters.Add(new Setter(Control.BorderBrushProperty, Outline));
        input.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        input.Setters.Add(new Setter(TextBox.CaretBrushProperty, Accent));
        window.Resources[typeof(TextBox)] = input;
        var choice = new Style(typeof(ComboBox));
        choice.Setters.Add(new Setter(Control.BackgroundProperty, Surface));
        choice.Setters.Add(new Setter(Control.ForegroundProperty, Foreground));
        choice.Setters.Add(new Setter(Control.BorderBrushProperty, Outline));
        window.Resources[typeof(ComboBox)] = choice;
    }

    public static TextBlock Text(string text, double size = 16, Brush? color = null) => new()
    {
        Text = text, FontSize = size, Foreground = color ?? Foreground,
        FontFamily = new FontFamily("Segoe UI"), TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 12)
    };
    public static Button Button(string title, Action action, bool primary = false)
    {
        var b = new Button
        {
            Content = title, FontSize = 15, Padding = new Thickness(18, 11, 18, 11),
            Margin = new Thickness(0, 0, 10, 8), Cursor = Cursors.Hand,
            Background = primary ? Foreground : Surface,
            Foreground = primary ? Brushes.White : Foreground,
            BorderThickness = new Thickness(0), MinHeight = 42
        };
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, Accent);
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, .83));
        template.Triggers.Add(hover);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));
        template.Triggers.Add(focus);
        b.Template = template;
        b.Click += (_, _) => action();
        return b;
    }
}
