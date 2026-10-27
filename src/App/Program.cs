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
            MessageBox.Show("DragonBreak is already running. Right-click the pet or its tray icon for settings.", "DragonBreak");
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
