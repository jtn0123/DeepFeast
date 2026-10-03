# Deep Feast — completed Unity visual pass

This implements all five priorities from the [v2 review](../REVIEW.md), starting from the actual committed Unity baseline **5f75e027ddcdf16a3524690ab2bdecf716ecfbd7**. The implementation commit is the commit containing this document. The macOS player was rebuilt with Unity **6000.3.25f1** and captured through its real camera, shaders, meshes and uGUI HUD.

## Review the result

- [Interactive before/after gallery](index.html): remaining six, original six, jaws, reef, abyss and animation.
- [Remaining six](comparisons/remaining.png) · [Original six](comparisons/original.png) · [Bite poses](comparisons/bites.png).
- [Shallow reef](comparisons/reef.png) · [Abyss](comparisons/abyss.png).
- [Native swimming and bite video](animation.mp4): two cycles at 12 captured frames per second.
- [Small fry gameplay](fry/play_00_t4_r12.png) · [Tier-up feedback](feedback/play_03_t5_r19.png).

The left side uses the previous native Unity app at `5f75e02`. The right side uses this implemented native player. Fish crops use matching scale within each comparison. World captures use the same starting-size/location flags and 1280×720 output, with randomized fish spawns and bot movement; the terrain composition can therefore differ. Comparison PNGs are assembled from those unchanged captures, rather than generated scene mockups.

## What shipped

| Priority | Installed result | Main source |
| --- | --- | --- |
| 1. Complete the fish family | Painted minnow, parrotfish, snapper, barracuda, grouper and tuna. Refined player, clown, tang, puffer, angel and shark to the same production atlas format. All twelve retain distinct silhouettes and palettes. | `PaintedArt.cs`, `FishArt.cs`, `Concept/atlas-*-v2.png` |
| 2. Finish the environment | Rounded distant reef contours, broader/sparser background kelp, broader foreground leaves, depth tint, painted rocks/coral with contact shadows, subdued sand ripples, softer rays and reduced caustics. Cool low reef clusters and local light give the abyss detail. Prop scale was reduced after native review so scenery stays subordinate to fish. | `World.cs`, `SceneFx.cs`, `Util.cs` |
| 3. Improve bites and swimming | Twenty-four authored closed/open jaw sprites, including the shark. Aligned tail/spine anchors, anticipation and recovery squash, GPU tail flex and gentle fin flutter. Player feeding retains its friendly expression. Existing blink/happy/threat lids remain supported. | `PaintedArt.cs`, `FishArt.cs`, `Game.cs`, `Shaders/FishSwim.shader` |
| 4. Unify props | Six transparent painted props: branching coral, brain coral, tube coral, sea fan, rock cluster and deep reef group. Softer tapered jelly tentacles/arms and small bead tips; smaller, quieter pearl halo and sparkle. | `Concept/atlas-props-v2.png`, `World.cs`, `Game.cs` |
| 5. Polish feedback | Fewer feeding particles, softer crumbs, smaller score labels/tier bursts, short dash wakes, quieter threat halos, smoothed growth fill, score/growth pulses and a `RECOVER` dash label. Existing font, panels, banners and controls are retained. | `Entities.cs`, `Game.cs`, `Hud.cs` |

There are **two authored raster keys per fish**. Anticipation and recovery are runtime deformation states, not two additional hand-painted frames. Swimming deforms a 16×8 grid; it does not add duplicate painted fins or eyes. Fish size, scoring, tier thresholds, AI and collision rules are retained. The player chomp timer now decays once per update so visual recovery follows one consistent timer.

## Production assets and maintenance

Production files live under `unity/Assets/Resources/Concept/`:

| Atlas | Dimensions | Contents |
| --- | --- | --- |
| `atlas-original-v2.png` | 1024×1536 | Player, clown, tang, puffer, angel and shark; closed/open pairs |
| `atlas-remaining-v2.png` | 1024×1536 | Minnow, parrot, snapper, barracuda, grouper and tuna; closed/open pairs |
| `atlas-props-v2.png` | 1536×1024 | Six grounded reef/rock props |

The PNGs preserve the image-generation tool's RGBA output bytes. Their transparent padding is real alpha, not a checkerboard. Exact production prompts and reference paths are in [`production-prompts.json`](../production-prompts.json); the final spacing correction is in [`production-layout-prompt.txt`](../production-layout-prompt.txt). [`evidence.json`](evidence.json) records final asset/capture hashes and capture provenance.

`tools/catalog_atlases.py` reads alpha silhouettes and writes `painted-atlas.json` sprite rectangles, scale and anchors. It does not edit or re-encode PNGs. It requires Pillow. After replacing an atlas, re-run it and inspect the native gallery, especially eyes, jaw transitions, tall angelfish fins and small gameplay sizes.

`PaintedAtlasImporter.cs` preserves alpha, uses uncompressed bilinear textures, disables mipmaps and CPU readability, and retains full atlas dimensions. The three full atlases use about 18 MiB of base GPU texture storage. The original concept assets and procedural art remain available as fallbacks; unused procedural coral/rock variants are no longer baked when their painted replacement exists.

Unity permits `Sprite.OverrideGeometry` in the player loop; the fish grids are queued during asset loading and prepared in the first game update. Validation can run repeatedly before or after mesh preparation. Do not move the override into editor build/import callbacks.

## Verified

| Check | Result / evidence |
| --- | --- |
| Latest baseline | Worktree fast-forwarded to the actual local `main` at `5f75e02` before implementation; baseline captures came from that app. |
| macOS build | Succeeded, **0 build errors**, 125.7 MB; [`logs/build-final.log`](logs/build-final.log). |
| Production-art validation | All 12 species, 24 distinct aligned pose keys, 6 props, bounding rectangles, scale, actual alpha, import settings and shader compilation passed before the build. |
| Runtime swimming geometry | All 24 grids prepared successfully; gallery and gameplay logs. |
| Species/jaw gallery | Six-second native run exited successfully; closed/open captures visually inspected for clipping, opaque backgrounds and duplicate features. |
| Animation | 51 native camera frames encoded into a 4.25-second 1280×720 H.264 video; relaxed/open/recovery states and tail/fin motion reviewed. |
| Shallow reef / abyss | 23-second and 16-second native autoplay runs exited successfully; placement, scale, materials, depth and fish readability inspected. |
| Small fry | 45-second native autoplay run at starting radius 12; feeding, growth, score, threat, death/respawn and small-size readability exercised. Log reached 19 eaten, radius 17.6 and 715 points at its final periodic sample. |
| Near-threshold feedback | 22-second run starting at radius 17.9 reached **Minnow**, radius 20.4, 4 lives and 131 points at its periodic sample; tier-up capture inspected. |
| Native logs / whitespace | No game exceptions or shader/geometry errors in these completed runs; `git diff --check` passed. |

Native autoplay logs sampled approximately **55–59 FPS**, with a 60 FPS player target, offscreen capture overhead and some concurrent runs. This is limited local evidence, not a comparative performance benchmark.

During verification macOS reported **zero active displays**. Windowed builds, including the unchanged previous app, stalled waiting for presentation. The completed captures therefore use the native macOS player in batch mode with a GPU render texture and the same HUD rendered by a capture camera. This verifies scene rendering and bot-driven gameplay; **interactive windowed input, resizing, audio and display-connected performance still need a play check**. WebGL and mobile were not built or tested in this pass.

## Reproduce

Build and validate:

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath /path/to/DeepFeast/unity \
  -executeMethod DeepFeast.EditorTools.Build.Mac -logFile build.log
```

All following commands launch `unity/Builds/Mac/DeepFeast.app/Contents/MacOS/Deep Feast`. Use an absolute `-shots` directory. Keep graphics enabled; **omit `-nographics`**. Omit `-batchmode` for a visible, display-connected run.

```sh
"Deep Feast" -batchmode -gallery -mute -nopause \
  -screen-width 1280 -screen-height 720 -shots /tmp/gallery -quitafter 6

"Deep Feast" -batchmode -gallery -animate-gallery -mute -nopause \
  -screen-width 1280 -screen-height 720 -shots /tmp/poses \
  -shotevery 0.0833333 -quitafter 4.2

"Deep Feast" -batchmode -autoplay -mute -nopause -size 30 -startx 5500 -shark 8 \
  -screen-width 1280 -screen-height 720 -shots /tmp/reef -shotevery 4 -quitafter 23

"Deep Feast" -batchmode -autoplay -mute -nopause -size 47 -startx 11000 -shark 80 \
  -screen-width 1280 -screen-height 720 -shots /tmp/abyss -shotevery 5 -quitafter 16

"Deep Feast" -batchmode -autoplay -mute -nopause -size 12 -startx 1500 -shark 4 \
  -screen-width 1280 -screen-height 720 -shots /tmp/fry -shotevery 4 -quitafter 45

"Deep Feast" -batchmode -autoplay -mute -nopause -size 17.9 -startx 1500 -shark 4 \
  -screen-width 1280 -screen-height 720 -shots /tmp/feedback -shotevery 0.5 -quitafter 22

ffmpeg -framerate 12 -i /tmp/poses/pose_%03d.png \
  -c:v libx264 -crf 18 -pix_fmt yuv420p -movflags +faststart animation.mp4
```

For the interactive review, serve the repository root and open `art-drafts/v2/implemented/index.html`. `export-board.js` assembles labeled comparison PNGs from the native captures; it preserves their content and does not generate replacement artwork.

## Turnover notes

Continue from the implementation commit containing this file, rather than the historical v1 web prototype or the v2 concept mockups. The implementation is committed locally; publishing to the remote is a separate action. On this Mac, the canonical project is `~/Documents/Github/DeepFeast/unity`; its `Builds/Mac/DeepFeast.app` is the installed review build. A copy of the previous app is retained in the ignored build directory for rollback.

Future art changes should keep the clear swimming area, species silhouettes, friendly player face, jaw anchors and restrained lighting. Review native captures at fry size as well as the gallery before accepting new textures or effects.
