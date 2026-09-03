using PortalNes.Emulator.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PortalNes.UnityBridge
{
    public sealed class NesInputProvider : MonoBehaviour, INesInputProvider
    {
        public const byte A = 1 << 0;
        public const byte B = 1 << 1;
        public const byte Select = 1 << 2;
        public const byte Start = 1 << 3;
        public const byte Up = 1 << 4;
        public const byte Down = 1 << 5;
        public const byte Left = 1 << 6;
        public const byte Right = 1 << 7;

        [SerializeField, Range(1f, 30f), Tooltip("Number of turbo button presses generated per second.")]
        private float turboPressesPerSecond = 15f;

        [SerializeField, Range(0.1f, 0.95f), Tooltip("Left-stick magnitude required to press an NES direction.")]
        private float leftStickDirectionThreshold = 0.5f;

        public byte GetControllerState(int controllerIndex)
        {
            if (controllerIndex != 0) return 0;
            byte state = 0;
            bool turboA = false;
            bool turboB = false;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.zKey.isPressed || keyboard.jKey.isPressed) state |= A;
                if (keyboard.xKey.isPressed || keyboard.kKey.isPressed) state |= B;
                turboA |= keyboard.cKey.isPressed || keyboard.uKey.isPressed;
                turboB |= keyboard.vKey.isPressed || keyboard.iKey.isPressed || keyboard.lKey.isPressed;
                if (keyboard.rightShiftKey.isPressed || keyboard.backspaceKey.isPressed) state |= Select;
                if (keyboard.enterKey.isPressed) state |= Start;
                if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) state |= Up;
                if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) state |= Down;
                if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) state |= Left;
                if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) state |= Right;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                // Follow the NES pad's physical layout rather than matching the
                // Xbox face-button letters: NES B is left/bottom, NES A is right.
                if (gamepad.buttonEast.isPressed) state |= A;
                if (gamepad.buttonSouth.isPressed) state |= B;
                turboA |= gamepad.buttonNorth.isPressed;
                turboB |= gamepad.buttonWest.isPressed;
                if (gamepad.selectButton.isPressed) state |= Select;
                if (gamepad.startButton.isPressed) state |= Start;
                if (gamepad.dpad.up.isPressed) state |= Up;
                if (gamepad.dpad.down.isPressed) state |= Down;
                if (gamepad.dpad.left.isPressed) state |= Left;
                if (gamepad.dpad.right.isPressed) state |= Right;

                // Treat the analog left stick as a digital NES D-pad. Testing
                // each axis independently preserves intentional diagonals.
                Vector2 stick = gamepad.leftStick.ReadValue();
                if (stick.y >= leftStickDirectionThreshold) state |= Up;
                if (stick.y <= -leftStickDirectionThreshold) state |= Down;
                if (stick.x <= -leftStickDirectionThreshold) state |= Left;
                if (stick.x >= leftStickDirectionThreshold) state |= Right;
            }

            // A turbo button is a separate physical input. Holding the normal
            // button at the same time still keeps the NES button continuously on.
            bool turboOn = ((long)(Time.unscaledTimeAsDouble * turboPressesPerSecond * 2.0) & 1L) == 0;
            if (turboOn)
            {
                if (turboA) state |= A;
                if (turboB) state |= B;
            }

            // NES hardware cannot press opposite directions simultaneously through a normal pad.
            if ((state & (Up | Down)) == (Up | Down)) state &= unchecked((byte)~Down);
            if ((state & (Left | Right)) == (Left | Right)) state &= unchecked((byte)~Right);
            return state;
        }
    }
}
