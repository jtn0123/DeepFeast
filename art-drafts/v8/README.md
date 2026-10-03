# Deep Feast: shark anatomy and remaining 3D refinements

This pass starts from **81f0a9b** and addresses the four next steps in the v7 handover: species silhouettes, fin attachments, jaw silhouette change and environment-aware reflections. The shark is the bottom fish in the animation and the lower-left fish in the gallery. All implementation changes are in the actual Unity mesh generator and shader. Sculpted 3D remains the draft default.

## Review

- [Shark turn before/after](shark-turn-before-after.gif) · [Sharper MP4](shark-turn-before-after.mp4).
- [Hero, clownfish and shark turning](species-turn-before-after.gif) · [Sharper MP4](species-turn-before-after.mp4).
- [Swimming, feeding and expressions](species-feeding-before-after.gif) · [Sharper MP4](species-feeding-before-after.mp4).
- [Interactive review with species galleries](index.html).
- [Previous species gallery](before-gallery/gallery.png) · [New species gallery](gallery/gallery.png).
- [Previous feeding poses](before-gallery/gallery_bite.png) · [New feeding poses](gallery/gallery_bite.png).

Left is the **v7 3D draft**, right is this pass. Both sides use native Unity Metal footage and identical four-second input timelines. The shark closeup crops the same region on both sides and preserves proportions and motion. It is not a comparison with the canonical installed sprite app, which remains at `8dbd365`.

## Implemented

**Shark identity.** Replaced the generic rounded front with a longer conical snout, a torpedo body, narrow tail stalk and side keels. Added a tall swept triangular first dorsal, a small second dorsal, long paired pectorals, paired pelvic fins and a small anal fin. The tail has narrow crescent lobes. Smaller dark eyes, five clear gill slits, nostril shading and a grey-to-white belly boundary reinforce the silhouette. Shark fins are thicker, opaque and less fluttery, and do not use the bony fish scale or fin-ray treatment. The visual reference is NOAA's [white shark species page](https://www.fisheries.noaa.gov/species/white-shark) and [identification guide](https://www.fisheries.noaa.gov/new-england-mid-atlantic/atlantic-highly-migratory-species/shark-identification-cooperative-shark-0).

**Other species.** Authored separate shoulder location, nose length and taper, rear taper, tail-stalk width, height and depth for all twelve species. Minnows and barracuda are slender; snappers taper more sharply; groupers have broader shoulders; parrotfish have fuller foreheads; tangs and angelfish are thin-bodied; puffers remain round. Tuna have a tapered torpedo body, longer pectorals and eight rear finlets. The hero's approved proportions and expression style remain.

**Fin attachment.** Tail roots use each body's actual stalk height. Spine fins follow its surface and centerline. Pectoral roots and flutter pivots are fitted to each species' cross-section rather than a fixed generic pivot. Fin bases have stronger attachment shading and thickness. Body and fin roots retain the same swimming displacement; edges flex separately. All geometry is cached and shared, with seven renderers and one material per fish.

**Feeding.** The shader now cuts an aperture in the body and recesses the interior of the fitted lip mesh, giving the mouth actual depth. The lower jaw has a stronger silhouette change. The shark's mouth sits below the projecting snout, has upper and lower teeth, and uses a restrained jaw rotation. The first native render showed an overly bulky shark jaw; it was reduced before the final captures. Both eyes, blinking, happy/angry expressions and the other fish's tongue treatment remain.

**Surrounding light.** Water-facing and downward-facing highlights blend upper water color with a reef, kelp or abyss fill based on each fish's depth. Grazing highlights use a Fresnel falloff, and the abyss treatment is weaker. Colors are cached and habitat calculations are shared across each fish's parts. This is an analytic environment-color approximation; it does not sample a cubemap or nearby objects.

Gameplay, collision radii, tier rules, scoring, AI, scene layouts and atlas pixels are unchanged.

## Validation and limits

The Unity 6000.3.25f1 macOS production build and existing twelve-species geometry/motion checks passed. The shader compiled for Metal, and the app signature passed verification. Final native captures completed after the finished rebuild. See [build checks](logs/build-checks.log) and [capture evidence](capture-checks.json).

Before frames are byte-identical copies from v7. All four 120-frame sequences were checked for finite samples, matching time/facing/yaw/mouth/expression inputs and frame-edge clipping. MP4s retain 30 FPS; GIFs use 20 FPS. Capture resolution is 960 × 720, the review camera and radii are unchanged, and animations use explicit 1/30-second steps. The slow review sequence is not ordinary gameplay speed.

The species and feeding galleries were visually inspected. Fry and Legend autoplay smoke checks completed. Native [reef](habitat-reef/scenery.png), [kelp](habitat-kelp/scenery.png) and [abyss](habitat-abyss/scenery.png) captures also completed without runtime exceptions. The review page's media, gallery decoding, clip selection, pause, half speed and replay reset were checked through DOM automation; background preview playback was intermittent. These remain stylized procedural models; this pass improves anatomy and motion without claiming photorealism. Physical input, audio, mobile, WebGL and target-device performance were not verified. Autoplay and native RenderTexture captures do not replace those checks.

## Handover

`unity/Assets/Scripts/FishVolume.cs` contains the authored anatomy parameters, fitted fin geometry and habitat uniforms. `unity/Assets/Resources/Shaders/FishVolume.shader` applies the same body profile to the mouth, jaw opening, fin stiffness, skin details and reflection tint. The source revision is the commit containing this document.

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath /absolute/DeepFeast/unity \
  -executeMethod DeepFeast.EditorTools.Build.Mac -logFile /absolute/build.log
"unity/Builds/Mac/DeepFeast.app/Contents/MacOS/Deep Feast" \
  -batchmode -turn-animation -shots /absolute/turn -logFile /absolute/turn.log
"unity/Builds/Mac/DeepFeast.app/Contents/MacOS/Deep Feast" \
  -batchmode -turn-animation -feed-animation -shots /absolute/feed -logFile /absolute/feed.log
```

Use `-gallery -quitafter 6 -mute -shots /absolute/gallery` for all species. Use `-autoplay -quitafter 20` for Fry, and add `-size 175` for Legend. Keep graphics enabled. The preceding draft is [v7](../v7/README.md).

Committed native MP4s, samples, caption PNGs and selected raw frames make the review portable. `python3 art-drafts/v8/compose.py` can rebuild comparisons from the native MP4s. Run `python3 art-drafts/v5/serve.py 4321`, then open `http://localhost:4321/art-drafts/v8/` for playback and seeking.
