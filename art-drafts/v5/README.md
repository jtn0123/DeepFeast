# Deep Feast: two native 3D fish drafts

The user requested more work on fish animation and more 3D volume, then asked to see both art directions. Both are implemented as review drafts in `t3code/draft-game-fish-visuals`, starting from **8dbd365e8cbb5272fa61fe0ff28ceddf1d6bc180**. The canonical local `main` and installed app remain at that baseline while the user reviews the styles. No push or deployment was performed.

## Review

- [Animated A/B comparison](both-3d-options.gif) · [Sharper MP4](both-3d-options.mp4).
- [Current installed animation beside both drafts](current-and-both-options.gif) · [MP4](current-and-both-options.mp4).
- [Interactive review](index.html): pause, replay, half speed, installed comparison and twelve-species galleries.
- [Painted 3D species gallery](gallery-painted/gallery.png) · [Sculpted 3D species gallery](gallery-sculpted/gallery.png).
- [Painted feeding pose](gallery-painted/gallery_bite.png) · [Sculpted feeding pose](gallery-sculpted/gallery_bite.png).

| Direction | Appearance | Common animation |
| --- | --- | --- |
| A: painted 3D | Existing atlas detail on rounded bodies. Baked eyes and pectoral fins are masked before adding mesh parts. Unseen frontal skin blends to a clean procedural surface. | Actual Y rotation through a head-on pose, paired eyes, solid fins, species-specific tail waves, turn curvature and body roll. |
| B: sculpted 3D | Procedural species palettes, bands, spots, belly shading, gill creases and stronger normal-based lighting. | The same rounded geometry and animation as A, making the art treatment directly comparable. |

These are native Unity renders, not generated concept images. The game remains a side-view game in the existing painted environment; option B is a 3D **fish** treatment, not a conversion of the whole world to 3D.

## Source and behavior

`FishVolume.cs` builds and caches seven meshes per species: body, caudal/dorsal/ventral fins, two pectoral fins, two globe eyes, and a mouth. Twelve shared models use one material and per-view property blocks. `FishVolume.shader` deforms the tail in depth, applies rooted fin movement and shades the curved surface. Fin corners are rounded and roots meet the body.

`FishView` retains the existing sprite fallback, but painted production fish use the volume rig. It rotates the model with positive scales rather than mirroring a sprite at the midpoint. The heading passes continuously through 90 degrees; roll and tail curvature follow turn direction. Blinks compress around the eye center, expressions tint lids, and feeding expands the mouth and selects the existing open-jaw artwork.

`Game.AnimateFish` advances the heading at a bounded angular rate: 540 degrees/second normally, 460 for puffers and 390 for sharks. The orthographic camera moves back to accommodate large models' depth without changing screen framing. Terrain, movement coordinates, collisions, AI, scoring and tier rules retain their existing logic. Atlas PNG files were not changed.

`FishTurnReview` is an opt-in native capture driver selected by `-turn-animation`. Normal play never enters this driver. `-sculpted` selects B; the draft build defaults to A. This branch's normal game also uses the draft renderer; do not copy its app into the canonical build until choosing the direction.

## Capture provenance and validation

The four-second clips show player, clownfish and shark, at radii 125, 110 and 130. Each uses 120 native 960×720 frames at 30 FPS, the same orthographic size 400, fixed positions, a closed jaw, `facing = cos(t*pi/2)` and `wag = 4+t*5`. This is a slowed visual review sequence; normal gameplay uses the angular rates above.

The installed baseline frames are byte-identical copies of the **after** frames from `v4/turn-animation`, captured from 8dbd365. They are not the older 6%-width renderer. A and B were freshly captured from the same final draft build. The camera's Z distance changes from −10 to −2000 to fit real mesh depth; orthographic projection and XY framing match. Species silhouettes intentionally change with the new geometry.

`compose.py` resizes and places untouched native captures together, then overlays caption PNGs drawn on a browser canvas. GIFs use 20 FPS; MP4s retain 30 FPS. The plain backdrop isolates fish motion. Galleries and gameplay screenshots come from the actual game camera and uGUI.

Verified locally:

- Unity **6000.3.25f1** macOS production build succeeded with **0 errors**; four shaders and existing atlas/import checks passed. Deep/strict app signature verification passed.
- The updated motion regression first failed against the installed sprite rig, then passed for all twelve species: real mesh depth, positive scales, finite transforms, continuous midpoint heading, a camera-facing midpoint, and correct downward pitch in both directions. A separate regression reproduced and fixed the draft rotation order that incorrectly tilted left-facing fish upward when descending.
- Both styles completed 120 native frames with exit 0. Captured turn inputs match the baseline exactly within floating-point tolerance. All 360 raw frames have non-background bounds strictly inside the capture edges. See [capture checks](capture-checks.json).
- Both twelve-species galleries, including feeding keys, completed with exit 0 and were visually reviewed.
- Twenty-second fry autoplay and eighteen-second Legend autoplay completed with exit 0 and no runtime exceptions. Periodic samples were 58–60 FPS on this Mac. This is limited capture-time evidence, not a target-device benchmark. A final eight-second Legend smoke run also exited 0 after the pitch correction.
- Both comparison MP4s/GIFs fully decode; their dimensions, duration and frame counts were checked. The review page's media and galleries load; pause, half speed, seek to one second and replay passed with byte-range media serving.

No active Mac display is available. Tests used the native player with Metal graphics and RenderTextures. Physical controls, windowed display behavior, audio, WebGL and mobile performance were not tested.

## Further polish after selecting a style

These models are procedural drafts. Species-specific sculpted heads, softer fin surfaces, a more articulated jaw, and stronger expression poses still need another pass. A preserves more character detail and better matches the current painted world; B makes volume clearer but needs richer authored surface detail. Keep the review comparison available while refining the chosen direction.

## Reproduce and hand over

Build with the project's standard method:

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath /absolute/DeepFeast/unity \
  -executeMethod DeepFeast.EditorTools.Build.Mac -logFile /absolute/build.log
```

Run each native capture from the repository root; `-shots` must use an absolute output path:

```sh
"unity/Builds/Mac/DeepFeast.app/Contents/MacOS/Deep Feast" \
  -batchmode -turn-animation -shots /absolute/painted -logFile /absolute/painted.log
"unity/Builds/Mac/DeepFeast.app/Contents/MacOS/Deep Feast" \
  -batchmode -turn-animation -sculpted -shots /absolute/sculpted -logFile /absolute/sculpted.log
```

For normal game review, omit `-turn-animation`; use `-gallery -quitafter 6 -mute -shots /absolute/gallery` for the twelve-species gallery, or `-autoplay -size 175 -quitafter 18` for a large-tier smoke run. Add `-sculpted` for B.

The committed native MP4s and selected PNG frames make this review portable without committing every raw frame. `python3 art-drafts/v5/compose.py` reassembles comparisons using the native MP4s when full PNG sequences are absent. Run `python3 art-drafts/v5/serve.py 4321` and open `http://localhost:4321/art-drafts/v5/`; this server supports video byte ranges for seeking. The source revision is the commit containing this document.
