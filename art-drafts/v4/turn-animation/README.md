# Fish turn animation comparison

[Looping comparison](index.html) · [GIF](fish-turn-before-after.gif) · [MP4](fish-turn-before-after.mp4) · [Midpoint still](mid-turn.png).

This close-up shows the player, clownfish and shark using the previous `542a24f` and current `8dbd365` fish renderers. Both use identical scripted turn inputs, closed mouths, fish positions and a plain review background. The old renderer reaches 6% width; the current renderer preserves 84%. These are native Unity player renders, assembled side by side, rather than a recording of player-controlled gameplay.

The capture-only `TurnReview.cs` and `CaptureBuild.cs` were placed in an isolated copy of the current Unity project. Bootstrap was redirected to `TurnReview` only when `-turn-animation` was supplied. Each capture restored `FishArt.cs` and `FishSwim.shader` from the relevant commit; species definitions, sprite creation, atlas selection and all atlas PNG bytes match between revisions. The production checkout and installed app were not altered for these captures.

Build the isolated project with `-executeMethod DeepFeast.EditorTools.CaptureBuild.Run`. Run its native player with `-batchmode -mute -turn-animation -shots /absolute/output`. The driver generates 120 native PNGs and `native-samples.json` at a repeatable 30 FPS timeline. Keep graphics enabled. Both final builds and captures exited 0. All frames were checked for top/bottom fin clipping; all timing/facing samples match between sides.

`compose.py` uses ffmpeg to create the captioned MP4, looping GIF and midpoint still. `labels.png` contains only the comparison captions and separators, rendered using a browser canvas. The MP4 contains 120 frames at 30 FPS; the GIF contains 80 frames at 20 FPS. Both last four seconds. See `evidence.json` for source/asset hashes and native measurements.
