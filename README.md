# Deep Feast

A Feeding Frenzy–style fish game: start as a tiny fry, eat anything smaller, dodge anything bigger, and grow into a legend of the deep.

| Folder | What it is |
| --- | --- |
| [`unity/`](unity/) | The real game — Unity 6.3 LTS (6000.3.25f1). All art and audio are generated in code at startup, so there are no asset files to manage. |
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
| `-gallery` | Art review: every species, two jellies and a pearl in a grid; with `-shots` saves `gallery` and `gallery_bite` |
| `-shark <s>` | First shark arrives after N seconds |
| `-timescale <x>` / `-mute` / `-nopause` | Speed up, silence, don't pause on focus loss |

## Code map (`unity/Assets/Scripts`)

- `Game.cs` — state machine, player, fish AI, spawning, camera, render glue, bot
- `FishArt.cs` — species/shape data and the baked fish sprites + fish rig
- `World.cs` — seabed, kelp, rocks, coral, anemones
- `SceneFx.cs` — water gradient, parallax ridges, god rays, surface, depth darkness, snow
- `Hud.cs` — uGUI HUD, banners and menus built in code
- `Sfx.cs` — synthesised sound effects and ambience
- `Raster.cs` / `Draw.cs` / `Util.cs` — software rasteriser (with blur / pseudo-3D lighting), mesh drawing helpers, noise, procedural textures

The display font is [Lilita One](https://fonts.google.com/specimen/Lilita+One) by Juan Montoreano, used under the SIL Open Font License (`unity/Assets/Resources/Fonts/OFL.txt`).
