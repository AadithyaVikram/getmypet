# DragonBreak

This folder is an independent Windows desktop-pet repository. Keep all build and asset paths relative to it; the parent checkout is unrelated.

- Start with `README.md` for commands. For 3D replacement, export, or performance work, read `docs/HANDOFF.md`.
- User intent: a recognizable Shenron-style dragon with continuous articulated motion, curved edge-to-centre flight, and offline spoken break reminders. The user rejected moving still images and swapping sprite frames as the final animation.
- Runtime code uses WPF/.NET; PNGs in `Assets` are visual references. `exports/Dragon-static.glb` is a static pose with no rig or clips. Represent those limitations accurately.
- Keep reminder dismissal (Esc/Skip/Snooze), volume/mute, quiet hours, and session/power behavior working when changing the renderer. Test mode uses isolated in-memory settings and muted audio.
- Use `.agents/skills/recreate-dragonbreak/SKILL.md` when recreating this pet, packaging it on another Windows machine, or replacing its model.
- `dragonbreak.cmd test` runs the meaningful behavior checks. It displays temporary reminder windows. Close only test processes that the current task launches; the user may have another pet/viewer running.
- Profile before calling an optimization successful. Frame rate, triangle count, or file size alone do not establish CPU/GPU/battery improvement.
- Keep `bin`, `obj`, `artifacts`, local settings, and credentials out of commits. Commit source, required audio, visual references, and deliberate portable exports.
