using UnityEngine;
using DragonBall.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DragonBall.Player
{
    /// <summary>
    /// Reads keyboard input for Goku using Unity's New Input System (with Legacy fallback).
    /// Supports A/D or Left/Right arrows for movement, and W / Up Arrow / Space for jumping.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour, IFighterInput
    {
        public float MoveX { get; private set; }
        public float MoveY { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }

        // Ready for future combat & flight modules
        public bool AttackPressed { get; private set; }
        public bool KiBlastPressed { get; private set; }
        public bool BlockHeld { get; private set; }
        public bool FlightTogglePressed { get; private set; }

        private void Update()
        {
            ReadKeyboardInput();
        }

        private void ReadKeyboardInput()
        {
            MoveX = 0f;
            MoveY = 0f;
            JumpPressed = false;
            JumpHeld = false;
            AttackPressed = false;
            KiBlastPressed = false;
            BlockHeld = false;
            FlightTogglePressed = false;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                MoveX -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                MoveX += 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                MoveY += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                MoveY -= 1f;
            }

            JumpPressed =
                keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.wKey.wasPressedThisFrame ||
                keyboard.upArrowKey.wasPressedThisFrame;

            JumpHeld =
                keyboard.spaceKey.isPressed ||
                keyboard.wKey.isPressed ||
                keyboard.upArrowKey.isPressed;

            // Reserved fighting bindings
            AttackPressed = keyboard.jKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame;
            KiBlastPressed = keyboard.kKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame;
            BlockHeld = keyboard.lKey.isPressed || keyboard.leftShiftKey.isPressed;
            FlightTogglePressed = keyboard.fKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                MoveX -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                MoveX += 1f;
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                MoveY += 1f;
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                MoveY -= 1f;
            }

            JumpPressed =
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.W) ||
                Input.GetKeyDown(KeyCode.UpArrow);

            JumpHeld =
                Input.GetKey(KeyCode.Space) ||
                Input.GetKey(KeyCode.W) ||
                Input.GetKey(KeyCode.UpArrow);

            AttackPressed = Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Z);
            KiBlastPressed = Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.X);
            BlockHeld = Input.GetKey(KeyCode.L) || Input.GetKey(KeyCode.LeftShift);
            FlightTogglePressed = Input.GetKeyDown(KeyCode.F);
#endif
        }
    }
}
