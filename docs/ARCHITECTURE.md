# Project layout and adding pets

```text
src/
  App/                 Entry point, desktop lifecycle and controller
  Configuration/       Saved preferences; existing LocalAppData location retained
  Reminders/           Reminder records and scheduler (no renderer dependency)
  UI/                  Shared controls, settings, pet host and reminder windows
  Pets/
    IPetDefinition.cs  Pet visuals, presentation and optional audio contract
    PetCatalog.cs      Available definitions and current default
    Dragon/
      DragonPet.cs     Presentation, flame effects and audio mapping
      DragonModel.cs   Dragon mesh and animation
      Assets/Audio/   Runtime WAV files
      Assets/References/  Concept art, excluded from runtime output
      Exports/        Static GLB, screenshot and validator result
Assets/Brand/          Shared app logo
tests/                 Self-checks and muted audio checks
tools/                 Dragon model viewer and generic GLB exporter
scripts/               Build, install, audio generation and profiling
```

The root project automatically includes C# files in these folders. Namespaces and executable identity remain `DragonBreak`; folder placement separates ownership without changing saved settings or launcher commands. Test and tool entry modes remain in the same assembly.

`PetController` and `PetWindow` consume `IPetDefinition` and `IPetVisual`, not the dragon model. Each presentation creates an independent WPF view and animation state. The host supplies movement direction, screen heading and banking; the pet owns appearance and effects. Reminder scheduling and custom text remain shared. Compact popups do not create pet models.

To add a cat or dog:

1. Add `src/Pets/Cat/` (or `Dog/`) with an `IPetDefinition` implementation and a visual implementing `IPetVisual`. Create a new view on every `CreatePresentation` call; a WPF element cannot have two parents. Stop animation work when the view is hidden/unloaded, as the dragon does.
2. Keep that pet's audio under `Assets/Audio/`, concept art under `Assets/References/`, and deliberate exports under `Exports/` within its folder. Runtime audio is automatically copied to `Pets/<name>/Assets/Audio/`; return paths relative to the executable directory. Audio properties may return null for silent pets.
3. Register the definition in `PetCatalog.All`. Add a saved pet ID and a selector when multiple pets actually ship; the dragon remains the default in this iteration.
4. Keep pet-specific effects in that implementation. Shared UI must use `CreatePresentation`; it should not construct a concrete model. The dragon development viewer may access its concrete model for camera and mesh export.
5. Run `dragonbreak.cmd test`, and verify the new pet in its host and full-screen presentation. Preserve popup focus behavior, snooze, quiet hours, lock/suspend handling and audio preferences.

No cat/dog assets, pet selection UI, renderer plugin loading or animated GLB importer are included yet. Dragon exports remain static poses.
`src/App/EdgeWander.cs` owns varied outer-band routing independently of WPF windows. Its clock advances only during active wandering; manual repositioning and working-area changes replan toward the outer band. `tests/WanderTests.cs` simulates routes on normal, negative-coordinate and degenerate working areas, checking containment, speed, pause behavior and centre occupancy.

The desktop host keeps a square rotation area. Screen heading uses rotation only; it must not introduce angle-dependent scale changes. Keep geometry within that fixed area and check transparent edge clearance in rendered previews.
