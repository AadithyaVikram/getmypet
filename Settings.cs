using System;
using System.IO;
using System.Text.Json;

namespace DragonBreak;

public enum Movement { Wander, Follow, Stay }
public enum BreakKind { Water, Stretch }

public sealed record Settings
{
    public int WaterMinutes { get; set; } = 45;
    public int StretchMinutes { get; set; } = 60;
    public int BreakSeconds { get; set; } = 30;
    public int Volume { get; set; } = 40;
    public bool Muted { get; set; }
    public bool QuietHours { get; set; } = true;
    public Movement Movement { get; set; } = Movement.Wander;
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragonBreak");
    public static string FilePath => Path.Combine(Folder, "settings.json");

    public Settings Validate() => this with
    {
        WaterMinutes = Math.Clamp(WaterMinutes, 1, 240),
        StretchMinutes = Math.Clamp(StretchMinutes, 1, 240),
        BreakSeconds = Math.Clamp(BreakSeconds, 5, 300),
        Volume = Math.Clamp(Volume, 0, 100),
        Movement = Enum.IsDefined(Movement) ? Movement : Movement.Wander
    };

    public static Settings Load()
    {
        try { return (JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings()).Validate(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new Settings(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Validate(), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }

    public bool IsQuiet(DateTimeOffset now) => QuietHours && (now.LocalDateTime.Hour >= 22 || now.LocalDateTime.Hour < 8);
}

public sealed class Schedule
{
    public DateTimeOffset WaterDue { get; private set; }
    public DateTimeOffset StretchDue { get; private set; }
    public Schedule(Settings settings, DateTimeOffset now) => Reset(settings, now);
    public void Reset(Settings s, DateTimeOffset now)
    {
        WaterDue = now.AddMinutes(s.WaterMinutes);
        StretchDue = now.AddMinutes(s.StretchMinutes);
    }
    public BreakKind? Due(DateTimeOffset now) => now < WaterDue && now < StretchDue ? null
        : WaterDue <= StretchDue ? BreakKind.Water : BreakKind.Stretch;
    public void Finish(BreakKind kind, bool snooze, Settings s, DateTimeOffset now)
    {
        if (kind == BreakKind.Water) WaterDue = now.AddMinutes(snooze ? 5 : s.WaterMinutes);
        else StretchDue = now.AddMinutes(snooze ? 5 : s.StretchMinutes);
        // Give the user space when water and stretch become due together.
        if (WaterDue < now.AddMinutes(2)) WaterDue = now.AddMinutes(2);
        if (StretchDue < now.AddMinutes(2)) StretchDue = now.AddMinutes(2);
    }
}
