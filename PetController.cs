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

internal sealed class PetController : IDisposable
{
    private readonly Application _app;
    private readonly bool _preview;
    private Settings _settings;
    private readonly Schedule _schedule;
    private readonly PetWindow _pet;
    private readonly Forms.NotifyIcon _tray;
    private readonly DispatcherTimer _movement = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly MediaPlayer _audio = new();
    private SettingsWindow? _settingsWindow;
    private BreakWindow? _overlay;
    private BreakKind _kind;
    private DateTimeOffset _breakEnd;
    private bool _paused, _suspended, _quiet, _stopping;
    private string? _nextAudio;
    private double _targetX, _targetY;
    private DateTimeOffset _nextWander;
    private DateTimeOffset _routeStart;
    private Point _route0, _route1, _route2, _route3;
    private int _routeNumber;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private long _lastMovement;
    private int _smokeStep;
    private bool _smoke;
    private NativeRect _smokePosition;

    public PetController(Application app, bool preview)
    {
        _app = app; _preview = preview;
        _settings = preview ? new Settings { Muted = true, QuietHours = false, BreakSeconds = 15 } : Settings.Load();
        _schedule = new Schedule(_settings, DateTimeOffset.Now);
        _pet = new PetWindow(OpenSettings, ContextMenu);
        _pet.Left = SystemParameters.WorkArea.Right - _pet.Width - 25;
        _pet.Top = SystemParameters.WorkArea.Bottom - _pet.Height - 25;
        _tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Information, Text = "DragonBreak · right-click for controls", Visible = !preview };
        _tray.DoubleClick += (_, _) => OpenSettings();
        _tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Right) { var m = ContextMenu(); m.IsOpen = true; } };
        _movement.Tick += (_, _) => MovePet();
        _clock.Tick += (_, _) => Tick();
        _audio.MediaEnded += (_, _) =>
        {
            if (_nextAudio is string file && _overlay != null && !_settings.Muted)
            { _nextAudio = null; Play(file); }
        };
        _audio.MediaFailed += (_, e) =>
        {
            _nextAudio = null;
            if (!_preview) { Directory.CreateDirectory(Settings.Folder); File.WriteAllText(Path.Combine(Settings.Folder, "audio-error.log"), e.ErrorException.Message); }
        };
        SystemEvents.SessionSwitch += SessionChanged;
        SystemEvents.PowerModeChanged += PowerChanged;
    }

    public void Start(string[] args)
    {
        _pet.Show(); _movement.Start(); _clock.Start();
        _smoke = args.Contains("--smoke-test");
        if (_smoke || args.Contains("--preview-water")) ShowBreak(BreakKind.Water);
        else if (args.Contains("--preview-stretch")) ShowBreak(BreakKind.Stretch);
        else OpenSettings();
    }

    private void Play(string name)
    {
        _audio.Open(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", name)));
        _audio.Volume = _settings.Volume / 100d;
        _audio.Play();
    }
    private void StopAudio() { _nextAudio = null; _audio.Stop(); _audio.Close(); }

    private void Tick()
    {
        var now = DateTimeOffset.Now;
        if (_smoke) { SmokeTick(); return; }
        if (_overlay != null)
        {
            int left = (int)Math.Ceiling((_breakEnd - now).TotalSeconds);
            _overlay.Update(left);
            if (left <= 0) Finish(false);
            return;
        }
        bool quiet = _settings.IsQuiet(now);
        if (_paused || _suspended || quiet) { _quiet = quiet; return; }
        if (_quiet) { _quiet = false; _schedule.Reset(_settings, now); }
        if (_schedule.Due(now) is BreakKind kind) ShowBreak(kind);
    }

    private void ShowBreak(BreakKind kind)
    {
        if (_overlay != null || _suspended || _stopping) return;
        _kind = kind;
        _overlay = new BreakWindow(kind, Finish, () => { _settings.Muted = true; StopAudio(); SaveSettings(_settings); });
        var hwnd = new WindowInteropHelper(_pet).Handle;
        var bounds = Forms.Screen.FromHandle(hwnd).Bounds;
        _overlay.SourceInitialized += (_, _) =>
        {
            var overlayHwnd = new WindowInteropHelper(_overlay).Handle;
            SetWindowPos(overlayHwnd, new IntPtr(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0040);
        };
        var shown = _overlay;
        _overlay.Closed += (_, _) => { if (ReferenceEquals(_overlay, shown)) Finish(false); };
        _breakEnd = DateTimeOffset.Now.AddSeconds(_settings.BreakSeconds);
        _overlay.Update(_settings.BreakSeconds);
        _pet.Hide(); _overlay.Show(); _overlay.Activate();
        if (!_settings.Muted && _settings.Volume > 0)
        {
            _nextAudio = kind == BreakKind.Water ? "water.wav" : "stretch.wav";
            Play("roar.wav");
        }
    }

    private void Finish(bool snooze)
    {
        if (_overlay == null) return;
        var window = _overlay; _overlay = null;
        StopAudio(); window.Close();
        _schedule.Finish(_kind, snooze, _settings, DateTimeOffset.Now);
        if (!_stopping && !_suspended) _pet.Show();
        if (_preview && !_smoke) Quit();
    }

    private void OpenSettings()
    {
        if (_settingsWindow != null) { _settingsWindow.Activate(); return; }
        _settingsWindow = new SettingsWindow(_settings, SaveSettings, ShowBreak, Quit);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void SaveSettings(Settings settings)
    {
        var validated = settings.Validate();
        if (!_preview) validated.Save();
        _settings = validated;
        _nextWander = DateTimeOffset.MinValue;
        _schedule.Reset(_settings, DateTimeOffset.Now);
        _audio.Volume = _settings.Volume / 100d;
        if (_settings.Muted) StopAudio();
    }

    private ContextMenu ContextMenu()
    {
        var menu = new ContextMenu();
        void Add(string name, Action action) { var item = new MenuItem { Header = name }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Add("Settings…", OpenSettings);
        Add(_paused ? "Resume reminders and movement" : "Pause reminders and movement", () => { _paused = !_paused; _schedule.Reset(_settings, DateTimeOffset.Now); if (_paused) Finish(false); });
        Add("Test water reminder", () => ShowBreak(BreakKind.Water));
        Add("Test stretch reminder", () => ShowBreak(BreakKind.Stretch));
        foreach (Movement mode in Enum.GetValues<Movement>())
            Add((mode == _settings.Movement ? "✓ " : "") + mode, () => { _settings.Movement = mode; SaveSettings(_settings); });
        Add(_settings.Muted ? "Unmute" : "Mute", () => { _settings.Muted = !_settings.Muted; SaveSettings(_settings); });
        Add("Quit DragonBreak", Quit);
        return menu;
    }

    private void MovePet()
    {
        long elapsed = _elapsed.ElapsedMilliseconds;
        double seconds = Math.Min(.1, (elapsed - _lastMovement) / 1000d); _lastMovement = elapsed;
        if (_paused || _suspended || _overlay != null || _pet.Dragging || _pet.IsMouseOver || !_pet.IsVisible) return;
        var hwnd = new WindowInteropHelper(_pet).Handle;
        if (!GetWindowRect(hwnd, out var rect)) return;
        var cursor = Forms.Cursor.Position;
        var screen = _settings.Movement == Movement.Follow ? Forms.Screen.FromPoint(cursor) : Forms.Screen.FromHandle(hwnd);
        var area = screen.WorkingArea;
        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        double right = Math.Max(area.Left, area.Right - width), bottom = Math.Max(area.Top, area.Bottom - height);
        if (_settings.Movement == Movement.Stay)
        {
            SetWindowPos(hwnd, IntPtr.Zero, (int)Math.Clamp(rect.Left, area.Left, right), (int)Math.Clamp(rect.Top, area.Top, bottom), 0, 0, 0x0015);
            return;
        }
        if (_settings.Movement == Movement.Follow) { _targetX = cursor.X + 35; _targetY = cursor.Y + 30; }
        else
        {
            var now = DateTimeOffset.Now;
            if (now >= _nextWander)
            {
                double rangeX = right - area.Left, rangeY = bottom - area.Top;
                _route0 = new Point(rect.Left, rect.Top);
                // Alternate broad edge-to-centre arcs, then bank out toward another corner.
                _route3 = _routeNumber % 2 == 0
                    ? new Point(area.Left + rangeX * .5, area.Top + rangeY * .48)
                    : new Point(area.Left + rangeX * (_routeNumber % 4 == 1 ? .05 : .95), area.Top + rangeY * (_routeNumber % 6 == 1 ? .08 : .88));
                _route1 = new Point(area.Left + rangeX * (_routeNumber % 2 == 0 ? .05 : .95), area.Top + rangeY * .08);
                _route2 = new Point(area.Left + rangeX * (_routeNumber % 2 == 0 ? .95 : .05), area.Top + rangeY * .90);
                _routeStart = now; _nextWander = now.AddSeconds(9); _routeNumber++;
            }
            double t = Math.Clamp((now - _routeStart).TotalSeconds / 9, 0, 1), inv = 1 - t;
            _targetX = inv * inv * inv * _route0.X + 3 * inv * inv * t * _route1.X + 3 * inv * t * t * _route2.X + t * t * t * _route3.X;
            _targetY = inv * inv * inv * _route0.Y + 3 * inv * inv * t * _route1.Y + 3 * inv * t * t * _route2.Y + t * t * t * _route3.Y;
        }
        _targetX = Math.Clamp(_targetX, area.Left, right); _targetY = Math.Clamp(_targetY, area.Top, bottom);
        double dx = _targetX - rect.Left, dy = _targetY - rect.Top, distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < 1) return;
        if (Math.Abs(dx) > 1) _pet.Model.FacingRight = dx > 0;
        _pet.Model.Bank = -Math.Atan2(dy, Math.Max(1, Math.Abs(dx))) * 180 / Math.PI * .4;
        double step = Math.Min(distance, (_settings.Movement == Movement.Follow ? 500 : 300) * seconds);
        SetWindowPos(hwnd, IntPtr.Zero, (int)(rect.Left + dx / distance * step), (int)(rect.Top + dy / distance * step), 0, 0, 0x0015);
    }

    private void SessionChanged(object sender, SessionSwitchEventArgs e) => _app.Dispatcher.BeginInvoke(() =>
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.RemoteDisconnect or SessionSwitchReason.ConsoleDisconnect)
        { _suspended = true; Finish(false); _pet.Hide(); }
        else if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.RemoteConnect or SessionSwitchReason.ConsoleConnect)
        { _suspended = false; _schedule.Reset(_settings, DateTimeOffset.Now); _pet.Show(); }
    });
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) => _app.Dispatcher.BeginInvoke(() =>
    {
        if (e.Mode == PowerModes.Suspend) { _suspended = true; Finish(false); }
        if (e.Mode == PowerModes.Resume) { _suspended = false; _schedule.Reset(_settings, DateTimeOffset.Now); }
    });

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
                _settingsWindow.Close();
                _settings.Movement = Movement.Wander; _nextWander = DateTimeOffset.MinValue;
                GetWindowRect(new WindowInteropHelper(_pet).Handle, out _smokePosition);
            }
            if (_smokeStep == 9)
            {
                GetWindowRect(new WindowInteropHelper(_pet).Handle, out var current);
                if (current.Left == _smokePosition.Left && current.Top == _smokePosition.Top) throw new Exception("Curved flight did not move the pet");
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
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-test.log"), "PASS: continuously animated 3D geometry; water overlay; Snooze click; five-minute reschedule; stretch overlay; Escape dismissal; pet restored; settings opened; curved flight; pause; pointer-follow movement. Audio muted during UI test.\n");
                Quit();
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-test.log"), "FAIL: " + ex);
            Quit();
        }
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
    private static DragonModel FindModel(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is DragonModel model) return model;
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
    private void Quit() { _stopping = true; Finish(false); _app.Shutdown(); }
    public void Dispose()
    {
        _stopping = true; _movement.Stop(); _clock.Stop(); StopAudio();
        SystemEvents.SessionSwitch -= SessionChanged; SystemEvents.PowerModeChanged -= PowerChanged;
        _tray.Visible = false; _tray.Dispose();
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
