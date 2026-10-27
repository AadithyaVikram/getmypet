using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DragonBreak;

internal sealed partial class PetController
{
    private void SmokeTick()
    {
        try
        {
            _smokeStep++;
            if (_smokeStep == 2)
            {
                if (_overlay == null) throw new Exception("Water overlay missing");
                if (FindModel(_overlay).PoseUpdates < 3) throw new Exception("Dragon geometry is not animating");
                Capture(_overlay, "water-preview.png");
                FindButton(_overlay, "Snooze 5 min").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (_overlay != null || (_schedule.WaterDue - DateTimeOffset.Now).TotalMinutes < 4.9) throw new Exception("Snooze did not close and reschedule");
            }
            if (_smokeStep == 3) ShowBreak(BreakKind.Stretch);
            if (_smokeStep == 5)
            {
                if (_overlay == null) throw new Exception("Stretch overlay missing");
                Capture(_overlay, "stretch-preview.png");
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(_overlay), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                _overlay.RaiseEvent(key);
                if (_overlay != null) throw new Exception("Escape did not dismiss");
            }
            if (_smokeStep == 6)
            {
                if (!_pet.IsVisible) throw new Exception("Pet did not return");
                OpenSettings();
            }
            if (_smokeStep == 7)
            {
                if (_settingsWindow == null) throw new Exception("Settings missing");
                Capture(_settingsWindow, "settings-preview.png");
                OrientPet(0, -100);
                if (_pet.Model.TravelHeadingDegrees != -90) throw new Exception("Upward heading not applied");
                OrientPet(0, 100);
                if (_pet.Model.TravelHeadingDegrees != 90) throw new Exception("Downward heading not applied");
                OrientPet(100, 100);
                if (_pet.Model.TravelHeadingDegrees != 45) throw new Exception("Diagonal heading not applied");
                _settings.Movement = Movement.Stay;
                _settings.PetSizePercent = 50; ResizePet();
                GetWindowRect(new WindowInteropHelper(_pet).Handle, out var small);
                _settings.PetSizePercent = 200; ResizePet();
                GetWindowRect(new WindowInteropHelper(_pet).Handle, out var large);
                if (large.Right - large.Left <= small.Right - small.Left || large.Bottom - large.Top <= small.Bottom - small.Top)
                    throw new Exception("Pet size did not change");
                var area = Forms.Screen.FromHandle(new WindowInteropHelper(_pet).Handle).WorkingArea;
                if (large.Left < area.Left || large.Top < area.Top || large.Right > area.Right || large.Bottom > area.Bottom)
                    throw new Exception("Resized pet escaped working area");
                Capture(_pet, "large-pet-preview.png");
                _settings.PetSizePercent = 100; ResizePet();
                _settingsWindow.Close();
                _settings.Movement = Movement.Wander; _wander.Reset();
                GetWindowRect(new WindowInteropHelper(_pet).Handle, out _smokePosition);
            }
            if (_smokeStep == 9)
            {
                GetWindowRect(new WindowInteropHelper(_pet).Handle, out var current);
                if (current.Left == _smokePosition.Left && current.Top == _smokePosition.Top) throw new Exception("Edge wandering did not move the pet");
                Capture(_pet, "pet-preview.png");
                _paused = true; _smokePosition = current;
            }
            if (_smokeStep == 10)
            {
                GetWindowRect(new WindowInteropHelper(_pet).Handle, out var current);
                if (current.Left != _smokePosition.Left || current.Top != _smokePosition.Top) throw new Exception("Pause did not stop flight");
                _paused = false; _settings.Movement = Movement.Follow;
            }
            if (_smokeStep == 12)
            {
                GetWindowRect(new WindowInteropHelper(_pet).Handle, out var current);
                if (current.Left == _smokePosition.Left && current.Top == _smokePosition.Top) throw new Exception("Follow did not move the pet");
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-test.log"), "PASS: continuously animated 3D geometry; water overlay; Snooze click; five-minute reschedule; stretch overlay; Escape dismissal; pet restored; settings opened; pet size 50/200 percent and screen bounds; edge wandering; pause; pointer-follow movement. Audio muted during UI test.\n");
            }
            if (_smokeStep == 13)
            {
                var reminder = new CustomReminder { Message = "Look away from the screen", Minutes = 10 };
                _settings.CustomReminders.Add(reminder);
                _schedule.Reset(_settings, DateTimeOffset.Now);
                ShowReminder(new(null, reminder.Id, "A little reminder", reminder.Message, ReminderStyle.Popup));
            }
            if (_smokeStep == 15)
            {
                if (_overlay == null || _overlay.IsActive || !_pet.IsVisible || _overlay.ShowActivated)
                    throw new Exception($"Popup state: missing={_overlay == null}, active={_overlay?.IsActive}, petVisible={_pet.IsVisible}, showActivated={_overlay?.ShowActivated}");
                GetWindowRect(new WindowInteropHelper(_overlay).Handle, out var popupRect);
                var area = Forms.Screen.FromHandle(new WindowInteropHelper(_pet).Handle).WorkingArea;
                if (popupRect.Left < area.Left || popupRect.Top < area.Top || popupRect.Right > area.Right || popupRect.Bottom > area.Bottom)
                    throw new Exception("Popup outside working area");
                Capture(_overlay, "popup-preview.png");
                FindButton(_overlay, "Snooze 5 min").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (_overlay != null) throw new Exception("Popup snooze failed");
                if (_schedule.Next(_settings, DateTimeOffset.Now.AddMinutes(4)) != null) throw new Exception("Popup snooze too short");
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "smoke-test.log"), "PASS: custom popup; no activation; pet stays visible; monitor working-area placement; popup snooze.\n");
                _paused = true;
                OrientPet(0, -100);
            }
            if (_smokeStep == 17)
            {
                if (_pet.Model is not DragonModel model) throw new Exception("Expected dragon for rendering check");
                var viewport = (Viewport3D)model.Children[0];
                double actual = ((RotateTransform)viewport.RenderTransform).Angle;
                double expected = PetHeading.Relative(-90, model.FacingRight);
                if (Math.Abs(PetHeading.Delta(actual, expected)) > 2) throw new Exception("Rendered dragon did not turn vertically");
                AssertPetClearance();
                Capture(_pet, "vertical-pet-preview.png");
                OrientPet(100, 100);
            }
            if (_smokeStep == 19)
            {
                var model = (DragonModel)_pet.Model;
                var viewport = (Viewport3D)model.Children[0];
                double actual = ((RotateTransform)viewport.RenderTransform).Angle;
                if (Math.Abs(PetHeading.Delta(actual, 45)) > 2) throw new Exception("Rendered dragon did not turn diagonally");
                AssertPetClearance();
                Capture(_pet, "diagonal-pet-preview.png");
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "smoke-test.log"), "PASS: rendered vertical and diagonal travel orientation with fixed scale and unclipped host.\n");
                Quit();
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-test.log"), "FAIL: " + ex);
            Quit();
        }
    }
    private void AssertPetClearance()
    {
        // Rotation alone must neither resize the host nor clip rendered pixels at its edges.
        if (Math.Abs(_pet.ActualWidth - _pet.ActualHeight) > 1) throw new Exception("Rotation host must remain square");
        int width = (int)_pet.ActualWidth, height = (int)_pet.ActualHeight;
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(_pet);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if ((x < 2 || y < 2 || x >= width - 2 || y >= height - 2) && pixels[(y * width + x) * 4 + 3] > 8)
                    throw new Exception("Rotated dragon reaches the host boundary");
    }

    private static Button FindButton(DependencyObject parent, string title)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button button && Equals(button.Content, title)) return button;
            try { return FindButton(child, title); } catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException("Button missing: " + title);
    }
    private static IPetVisual FindModel(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is IPetVisual model) return model;
            try { return FindModel(child); } catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException("Dragon model missing");
    }
    private static void Capture(Window window, string filename)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(AppContext.BaseDirectory, filename)); png.Save(file);
    }
}
