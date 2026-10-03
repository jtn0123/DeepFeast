# Deep Feast

A "Feeding Frenzy"-style arcade game. You start as a tiny fish, eat anything smaller than you, run from anything bigger, and grow up the food chain.

**Play:** open `index.html` in a browser (or `python3 -m http.server` in this folder, then visit `http://localhost:8000`).
It is one file with no dependencies, no build step and no image assets: every fish, coral and light ray is drawn in code.

## Controls
| Input | Action |
|---|---|
| Mouse / touch / WASD / arrows | Steer (the fish follows the cursor and slows down when the cursor is close) |
| Hold click / Space / Shift (on-screen DASH button on touch screens) | Dash, which drains the stamina bar |
| P / Esc | Pause |
| M | Mute |

## What's in the prototype
- **Food chain.** A fish can be eaten if it is under 90% of your size. A fish over 110% of your size can eat you. Fish in between just bump into you. Fish that can eat you get a red glow, and when one starts hunting you it shows a "!", gets an angry eyebrow and red eyes. A red arrow at the screen edge points to off-screen hunters.
- **Growth.** You grow by the area of each fish you eat, and the camera zooms out as you get bigger. There are 7 tiers: Fry → Minnow → Hunter → Predator → Apex → Leviathan → Legend. Each new tier gives you an extra life.
- **Ecosystem.** 10 fish species appear at different sizes: minnow schools, clownfish, tangs, angelfish, puffers, parrotfish, snappers, barracuda, groupers and tuna. Big fish eat small fish on their own, and schools scatter when you get close.
- **Events.** A shark shows up every 35–55 s and hunts you for about 14 s. Once you reach Leviathan, the shark runs from you instead. Jellyfish sting you at any size (you're stunned and lose your stamina). Glowing pearls give a 7-second bubble shield.
- **Scoring.** Eating several fish quickly builds a combo. At ×5 you get FEEDING FRENZY and at ×10 MEGA FRENZY. Your best score is saved in localStorage.
- **Look.**
  - A hand-shaped seabed with reefs, kelp slopes and two dark trenches.
  - Two layers of background rock spires that move slower than the foreground (parallax).
  - Light rays from the surface.
  - Darkness that increases with depth, with the player, jellyfish, pearls and anemones lighting it up.
  - Sea snow, bubbles, screen shake and a slow-motion moment when you die.
- **Audio.** All sound is generated live with WebAudio: chomps, combo notes, a tier-up arpeggio, a two-note shark motif, jellyfish zaps and an underwater ambient rumble.

A debug hook is available in the browser console: `__deepFeast.spawnShark()`, `__deepFeast.player.r = 60`, and so on.

---

## Which tech should the real game use?

| Option | Good at | Weak at | Verdict |
|---|---|---|---|
| **Vanilla JS + Canvas 2D** (this prototype) | No build step, runs anywhere, procedural art is easy, very fast to iterate | No built-in physics, scenes or asset pipeline. Several hundred gradient-filled fish is about the limit before you need WebGL | Best choice for proving the fun. That's done. |
| **Phaser 4** (released Apr 2026, now v4.2) | 2D web games. New WebGL renderer, built-in glow, bloom and blur filters, scenes, input, audio, tweens, sprite atlases. Deploys to web and wraps for mobile via Capacitor | Plain JS/TS stays verbose for big games | **Recommended next step** if this stays a web game. Most of this code ports directly. |
| **PixiJS v8** | The fastest 2D WebGL/WebGPU renderer, with shaders for water caustics and distortion | It only draws. You write the game loop, input and audio yourself | Good if we want shader-heavy visuals and keep our own game code |
| **Godot 4.7** (June 2026) | A real engine: editor, scenes, particles, 2D lights and shaders, GDScript. One-click export to desktop, mobile and web | Web builds are heavier (several MB WASM) and the web export uses WebGL 2 only | **Recommended** if the goal is Steam, itch.io or app stores, or a bigger game with levels |
| Unity / Defold / Bevy | Unity has the biggest ecosystem; Defold is a tiny-runtime 2D engine; Bevy is Rust | Overkill or a steeper learning curve for a 2D arcade game | Not needed here |

**My recommendation:** keep improving the gameplay in this single-file version until it feels great. Then either move to **Phaser 4** (stay on the web, share links, wrap for phones) or **Godot 4.7** (a "real" game with levels, an editor and store builds).

## Ideas for next steps
1. **Levels like Feeding Frenzy.** Each level has 3 growth stages and a boss, such as a Shark King or a giant anglerfish in the trench.
2. **Play as different species,** each with its own ability: a pufferfish that inflates to scare predators, a swordfish that dashes further, an anglerfish with a lure that pulls prey toward it.
3. **Biomes:** reef, kelp forest, open ocean, abyss (where you can only see by your own light) and an ice shelf.
4. **Hazards:** fishing hooks and nets, mines, whirlpools, electric eels.
5. **Power-ups:** speed, a magnet that draws in small fish, a frenzy multiplier, temporary invisibility.
6. **Better art:** real sprite sheets (hand-drawn or AI-assisted) and shaders for caustics, refraction and bloom.
7. **Multiplayer** in the style of agar.io, using WebSockets with a small Node or Bun server.
8. **Mobile:** a PWA or a Capacitor wrapper with tilt-to-steer.
