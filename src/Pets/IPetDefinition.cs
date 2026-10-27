using System.Windows;

namespace DragonBreak;

// Each instance owns its view and animation state. The shared host controls position and direction.
internal interface IPetVisual
{
    bool FacingRight { get; set; }
    double Bank { get; set; }
    double TravelHeadingDegrees { get; set; }
    int PoseUpdates { get; }
}

internal sealed record PetPresentation(FrameworkElement View, IPetVisual Visual);

internal interface IPetDefinition
{
    string Id { get; }
    string DisplayName { get; }
    string? IntroAudio { get; }
    string? ReminderAudio(BreakKind kind);
    PetPresentation CreatePresentation(double width, bool reminder);
}