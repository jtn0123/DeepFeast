# Deep Feast: sharks and expanded Atlantic species

This pass starts from **3da41f7** on `t3code/draft-game-fish-visuals`. It changes the actual Unity models, shader, species data and spawning logic in this worktree. The canonical checkout was separately checked at **8dbd365** with clean tracked files; this pass has not been installed there.

## Review

- [White shark before/after](shark-before-after.gif) · [MP4](shark-before-after.mp4).
- [White, tiger and shortfin mako loops](sharks.gif) · [MP4](sharks.mp4).
- [All eight requested Atlantic fish](new-fish.gif) · [MP4](new-fish.mp4).
- [Full-size fish group 1](atlantic-a.mp4) · [Full-size fish group 2](atlantic-b.mp4).
- [Bluefish and red drum](extras.mp4).
- [Interactive review](index.html), with pause, half speed, replay and the retained fish models.
- [Species identities and source links](species-catalog.json), [fish anatomy references](fish-notes.md) and [shark anatomy references](shark-notes.md).

The shark comparison uses the previous **v8 3D draft** on the left and this pass on the right. Both sides use the same native review camera, radii, four-second inputs and crop. It does not compare against the installed sprite app. New species have labeled native loops because they had no previous model.

## Implemented

**Three shark identities.** White shark, tiger shark and shortfin mako have different body depth, snout profiles, dorsal positions, pectorals and tail lobes. Curved leading and trailing tail borders replace the straight V outline. Solid cartilage fins have camber, rooted attachments and restrained flutter. Five smaller gill marks, a shorter jaw crease, fitted mouths and teeth accompany the anatomy. Tiger stripes fade toward the underside; mako has a blue back and narrower pointed head.

**All eight requested fish.** Almaco jack, Atlantic goliath grouper, Atlantic halibut, Atlantic mackerel, Atlantic mahi-mahi, Atlantic skipjack tuna, Atlantic striped bass and Atlantic yellowfin tuna are in the normal fish pool. Bluefish and red drum are additional Atlantic species. They receive authored body proportions and dorsal/anal outlines, not just new colors. Mahi has a long banner dorsal and steep male forehead; yellowfin has swept sickle fins; mackerel and tunas have finlets; bass has two separate dorsals. Halibut is flattened through Z, with both eyes on the dark flank and a pale blind flank. Goliath and halibut do not appear in schooling packs.

**Skin and faces.** Markings include mackerel waves, bass flank stripes, skipjack belly stripes, yellowfin accents, scattered grouper/halibut mottling and red drum's tail-stalk spot. Spots vary position and radius to reduce the regular grid appearance. New wild-fish mouths use restrained interior colors; larger grouper gape and shark jaw parameters are species-specific. The existing hero style remains available.

**Gameplay and identity.** Timed shark encounters rotate white → tiger → mako, continuing through retries in the same app session. The mako's pursuit is 8% faster; shark life, spawn timing, size escalation, edible/flee and leaving rules retain their existing values. This is a game behavior choice, not a ranking of real shark aggression. Data stores a stable key, display name, scientific name and source URL for each new species. Normal gameplay has no floating species names. The opt-in review and JSON export expose those identities for handover. Generic legacy fish and the fictional hero do not receive invented scientific names.

**Rendering integration.** All 24 identities use the native 3D path, including species with generated skin fallbacks. Swapping a pooled view replaces all seven meshes and resets its motion. Geometry and materials remain shared per species. Player meshes discard CPU geometry after upload; editor meshes retain it so validation can inspect actual vertices rather than padded bounds. The legacy twelve-species atlas and its 24 aligned painted poses remain intact.

## Validation

The Unity 6000.3.25f1 macOS production build passes with zero errors and no FishVolume shader warnings. Build validation checks all 24 identities, all seven visible native mesh parts per species, finite actual vertices, real body depth, continuous turns, correct pitch, settled bank and frozen pause. It also checks generated sprite buffers before their CPU pixels are released, legacy atlas poses, metadata and the three-shark rotation. See [build checks](logs/build-checks.log).

Native captures use graphics-enabled Metal batch mode. Each loop contains 120 frames at 30 FPS with explicit 1/30-second animation steps. Labeled review groups are 1280 × 960; the unchanged comparison driver is 960 × 720. GIFs use 20 FPS. The native MP4s, JSON timelines and selected raw side/oblique/head-on frames are committed with this handover. [Capture checks](capture-checks.json) record timeline, frame-edge and media verification.

Fry and Legend autoplay checks exercise the actual shark encounter rotation in predator and edible cases. Native gallery checks cover all 24 identities. The review page's media decoding, selection, half speed, replay and links were checked through shared-browser DOM/media automation. Background preview playback was intermittent, and native browser snapshots were unavailable; physical playback controls were not verified. Review turns are deliberately slow. These remain stylized procedural models; mobile/WebGL, physical controls and target-device performance are not established by these captures.

## Source handover

| File | Responsibility |
| --- | --- |
| `unity/Assets/Scripts/FishArt.cs` | Species keys, names, references, ranges, fallback skins and native FishView routing. |
| `unity/Assets/Scripts/Game.cs` | Timed shark rotation and opt-in review/gallery entry points. |
| `unity/Assets/Scripts/FishVolume.cs` | Shared mesh assembly, eyes, mouths, patterns, pose uniforms and cache. |
| `unity/Assets/Scripts/SharkAnatomy.cs` | Three shark bodies, curved cartilage fins, caudal lobes and keels. |
| `unity/Assets/Scripts/ExpandedFishAnatomy.cs` | Ten new fish profiles, margin fins, sickles, finlets and selected tails. |
| `unity/Assets/Resources/Shaders/FishVolume.shader` | Stable wrapped markings, mouths, skin shading and shared fin deformation. |
| `unity/Assets/Scripts/SpeciesVisualReview.cs` | Labeled native review capture; opt-in only. |
| `unity/Assets/Editor/ArtValidation.cs`, `MotionValidation.cs` | Complete catalog and actual native geometry/motion checks. |

The source revision is the commit containing this document. Apply that source commit to the intended Unity branch before rebuilding; review media alone does not install the models.

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath /absolute/DeepFeast/unity \
  -executeMethod DeepFeast.EditorTools.Build.Mac -logFile /absolute/build.log
"unity/Builds/Mac/DeepFeast.app/Contents/MacOS/Deep Feast" \
  -batchmode -species-review -review-group sharks \
  -shots /absolute/sharks -logFile /absolute/sharks.log
"unity/Builds/Mac/DeepFeast.app/Contents/MacOS/Deep Feast" \
  -batchmode -turn-animation -shots /absolute/after-turn -logFile /absolute/turn.log
```

Review groups: `sharks`, `atlantic-a`, `atlantic-b`, `extras`, `legacy-a`, `legacy-b`, `legacy-c`. Use `-gallery -quitafter 6 -mute -shots /absolute/gallery` for the gameplay gallery. Use `-autoplay -shark 1 -timescale 3 -quitafter 62` for accelerated encounter smoke checks; add `-size 175` for Legend. Keep graphics enabled.

`python3 art-drafts/v9/compose.py` recreates review media from native frames or the committed native MP4s. `python3 art-drafts/v5/serve.py 4321` serves MP4 byte ranges for seeking; open `http://localhost:4321/art-drafts/v9/`.
