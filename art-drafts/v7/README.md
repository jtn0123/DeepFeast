# Deep Feast: focus on sculpted 3D

The user selected the 3D direction, preferred the painted version's color, and identified the tails and overall artificial appearance as the next priorities. This pass starts from **40c193e**, improves the actual Unity renderer, and makes sculpted 3D the draft app's default. The canonical installed app has not been replaced. Source and review artifacts are on `t3code/draft-game-fish-visuals`.

## Review

- [Native turn before/after](sculpted-turn-before-after.gif) · [Sharper MP4](sculpted-turn-before-after.mp4).
- [Native swimming and feeding before/after](sculpted-swim-feeding-before-after.gif) · [Sharper MP4](sculpted-swim-feeding-before-after.mp4).
- [Tail and fin closeup](fin-swim-closeup.gif) · [Sharper MP4](fin-swim-closeup.mp4). This crops and enlarges the same hero footage without changing its motion or proportions.
- [Interactive review](index.html): all three clips, pause, replay, half speed and the twelve-species gallery.
- [All twelve species](gallery/gallery.png) · [Feeding poses](gallery/gallery_bite.png).

Left is the previous sculpted 3D draft from v6; right is this pass. Both are native Unity footage with matching four-second pose inputs. Neither is a generated concept illustration. These comparisons do not use the older installed sprite renderer as their left side.

## Changes

The bodies have a fuller shoulder, a smoother rear taper and a broader connection into the tail. The nose is more elongated, the oval species are less inflated, and species retain different body depth and proportions. The approved face and fitted mouth remain, including the animated jaw, tongue, teeth, paired eyes and expressions.

Caudal fins now use curved membrane strips running from a broad root to a forked, rounded, crescent or asymmetric shark edge. Clownfish get a rounded fan matching their painted reference; sharks get a stronger difference between upper and lower lobes. Dorsal and ventral roots follow the body surface; pectoral roots are fitted to its cross-section. Each fin has two surfaces, thickness that tapers toward the edge, camber, soft color variation, subtle rays and partial transparency. The old polygon-and-subdivision fin builder has been replaced.

The swim wave now travels from shoulder toward tail instead of in the opposite direction. Body and fin roots receive the same deformation, while the fin edge has additional delayed flex. Surface normals follow the body bend and fin movement. Mesh bounds include the larger tail sweep so bent tails remain visible at the viewport edge.

Color is richer through flank pigment, warmer fin tips, a curved hero stripe, stronger angelfish bands, pale belly tones, cooler rim color and wet highlights. Small scale arcs wrap around the body rather than being projected onto a flat side. Lighting keeps stronger shading around curved surfaces while adding a soft reflected fill below.

Sculpted 3D is selected without command-line flags. `-painted` keeps the painted treatment available as a comparison/debug option; the older `-sculpted` flag still works. Geometry remains cached and shared, with seven renderers per fish and one material. Gameplay, collision radii, scoring, AI, tier rules, terrain and atlas image files are unchanged.

## Validation and limits

The Unity **6000.3.25f1** macOS production build passed with zero errors. Production checks cover twelve species, 24 aligned pose keys, sixteen props, ten shared environment meshes, transparent atlases, four shaders and motion regressions. App signature verification passed.

The first native render exposed an invalid fin endpoint: floating-point `sin(PI)` rounded slightly below zero, and the fractional shape power generated NaN vertices. Unity emitted invalid-bounds warnings and omitted the combined fin mesh. A bounds-only check did not catch that state reliably, so generation now rejects nonfinite vertices before uploading a mesh. The guarded generation **failed before** the endpoint clamp and then **passed after** it for all twelve species. [Failure evidence](logs/mesh-guard-fail-before.log) · [Build checks](logs/build-checks.log).

Both new turn and feeding captures completed with exit 0 and the default style recorded as Sculpted. Before frames are byte-identical copies from v6. All four 120-frame sequences were checked for matching pose inputs, finite native samples and clipping. The twelve-species gallery and feeding poses were visually reviewed; ordinary Fry and Legend autoplay completed without runtime exceptions or invalid-mesh warnings. See [capture and runtime evidence](capture-checks.json).

All three comparison MP4s and GIFs passed full decoding checks. The review page's media, clip selection, pause, half speed, replay reset, download links and gallery decoding were checked through DOM automation. Preview playback was intermittent when backgrounded; native pointer input was not verified.

The comparison MP4s retain 30 FPS; GIFs show 20 FPS. Captures use native Metal graphics at 960 × 720, player/clownfish/shark radii 125/110/130, orthographic size 400, and a fixed camera. Turn input is `facing = cos(t*pi/2)`; feeding holds 45 degrees, opens twice, blinks and smiles. Both use `wag = 4+t*5` and explicit 1/30-second animation steps. The slow review sequence is not normal gameplay speed.

These are still stylized procedural fish, not finished realistic models. Authored species-specific silhouettes, more detailed fin attachment anatomy, a jaw with greater silhouette change, and an environment-aware reflection treatment would be worthwhile next steps. Target-device performance, physical input, audio, windowed presentation, WebGL and mobile were not tested. Native capture and smoke checks are not a target-device benchmark.

## Handover and reproduction

The implementation is in `FishVolume.cs` (body and membrane geometry, mesh guard and bounds), `FishVolume.shader` (coupled swimming, skin, fin shading), `Game.cs` and `FishTurnReview.cs` (3D default), and `MotionValidation.cs` (finite bounds and existing turn checks).

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath /absolute/DeepFeast/unity \
  -executeMethod DeepFeast.EditorTools.Build.Mac -logFile /absolute/build.log
"unity/Builds/Mac/DeepFeast.app/Contents/MacOS/Deep Feast" \
  -batchmode -turn-animation -shots /absolute/turn -logFile /absolute/turn.log
"unity/Builds/Mac/DeepFeast.app/Contents/MacOS/Deep Feast" \
  -batchmode -turn-animation -feed-animation -shots /absolute/feed -logFile /absolute/feed.log
```

Use `-gallery -quitafter 6 -mute -shots /absolute/gallery` for all species. Use `-autoplay -quitafter 20` for Fry, and add `-size 175` for Legend. Keep graphics enabled when capturing. Production meshes are shared and uploaded without retaining CPU-readable copies; reject invalid geometry before upload.

Committed native MP4s, caption PNGs and selected raw frames make the review portable. `python3 art-drafts/v7/compose.py` rebuilds comparisons from native MP4s when full PNG sequences are absent. Run `python3 art-drafts/v5/serve.py 4321`, then open `http://localhost:4321/art-drafts/v7/`; the server supports byte-range video seeking. The source revision is the commit containing this document.
