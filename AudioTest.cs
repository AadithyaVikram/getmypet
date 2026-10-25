using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace DragonBreak;

internal static class AudioTest
{
    public static int Run()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var player = new MediaPlayer { Volume = 0 };
        string[] clips = { "roar.wav", "water.wav", "stretch.wav" };
        int index = 0, opened = 0, exit = 1;
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
        void Finish(bool success, string detail)
        {
            timeout.Stop(); player.Close(); exit = success ? 0 : 1;
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "audio-test.log"), (success ? "PASS: " : "FAIL: ") + detail + "\n");
            app.Shutdown(exit);
        }
        void Play() { player.Open(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", clips[index]))); player.Play(); }
        player.MediaOpened += (_, _) => opened++;
        player.MediaFailed += (_, e) => Finish(false, e.ErrorException.Message);
        player.MediaEnded += (_, _) =>
        {
            index++;
            if (index == clips.Length) Finish(opened == clips.Length, "Roar, water speech and stretch speech decoded and played to completion at muted volume.");
            else Play();
        };
        timeout.Tick += (_, _) => Finish(false, "Audio playback timed out");
        timeout.Start(); Play(); app.Run(); return exit;
    }
}
