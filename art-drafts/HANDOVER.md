# Latest Unity visual review

The latest review is [v8: shark anatomy and species refinement](v8/README.md), with [native shark, turn and feeding comparisons](v8/index.html). Sculpted 3D remains the draft app's default. The installed version remains **8dbd365**; see [v4 implementation handover](v4/HANDOVER.md) for that pass.

The original v1 concept handover is preserved below as historical context.

---

# Deep Feast — Visual Draft Handover

Prepared: October 2, 2026 (America/Los_Angeles). Version: 1.

## Start here

The user requested improved draft graphics for fish and other game visuals, presented beside the existing artwork for review. They then requested a handover document to pass the work to another agent or developer.

Six fish concepts, four hazard/prop groups, and one full gameplay art mockup are complete. These are review concepts, not integrated production assets. The game source is unchanged. No final art selection, game integration, commit, pull request, or deployment has happened in this thread.

Read this document with the accompanying images. This folder holds everything it refers to: all image files, the review gallery, reference renderers, and generation prompts.

## Project and working state

- Repository: DeepFeast.
- Worktree used: `~/.t3/worktrees/DeepFeast/t3code-ec3d5863`.
- Branch: `t3code/draft-game-fish-visuals`.
- Source HEAD when drafted: `d1e40a408a4d2046d01d6fbd8eb0b38bd186cb69`.
- New work is under `art-drafts/`; it was untracked when handed over.
- The real game is in `unity/`, using Unity 6000.3.25f1. The original browser reference is `web-prototype/index.html`.

Refresh the receiving checkout before implementation. The separate local checkout at `~/Documents/Github/DeepFeast` already had different `FishArt.cs` contents during inspection. Its existing Unity build was not used as the comparison baseline. Do not assume that build or another checkout matches this worktree.

## Delivered files

Paths below are relative to this document's folder, `art-drafts/`.

| File | What it provides |
| --- | --- |
| `comparisons/fish-current-vs-draft.png` | Six labeled fish comparisons, each with current art left and draft right |
| `comparisons/items-current-vs-draft.png` | Jellyfish, shield pearl, kelp, and coral comparisons |
| `comparisons/scene-current-vs-draft.png` | Current staged scene beside the proposed full-scene art direction |
| `concepts/fish-polished-v1.png` | 1536 × 1024 fish concept sheet |
| `concepts/items-polished-v1.png` | 1536 × 1024 hazard/prop concept sheet |
| `concepts/scene-polished-v1.png` | 1586 × 992 gameplay art mockup |
| `references/current-fish.png` | Existing fish rendered by the web prototype's drawing functions |
| `references/current-items.png` | Existing hazards and props rendered by the web prototype's drawing functions |
| `references/current-scene.png` | Frozen staged scene rendered by the existing web prototype |
| `index.html` | Browser review gallery: Fish, Hazards & reef, Scene mockup |
| `prompts.json` | Exact prompts used with the built-in image generation tool |
| `references/procedural-reference.js` | Copied existing fish/prop drawing functions plus reference-sheet staging |
| `references/capture.html` | Isolated reference-sheet viewer; `?mode=fish` or `?mode=items` |
| `references/gameplay-reference.html` | Copy of the prototype with a frozen review scene |
| `README.md` | Short overview and viewing instructions |

### Fish sheet order

The sheet has three columns and two rows. Each nominal cell is 512 × 512 pixels.

| Position | Species | Retained identity | Proposed improvement |
| --- | --- | --- | --- |
| Top left | Player | Turquoise body, golden fins/stripe, cream belly, three white spots | Bright hero shading, clearer face, detailed fins |
| Top middle | Clownfish | Orange body, three white bands with dark edges | Rounded volume, scalloped fins, playful expression |
| Top right | Blue tang | Blue disk body, navy markings, yellow tail | Stronger silhouette, fin detail, richer blue shading |
| Bottom left | Pufferfish | Round tan/gold body, cream belly, brown spots | Subtle spines, expressive face, fuller volume |
| Bottom middle | Angelfish | Tall gold fins, dark vertical stripes | Flowing fins, distinct attitude, more elegant shape |
| Bottom right | Shark | Slate-blue body, pale belly, triangular fins, shark tail | Sharper profile, gills, small eye and predatory smirk |

All fish on the concept sheet face right. The scene mockup mirrors appropriate species to match the staged scene.

### Prop sheet order

The sheet has two columns and two rows: jellyfish upper left, shield pearl upper right, kelp lower left, coral group lower right. The coral group contains pink branching coral, three purple tube sponges, and an orange brain coral.

The nominal quadrants are 768 × 512 pixels, but artwork is not guaranteed to fit exact atlas boundaries: a kelp tip slightly crosses the horizontal midpoint. The gallery adjusts the crop boundary for presentation. Extract individual subjects deliberately rather than importing this as a finished atlas.

## Art direction

Polished, colorful 2D arcade illustration with clean navy contours, restrained rounded shading, pearlescent highlights, translucent ribbed fins, and expressive faces. Preserve familiar species colors and shapes. The player should remain easy to locate through its turquoise/gold identity.

The environment draft explores soft underwater rays, distant reef and kelp layers, more dimensional sand/rocks/coral, and restrained floating particles. Keep the main swimming area open. Jellyfish remain visually distinct from collectible pearls: lavender sting hazard versus an ivory/pink pearl with a cyan protective halo.

The scene is a visual mockup. It broadly follows the current staged subject placement, size hierarchy, fish counts, and HUD, but its pixels, geometry, and text rendering are not a production layout contract.

## Reference provenance and limitations

The left-side images are actual renders of the existing **web prototype** drawing functions. Unity shares the species, palettes, and procedural-art approach, but these are not Unity player screenshots. The scene was deliberately staged and frozen for comparison, rather than captured from a natural play session.

The right-side images were generated with the built-in image generation tool using those references. All final concept PNGs are saved locally in this folder. Exact prompts are in `prompts.json`.

The sheets have opaque navy backgrounds. They are not transparent sprites, animation sheets, rigged assets, or performance-tested game resources. Fish include their eyes and fins in the illustration; importing them directly while retaining the existing separate eyes/fins would create duplicate features.

## Unity implementation map

| Source file | Relevant responsibility |
| --- | --- |
| `unity/Assets/Scripts/FishArt.cs` | Species/shape data, body and open-mouth sprites, tail, eye variants, pectoral fin, `FishView` rig |
| `unity/Assets/Scripts/Game.cs` | Fish pose/rendering, jellyfish bell and tentacles, pearl/glow, shield effects |
| `unity/Assets/Scripts/World.cs` | Seabed, rocks, coral, kelp, grass, anemones, procedural decor sprites |
| `unity/Assets/Scripts/SceneFx.cs` | Water, rays, parallax backgrounds, surface and depth effects |
| `unity/Assets/Scripts/Hud.cs` | HUD and menu construction |
| `unity/Assets/Scripts/Raster.cs` | Procedural sprite rasterization |

## Suggested continuation after the user selects a direction

1. Refresh the actual target checkout and confirm which drafted designs the user wants to carry forward.
2. Prepare one turquoise player asset first as a visible in-game proof of the direction.
3. Produce transparent, consistently scaled sprites. Choose either separate rig parts compatible with `FishView` or complete animation frames; adapt eye/fin rendering accordingly. Prepare normal, bite, blink, happy, and threat states as needed.
4. Check player/prey/predator readability at real gameplay sizes and depths, including facing flips and dash movement. Preserve growth, collision sizes, spawn rules, and scoring behavior.
5. Extend the selected style to the other species, jellyfish, pearl, and reef props. Keep animated tentacles/kelp and glow effects consistent with the prepared sprites.
6. Add the selected environment treatment in layers, with subtle rays and sufficient empty space. Avoid baking fish or HUD into a background image.
7. Capture real Unity before/after gameplay screenshots and verify animation, readability, sprite sorting, glow, frame time, and memory use.

This sequence is a proposed implementation path, not work already performed or authorization to change gameplay.

## Viewing and verification

From the repository root:

```sh
python3 -m http.server 4318
```

Open `http://localhost:4318/art-drafts/`. When using the transferred archive, extract it first and serve the directory containing `art-drafts/`.

The gallery and comparison views were inspected in the collaborative browser. Images decoded successfully; category buttons were exercised; the scene view had no horizontal overflow at a 390-pixel viewport. No Unity build, gameplay integration, automated gameplay test, or production-performance claim is made for these drafts.

## Paste-ready instruction for a receiving agent

> Continue the Deep Feast visual-draft work using the attached `art-drafts/HANDOVER.md` and image bundle. First inspect all three comparison boards and the actual target checkout. These are review concepts, and the baseline images come from the web prototype, not the Unity player. Preserve the turquoise/gold player and recognizable species palettes. Use the user's selected designs as the direction; prepare transparent sprites and animation/rig parts before integrating them. Keep gameplay mechanics unchanged and show a real Unity before/after proof when implementation is authorized. Do not treat the opaque concept sheets or full-scene mockup as ready-to-import production assets. Exact prompts and source references are included.
