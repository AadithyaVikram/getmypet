# 3D and desktop handoff

## Outcome the user wants

A faithful anime-style Shenron desktop companion: green scales, golden belly, red eyes, antlers, whiskers and clawed legs. It should fly with continuous body and tail articulation, coil and turn through the screen, travel primarily along screen edges with rare brief crossings, and follow the pointer optionally. Water and stretch breaks enlarge it over the current monitor, add fire and a low roar, then speak the reminder. The user wants low CPU, GPU, RAM, battery, and download cost, with a simple offline app.

The user explicitly rejected a still image sliding around and a sprite sheet as the final solution. The current procedural 3D dragon proves continuous motion, but its simple head/body are not the desired final visual fidelity. Replace it when a better asset/renderer is available; retaining this exact mesh is not a requirement.

## What exists

| File | Responsibility |
| --- | --- |
| `src/Pets/Dragon/DragonModel.cs` | Procedural WPF mesh, 64 sampled spine points, tube skin, head, antlers, whiskers, articulated legs, camera |
| `src/App/PetController.cs` | Travel along screen edges, following, tray, scheduling, audio playback, session/power handling |
| `src/UI/` | Transparent desktop pet, reminder screen, settings |
| `src/Configuration/Settings.cs` | JSON preferences |
| `src/Pets/Dragon/DragonPet.cs` | Pet presentation, flame effect and audio mapping |
| `src/Reminders/` | Reminder records and scheduling |
| `tests/SelfTest.cs`, `tests/UISmokeTest.cs` | Self-checks and UI regression checks |
| `tools/ModelPreview.cs` | Drag-to-orbit viewer and static-pose export |
| `tools/ModelExport.cs` | GLB geometry/material export, evaluated pose only |
| `src/App/Program.cs`, `tests/AudioTest.cs` | Entry point and audio checks |
| `src/Pets/Dragon/Assets/Audio/*.wav`, `scripts/Make-Audio.ps1` | Shipped offline audio; optional regeneration with Windows SAPI |
| `src/Pets/Dragon/Assets/References/*.png` | Generated reference illustrations, not runtime animation |
| `src/Pets/Dragon/Exports/` | Portable static GLB, screenshot, Khronos validation report |

The packaged app uses no network service. Windows speech was rendered into WAV files and lowered in pitch. The roar is synthesized; neither sound is the original actor/recording. Startup is opt-in. Settings and installed binaries live under separate per-user LocalAppData folders (see README).

## Model/export limitations

`src/Pets/Dragon/Exports/Dragon-static.glb` passed Khronos validation with zero errors/warnings. It contains 5,254 vertices, 7,080 triangles, 90 primitives/material entries and no textures, skeleton, morph targets, or animation clips. The live C# animation is not embedded in that file. It can be a shape/blockout reference; it is not an animated production asset.

WPF `Viewport3D` does not currently load GLB. Exporting a rigged replacement is only half of integration: either implement an appropriate importer/animation path or choose a renderer that supports the chosen asset, with measured cost. Avoid claiming that dropping a GLB into `src/Pets/Dragon/Assets` makes it work.

For a finished replacement, retain an editable source model and a portable GLB containing the rig, materials, and named idle/flight/coil/roar clips. Check skin weights, joint names, bounding boxes, scale, clip looping, and dependencies on external textures. Match the reference's silhouette and face before spending detail on hidden surfaces.

## Performance work

Current code allocates mesh-position collections and bone transforms during each geometry update (~30 Hz), and exports duplicate materials. Those are candidates to measure, not proof of the dominant bottleneck. The pause menu stops flight/reminders; it currently leaves body animation running. Hidden models stop their animation timers. The current desktop pet's transparent hit area is rectangular, not silhouette-aware.

Use `scripts/Measure-Performance.ps1 -ProcessId <id> -Seconds 30 -Scenario wander` for process CPU and working set. Record hardware, screen DPI, renderer/version, visibility, number of instances, and whether a model viewer is also open. Sample idle/stay, wander, follow, reminder, paused, and hidden/locked scenarios on the same machine. This script does not measure GPU, frame times, or power: use an appropriate Windows profiler for those before making battery claims.

Priorities: reuse transforms/buffers; eliminate unnecessary rendering when hidden or paused; share meshes/materials; assess screen-size-based detail; cap update rates sensibly; keep the visual motion smooth. Target budgets should be agreed from a measured baseline rather than invented percentages. No performance optimization has been claimed as complete.

## Verification and remaining checks

`dragonbreak.cmd test` builds and checks scheduling/settings, 3D initialization, animated geometry, edge wandering, pointer following, pause, reminder dismissal and snoozing, settings UI, and muted playback of all three audio clips. Read each PASS log; a successful process exit alone is insufficient for the UI test.

After a renderer/model change, inspect the actual animation, including turns, loop seams and face/limb deformation. Exercise Esc, Skip, Snooze and volume; test on multiple DPI monitors. Real session-lock/suspend recovery, GPU/power measurements, long-duration stability, and exact Shenron fidelity remain manual/unverified work. Audio checks validate playback, not subjective voice quality.

Runtime and publish scripts are independent of PwdVault. The reference images were made with the built-in image generator; prompts are in README. Keep asset provenance and third-party rights visible when bringing in outside models. No blanket license has been assigned to this repository or the referenced character.
