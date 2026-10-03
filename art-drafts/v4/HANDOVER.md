# Deep Feast — composition, motion, habitats and interface

All five requested follow-up areas are implemented in the Unity game. This pass starts from **542a24fd2e1f8bc636d80918a3c7b7b6cfccf4d8**, the previously installed painted-environment revision. The implementation revision is the commit containing this document.

## Review the actual game

- [Interactive before/after review](index.html): nine comparisons, a wipe slider and two native motion clips.
- [Composition / kelp](comparisons/kelp.png) · [Shared fish lighting](comparisons/gallery.png) · [Reef](comparisons/reef.png) · [Abyss](comparisons/abyss.png) · [Surface](comparisons/surface.png).
- [Title](comparisons/menu.png) · [Pause](comparisons/pause.png) · [Results](comparisons/over.png) · [Turn visibility](comparisons/turn.png).
- [Fish motion](fish-motion.mp4): 51 native frames, 12 FPS, 4.25 seconds. [Habitat motion](habitat-motion.mp4): 44 native frames, 12 FPS, 3.67 seconds.
- [Small-fry gameplay](fry/play_01_t8_r13.png) · [Hunter gameplay](gameplay/play_00_t4_r30.png) · [960×720 title](after/narrow-menu/interface.png).

Both sides use native Unity camera/HUD renders. The environment baseline images are unchanged copies of the four `v3/after` captures from `542a24f`. Title, pause, results, turning and species-gallery baselines were freshly captured from that revision with only the [review harness patch](baseline-harness.patch) added. The patch applies with `git apply --unidiff-zero`. Camera framing and review fish placements match; scenery, shading and UI intentionally change. Ambient schools and real-time gallery phases can vary. Comparison boards assemble the untouched native images; they are not generated concepts or browser screenshots.

## Completed priorities

| Priority | Implemented result | Main source |
| --- | --- | --- |
| Composition and readability | Thinner, varied kelp; lower background contrast; varying coral patches; a more open swimming corridor. Painted vegetation drops from 203 to 142 instances. | `World.cs`, `SceneFx.cs` |
| Shared art direction | Fish, plants and painted props share restrained saturation, soft upper light and habitat tint. Fish retain clear outlines and a slight water-fog response. | `PaintedLighting.cginc`, `FishSwim.shader`, `PlantSway.shader`, `PaintedScenery.shader` |
| Fish movement | Broad silhouettes through turns, gentle banking, shape-specific tail/fin motion and body drift, stronger feeding anticipation and recovery. | `FishArt.cs`, `FishSwim.shader` |
| Habitat identity | Turquoise reef, green kelp and violet-blue abyss; an arch, terrace and eroded outcrop; continuously blended lighting/particles and a habitat HUD label. | `Habitat.cs`, `World.cs`, `SceneFx.cs`, `Game.cs` |
| Menus and HUD | Painted title hero, readable control cards, larger primary actions, consistent pause/results cards, keyboard focus and clearer score/depth hierarchy. | `Hud.cs` |

The landmarks are decorative, grounded behind the terrain and fish. Gameplay terrain, collisions, spawning rules, AI, scores and tier thresholds retain their existing behavior. All five production atlas PNGs are byte-identical to the baseline. This pass changes rendering, placement and animation code rather than generating replacement art. Existing jaw keys, alpha and sprite outlines remain intact.

## Turn regression

`AnimateFish` interpolated facing between −1 and +1; `FishView.Pose` used that value as physical width. Mid-turn fish collapsed toward zero, with an old 6% minimum. The new renderer uses facing as turn progress and preserves an 84% minimum width.

The new editor regression deliberately failed on the old implementation: a minnow at facing −0.1 rendered at 10% width. It passed after the minimal fix, then passed again during the final production build for **12 species × 7 facing samples**. Native midpoint captures show the player changing from **0.060** to **0.842** width ratio. The test requires at least 80%, allowing future restrained turn animation while preventing disappearance. See [fail-before](logs/turn-fail-before.log), [pass-after](logs/turn-pass-after.log) and [final build](logs/build-final.log).

## Verification

| Check | Result |
| --- | --- |
| Current source | Worktree and canonical local `main` began at `542a24f`; origin was fetched and inspected before editing. |
| Build and production art | Unity **6000.3.25f1**, macOS build succeeded, **0 build errors**, **137.7 MB**. Twelve species, 24 pose keys, 16 props, 10 environment meshes, atlas/import checks, three painted shaders and turn regression passed. |
| Native comparisons | Five fresh baseline player runs and nine final comparison runs exited 0; every delivered comparison was visually inspected. The four environment baselines retain their prior native capture provenance. |
| Menu flow | Real Unity Submit events passed play → pause → resume → game over → retry, with state/overlay assertions. This verifies button callbacks, not physical keyboard or pointer input. [Log](logs/after-flow.log). |
| Narrow layout | Native title captured and inspected at 960×720, with no card/text clipping. |
| Native animation | Both videos encoded and fully decoded without errors; beginning/middle/end source frames inspected. Fish clip includes turns and feeding poses; habitat clip shows rooted sway and drifting particles. |
| Small fry | 35-second native autoplay starting at radius 12 in kelp. Last periodic sample: radius **16.1**, **3 lives**, **422 points**, **10 eaten**. [Log](logs/fry.log). |
| Hunter | 22-second native autoplay starting at radius 30 over the reef; larger fish/scenery visibility inspected. [Log](logs/gameplay.log). |
| Review artifact | All nine before/after pairs loaded at 1280×720; nine boards exported; slider toggled and clipped correctly. Both videos loaded at 1280×720; seeking to a sampled frame succeeded with no media errors. |
| Installed native app | Copied to the canonical checkout; all **147 regular bundle files** match the verified build, and deep/strict code-signature verification passed. Installed app completed a kelp capture with exit 0. [Log](logs/installed-smoke.log). |
| Source hygiene | Atlas PNG bytes unchanged; baseline patch applies; whitespace check passed. |

Autoplay sampled approximately **57–59 FPS** with a 60 FPS target, capture overhead and concurrent runs. This is limited local evidence, not a performance comparison or target-device benchmark.

macOS reports **zero active displays**. Verification used the native macOS player with Metal graphics enabled, an offscreen render texture, real meshes/shaders and uGUI. Windowed controls, physical keyboard focus, audio, display resizing and display-connected video playback remain unverified. No WebGL/mobile build was performed. Review values on menu/results captures are fixed samples and do not write player records. The callback-flow harness freezes gameplay simulation and deliberately uses a zero-score game over.

## Build and reproduce

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath /path/to/DeepFeast/unity \
  -executeMethod DeepFeast.EditorTools.Build.Mac -logFile build.log

"Deep Feast" -batchmode -mute -nopause -screen-width 1280 -screen-height 720 \
  -scenery kelp -shots /absolute/review/kelp -quitafter 4

"Deep Feast" -batchmode -mute -nopause -screen-width 1280 -screen-height 720 \
  -interface menu -shots /absolute/review/menu -quitafter 4

"Deep Feast" -batchmode -mute -nopause -screen-width 1280 -screen-height 720 \
  -interface flow -shots /absolute/review/flow -quitafter 8.5

"Deep Feast" -batchmode -mute -nopause -screen-width 1280 -screen-height 720 \
  -scenery kelp -turn-review -turn 0.01 -shots /absolute/review/turn -quitafter 4

"Deep Feast" -batchmode -mute -nopause -screen-width 1280 -screen-height 720 \
  -gallery -animate-turns -shotevery 0.0833333 \
  -shots /absolute/review/fish-motion -quitafter 4.2

"Deep Feast" -batchmode -mute -nopause -screen-width 1280 -screen-height 720 \
  -scenery kelp -animate-scenery -shotevery 0.0833333 \
  -shots /absolute/review/habitat-motion -quitafter 7.2
```

Use `reef`, `abyss` or `surface` for the other environments, and `pause` or `over` for the other fixed interface states. Do not add `-nographics`. Omit `-batchmode` on a display-connected machine for windowed play. Serve the repository with a static HTTP server and open `art-drafts/v4/index.html` for the interactive review.

The earlier installed app is preserved at `unity/Builds/Mac/DeepFeast-before-542a24f.app` in the canonical checkout. Build output is Git-ignored. The portable source ZIP accompanying this commit includes Unity source/assets, tools and the v2/v3/v4 handovers; it excludes builds, Library, raw motion frames and full editor logs. No remote push is included. [Evidence and SHA-256 hashes](evidence.json) identify the source, assets, captures and videos used for this pass.
