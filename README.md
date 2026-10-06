# Deep Feast

A Feeding Frenzy–style fish game: start as a tiny fry, eat anything smaller, dodge anything bigger, and grow into a legend of the deep.

| Folder | What it is |
| --- | --- |
| [`unity/`](unity/) | The real game — Unity 6.3 LTS (6000.3.25f1), with painted fish, animated vegetation, parallax reefs and synthesized audio. |
| [`web-prototype/`](web-prototype/) | The original single-file HTML5 canvas prototype. Open `index.html` in a browser. Kept as the reference for gameplay feel. |

## Unity quick start

1. Unity Hub → **Add project from disk** → `unity/` (editor 6000.3.25f1).
2. Open any scene (or `Assets/Scenes/Main.unity`) and press Play — `Bootstrap` builds the whole game from code.
3. Menu **Deep Feast → Build macOS / Build WebGL** for players (output goes to `unity/Builds/`, git-ignored).

Headless build and checks (macOS; set `UNITY=/path/to/Unity` if the editor isn't in the default Hub folder):

```sh
tools/build.sh                     # build the macOS player; fails on errors, warnings or a failed art validation
tools/verify.sh                    # build, then run the UI flow, gamepad flows, autoplay and menu captures; run before committing
tools/run.sh <outdir> <name> ...   # one test run of the built player with any harness flags below
```

`tools/run.sh` always runs the player with `-batchmode -mute` and adds `-quitafter 300` if the flags don't set a deadline. It prints the run's `[DeepFeast] exit` line and returns its exit code.

### Controls

| | Mouse / touch | Keyboard | Gamepad |
| --- | --- | --- | --- |
| Steer | Point / drag | WASD or arrows | Left stick or d-pad (analog speed) |
| Dash | Hold click / dash button | Space or Shift | Hold A / Cross, RT / R2 or RB / R1 |
| Pause | — | P or Esc | Start / Options (B / Circle resumes) |
| Mute | Corner button | M | View / Share |

Menus, the results cards and the Fishdex navigate with arrows or the d-pad and confirm with Enter or A / Cross. In **Settings** (menu or pause card), left and right change the focused option, Tab or LB / RB turns the page, and Esc or B goes back.

### Settings

| Page | Options |
| --- | --- |
| Display | Fullscreen or windowed, resolution, render scale (50–200% of the output), VSync, max frame rate (Unlimited, 30, 60, 90, 120, 144, 165, 240, 360 or Custom), custom frame rate (20–1000), FPS counter |
| Graphics | Anti-aliasing (off, 2×, 4×, 8× MSAA), glow (off, subtle, medium, strong), colour grading (off, subtle, rich), water ripples, sprite detail (1×, 2×, 3×; next launch), effects detail (low, normal, high), fish shadows |
| Sound | Master, music, effects and ambience volume |
| Accessibility | Text size (100, 125 or 150%, as far as the screen has room), reduce flashing (every blink and flicker slows to once a second), screen shake, dash (hold, or toggle with a press) |

Settings are saved as JSON in PlayerPrefs (`deepfeast.settings`). The scene is drawn into its own half-float target at the render scale and MSAA level, then presented under the HUD through `Shaders/PostFx.shader`: a soft-threshold bloom chain, a colour grade blended between reef, kelp and abyss looks, and ring-shaped refraction ripples from dashes and hits. The text stays sharp and ungraded at any scale. Very large targets step down to fewer MSAA samples to stay under about 1.5 GB.

Sprite detail sets how finely the game paints its generated effects (pearls, bubbles, sparks, glows and the HUD icons) at launch: 2× by default, 1× on the web. Fish don't use it: every fish is a 3D volume, painted from the atlas art or its own procedural skin, so their generated fallback sprites always bake at 1×. The bake runs on worker threads and the log reports it as `sprite detail`. Effects detail scales marine snow, lens motes, vent and dash bubbles and the sparks of each bite. Fish near the sunlit seabed cast soft shadows on the sand. The playing HUD keeps inside `Screen.safeArea`, clear of a notch. Input runs on the Input System package with the legacy Input Manager still enabled (Active Input Handling: Both).

### Test-harness flags (player builds)

| Flag | Effect |
| --- | --- |
| `-autoplay` | A bot plays the game (keeps swimming after a victory, restarts on game over); every frame over 50 ms is logged with what it overlapped, and the run fails if the sea drops below 40 fish |
| `-shots <dir>` / `-shotevery <s>` | Save screenshots of the menu and every N seconds of play |
| `-quitafter <s>` | Quit after N real seconds; a `flow` or `-padtest` run that has not finished by then fails |
| `-size <r>` | Start at radius `r` (jump straight to bigger tiers) |
| `-startx <x>` | Start near the seabed at world x (e.g. `11000` for the deep trench) |
| `-dumpart <dir>` | Write every baked sprite texture to `dir` as a PNG |
| `-gallery` | Art review: every species, two jellies and one pearl of each kind in a grid; with `-shots` saves `gallery` and `gallery_bite` |
| `-gallery -zoom <x> -focus <key>` | Move the gallery camera in `x` times, centred on one species by its key (the gallery log lists them, e.g. `atlantic_halibut`), to review fish up close |
| `-gallery -animate-gallery` | Capture swimming and a repeating anticipation/open/recovery cycle; `-shotevery` sets the frame interval |
| `-gallery -animate-turns` | Add direction changes to the animated species gallery |
| `-interface <menu\|pause\|over\|deep\|victory\|dex\|settings\|flow>` | Fixed native menu/results review (`deep` is the game-over card after the endless deep); `flow` verifies play, pause, resume, retry, victory, keep swimming, the Fishdex and settings through Unity Submit and Move events |
| `-settingspage <n>` | With `-interface settings`, show page `n` (0 display, 1 graphics, 2 sound, 3 accessibility) |
| `-set <key=value,...>` | Pin settings for a test run by field name, e.g. `-set vSync=false,maxFps=0` or `renderScale=200,msaa=8` |
| `-scenery kelp -turn-review -turn <facing>` | Hold facing interpolation near the midpoint for native turn-visibility regression captures |
| `-scenery <reef\|kelp\|abyss\|surface>` | Fixed camera, fish placement and visual time for native environment comparisons; saves `scenery.png` after 3.1 seconds |
| `-scenery <name> -ripple` | Send a water ripple from the hero just before the shot |
| `-scenery <name> -nearfloor` | Place the fish just above the sand to review their shadows |
| `-notch <px>` | Fake a notch this many pixels tall at the top of the screen; the playing HUD moves below it |
| `-scenery kelp -animate-scenery` | Capture rooted plant motion; `-shotevery` sets the frame interval, with a 3.1-second HUD settling period |
| `-batchmode -shots <dir>` | Render native camera/HUD captures offscreen when no active display is available; omit `-nographics` |
| `-shark <s>` | First shark arrives after N seconds |
| `-sharkvariant <n>` | Start the shark rotation at variant `n` |
| `-finale <s>` | Seconds after reaching Legend before the whale shark finale (default 12) |
| `-padtest` | A virtual gamepad plays through the menu, steering, dashing, pause, results card, Fishdex and settings, checking each step; with `-set dashToggle=true` it also checks that a toggled dash outlasts the press |
| `-pearlevery <s>` | Drop a pearl every N seconds, cycling shield, magnet, burst and lantern |
| `-timescale <x>` / `-mute` / `-nopause` | Speed up, silence, don't pause on focus loss |

A run with any of these flags (other than `-mute`), or any `-batchmode` run, is a test run: it never saves the best score, Fishdex, settings or mute switch. Every harness run ends with an exit code: 0 when it ran cleanly, 1 when a check failed, anything logged an error or exception, or a flow stalled before `-quitafter`. The log's last `[DeepFeast] exit` line gives the reason. Headless `flow` and `-padtest` runs quit as soon as their flow passes.

## Code map (`unity/Assets/Scripts`)

- `Game.cs` — state machine, player, fish AI, spawning, camera, render glue
- `FishArt.cs` — species/shape data and the baked fish sprites + fish rig
- `PaintedArt.cs` / `Resources/Concept/painted-atlas.json` — twelve painted species, aligned pose keys, deformable meshes and reef props
- `EnvironmentArt.cs` / `Shaders/PlantSway.shader` — shared painted plant/reef meshes, rooted current deformation and water fog
- `Habitat.cs` / `Shaders/PaintedLighting.cginc` — continuous habitat palette, light, HUD accent and shared painted shading
- `World.cs` — seabed, kelp, rocks, coral, anemones and grounded habitat landmarks
- `SceneFx.cs` — water gradient, parallax ridges, god rays, surface, depth darkness, snow
- `Hud.cs` — uGUI HUD, banners, power-up timers, menus and the Fishdex screen built in code
- `HudSettings.cs` / `GameSettings.cs` — the settings card and its option rows; saving and applying display, frame-rate, graphics and sound options
- `Presenter.cs` / `Shaders/PostFx.shader` — draws the scene into a scaled, multisampled target and presents it under the HUD with glow, grading and ripples
- `PadInput.cs` — gamepad steering, dash and pause
- `Fishdex.cs` — saved species discoveries and the portraits photographed from the 3D models
- `Sfx.cs` — synthesised sound effects and ambience
- `Music.cs` — synthesised music: a pad for each habitat, an arpeggio that grows with the hero, a pulse under shark encounters and the finale, and a victory sting
- `Harness/` — the command-line test harness, kept out of the game code: flags, screenshots, recording and exit codes (`GameHarness.cs`), the gallery, scenery and UI-flow reviews (`GameReviews.cs`), the autoplay bot (`GameBot.cs`), the `-padtest` virtual-pad check (`PadFlow.cs`), `-record` (`Recorder.cs`) and the species and turn reviews (`SpeciesVisualReview.cs`, `FishTurnReview.cs`)
- `Raster.cs` / `Draw.cs` / `Util.cs` — software rasteriser (with blur / pseudo-3D lighting), mesh drawing helpers, noise, procedural textures

The display font is [Lilita One](https://fonts.google.com/specimen/Lilita+One) by Juan Montoreano, used under the SIL Open Font License (`unity/Assets/Resources/Fonts/OFL.txt`).

The [latest visual handover](art-drafts/v4/HANDOVER.md) and [interactive comparisons](art-drafts/v4/index.html) cover composition, shared lighting, fish movement, habitat identity and menus. The [environment handover](art-drafts/v3/HANDOVER.md) covers the preceding painted plant/background pass. The [fish implementation handover](art-drafts/v2/implemented/HANDOVER.md) covers the previous fish, props and HUD pass. `Deep Feast → Validate Production Art` checks all twelve species, both poses, sixteen props, environment meshes, transparency, import settings, all three painted shaders and turn visibility before builds. `Deep Feast → Validate Fish Turns` runs the 12-species turn regression separately. Re-run `python3 tools/catalog_atlases.py` after changing an atlas layout (requires Pillow); it updates metadata without modifying the PNGs.
