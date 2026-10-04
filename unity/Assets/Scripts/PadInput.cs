using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;

namespace DeepFeast
{
    // Gamepad play. The left stick or d-pad steers, with speed following how far the stick is
    // pushed; the south button, right trigger or right bumper dashes; Start pauses; East backs out
    // of the pause card and the Fishdex; Select mutes. Menus take the same pad through the UI
    // module's navigate, submit and cancel actions.
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
                if (state == GState.Play || state == GState.Paused) TogglePause();
                else if (!hud.DexOpen) hud.SubmitFocused();
            }
            if (pad.buttonEast.wasPressedThisFrame)
            {
                if (hud.DexOpen) hud.CloseDex();
                else if (state == GState.Paused) TogglePause(false);
            }
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
        // results card and the Fishdex, checking each step through the same input path as a real pad.
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
                case 29: Check(state == GState.Play, "south swims again"); Debug.Log("[DeepFeast] complete gamepad flow passed."); break;
            }
            padStage++;
        }
    }
}
