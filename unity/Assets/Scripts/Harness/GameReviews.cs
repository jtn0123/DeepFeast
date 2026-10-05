using System;
using UnityEngine;

namespace DeepFeast
{
    // Fixed reviews for art and interface checks: the species gallery, environment scenery, the
    // menu and results screens, and the native UI flow that clicks through them.
    public sealed partial class Game
    {
        void SetupReview()
        {
            if (gallery) SetupGallery();
            if (scenery != null) SetupScenery();
            if (interfaceReview != null) SetupInterface();
        }

        // -gallery: every species side by side at one size, plus jellies and a pearl, for art review
        void SetupGallery()
        {
            state = GState.Play;
            hud.StartPlay();
            foreach (var f in fish) ReleaseView(f);
            fish.Clear(); schools.Clear();
            var all = Data.AllSpecies;
            zoom = 1.5f;
            cam = new Vector2(10200, 1300);
            int columns = all.Count > 30 ? 8 : 6;
            int slots = all.Count + 3, rows = Mathf.CeilToInt(slots / (float)columns);
            float cw = refW / columns / zoom, top = cam.y - refH / 2 / zoom;
            for (int i = 0; i < slots; i++)
            {
                float x = cam.x + (i % columns - (columns - 1) * 0.5f) * cw;
                float y = top + (105 + (i / columns + 0.5f) * (refH - 125) / rows) / zoom;
                if (i < all.Count)
                {
                    var f = MakeFish(all[i], 26, x, y, 1);
                    // -stage <tier index> shows the hero's look at that growth stage.
                    if (all[i] == Data.Player) f.stage = ArgF("-stage", 0);
                    fish.Add(f);
                }
                else if (i < all.Count + 2) AddJelly(x, y - 20, 20, i == all.Count ? 270 : 190);
                else
                    for (int k = 0; k < Powers.Count; k++) AddPearl(x + (k - 1.5f) * cw * 0.22f, y, 9, (PearlKind)k);
            }
            // Detail reviews: -zoom <times> moves in close, on -focus <species key> if given.
            zoom *= ArgF("-zoom", 1);
            string focusKey = Arg("-focus");
            if (focusKey != null)
            {
                var focus = fish.Find(f => f.sp.key == focusKey);
                if (focus != null) cam = new Vector2(focus.x, focus.y);
                else Debug.LogWarning($"[DeepFeast] -focus: no species with key '{focusKey}'.");
            }
            Debug.Log($"[DeepFeast] native gallery: {all.Count} catalog identities, {columns} columns, {rows} rows; " +
                string.Join(", ", System.Linq.Enumerable.Select(all, sp => sp.key + "=" + sp.displayName)));
        }

        void Gallery(float rdt)
        {
            realTime += rdt; time += rdt;
            foreach (var f in fish) f.wag += rdt * 5;
            foreach (var j in jellies) j.phase += rdt * j.pf;
            if (animateGallery)
            {
                float poseTime = time % 2.1f;
                float mouth = poseTime < 0.55f ? 0 : poseTime < 0.75f ? Mathf.InverseLerp(0.55f, 0.75f, poseTime) :
                    poseTime < 0.98f ? 1 : poseTime < 1.17f ? 1 - Mathf.InverseLerp(0.98f, 1.17f, poseTime) : 0;
                foreach (var f in fish)
                {
                    if (Has("-animate-turns")) { f.faceS = Mathf.Cos(time * Mathf.PI / 1.6f); f.face = f.faceS < 0 ? -1 : 1; }
                    f.mouth = mouth;
                    f.chomp = poseTime >= 0.98f && poseTime < 1.2f ? 1.2f - poseTime : 0;
                    f.state = mouth > 0.4f && f.sp != Data.Player ? FState.Chase : FState.Wander;
                }
                if (realTime >= nextShot) { Shot($"pose_{shotN++:000}"); nextShot += Mathf.Max(1 / 30f, shotEvery); }
                QuitWhenDue();
                return;
            }
            if (shotN == 0 && realTime > 2.5f) { Shot("gallery"); shotN = 1; }
            if (shotN == 1 && realTime > 3.5f) { foreach (var f in fish) { f.state = f.sp == Data.Player ? FState.Wander : FState.Chase; f.mouth = 1; } shotN = 2; }
            if (shotN == 2 && realTime > 4.5f) { Shot("gallery_bite"); shotN = 3; }
            QuitWhenDue();
        }

        // Fixed native camera/creatures/time for comparable environment reviews; normal gameplay never enters this path.
        void SetupScenery()
        {
            StartGame();
            foreach (var f in fish) ReleaseView(f);
            fish.Clear(); schools.Clear();
            foreach (var j in jellies) DestroyJelly(j);
            jellies.Clear();
            hud.ClearTexts();
            float x = scenery == "abyss" ? 11000 : scenery == "kelp" ? 7200 : scenery == "surface" ? 8600 : 5500;
            zoom = 1.05f;
            cam = new Vector2(x, scenery == "surface" ? 340 : world.FloorY(x) - 260);
            // -size shrinks the hero so the review fish become threats.
            player.r = startSize > 0 ? startSize : 22; tier = 1; pInvuln = 0;
            player.x = x; player.y = cam.y; player.wag = 2;
            var species = new[] { Data.SpeciesMap["clown"], Data.SpeciesMap["tang"], Data.SpeciesMap["snapper"], Data.SpeciesMap["angel"] };
            for (int i = 0; i < species.Length; i++)
            {
                float fx = x + (i - 1.5f) * 250;
                // -nearfloor: the fish swim just above the sand, to review their shadows.
                float fy = Has("-nearfloor") ? world.FloorY(fx) - 40 - i * 45 : cam.y - 100 + (i % 2) * 90;
                fish.Add(MakeFish(species[i], 17 + i * 2, fx, fy, i % 2 == 0 ? 1 : -1));
            }
            Debug.Log($"[DeepFeast] scenery {scenery}: camera={cam}, zoom={zoom}, fixed seed=20261003.");
        }

        void Scenery(float rdt)
        {
            realTime += rdt;
            // Checked first: a flow whose check failed or stalled still ends on time.
            QuitWhenDue();
            if (Has("-turn-review") && !turnReviewLogged && realTime > 3.2f)
            {
                turnReviewLogged = true;
                Debug.Log($"[DeepFeast] turn review: player width ratio={Mathf.Abs(playerView.root.transform.localScale.x):0.000}.");
            }
            if (interfaceReview == "flow")
            {
                time = 2;
                InterfaceFlow();
                return;
            }
            bool animate = Has("-animate-scenery");
            time = 2 + (animate ? shotN * shotEvery : 0);
            // -ripple: a ring from the hero just before the shot, as a hit would send.
            if (Has("-ripple") && !rippleShown && realTime > 2.8f) { rippleShown = true; Ripple(player.x, player.y, 1.6f); }
            foreach (var f in fish) f.wag = time * 5;
            player.wag = time * 5;
            if (Has("-turn-review"))
            {
                player.face = -1; player.faceS = ArgF("-turn", 0.01f);
                foreach (var f in fish) { f.face = -1; f.faceS = player.faceS; }
            }
            if (realTime > 3.1f && (shotN == 0 || animate && realTime >= nextShot))
            {
                Shot(animate ? $"scenery_{shotN:000}" : interfaceReview != null ? "interface" : Has("-turn-review") ? "turn" : "scenery");
                shotN++; nextShot = realTime + shotEvery;
            }
        }

        void SetupInterface()
        {
            if (interfaceReview == "menu" || interfaceReview == "flow") { state = GState.Menu; pActive = false; hud.ShowMenu(4240); }
            else if (interfaceReview == "pause") { state = GState.Paused; hud.ShowPause(true); }
            else if (interfaceReview == "victory") { state = GState.Victory; hud.ShowVictory(48210, 512, 1312, 48210); }
            else if (interfaceReview == "settings")
            {
                state = GState.Menu; pActive = false; hud.ShowMenu(4240); hud.OpenSettings();
                // -settingspage N shows another page; its first row takes focus.
                for (int i = 0; i < (int)ArgF("-settingspage", 0); i++) hud.SettingsTab(1);
            }
            else if (interfaceReview == "dex")
            {
                // A sample collection: most of the reef and kelp, a little of the deep, no sharks yet.
                for (int i = 0; i < Fishdex.Entries.Count; i++) if (i % 5 < 3 && !Fishdex.Entries[i].IsShark) Fishdex.Record(Fishdex.Entries[i]);
                state = GState.Menu; pActive = false; hud.ShowMenu(4240); hud.OpenDex();
            }
            else if (interfaceReview == "deep") { state = GState.Over; pActive = false; hud.ShowOver(48210, "Legend", 731, 1688, false, 48210, 2, 219, true, 219); }
            else { state = GState.Over; pActive = false; hud.ShowOver(2340, "Predator", 42, 164, false, 4240, 3); }
        }

        void InterfaceFlow()
        {
            void Check(GState expected, string overlay, string action)
            {
                if (state != expected || hud.ActiveOverlay != overlay)
                    throw new InvalidOperationException($"UI flow {action} failed: state={state}, overlay={hud.ActiveOverlay}.");
                Debug.Log($"[DeepFeast] UI flow {action} passed: state={state}, overlay={overlay}.");
            }
            void CheckSetting(bool ok, string action)
            {
                if (!ok) throw new InvalidOperationException($"UI flow {action} failed: selected={hud.Selected}, settings={JsonUtility.ToJson(GameSettings.Data)}.");
                Debug.Log($"[DeepFeast] UI flow {action} passed: selected={hud.Selected}.");
            }
            switch (uiFlowStage)
            {
                case 0 when realTime > 3.2f: Shot("menu"); uiFlowStage++; break;
                case 1 when realTime > 3.8f: hud.SubmitPrimary(); pInvuln = 0; Check(GState.Play, "none", "play submit"); uiFlowStage++; break;
                case 2 when realTime > 4.4f: TogglePause(true); Check(GState.Paused, "pause", "pause"); uiFlowStage++; break;
                case 3 when realTime > 4.8f: Shot("pause"); uiFlowStage++; break;
                case 4 when realTime > 5.4f: hud.SubmitPrimary(); Check(GState.Play, "none", "resume submit"); uiFlowStage++; break;
                case 5 when realTime > 5.8f: Shot("resumed"); uiFlowStage++; break;
                case 6 when realTime > 6.4f: GameOver(); Check(GState.Over, "over", "game over"); uiFlowStage++; break;
                case 7 when realTime > 7: Shot("over"); uiFlowStage++; break;
                case 8 when realTime > 7.4f: hud.SubmitPrimary(); Check(GState.Play, "none", "retry submit"); uiFlowStage++; break;
                case 9 when realTime > 8: Victory(); Check(GState.Victory, "victory", "victory"); uiFlowStage++; break;
                case 10 when realTime > 8.6f: Shot("victory"); uiFlowStage++; break;
                case 11 when realTime > 9: hud.SubmitPrimary(); Check(GState.Play, "none", "keep swimming submit"); uiFlowStage++; break;
                case 12 when realTime > 9.6f: GameOver(); Check(GState.Over, "over", "second game over"); uiFlowStage++; break;
                case 13 when realTime > 10: hud.SubmitButton("FISHDEX"); Check(GState.Over, "fishdex", "fishdex submit"); uiFlowStage++; break;
                case 14 when realTime > 10.6f: Shot("fishdex"); uiFlowStage++; break;
                case 15 when realTime > 11: hud.SubmitButton("BACK"); Check(GState.Over, "over", "fishdex back"); uiFlowStage++; break;
                case 16 when realTime > 11.4f: hud.SubmitPrimary(); Check(GState.Play, "none", "retry after fishdex"); uiFlowStage++; break;
                case 17 when realTime > 11.8f: TogglePause(true); Check(GState.Paused, "pause", "pause again"); uiFlowStage++; break;
                case 18 when realTime > 12.2f: hud.SubmitButton("SETTINGS"); Check(GState.Paused, "settings", "settings submit"); uiFlowStage++; break;
                case 19 when realTime > 12.8f: Shot("settings"); uiFlowStage++; break;
                case 20 when realTime > 13.2f:
                    hud.FocusSetting("master"); hud.MoveFocused(UnityEngine.EventSystems.MoveDirection.Left);
                    CheckSetting(GameSettings.Data.master == 90, "left lowers the master volume");
                    uiFlowStage++; break;
                case 21 when realTime > 13.8f:
                    Shot("settings_sound"); hud.MoveFocused(UnityEngine.EventSystems.MoveDirection.Right);
                    CheckSetting(GameSettings.Data.master == 100, "right raises it again");
                    uiFlowStage++; break;
                case 22 when realTime > 14.2f:
                    hud.SubmitButton("BACK"); Check(GState.Paused, "pause", "settings back");
                    CheckSetting(hud.Selected == "Button_SETTINGS", "focus returns to the settings button");
                    uiFlowStage++; break;
                case 23 when realTime > 14.6f: hud.SubmitPrimary(); Check(GState.Play, "none", "resume after settings"); uiFlowStage++; break;
                case 24 when realTime > 15: uiFlowStage++; FlowPassed("native UI"); break;
            }
        }
    }
}
