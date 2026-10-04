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

Headless build:

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath unity -executeMethod DeepFeast.EditorTools.Build.Mac -logFile -
```

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
| Sound & play | Master, effects and ambience volume; screen shake |

Settings are saved as JSON in PlayerPrefs (`deepfeast.settings`). The scene is drawn into its own half-float target at the render scale and MSAA level, then presented under the HUD through `Shaders/PostFx.shader`: a soft-threshold bloom chain, a colour grade blended between reef, kelp and abyss looks, and ring-shaped refraction ripples from dashes and hits. The text stays sharp and ungraded at any scale. Very large targets step down to fewer MSAA samples to stay under about 1.5 GB.

Sprite detail sets how finely the game paints its generated art (fish skins without painted art, eyes, pearls, bubbles, particles) at launch: 2× by default, 1× on the web. Fish are painted on worker threads, so the whole bake takes about 0.6 s at 1×, 1.7 s at 2× and 3.2 s at 3× on an Apple-silicon Mac (`sprite detail` in the log). Effects detail scales marine snow, lens motes, vent and dash bubbles and the sparks of each bite. Fish near the sunlit seabed cast soft shadows on the sand. The playing HUD keeps inside `Screen.safeArea`, clear of a notch. Input runs on the Input System package with the legacy Input Manager still enabled (Active Input Handling: Both).

### Test-harness flags (player builds)

| Flag | Effect |
| --- | --- |
| `-autoplay` | A bot plays the game (and restarts on game over); every frame over 50 ms is logged with what it overlapped |
| `-shots <dir>` / `-shotevery <s>` | Save screenshots of the menu and every N seconds of play |
| `-quitafter <s>` | Quit after N real seconds |
| `-size <r>` | Start at radius `r` (jump straight to bigger tiers) |
| `-startx <x>` | Start near the seabed at world x (e.g. `11000` for the deep trench) |
| `-dumpart <dir>` | Write every baked sprite texture to `dir` as a PNG |
| `-gallery` | Art review: every species, two jellies and one pearl of each kind in a grid; with `-shots` saves `gallery` and `gallery_bite` |
| `-gallery -zoom <x> -focus <key>` | Move the gallery camera in `x` times, centred on one species by its key (the gallery log lists them, e.g. `atlantic_halibut`), to review sprite detail |
| `-gallery -animate-gallery` | Capture swimming and a repeating anticipation/open/recovery cycle; `-shotevery` sets the frame interval |
| `-gallery -animate-turns` | Add direction changes to the animated species gallery |
| `-interface <menu\|pause\|over\|victory\|dex\|settings\|flow>` | Fixed native menu/results review; `flow` verifies play, pause, resume, retry, victory, keep swimming, the Fishdex and settings through Unity Submit and Move events |
| `-settingspage <n>` | With `-interface settings`, show page `n` (0 display, 1 graphics, 2 sound & play) |
| `-set <key=value,...>` | Pin settings for a test run by field name, e.g. `-set vSync=false,maxFps=0` or `renderScale=200,msaa=8`; test runs never save settings |
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
| `-padtest` | A virtual gamepad plays through the menu, steering, dashing, pause, results card, Fishdex and settings, checking each step |
| `-pearlevery <s>` | Drop a pearl every N seconds, cycling shield, magnet, burst and lantern |
| `-timescale <x>` / `-mute` / `-nopause` | Speed up, silence, don't pause on focus loss |

## Code map (`unity/Assets/Scripts`)

- `Game.cs` — state machine, player, fish AI, spawning, camera, render glue, bot
- `FishArt.cs` — species/shape data and the baked fish sprites + fish rig
- `PaintedArt.cs` / `Resources/Concept/painted-atlas.json` — twelve painted species, aligned pose keys, deformable meshes and reef props
- `EnvironmentArt.cs` / `Shaders/PlantSway.shader` — shared painted plant/reef meshes, rooted current deformation and water fog
- `Habitat.cs` / `Shaders/PaintedLighting.cginc` — continuous habitat palette, light, HUD accent and shared painted shading
- `World.cs` — seabed, kelp, rocks, coral, anemones and grounded habitat landmarks
- `SceneFx.cs` — water gradient, parallax ridges, god rays, surface, depth darkness, snow
- `Hud.cs` — uGUI HUD, banners, power-up timers, menus and the Fishdex screen built in code
- `HudSettings.cs` / `GameSettings.cs` — the settings card and its option rows; saving and applying display, frame-rate, graphics and sound options
- `Presenter.cs` / `Shaders/PostFx.shader` — draws the scene into a scaled, multisampled target and presents it under the HUD with glow, grading and ripples
- `PadInput.cs` — gamepad steering, dash, pause and the `-padtest` virtual-pad check
- `Fishdex.cs` — saved species discoveries and the portraits photographed from the 3D models
- `Sfx.cs` — synthesised sound effects and ambience
- `Raster.cs` / `Draw.cs` / `Util.cs` — software rasteriser (with blur / pseudo-3D lighting), mesh drawing helpers, noise, procedural textures

The display font is [Lilita One](https://fonts.google.com/specimen/Lilita+One) by Juan Montoreano, used under the SIL Open Font License (`unity/Assets/Resources/Fonts/OFL.txt`).

The [latest visual handover](art-drafts/v4/HANDOVER.md) and [interactive comparisons](art-drafts/v4/index.html) cover composition, shared lighting, fish movement, habitat identity and menus. The [environment handover](art-drafts/v3/HANDOVER.md) covers the preceding painted plant/background pass. The [fish implementation handover](art-drafts/v2/implemented/HANDOVER.md) covers the previous fish, props and HUD pass. `Deep Feast → Validate Production Art` checks all twelve species, both poses, sixteen props, environment meshes, transparency, import settings, all three painted shaders and turn visibility before builds. `Deep Feast → Validate Fish Turns` runs the 12-species turn regression separately. Re-run `python3 tools/catalog_atlases.py` after changing an atlas layout (requires Pillow); it updates metadata without modifying the PNGs.
