using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;

namespace DeepFeast
{
    // Gamepad play. The left stick or d-pad steers, with speed following how far the stick is
    // pushed; the south button, right trigger or right bumper dashes; Start pauses; East backs out
    // of the pause card, the Fishdex and the settings; the bumpers turn the settings pages; Select
    // mutes. Menus take the same pad through the UI module's navigate, submit and cancel actions.
    public sealed partial class Game
    {
        const float PadDeadZone = 0.2f;
        bool padMode, padDash;
        Vector2 padMove;

        void ReadPad()
        {
            var pad = Gamepad.current;
            hud.ShowControls(pad == null ? Hud.Controls.KeyboardMouse : pad is DualShockGamepad ? Hud.Controls.PlayStation : Hud.Controls.Xbox);
            hud.ShowFocus = padMode || kbMode;
            padMove = Vector2.zero; padDash = false;
            if (pad == null) return;

            var stick = pad.leftStick.ReadValue();
            var dpad = pad.dpad.ReadValue();
            var move = dpad.sqrMagnitude > stick.sqrMagnitude ? dpad : stick;
            if (move.magnitude > PadDeadZone) { padMode = true; kbMode = false; pointerMode = false; }
            // The stick points up; the world's y runs down.
            padMove = new Vector2(move.x, -move.y);
            padDash = pad.buttonSouth.isPressed || pad.rightTrigger.isPressed || pad.rightShoulder.isPressed;

            if (pad.startButton.wasPressedThisFrame)
            {
                if (hud.SettingsOpen) hud.CloseSettings();
                else if (state == GState.Play || state == GState.Paused) TogglePause();
                else if (!hud.DexOpen) hud.SubmitFocused();
            }
            if (pad.buttonEast.wasPressedThisFrame)
            {
                if (hud.SettingsOpen) hud.CloseSettings();
                else if (hud.DexOpen) hud.CloseDex();
                else if (state == GState.Paused) TogglePause(false);
            }
            if (hud.SettingsOpen && pad.leftShoulder.wasPressedThisFrame) hud.SettingsTab(-1);
            if (hud.SettingsOpen && pad.rightShoulder.wasPressedThisFrame) hud.SettingsTab(1);
            if (pad.selectButton.wasPressedThisFrame) ToggleMute();
        }

        void PadSteer(out float dx, out float dy, out float mag)
        {
            dx = dy = mag = 0;
            float n = padMove.magnitude;
            if (n <= PadDeadZone) return;
            dx = padMove.x / n; dy = padMove.y / n;
            mag = Mathf.Clamp01((n - PadDeadZone) / (0.9f - PadDeadZone));
        }

        // -padtest: a virtual gamepad plays through the menu, steering, dashing, pausing, the
        // results card, the Fishdex and the settings, checking each step through the same input
        // path as a real pad.
        Gamepad testPad;
        int padStage;
        float padStartX;

        void PadFlow()
        {
            if (testPad == null)
            {
                // A headless player never has focus; keep the pad live regardless.
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                testPad = InputSystem.AddDevice<Gamepad>("DeepFeastTestPad");
            }
            void Set(GamepadState s) => InputSystem.QueueStateEvent(testPad, s);
            void Press(GamepadButton b) => Set(new GamepadState().WithButton(b));
            void Release() => Set(new GamepadState());
            void Check(bool ok, string step)
            {
                if (!ok) throw new InvalidOperationException($"gamepad {step} failed: state={state}, overlay={hud.ActiveOverlay}, selected={hud.Selected}.");
                Debug.Log($"[DeepFeast] gamepad {step} passed: state={state}, overlay={hud.ActiveOverlay}, selected={hud.Selected}.");
            }
            // Each step waits a beat after the last, giving the Input System and the UI module
            // a few frames to see a press and its release.
            float at = 3 + padStage * 0.3f;
            if (realTime < at) return;
            switch (padStage)
            {
                case 0: Press(GamepadButton.South); break;
                case 1: Release(); break;
                case 2:
                    Check(state == GState.Play, "south starts the swim");
                    pInvuln = 60; padStartX = player.x;
                    Set(new GamepadState { leftStick = new Vector2(1, 0) });
                    break;
                case 6:
                    Check(padMode && player.x > padStartX + player.r * 3, "left stick steers");
                    Set(new GamepadState { leftStick = new Vector2(1, 0) }.WithButton(GamepadButton.South));
                    break;
                case 7: Check(pDashing, "south dashes"); Release(); break;
                case 8: Press(GamepadButton.Start); break;
                case 9: Release(); break;
                case 10: Check(state == GState.Paused, "start pauses"); Shot("pad_pause"); Press(GamepadButton.East); break;
                case 11: Release(); break;
                case 12: Check(state == GState.Play, "east resumes"); GameOver(); break;
                // The results card takes focus after its short delay.
                case 16: Check(state == GState.Over && hud.Selected == "Button_ONE MORE SWIM", "results focus"); Press(GamepadButton.DpadRight); break;
                case 17: Release(); break;
                case 18: Check(hud.Selected == "Button_FISHDEX", "d-pad moves focus"); Shot("pad_over"); Press(GamepadButton.South); break;
                case 19: Release(); break;
                case 20: Check(hud.DexOpen, "south opens the Fishdex"); Press(GamepadButton.DpadRight); break;
                case 21: Release(); break;
                case 22: Check(hud.Selected == "Dex_" + Fishdex.Entries[1].key, "d-pad walks the Fishdex"); Shot("pad_dex"); Press(GamepadButton.East); break;
                case 23: Release(); break;
                case 24: Check(!hud.DexOpen && hud.ActiveOverlay == "over", "east closes the Fishdex"); break;
                case 27: Press(GamepadButton.South); break;
                case 28: Release(); break;
                case 29: Check(state == GState.Play, "south swims again"); Press(GamepadButton.Start); break;
                case 30: Release(); break;
                case 31: Check(state == GState.Paused && hud.Selected == "Button_KEEP SWIMMING", "start pauses again"); Press(GamepadButton.DpadDown); break;
                case 32: Release(); break;
                case 33: Check(hud.Selected == "Button_SETTINGS", "d-pad reaches settings"); Press(GamepadButton.South); break;
                case 34: Release(); break;
                case 35: Check(hud.SettingsOpen && hud.Selected == "Row_displayMode", "south opens settings"); Press(GamepadButton.RightShoulder); break;
                case 36: Release(); break;
                case 37: Check(hud.Selected == "Row_msaa", "right bumper turns the page"); Press(GamepadButton.DpadRight); break;
                case 38: Release(); break;
                case 39: Check(GameSettings.Data.msaa == 8, "d-pad right steps anti-aliasing up"); Press(GamepadButton.DpadLeft); break;
                case 40: Release(); break;
                case 41: Check(GameSettings.Data.msaa == 4, "d-pad left steps it back"); Shot("pad_settings"); Press(GamepadButton.East); break;
                case 42: Release(); break;
                case 43: Check(hud.ActiveOverlay == "pause" && hud.Selected == "Button_SETTINGS", "east closes settings"); Press(GamepadButton.Start); break;
                case 44: Release(); break;
                case 45: Check(state == GState.Play, "start resumes"); FlowPassed("gamepad"); break;
            }
            padStage++;
        }
    }
}
