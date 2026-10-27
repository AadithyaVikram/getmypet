using System.Windows;

namespace DragonBreak;

internal sealed class CatPet(bool use3D = false) : IPetDefinition
{
    public string Id => use3D ? "cat-3d" : "cat";
    public string DisplayName => use3D ? "Orange cat (3D preview)" : "Orange cat (2D)";
    public PetLocomotion Locomotion => PetLocomotion.Ground;
    public string? IntroAudio => null;
    public string? ReminderAudio(BreakKind kind) => null;
    public PetPresentation CreatePresentation(double width, bool reminder)
    {
        var activity = reminder ? PetActivity.Sitting : PetActivity.Walking;
        if (use3D)
        {
            var model = new CatModel { Width = width, Height = width, Reminder = reminder, Activity = activity };
            return new(model, model);
        }
        var cat = new Cat2D { Width = width, Height = width, Reminder = reminder, Activity = activity };
        return new(cat, cat);
    }
}