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

internal sealed partial class PetController : IDisposable
{
    private readonly Application _app;
    private readonly bool _preview;
    private Settings _settings;
    private readonly Schedule _schedule;
    private readonly PetWindow _pet;
    private readonly IPetDefinition _definition = PetCatalog.Default;
    private readonly Forms.NotifyIcon _tray;
    private readonly DispatcherTimer _movement = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly MediaPlayer _audio = new();
    private SettingsWindow? _settingsWindow;
    private BreakWindow? _overlay;
    private ReminderRequest? _request;
    private DateTimeOffset _breakEnd;
    private bool _paused, _suspended, _quiet, _stopping;
    private string? _nextAudio;
    private double _targetX, _targetY;
    private readonly EdgeWander _wander = new();
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
        _pet = new PetWindow(OpenSettings, ContextMenu, _definition);
        _pet.ApplySize(_settings.PetSizePercent);
        _pet.SourceInitialized += (_, _) => ResizePet();
        _pet.Left = SystemParameters.WorkArea.Right - _pet.Width - 25;
        _pet.Top = SystemParameters.WorkArea.Bottom - _pet.Height - 25;
        _tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "app.ico")), Text = "DragonBreak · right-click for controls", Visible = !preview };
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
        _audio.Open(new Uri(Path.Combine(AppContext.BaseDirectory, name)));
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
        if (_schedule.Next(_settings, now) is ReminderRequest request) ShowReminder(request);
    }

    private void ShowBreak(BreakKind kind)
    {
        ShowReminder(_schedule.BuiltIn(kind, _settings));
    }

    private void ShowReminder(ReminderRequest request)
    {
        if (_overlay != null || _suspended || _stopping) return;
        _request = request;
        bool popup = request.Style == ReminderStyle.Popup;
        _overlay = new BreakWindow(request, Finish, () => { _settings.Muted = true; StopAudio(); SaveSettings(_settings); }, _definition);
        var hwnd = new WindowInteropHelper(_pet).Handle;
        var screen = Forms.Screen.FromHandle(hwnd);
        var bounds = popup ? screen.WorkingArea : screen.Bounds;
        _overlay.SourceInitialized += (_, _) =>
        {
            var overlayHwnd = new WindowInteropHelper(_overlay).Handle;
            if (popup)
            {
                var source = PresentationSource.FromVisual(_overlay);
                double scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1;
                int width = Math.Min(bounds.Width, (int)(420 * scale)), height = Math.Min(bounds.Height, (int)(330 * scale));
                SetWindowPos(overlayHwnd, new IntPtr(-1), bounds.Right - width, bounds.Bottom - height, width, height, 0x0050);
                int style = GetWindowLong(overlayHwnd, -20);
                SetWindowLong(overlayHwnd, -20, style | 0x08000000 | 0x00000080);
            }
            else SetWindowPos(overlayHwnd, new IntPtr(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0040);
        };
        var shown = _overlay;
        _overlay.Closed += (_, _) => { if (ReferenceEquals(_overlay, shown)) Finish(false); };
        _breakEnd = DateTimeOffset.Now.AddSeconds(_settings.BreakSeconds);
        _overlay.Update(_settings.BreakSeconds);
        if (!popup) _pet.Hide();
        _overlay.Show(); if (!popup) _overlay.Activate();
        if (!popup && request.Kind != null && !_settings.Muted && _settings.Volume > 0)
        {
            _nextAudio = _definition.ReminderAudio(request.Kind.Value);
            if (_definition.IntroAudio is string intro) Play(intro);
            else if (_nextAudio is string speech) { _nextAudio = null; Play(speech); }
        }
    }

    private void Finish(bool snooze)
    {
        if (_overlay == null) return;
        var window = _overlay; _overlay = null;
        StopAudio(); window.Close();
        if (_request != null) _schedule.Finish(_request, snooze, _settings, DateTimeOffset.Now);
        _request = null;
        if (!_stopping && !_suspended) _pet.Show();
        if (_preview && !_smoke) Quit();
    }

    private void OpenSettings()
    {
        if (_settingsWindow != null) { _settingsWindow.Activate(); return; }
        _settingsWindow = new SettingsWindow(_settings, SaveSettings, ShowBreak, Quit, r => ShowReminder(new(null, r.Id, "A little reminder", r.Message, r.Style)));
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void SaveSettings(Settings settings)
    {
        var validated = settings.Validate();
        if (!_preview) validated.Save();
        _settings = validated;
        ResizePet();
        _wander.Reset();
        _schedule.Reset(_settings, DateTimeOffset.Now);
        _audio.Volume = _settings.Volume / 100d;
        if (_settings.Muted) StopAudio();
    }

    private void ResizePet()
    {
        var hwnd = new WindowInteropHelper(_pet).Handle;
        if (hwnd == IntPtr.Zero) { _pet.ApplySize(_settings.PetSizePercent); return; }
        GetWindowRect(hwnd, out var before);
        var area = Forms.Screen.FromHandle(hwnd).WorkingArea;
        _pet.ApplySize(_settings.PetSizePercent);
        _pet.UpdateLayout();
        var transform = PresentationSource.FromVisual(_pet)?.CompositionTarget?.TransformToDevice;
        int width = (int)Math.Round(_pet.Width * (transform?.M11 ?? 1));
        int height = (int)Math.Round(_pet.Height * (transform?.M22 ?? 1));
        int x = Math.Clamp(before.Left + (before.Right - before.Left - width) / 2, area.Left, Math.Max(area.Left, area.Right - width));
        int y = Math.Clamp(before.Top + (before.Bottom - before.Top - height) / 2, area.Top, Math.Max(area.Top, area.Bottom - height));
        SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, 0x0014);
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
        if (_settings.Movement != Movement.Wander) _wander.Reset();
        if (_settings.Movement == Movement.Stay)
        {
            SetWindowPos(hwnd, IntPtr.Zero, (int)Math.Clamp(rect.Left, area.Left, right), (int)Math.Clamp(rect.Top, area.Top, bottom), 0, 0, 0x0015);
            return;
        }
        if (_settings.Movement == Movement.Follow) { _targetX = cursor.X + 35; _targetY = cursor.Y + 30; }
        else
        {
            var next = _wander.Advance(new Point(rect.Left, rect.Top), new Rect(area.Left, area.Top, right - area.Left, bottom - area.Top), seconds);
            double wanderDx = next.X - rect.Left, wanderDy = next.Y - rect.Top;
            OrientPet(wanderDx, wanderDy);
            SetWindowPos(hwnd, IntPtr.Zero, (int)Math.Round(next.X), (int)Math.Round(next.Y), 0, 0, 0x0015);
            return;
        }
        _targetX = Math.Clamp(_targetX, area.Left, right); _targetY = Math.Clamp(_targetY, area.Top, bottom);
        double dx = _targetX - rect.Left, dy = _targetY - rect.Top, distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < 1) return;
        OrientPet(dx, dy);
        double step = Math.Min(distance, (_settings.Movement == Movement.Follow ? 500 : 300) * seconds);
        SetWindowPos(hwnd, IntPtr.Zero, (int)(rect.Left + dx / distance * step), (int)(rect.Top + dy / distance * step), 0, 0, 0x0015);
    }

    private void OrientPet(double dx, double dy)
    {
        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < .1) return;
        // Preserve facing near vertical so tiny horizontal rounding errors cannot flip the model.
        if (Math.Abs(dx) > distance * .2) _pet.Model.FacingRight = dx > 0;
        _pet.Model.TravelHeadingDegrees = PetHeading.FromVelocity(dx, dy);
        _pet.Model.Bank = 0;
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

    private void Quit() { _stopping = true; Finish(false); _app.Shutdown(); }
    public void Dispose()
    {
        _stopping = true; _movement.Stop(); _clock.Stop(); StopAudio();
        SystemEvents.SessionSwitch -= SessionChanged; SystemEvents.PowerModeChanged -= PowerChanged;
        _tray.Visible = false; _tray.Dispose();
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
