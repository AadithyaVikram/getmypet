using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace DragonBreak;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        if (args.Contains("--self-test")) return SelfTest.Run();
        if (args.Contains("--audio-test")) return AudioTest.Run();
        if (args.Contains("--model-preview")) return new Application().Run(new ModelPreview());
        bool preview = args.Any(a => a.StartsWith("--preview", StringComparison.Ordinal)) || args.Contains("--smoke-test");
        using var single = new Mutex(true, "Local\\DragonBreak.DesktopPet", out bool first);
        if (!first && !preview)
        {
            MessageBox.Show("DragonBreak is already running. Right-click the dragon or its tray icon for settings.", "DragonBreak");
            return 0;
        }
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            using var controller = new PetController(app, preview);
            controller.Start(args);
            return app.Run();
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Settings.Folder);
            File.WriteAllText(Path.Combine(Settings.Folder, "error.log"), ex.ToString());
            MessageBox.Show("DragonBreak could not start. Details are in " + Path.Combine(Settings.Folder, "error.log"), "DragonBreak");
            return 1;
        }
    }
}

internal static class SelfTest
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
        try
        {
            var s = new Settings { WaterMinutes = 1, StretchMinutes = 1 };
            var now = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
            var timer = new Schedule(s, now);
            Check(timer.Due(now) == null, "No immediate reminder");
            Check(timer.Due(now.AddMinutes(1)) == BreakKind.Water, "Water wins equal deadlines");
            timer.Finish(BreakKind.Water, true, s, now.AddMinutes(1));
            Check(timer.WaterDue == now.AddMinutes(6), "Five-minute snooze");
            Check(timer.StretchDue == now.AddMinutes(3), "No stacked reminders");
            Check(timer.Due(now.AddMinutes(2)) == null, "Grace period");
            Check(timer.Due(now.AddMinutes(3)) == BreakKind.Stretch, "Stretch remains scheduled");
            timer.Reset(s, now.AddHours(8));
            Check(timer.Due(now.AddHours(8)) == null, "Resume resets elapsed reminders");
            var valid = new Settings { WaterMinutes = -1, StretchMinutes = 999, Volume = 500, BreakSeconds = 0, Movement = (Movement)99 }.Validate();
            Check(valid.WaterMinutes == 1 && valid.StretchMinutes == 240, "Interval bounds");
            Check(valid.Volume == 100 && valid.BreakSeconds == 5 && valid.Movement == Movement.Wander, "Other setting bounds");
            var roundTrip = System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(s));
            Check(roundTrip == s, "Settings round trip");
            var model = new DragonModel();
            Check(model.PoseUpdates > 0, "Live 3D geometry initializes");
            foreach (string name in new[] { "water.wav", "stretch.wav", "roar.wav" })
            {
                using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", name));
                using var reader = new BinaryReader(stream);
                Check(new string(reader.ReadChars(4)) == "RIFF" && stream.Length > 1000, name + " valid WAV");
            }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.log"), $"PASS: {count} checks.\n");
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.log"), "FAIL: " + ex);
            return 1;
        }
    }
}
