# Deep Feast — painted environment implementation

This pass builds on committed Unity **da45f4bb8386e3fc87e0df045d967e5a3dbe1e53**, the completed fish/props/HUD pass. It implements more background, plant and seabed polish in the actual Unity game. The implementation revision is the commit containing this document. No remote push is included.

## Review

- [Interactive native before/after review](index.html): kelp, reef, abyss, near surface, comparison slider and animation.
- [Kelp forest](comparisons/kelp.png) · [Shallow reef](comparisons/reef.png) · [Abyss](comparisons/abyss.png) · [Near surface](comparisons/surface.png).
- [Native current-motion video](current-animation.mp4): 45 frames, 1280×720, 12 FPS, 3.75 seconds.
- [Small-fry gameplay](fry/play_01_t8_r14.png) · [Hunter gameplay](gameplay/play_00_t4_r30.png).

Both sides are native Unity camera/HUD renders at 1280×720. The baseline is `da45f4b` with only the [fixed-camera capture harness](baseline-harness.patch) added. The camera, zoom, visual time, terrain and five fish placements match between sides. Ambient schools/particles can vary. Comparison boards are browser-canvas assemblies of untouched native renders, not generated scene concepts or browser screenshots.

## Implemented changes

| Area | Result | Source |
| --- | --- | --- |
| Kelp | Two painted ribbon-blade varieties with textured volume, buoyancy bladders and soft current motion. Broad footings are buried behind terrain on slopes. | `World.cs`, `EnvironmentArt.cs`, `PlantSway.shader` |
| Low vegetation | Lush seagrass, red seaweed and painted rose/violet anemones replace thin grass and radial tentacle strokes. Existing deep-anemone light locations remain active. | `World.cs`, `atlas-plants-v3.png` |
| Background | Painted reef shelves, natural arches, terraces and abyss fingers replace the procedural mountain/tree silhouettes. Three layers retain separate parallax speeds and water fog. | `SceneFx.cs`, `atlas-reefs-v3.png` |
| Depth transition | Shallow formations dissolve into blue/violet abyss outcrops between camera depths 2400–3600; the existing darkness and local-light system remains. | `SceneFx.cs`, `PlantSway.shader` |
| Seabed | Broken crescent ripples, occasional ribbed shells and a finer edge highlight soften the uniform sand. | `World.cs` |
| Review harness | `-scenery reef/kelp/abyss/surface` fixes framing and fish placements; `-animate-scenery` advances a repeatable plant-motion timeline. | `Game.cs` |

Fish artwork, gameplay terrain/collision geometry, AI, scoring, controls and tier thresholds are unchanged. New vegetation is decorative. Existing procedural plants/backgrounds remain fallback paths when their painted assets are absent; production builds require all authored assets.

## Assets and animation

The two new atlases live in `unity/Assets/Resources/Concept/`:

| Asset | Size | Contents |
| --- | --- | --- |
| `atlas-plants-v3.png` | 1536×1024 RGBA | Teal kelp, olive kelp, red seaweed, seagrass, rose anemone, deep anemone |
| `atlas-reefs-v3.png` | 1536×1024 RGBA | Shelf, arch, terrace, abyss outcrop |

Generated with the built-in imagegen tool using the exact [production prompts](production-prompts.json). The original generated PNG bytes and alpha were copied unchanged. Transparent pixels occupy about 56% of the plant sheet and 50% of the reef sheet. `tools/catalog_atlases.py` analyzes connected alpha regions and writes rectangles, scales and grounded pivots; it does not edit bitmaps. Heights and row baselines vary in the authored sheets, so the catalog identifies objects by their grounding bases and column order.

`EnvironmentArt.cs` builds ten cached 12×20 grids (273 vertices each), reuses two atlas materials, and uses material property blocks for motion/fog. The current shader has zero displacement at the footing and increasing bend toward the crown. Plants use normalized height; formations use a shared 2.4-unit width with zero bend. Imported textures and finished environment meshes are not CPU-readable. Horizontal visibility culling disables offscreen instances. The native world contains 203 painted vegetation instances, plus background formations.

The extra uncompressed atlases add about **12 MiB of base GPU texture storage**. The macOS build increased from 125.7 to **137.7 MB**. This pass prioritizes clean alpha/painted edges; no atlas compression or hardware performance optimization was attempted.

## Verification

| Check | Result |
| --- | --- |
| Current source | Worktree and canonical local `main` started at `da45f4b`; origin fetched and checked for newer source. |
| Unity build | Unity **6000.3.25f1**, macOS build succeeded with **0 build errors**; [build log](logs/build-final.log). |
| Production validation | 12 fish, 24 aligned pose keys, 16 props, 10 environment grids, alpha, import settings, pivots, dimensions and both shaders passed. |
| Native environments | Four baseline and four final native runs completed with exit 0; all eight renders inspected. |
| Grounding iteration | First render exposed floating wide kelp footings on steep terrain. Revised placement buries them to the lower side of the slope; final native captures reviewed. |
| Native motion | 45 captured frames encoded successfully. Beginning/middle/end native frames inspected; browser video decoded 1280×720, duration 3.75, with no media error. |
| Small fry | 35-second native autoplay starting at radius 12 in the kelp biome. Final periodic sample: radius **15.7**, 3 lives, **385 points**, **10 eaten**. |
| Hunter | 22-second native autoplay starting at radius 30 over the reef; native captures inspected for scale and visibility. |
| Review artifact | All four views loaded their before/after images, exported boards, slider toggled/clipped correctly, video decoded. |
| Runtime logs / whitespace | Completed native runs had no game exceptions or shader errors; `git diff --check` passed. |

Autoplay sampled about **56–59 FPS** with a 60 FPS target, capture overhead and some concurrent runs. This is limited local evidence, not a comparative performance benchmark.

macOS still reports **zero active displays**. Verification used the built native macOS player with graphics enabled, an offscreen render texture, real shaders/meshes and the same HUD. Interactive windowed input, resizing, audio and display-connected performance remain unverified. WebGL/mobile were not built in this pass. Browser screenshot capture also failed in this environment; delivered PNG boards are explicitly comparison assemblies, while their source images are native Unity captures.

## Reproduce and hand off

Build the Unity project:

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath /path/to/DeepFeast/unity \
  -executeMethod DeepFeast.EditorTools.Build.Mac -logFile build.log
```

Run `unity/Builds/Mac/DeepFeast.app/Contents/MacOS/Deep Feast`, with absolute screenshot directories and graphics enabled:

```sh
"Deep Feast" -batchmode -mute -nopause -screen-width 1280 -screen-height 720 \
  -scenery kelp -shots /absolute/review/kelp -quitafter 4

"Deep Feast" -batchmode -mute -nopause -screen-width 1280 -screen-height 720 \
  -scenery kelp -animate-scenery -shotevery 0.0833333 \
  -shots /absolute/review/motion -quitafter 7.2

"Deep Feast" -batchmode -mute -nopause -screen-width 1280 -screen-height 720 \
  -autoplay -size 12 -startx 7200 -shark 4 -shotevery 4 \
  -shots /absolute/review/fry -quitafter 35
```

Use `reef`, `abyss` or `surface` instead of `kelp` for other fixed views. Do not add `-nographics`. Omit `-batchmode` on a display-connected machine for windowed play. For a baseline rebuild, use a separate checkout at `da45f4b` and run `git apply --unidiff-zero /path/to/baseline-harness.patch` before building.

After authoring a replacement atlas, run `python3 tools/catalog_atlases.py` (Pillow required), then validate/build and inspect native scenes. Roots, transparent gutters, slope contact, biome transitions and small-fish visibility are the key review points. [Evidence and SHA-256 hashes](evidence.json) identify the assets and captures used here. Local build output is ignored by Git; the source project, imported asset metadata and review artifacts are committed.
