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

internal sealed class SettingsWindow : Window
{
    public SettingsWindow(Settings current, Action<Settings> save, Action<BreakKind> preview, Action quit, Action<CustomReminder> previewCustom)
    {
        Ui.ApplyTheme(this);
        Icon = Ui.Logo;
        Title = "DragonBreak settings"; Width = 510; Height = 740; MinWidth = 450; MinHeight = 600;
        Background = Ui.Background; Foreground = Ui.Foreground; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new StackPanel { Margin = new Thickness(30) };
        panel.Children.Add(new Image { Source = Ui.Logo, Width = 64, Height = 64, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(Ui.Text("Your little dragon.\nYour time to recharge.", 29, Ui.Accent));
        panel.Children.Add(Ui.Text("Right-click the pet or tray icon any time.", 14, Ui.Muted));
        TextBox Field(string label, int value)
        {
            panel.Children.Add(Ui.Text(label, 14));
            var box = new TextBox { Text = value.ToString(), FontSize = 17, Padding = new Thickness(9), Margin = new Thickness(0, 0, 0, 14) };
            panel.Children.Add(box); return box;
        }
        panel.Children.Add(Ui.Text("Water & movement reminder style", 14));
        var style = new ComboBox { ItemsSource = Enum.GetValues<ReminderStyle>(), SelectedItem = current.Style, FontSize = 16, Margin = new Thickness(0, 0, 0, 14) };
        panel.Children.Add(style);
        panel.Children.Add(Ui.Text("Popup stays in a screen corner, stays silent, and keeps keyboard focus in your work. FullScreen uses the current dragon takeover.", 13, Ui.Muted));
        var water = Field("Water reminder · minutes (1–240)", current.WaterMinutes);
        var stretch = Field("Movement reminder · minutes (1–240)", current.StretchMinutes);
        var duration = Field("Break countdown · seconds (5–300)", current.BreakSeconds);
        var sizeLabel = Ui.Text($"Pet size: {current.PetSizePercent}%", 14);
        panel.Children.Add(sizeLabel);
        var petSize = new Slider { Minimum = 50, Maximum = 200, Value = current.PetSizePercent,
            TickFrequency = 10, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 0, 12) };
        petSize.ValueChanged += (_, _) => sizeLabel.Text = $"Pet size: {(int)petSize.Value}%";
        panel.Children.Add(petSize);
        panel.Children.Add(Ui.Text("50% to 200%. Save applies the desktop pet size. Reminder layouts keep their own size.", 13, Ui.Muted));
        panel.Children.Add(Ui.Text("Movement", 14));
        var movement = new ComboBox { ItemsSource = Enum.GetValues<Movement>(), SelectedItem = current.Movement, FontSize = 16, Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(movement);
        panel.Children.Add(Ui.Text("Roar + voice volume", 14));
        var volume = new Slider { Minimum = 0, Maximum = 100, Value = current.Volume, TickFrequency = 10, Margin = new Thickness(0, 0, 0, 18) };
        panel.Children.Add(volume);
        var muted = new CheckBox { Content = "Mute all reminder audio", IsChecked = current.Muted, Foreground = Ui.Foreground, Margin = new Thickness(0, 0, 0, 14) };
        var quiet = new CheckBox { Content = "Quiet hours: 10 pm–8 am (no automatic reminders)", IsChecked = current.QuietHours, Foreground = Ui.Foreground, Margin = new Thickness(0, 0, 0, 20) };
        panel.Children.Add(muted); panel.Children.Add(quiet);
        panel.Children.Add(Ui.Text("Custom reminders", 23, Ui.Accent));
        panel.Children.Add(Ui.Text("Repeat every 1 to 10080 minutes. Each reminder has its own style. Save to apply changes.", 13, Ui.Muted));
        var rows = new StackPanel(); panel.Children.Add(rows);
        var entries = new List<(CustomReminder Reminder, TextBox Message, TextBox Minutes, CheckBox Enabled, ComboBox Style)>();
        void AddReminder(CustomReminder reminder)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            var message = new TextBox { Text = reminder.Message, MaxLength = 240, FontSize = 16, Padding = new Thickness(8), AcceptsReturn = false };
            var minutes = new TextBox { Text = reminder.Minutes.ToString(), Padding = new Thickness(8) };
            var enabled = new CheckBox { Content = "Enabled", IsChecked = reminder.Enabled, Foreground = Ui.Foreground, Margin = new Thickness(0, 8, 0, 8) };
            var mode = new ComboBox { ItemsSource = Enum.GetValues<ReminderStyle>(), SelectedItem = reminder.Style, FontSize = 16 };
            row.Children.Add(Ui.Text("Message", 14)); row.Children.Add(message);
            row.Children.Add(Ui.Text("Repeat interval (minutes)", 14)); row.Children.Add(minutes);
            row.Children.Add(enabled); row.Children.Add(mode);
            var entry = (reminder, message, minutes, enabled, mode); entries.Add(entry);
            row.Children.Add(Ui.Button("Test reminder", () =>
            {
                if (!string.IsNullOrWhiteSpace(message.Text))
                    previewCustom(reminder with { Message = message.Text.Trim(), Style = (ReminderStyle)mode.SelectedItem });
            }));
            row.Children.Add(Ui.Button("Remove reminder", () => { entries.Remove(entry); rows.Children.Remove(row); }));
            rows.Children.Add(row);
        }
        foreach (var reminder in current.CustomReminders) AddReminder(reminder with { });
        panel.Children.Add(Ui.Button("Add custom reminder", () => AddReminder(new CustomReminder())));
        panel.Children.Add(Ui.Text("Pet: Dragon. Additional companions are planned for a future version.", 13, Ui.Muted));
        var error = Ui.Text("", 13, Ui.Error); panel.Children.Add(error);
        bool Apply()
        {
            if (!int.TryParse(water.Text, out int w) || w < 1 || w > 240 || !int.TryParse(stretch.Text, out int s) || s < 1 || s > 240 || !int.TryParse(duration.Text, out int d) || d < 5 || d > 300)
            { error.Text = "Enter intervals from 1–240 and a countdown from 5–300."; return false; }
            var custom = new List<CustomReminder>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Message.Text) || !int.TryParse(entry.Minutes.Text, out int minutes) || minutes < 1 || minutes > 10080)
                { error.Text = "Each custom reminder needs a message and an interval from 1 to 10080 minutes."; return false; }
                custom.Add(entry.Reminder with { Message = entry.Message.Text.Trim(), Minutes = minutes, Enabled = entry.Enabled.IsChecked == true, Style = (ReminderStyle)entry.Style.SelectedItem });
            }
            try
            {
                save(new Settings { PetSizePercent = (int)petSize.Value, Style = (ReminderStyle)style.SelectedItem, CustomReminders = custom, WaterMinutes = w, StretchMinutes = s, BreakSeconds = d, Volume = (int)volume.Value,
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
