using UnityEngine;

namespace UZS.Gameplay
{
    /// <summary>
    /// Input abstraction. Gameplay reads only this interface, so a gamepad / Input System implementation
    /// can be added later without touching movement or combat code.
    /// </summary>
    public interface IPlayerInput
    {
        Vector2 Move { get; }
        Vector2 Look { get; }
        bool JumpPressed { get; }
        bool SprintHeld { get; }
        bool CrouchHeld { get; }
        bool FireHeld { get; }
        bool FirePressed { get; }
        bool AimHeld { get; }
        bool ReloadPressed { get; }
        bool InteractPressed { get; }
        bool InteractHeld { get; }
        bool MeleePressed { get; }
        bool SwapPressed { get; }
        int SlotPressed { get; }
        bool PausePressed { get; }
        bool ScoreboardHeld { get; }
    }

    /// <summary>Keyboard and mouse via the built-in Input Manager, using rebindable keys from <see cref="GameSettings"/>.</summary>
    public sealed class KeyboardMouseInput : IPlayerInput
    {
        public static readonly KeyboardMouseInput Instance = new KeyboardMouseInput();

        private static GameSettings S => GameSettings.Current;

        public Vector2 Move
        {
            get
            {
                float x = (Input.GetKey(S.KeyRight) ? 1f : 0f) - (Input.GetKey(S.KeyLeft) ? 1f : 0f);
                float y = (Input.GetKey(S.KeyForward) ? 1f : 0f) - (Input.GetKey(S.KeyBack) ? 1f : 0f);
                var v = new Vector2(x, y);
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public Vector2 Look
        {
            get
            {
                float sens = S.MouseSensitivity;
                float y = Input.GetAxisRaw("Mouse Y") * sens;
                return new Vector2(Input.GetAxisRaw("Mouse X") * sens, S.InvertY ? -y : y);
            }
        }

        public bool JumpPressed => Input.GetKeyDown(S.KeyJump);
        public bool SprintHeld => Input.GetKey(S.KeySprint);
        public bool CrouchHeld => Input.GetKey(S.KeyCrouch) || Input.GetKey(KeyCode.C);
        public bool FireHeld => Input.GetMouseButton(0);
        public bool FirePressed => Input.GetMouseButtonDown(0);
        public bool AimHeld => Input.GetMouseButton(1);
        public bool ReloadPressed => Input.GetKeyDown(S.KeyReload);
        public bool InteractPressed => Input.GetKeyDown(S.KeyInteract);
        public bool InteractHeld => Input.GetKey(S.KeyInteract);
        public bool MeleePressed => Input.GetKeyDown(S.KeyMelee) || Input.GetMouseButtonDown(2);
        public bool SwapPressed => Input.GetKeyDown(S.KeySwap) || Input.mouseScrollDelta.y != 0f;
        public int SlotPressed => Input.GetKeyDown(KeyCode.Alpha1) ? 0 : (Input.GetKeyDown(KeyCode.Alpha2) ? 1 : -1);
        public bool PausePressed => Input.GetKeyDown(KeyCode.Escape);
        public bool ScoreboardHeld => Input.GetKey(KeyCode.Tab);
    }
}
