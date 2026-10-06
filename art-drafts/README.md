# Latest Unity visual review

The latest review is [v8: shark anatomy and species refinement](v8/README.md), with [native shark, turn and feeding comparisons](v8/index.html). Sculpted 3D remains the draft app's default. The installed version remains **8dbd365**; see [v4 implementation handover](v4/HANDOVER.md) for that pass.

The original v1 concept handover is preserved below as historical context.

---

# Deep Feast visual drafts, v1

Start with `HANDOVER.md` for a complete receiving-agent briefing.

These are review concepts. No game code or existing artwork was replaced.

- `comparisons/` contains the current/draft boards presented in the conversation.
- `concepts/` contains AI-generated fish, prop, and full-scene concept sheets.
- `references/` contains screenshots rendered from the existing web prototype drawing functions, plus the isolated reference renderer and a frozen staged scene. These are web-rendered references, not screenshots of the Unity player.
- `index.html` is a review gallery with Fish, Hazards & reef, and Scene mockup views.
- `prompts.json` contains the exact prompts used with the built-in image generation tool.

## Draft direction

Keep the turquoise/gold player and recognizable species palettes. Add expressive eyes and mouths, clean contours, rounded shading, and fin detail. Give jellyfish a luminous bell and more visible tendrils; make the shield pearl iridescent; give kelp and coral more organic shapes and volume. The scene explores softer rays, layered background reefs, and richer foreground detail while retaining open swimming space.

## What remains after selecting a direction

The concept sheets have opaque backgrounds and are not animation-ready sprite atlases. Selected designs would need transparent individual sprites, separate rig parts or animation frames, small-size readability checks, and Unity integration. The full scene is an AI art mockup; it is not a tested game build or a pixel-exact layout contract.

## Open locally

From the repository root, run `python3 -m http.server 4318`, then open `http://localhost:4318/art-drafts/`.
