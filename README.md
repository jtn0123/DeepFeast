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

### Test-harness flags (player builds)

| Flag | Effect |
| --- | --- |
| `-autoplay` | A bot plays the game (and restarts on game over) |
| `-shots <dir>` / `-shotevery <s>` | Save screenshots of the menu and every N seconds of play |
| `-quitafter <s>` | Quit after N real seconds |
| `-size <r>` | Start at radius `r` (jump straight to bigger tiers) |
| `-startx <x>` | Start near the seabed at world x (e.g. `11000` for the deep trench) |
| `-dumpart <dir>` | Write every baked sprite texture to `dir` as a PNG |
| `-gallery` | Art review: every species, two jellies and one pearl of each kind in a grid; with `-shots` saves `gallery` and `gallery_bite` |
| `-gallery -animate-gallery` | Capture swimming and a repeating anticipation/open/recovery cycle; `-shotevery` sets the frame interval |
| `-gallery -animate-turns` | Add direction changes to the animated species gallery |
| `-interface <menu\|pause\|over\|victory\|dex\|flow>` | Fixed native menu/results review; `flow` verifies play, pause, resume, retry, victory, keep swimming and the Fishdex through Unity Submit events |
| `-scenery kelp -turn-review -turn <facing>` | Hold facing interpolation near the midpoint for native turn-visibility regression captures |
| `-scenery <reef\|kelp\|abyss\|surface>` | Fixed camera, fish placement and visual time for native environment comparisons; saves `scenery.png` after 3.1 seconds |
| `-scenery kelp -animate-scenery` | Capture rooted plant motion; `-shotevery` sets the frame interval, with a 3.1-second HUD settling period |
| `-batchmode -shots <dir>` | Render native camera/HUD captures offscreen when no active display is available; omit `-nographics` |
| `-shark <s>` | First shark arrives after N seconds |
| `-sharkvariant <n>` | Start the shark rotation at variant `n` |
| `-finale <s>` | Seconds after reaching Legend before the whale shark finale (default 12) |
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
- `Fishdex.cs` — saved species discoveries and the portraits photographed from the 3D models
- `Sfx.cs` — synthesised sound effects and ambience
- `Raster.cs` / `Draw.cs` / `Util.cs` — software rasteriser (with blur / pseudo-3D lighting), mesh drawing helpers, noise, procedural textures

The display font is [Lilita One](https://fonts.google.com/specimen/Lilita+One) by Juan Montoreano, used under the SIL Open Font License (`unity/Assets/Resources/Fonts/OFL.txt`).

The [latest visual handover](art-drafts/v4/HANDOVER.md) and [interactive comparisons](art-drafts/v4/index.html) cover composition, shared lighting, fish movement, habitat identity and menus. The [environment handover](art-drafts/v3/HANDOVER.md) covers the preceding painted plant/background pass. The [fish implementation handover](art-drafts/v2/implemented/HANDOVER.md) covers the previous fish, props and HUD pass. `Deep Feast → Validate Production Art` checks all twelve species, both poses, sixteen props, environment meshes, transparency, import settings, all three painted shaders and turn visibility before builds. `Deep Feast → Validate Fish Turns` runs the 12-species turn regression separately. Re-run `python3 tools/catalog_atlases.py` after changing an atlas layout (requires Pillow); it updates metadata without modifying the PNGs.
