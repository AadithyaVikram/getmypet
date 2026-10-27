# DragonBreak

A small, offline Windows desktop pet inspired by Shenron. A live 3D dragon bends its spine continuously, paddles its legs, and sweeps its tail. It wanders through varied outer-screen positions with brief crossings, turns to face its direction of travel, or follows your pointer. Water and movement breaks have a full-screen dragon, animated flame, synthesized roar, and low-pitched Windows voice.

## Use

Open **DragonBreak** from the Desktop or Start menu. Settings open alongside the pet. Closing settings leaves the pet running.

- Drag the dragon to place it. Right-click it or its tray icon for settings, movement modes, pause, preview, mute, and quit. Double-click the dragon for settings.
- **Wander** flies between varied positions in the outer areas of its current monitor. **Follow** moves toward your pointer, with a gap. **Stay** keeps the pet in place while its body keeps moving.
- Water defaults to every 45 minutes and movement to every 60 minutes. These are editable preferences, not medical recommendations. Save restarts the timers.
- Reminders cover the pet's current monitor. Other monitors remain usable. **Done**, **Skip**, **Esc**, or Alt+F4 dismiss; **Snooze 5 min** delays that reminder. The countdown dismisses automatically.
- Default volume is 40%. **Test water** and **Test stretch** play a reminder immediately, even during quiet hours. Turn your speaker volume down before your first preview.
- Quiet hours are enabled by default: 10 pm–8 am, using Windows local time. Scheduled reminders pause during quiet hours, session lock, and suspend; timers restart afterward. You can turn quiet hours off.
- Quit from the pet/tray menu. Automatic startup is off. To enable it manually, copy the Desktop shortcut into the folder opened by `shell:startup`.

The pet is a stylized procedural 3D model rendered with Windows WPF: a 64-sample curved spine, deforming tube, articulated legs, horns, whiskers, eyes, and head. Geometry updates about 30 times per second; no image swapping is used. This simple model is not an exact or photorealistic Shenron recreation. There is no lip sync. Its voice is Windows speech lowered in pitch, not the original character's actor. The roar is a locally synthesized bass growl.

## Build, test, install

Requires Windows x64 and .NET SDK 10.0.400 (the published app bundles its runtime). From this folder:

```powershell
.\init.cmd                  # Restore, build, and run self-checks
.\dragonbreak.cmd preview   # Open the interactive 3D viewer
.\dragonbreak.cmd run       # Open the desktop companion
.\dragonbreak.cmd test      # Self-checks, muted UI previews, audio playback checks
.\dragonbreak.cmd publish   # Bundle the Windows runtime
.\dragonbreak.cmd install   # Install published app and shortcuts
```

To update, quit the installed app, publish again, then run the install command. Settings are stored separately in `%LOCALAPPDATA%\DragonBreak\settings.json`. The app lives in `%LOCALAPPDATA%\Programs\DragonBreak`.

No network calls, credentials, telemetry, or paid services are used by the running app. The project is separate from PwdVault and does not read vaults.

## Inspect and export the 3D model

Double-click `Open-3D-Preview.cmd` to open an interactive model viewer. Drag to orbit, scroll to zoom, pause the live geometry, or export its current pose to GLB. The viewer saves its exports in `artifacts/dev/model-preview`.

The initial portable model is also in `src/Pets/Dragon/Exports/Dragon-static.glb`, with a preview and Khronos validator report (zero errors or warnings). This file contains colored geometry in one evaluated pose. It does not contain a skeleton or animation clips; the viewer's animation is implemented in C#. It is a basic stylized model, not a detailed Shenron asset. The file follows the [glTF 2.0 specification](https://registry.khronos.org/glTF/).

### Concept images

`src/Pets/Dragon/Assets/References/dragon.png` was generated with the built-in image-generation tool, using the user-supplied Shenron image as reference. Prompt: a transparent, full-body, compact coiled green Shenron with golden segmented belly, red eyes, brown antlers, long whiskers, small clawed arms, clean anime cel shading, commanding open-mouth expression, no scenery or text, and generous transparent margin for a desktop companion.

`src/Pets/Dragon/Assets/References/dragon-slither.png` was generated with the same built-in tool using the first image as reference. Prompt: eight different sequential frames, four columns by two rows, transparent cells, consistent Shenron character at the same scale, compact three-quarter view facing right, travelling body undulation, alternating reptilian leg paddles and tail sweep, no labels or grid lines. Both PNGs are reference/concept artwork only. The final app renders live 3D geometry and does not package these images.

Implementation references: [WPF windows](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/windows/) and [WPF multimedia](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/multimedia-overview).

## Repository and agent handoff

Everything needed to recreate this app lives in this repository. `init.cmd` is the first command after cloning; it requires the SDK in `global.json`. Run `dragonbreak.cmd help` for the command list. Build/test logs and screenshots are under `artifacts/dev`; generated files are excluded from Git. The UI test briefly displays full-screen reminders and closes them automatically. Optional audio regeneration: `scripts/Make-Audio.ps1`.

- `AGENTS.md`: instructions for coding agents.
- `.agents/skills/recreate-dragonbreak/SKILL.md`: reusable desktop-pet recreation skill.
- `docs/HANDOFF.md`: model/renderer map, visual goals, export limitations, and performance work.
- `scripts/Measure-Performance.ps1`: measure a running DragonBreak process's CPU and memory; see the handoff for methodology.
- `src/Pets/Dragon/Assets/` and `src/Pets/Dragon/Exports/`: required audio, visual references, and portable static model.

The next 3D agent can replace the prototype model and renderer. Detailed character fidelity and reduced resource consumption remain future work, with measurements required before claiming improvements.
## Custom reminders and presentation

Settings lets you add, edit, disable, or remove recurring custom messages (up to 240 characters), with intervals from 1 minute to 7 days. Save applies changes and restarts timers. Custom messages are displayed as text; recorded voices remain specific to water and movement.

Choose FullScreen or Popup for water and movement, and separately for each custom reminder. Popup is silent, appears in the lower corner of the pet monitor's working area, and does not activate or take keyboard focus. It supports Done, Skip, Snooze 5 min, and automatic dismissal. FullScreen retains the animated takeover. Quiet hours, pause, lock, and suspend apply to all automatic reminders. Reminders are spaced by at least two minutes after dismissal.

The app icon is an original ivory paw and copper reminder spark on a plum tile, shared by the executable, tray, and settings. Dragon remains the only available pet in this version. Future cat/dog companions can implement the shared pet contracts described in docs/ARCHITECTURE.md; reminder records and scheduling contain no pet or renderer dependency.

## Source organization

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for folder ownership, the shared pet contract, and steps for adding a future pet.

Desktop pet size: use the 50-200% slider in Settings and Save. The preference persists across restarts and scales any pet through the shared host. Oversized pets are fitted to the monitor working area; full-screen reminders and compact popup sizes are independent.

Wander varies destinations across an outer band, with randomized inset and direction, rather than sticking to the border. It occasionally crosses the centre at higher speed (at most once per 90 seconds of active travel) and never targets a centre stop. Hover, drag, pause and reminders freeze route time. Follow and Stay retain their movement behavior. The dragon turns toward horizontal, vertical and diagonal travel. Large pets can still overlap central content; reduce size for more clearance.

Turning preserves the selected pet scale. The desktop host uses a square rotation area so vertical and diagonal flight do not trigger resizing. A real 3D turn may still change the projected silhouette; screen heading alone does not zoom the model.

The app palette uses warm ivory (#FAF4EB), plum (#382638), and copper (#9B472E), with blush surfaces. Shared colours and control styles live in src/UI/Ui.cs; editable logo artwork lives in Assets/Brand/logo.svg. The dragon retains its own character colours.
