using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

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
    }
}
