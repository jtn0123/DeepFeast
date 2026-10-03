# Deep Feast: refined 3D fish, both directions

This pass improves both native Unity 3D drafts from **dafc45f**. Source and review media are on `t3code/draft-game-fish-visuals`. The canonical local main and installed app remain at **8dbd365** while the user reviews the directions. No push or installation was performed.

## Review and transfer

Open [the interactive review](index.html) for both improved styles, previous/new turning and feeding, pause, half speed, replay and twelve-species galleries.

| Comparison | Animated GIF | Native MP4 |
| --- | --- | --- |
| Improved A and B | [Both styles](both-improved-options.gif) | [MP4](both-improved-options.mp4) |
| A: painted turn, previous/new | [Before/after](painted-before-after.gif) | [MP4](painted-before-after.mp4) |
| B: sculpted turn, previous/new | [Before/after](sculpted-before-after.gif) | [MP4](sculpted-before-after.mp4) |
| A: painted feeding, previous/new | [Before/after](painted-feeding-before-after.gif) | [MP4](painted-feeding-before-after.mp4) |
| B: sculpted feeding, previous/new | [Before/after](sculpted-feeding-before-after.gif) | [MP4](sculpted-feeding-before-after.mp4) |

These are actual native Unity frames. The comparisons labeled previous show the **previous 3D draft**, not the installed sprite renderer. The historical installed comparison is available in [v5](../v5/README.md).

## What changed

- Heads have a rounded ellipsoidal nose and a smooth taper into the tail, with species-specific depth and head proportions.
- Pectoral fins are curved, two-sided membranes with a broad root and softer tip. Dorsal, ventral and tail contours are rounded, triangulated through their concave shapes, subdivided and gently curved. Radial fin rays replace strong straight stripes.
- Eye sockets, colored irises, pupils and small glints sit closer to the head. Both eyes remain visible in the head-on pose.
- A fitted surface mouth replaces the floating oval. Its curved closed smile opens into a lip, dark cavity and tongue; shark and long predator mouths have small upper teeth. Lower head deformation follows the jaw. Painted skin stays on the closed atlas frame throughout feeding, avoiding the old texture swap.
- Turn bank and tail curvature follow smoothed angular velocity. Reversing a turn no longer flips bank instantly; the bank settles when the turn stops and freezes while paused. Reused fish views reset their motion state.
- Painted skin uses stable bind coordinates, projects inside the original body outline and masks baked eyes, fin and mouth features. Clean frontal skin is blended after atlas alpha composition, removing head-on color patches.
- Sculpted skin gains finer scales, a gold player stripe and spots, clownfish band borders, a minnow lateral line and multiple predator gill creases. Softer highlights and a proper orthographic view vector improve depth shading.

Both directions share the same geometry and motion. A preserves painted atlas detail; B uses procedural skin. Seven meshes per species remain cached and shared with one material. The world remains the existing painted side-view environment. Atlas image files, fish simulation, collisions, scoring and tier rules are unchanged.

## Validation and evidence

The Unity **6000.3.25f1** macOS production build succeeded with **0 errors**. Production validation covers twelve species, 24 aligned pose keys, sixteen props, ten shared environment meshes, transparent atlases, four shaders and volume turn checks. Deep/strict app signature verification passed.

The new bank regression **failed before** the motion fix: a 24.1-degree jump in one frame when a minnow's turn reversed. It then passed for all twelve species, including reversal continuity, settling, paused motion, positive scales, continuous midpoint heading and correct downward pitch in either direction. See [failure evidence](logs/bank-fail-before.log), [passing validation](logs/build-checks.log) and [capture checks](capture-checks.json).

Native capture timelines use 120 frames at 30 FPS, 960 × 720, player/clownfish/shark radii 125/110/130, fixed positions and orthographic size 400. Turning uses `facing = cos(t*pi/2)` and `wag = 4+t*5`. Feeding holds a 45-degree heading, opens twice with smooth input curves, then blinks and smiles. Both revisions receive identical pose inputs. The new rig receives an explicit animation step of 1/30 second so bank is independent of capture speed. Four seconds of scripted review motion is slower than ordinary gameplay.

Previous turn frames are byte-identical copies from v5. Previous feeding frames were captured before this pass's production changes, with only the shared feeding capture driver and an ignored optional time-step argument added. All eight sequences completed with exit 0, contain 120 native frames and stay inside the frame edges. MP4s retain 30 FPS; GIFs show 20 FPS. `compose.py` resizes native video and adds browser-canvas caption PNGs without illustrating or retouching the fish.

Both twelve-species galleries and feeding poses were captured and visually inspected. Painted Fry and sculpted Legend autoplay smoke runs completed with exit 0 and no runtime exceptions. These brief native runs provide smoke coverage, not a target-device performance benchmark. [Fry log](logs/gameplay.log) · [Legend log](logs/large-tier.log).

All five comparison videos and gallery images are available through the review server. The main clip's pause, half-speed mode, seek and replay were checked in the collaborative browser. The preview host disconnected during the final sweep through all five buttons, so that sweep was not completed. No active Mac display is available: native captures use Metal graphics and RenderTextures. Physical input, windowed presentation, audio, WebGL and mobile performance were not tested.

## Continue from this pass

Relevant source:

| File | Responsibility |
| --- | --- |
| `unity/Assets/Scripts/FishVolume.cs` | Shared body/fin/eye/mouth meshes and smoothed bank state |
| `unity/Assets/Resources/Shaders/FishVolume.shader` | Stable painted/procedural skin, fin/tail movement, fitted mouth and jaw shading |
| `unity/Assets/Scripts/FishArt.cs` | FishView integration, pooling reset, explicit animation time step |
| `unity/Assets/Scripts/Game.cs` | Pass the game's actual render time step, including zero while paused |
| `unity/Assets/Scripts/FishTurnReview.cs` | Opt-in turn and feeding native capture driver |
| `unity/Assets/Editor/MotionValidation.cs` | Meaningful turn, bank reversal, settling and pause regressions |

The draft app defaults to A. Add `-sculpted` to select B. Do not copy this draft app into the canonical installed build until the art direction has been selected. These are still procedural art drafts: species-specific authored UVs, more varied cheek/gill shapes, larger expression poses and lower-detail meshes for distant schools would be useful next refinements. Measure frame time on target devices before adopting this renderer broadly.

Build and capture from a receiving checkout:

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath /absolute/DeepFeast/unity \
  -executeMethod DeepFeast.EditorTools.Build.Mac -logFile /absolute/build.log
"unity/Builds/Mac/DeepFeast.app/Contents/MacOS/Deep Feast" \
  -batchmode -turn-animation -shots /absolute/turn -logFile /absolute/turn.log
"unity/Builds/Mac/DeepFeast.app/Contents/MacOS/Deep Feast" \
  -batchmode -turn-animation -feed-animation -sculpted \
  -shots /absolute/feed -logFile /absolute/feed.log
```

Omit `-turn-animation` for normal play. Use `-gallery -quitafter 6 -mute -shots /absolute/gallery` for all species, or `-autoplay -size 175 -quitafter 18` for Legend smoke coverage. Do not use `-nographics` when capturing.

Committed native MP4s and selected PNG frames make the review portable without all raw frame sequences. Run `python3 art-drafts/v6/compose.py` to reassemble comparisons from them. Run `python3 art-drafts/v5/serve.py 4321`, then open `http://localhost:4321/art-drafts/v6/`; this server supports video byte ranges. The source revision is the commit containing this document.
