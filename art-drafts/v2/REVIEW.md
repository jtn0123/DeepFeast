# Deep Feast — Unity implementation review and visual proposals, v2

> Historical baseline review at `5f75e02`. The subsequent implementation of all five priorities, fresh native captures and handover are in [`implemented/HANDOVER.md`](implemented/HANDOVER.md) and the [implemented before/after gallery](implemented/index.html). The review worktree has since advanced to that baseline and implemented the next pass.

## Verified current version

This review uses the actual Unity macOS app rebuilt from local `main` at **5f75e027ddcdf16a3524690ab2bdecf716ecfbd7**. The real source checkout is `~/Documents/Github/DeepFeast`. It was clean when inspected and after the build. The two newer local commits are:

- `362a181`: procedural lighting, reef layers, sand, rays, font and HUD polish.
- `5f75e02`: import six drafted fish, the jelly bell and reef cluster; improve other procedural props; add an art-gallery harness.

After fetching the remote, `origin/main` was still `d1e40a408a4d2046d01d6fbd8eb0b38bd186cb69`. The implementation is committed locally but not pushed to that remote. The original T3 review worktree is also still based on that older commit. Its source was not used as the current implementation baseline.

The rebuild passed with zero reported build errors. Its native macOS player was then run with the game's own `-gallery` and `-autoplay` screenshot flags. All current-side references in this v2 review come from that **Unity player**. No web-prototype artwork is used as a current baseline here.

The source concept sheets in the latest implementation have exactly the same SHA-256 checksums as the v1 drafts from this thread. These are genuine imports of those proposals.

## What was implemented from v1

| Proposed item | Observed implementation | Evidence |
| --- | --- | --- |
| Turquoise/gold player, clownfish, blue tang, puffer, angelfish and shark | Implemented as transparent body/tail textures cut from the original concept sheet. Visible in the Unity gallery. | `Assets/Resources/Concept/fish.json`; `FishArt.Get` / `LoadConcept`; `references/unity-gallery/gallery.png` |
| Avoid duplicated painted eyes/fins | Implemented: concept sprites retain painted features; the extra procedural pectoral fin and normal eye sprite are disabled for them. | `FishView.SetSpecies`, `HasFin`, `Pose` in `FishArt.cs` |
| Bite and expression states | Partly implemented: generated open-mouth textures and colored eyelid overlays; tail wag remains animated. The heads largely retain their closed silhouette. The painted shark has no separate open-mouth frame. | `tools/cut_concepts.py:216`; `fish.json`; `FishArt.cs:607`; `references/unity-gallery/gallery_bite.png` |
| Jellyfish | Concept bell imported in five hues, with animated procedural tentacles, bead tips and frilled oral arms. | `ConceptArt.JellyBell`; `Game.cs:182` and `1245`; native gallery |
| Shield pearl | Recreated procedurally with iridescent colors, cyan halo and sparkle. Visible in the gallery. It is not an imported crop of the pearl concept. | `Game.BuildPearlArt` at `Game.cs:140`; native gallery |
| Kelp | Procedural stems/leaves now have broad two-tone leaves and a central vein. Not the exact illustrated kelp sprite. | `World.cs:326`; native reef capture |
| Coral reef | Concept cluster exists, but is an occasional hero piece; most reef decor still uses improved procedural variants. | `ConceptArt.Reef`; `World.cs:165` |
| Water, rays, depth and HUD | Real improvements are present: layered reef silhouettes, soft-edged rays, caustics, ambient effects, a new display font and compact HUD panels. The full painted scene mockup was not imported as a background. | `SceneFx.cs`; `World.cs:377`; `Hud.cs`; native gameplay captures |
| Remaining six species | Still procedural: minnow, parrotfish, snapper, barracuda, grouper and tuna. In the native gallery, their flatter rendering contrasts with the six imported fish. | Only six entries in `fish.json`; fallback in `FishArt.cs:135`; native gallery |

These observations describe implementation and visual quality. They are not claims of gameplay defects.

## Recommended next pass, in priority order

### 1. Complete the fish style

Match the six remaining species to the already-installed painted fish. This has the clearest payoff because both styles are on screen together. Preserve species silhouettes, palettes and relative gameplay sizes. Give barracuda a narrow predatory jaw, grouper a heavier cheek/lip, and tuna a clear torpedo silhouette; avoid giving every fish the same face.

Deliverable: `concepts/remaining-six-fish-v2.png`, compared with crops from the native Unity gallery. This is a proposed style sheet, not installed assets.

### 2. Make the world as finished as the fish

The shallow capture has a large bright sand slope with a dense repeated caustic net. The distant reef and kelp still read as angular, flat silhouettes. The abyss is atmospheric but has broad empty dark/sand areas around the colorful fish. These differences make the painted fish look pasted onto a less finished world.

Use rounded layered distant reef forms, depth-tinted broad-leaf kelp, consistent foreground rock/coral shading, softer terrain-edge contact shadows and low-contrast sand ripples. Reduce continuous caustic lines in favor of sparse soft patches. Keep the swimming area clear. Treat the abyss separately with subdued bioluminescent clusters and cool silhouettes; do not simply brighten the entire depth.

Deliverable: `concepts/reef-lighting-depth-v2.png`, an art-direction mockup against the native shallow reef capture. It preserves the current scene's terrain and approximate object layout. It is not an implemented shader or performance-tested scene. The mockup intentionally makes lighting easy to compare; the final sand caustics should be more restrained than the bright illustrated patches, and background detail should remain subordinate to fish readability.

### 3. Draw bite poses as anatomy, then animate them

The current bite generator paints an ellipse and tongue onto the existing snout. A real open jaw should change the silhouette and deform the cheek/lips. The gallery's forced chase/bite state also applies an angry lid to the player; this is a deliberate harness pose, not proof that the player normally has that expression during feeding.

Prepare closed, anticipation, open, and recovery keys with matched head/body scale, aligned eye features and stable tail overlap. Keep the player eager and readable. Add a separate shark bite pose. Concept fish currently have their pectoral fins painted into the body; finishing their swimming animation will require separate parts, deformation, or whole-body frames.

Deliverable: `concepts/player-clown-bite-poses-v2.png`, proposed relaxed/open-jaw key poses beside current Unity rendered poses. This sheet demonstrates anatomy, not a finished looping animation.

### 4. Bring procedural props into the same material language

The jelly bell already matches the proposal well. Its procedural frilled arms, bead tips and fine tentacles can be softened and tapered. Keep pearl glow restrained. Let procedural coral and rocks share the installed fish's softer contours and light direction; the occasional imported reef cluster alone cannot unify the scene.

### 5. Preserve the HUD gains and polish feedback

The new font and panels are installed and substantially more coherent than the original. After the art/animation pass, tune bite particles, growth feedback, dash trails and threat emphasis around the clearer sprites. Avoid increasing glow and visual noise across every object.

## Artifacts and provenance

- `references/unity-gallery/gallery.png`: current normal species gallery, native Unity player.
- `references/unity-gallery/gallery_bite.png`: current forced chase/bite stress pose, native Unity player.
- `references/current-six-remaining-fish.png`: enlarged crops of the six procedural fish from that native gallery.
- `references/current-player-clown-poses.png`: enlarged player/clown normal and forced-bite crops from those native galleries.
- `references/unity-reef/`: menu and actual autoplay shallow gameplay captures.
- `references/unity-abyss/`: actual autoplay abyss captures.
- `concepts/`: new AI-generated review proposals using those current Unity captures as references.
- `comparisons/`: labeled current/proposal boards, assembled without replacing the current-side artwork.
- `prompts.json`: exact v2 prompts used with the built-in image generation tool.
- `logs/`: rebuild and native capture logs.
- `index.html`: interactive comparison gallery.

The screenshot crops intentionally enlarge the actual rendered game pixels. They are not high-resolution sprite exports and should not be used to judge source texture resolution in isolation.

## Reproduce the current captures

Unity executable:

```text
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity
```

Rebuild from the latest clean source:

```sh
Unity -batchmode -quit -projectPath /path/to/DeepFeast/unity \
  -executeMethod DeepFeast.EditorTools.Build.Mac -logFile build-latest-main.log
```

The resulting native player can capture the same types of views:

```sh
"Deep Feast" -gallery -mute -nopause -shots /path/to/gallery -quitafter 6 \
  -screen-width 1280 -screen-height 720

"Deep Feast" -autoplay -mute -nopause -size 30 -startx 5500 -shark 8 \
  -shots /path/to/reef -shotevery 4 -quitafter 23 \
  -screen-width 1280 -screen-height 720

"Deep Feast" -autoplay -mute -nopause -size 47 -startx 11000 -shark 80 \
  -shots /path/to/abyss -shotevery 5 -quitafter 16 \
  -screen-width 1280 -screen-height 720
```

Gameplay uses randomized spawns; these commands reproduce the capture conditions rather than an identical fish layout. Short autoplay logs reported approximately 120 FPS during these capture runs. That is limited local evidence, not a complete performance benchmark or animation validation.

## Implementation boundaries

This pass reviews the committed implementation and proposes further art. No game code, gameplay behavior, imported textures or source assets were edited. The installed app was rebuilt from the unchanged current source for verification. The v2 concept images still need selection, transparent sprites/rig parts or animation frames, small-size checks, and Unity integration before they become game assets.

The next developer should refresh the actual source checkout, start with the user's chosen priorities, and compare a fresh Unity player capture after each integrated change. Do not use the old v1 web-reference boards as proof of current Unity behavior.
