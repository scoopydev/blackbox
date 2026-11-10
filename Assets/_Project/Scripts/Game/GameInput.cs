using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blackbox.Game
{
    public enum Key
    {
        Space, Enter, Escape, Up, Down, Left, Right, Tab, Backspace, Delete,
        P, R, N, Z, H, D, E, G, T,
        Shift, Ctrl,
    }

    /// <summary>
    /// Keyboard and mouse that works with either input backend. Unity 6 projects
    /// usually have the Input System package active; older settings use the legacy
    /// Input Manager. Pointer clicks on the interface go through the EventSystem.
    /// </summary>
    public static class GameInput
    {
#if ENABLE_INPUT_SYSTEM
        static readonly System.Text.StringBuilder s_Typed = new System.Text.StringBuilder();
        static Keyboard s_Hooked;
        static int s_TypedFrame = -1;

        static UnityEngine.InputSystem.Key Map(Key k)
        {
            switch (k)
            {
                case Key.Space: return UnityEngine.InputSystem.Key.Space;
                case Key.Enter: return UnityEngine.InputSystem.Key.Enter;
                case Key.Escape: return UnityEngine.InputSystem.Key.Escape;
                case Key.Up: return UnityEngine.InputSystem.Key.UpArrow;
                case Key.Down: return UnityEngine.InputSystem.Key.DownArrow;
                case Key.Left: return UnityEngine.InputSystem.Key.LeftArrow;
                case Key.Right: return UnityEngine.InputSystem.Key.RightArrow;
                case Key.Tab: return UnityEngine.InputSystem.Key.Tab;
                case Key.Backspace: return UnityEngine.InputSystem.Key.Backspace;
                case Key.Delete: return UnityEngine.InputSystem.Key.Delete;
                case Key.P: return UnityEngine.InputSystem.Key.P;
                case Key.R: return UnityEngine.InputSystem.Key.R;
                case Key.N: return UnityEngine.InputSystem.Key.N;
                case Key.Z: return UnityEngine.InputSystem.Key.Z;
                case Key.H: return UnityEngine.InputSystem.Key.H;
                case Key.D: return UnityEngine.InputSystem.Key.D;
                case Key.E: return UnityEngine.InputSystem.Key.E;
                case Key.G: return UnityEngine.InputSystem.Key.G;
                case Key.T: return UnityEngine.InputSystem.Key.T;
                case Key.Shift: return UnityEngine.InputSystem.Key.LeftShift;
                default: return UnityEngine.InputSystem.Key.LeftCtrl;
            }
        }

        public static bool Down(Key k)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (k == Key.Enter && kb.numpadEnterKey.wasPressedThisFrame) return true;
            return kb[Map(k)].wasPressedThisFrame;
        }

        public static bool Held(Key k)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (k == Key.Shift) return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (k == Key.Ctrl) return kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
            return kb[Map(k)].isPressed;
        }

        /// <summary>Characters typed this frame.</summary>
        public static string Typed
        {
            get
            {
                Hook();
                return s_TypedFrame == Time.frameCount ? s_Typed.ToString() : string.Empty;
            }
        }

        /// <summary>Call once a frame so typed text is caught even before anything asks for it.</summary>
        public static void Poll() => Hook();

        static void Hook()
        {
            var kb = Keyboard.current;
            if (kb == s_Hooked) return;
            if (s_Hooked != null) s_Hooked.onTextInput -= OnText;
            s_Hooked = kb;
            if (kb != null) kb.onTextInput += OnText;
        }

        // Text arrives as events before Update runs. Only this frame's characters count,
        // so the N that opens the naming palette doesn't also type an N into it.
        static void OnText(char c)
        {
            if (s_TypedFrame != Time.frameCount)
            {
                s_TypedFrame = Time.frameCount;
                s_Typed.Clear();
            }
            s_Typed.Append(c);
        }

        public static Vector2 PointerPosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        static KeyCode Map(Key k)
        {
            switch (k)
            {
                case Key.Space: return KeyCode.Space;
                case Key.Enter: return KeyCode.Return;
                case Key.Escape: return KeyCode.Escape;
                case Key.Up: return KeyCode.UpArrow;
                case Key.Down: return KeyCode.DownArrow;
                case Key.Left: return KeyCode.LeftArrow;
                case Key.Right: return KeyCode.RightArrow;
                case Key.Tab: return KeyCode.Tab;
                case Key.Backspace: return KeyCode.Backspace;
                case Key.Delete: return KeyCode.Delete;
                case Key.P: return KeyCode.P;
                case Key.R: return KeyCode.R;
                case Key.N: return KeyCode.N;
                case Key.Z: return KeyCode.Z;
                case Key.H: return KeyCode.H;
                case Key.D: return KeyCode.D;
                case Key.E: return KeyCode.E;
                case Key.G: return KeyCode.G;
                case Key.T: return KeyCode.T;
                case Key.Shift: return KeyCode.LeftShift;
                default: return KeyCode.LeftControl;
            }
        }

        public static bool Down(Key k)
        {
            if (k == Key.Enter && Input.GetKeyDown(KeyCode.KeypadEnter)) return true;
            return Input.GetKeyDown(Map(k));
        }

        public static bool Held(Key k)
        {
            if (k == Key.Shift) return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (k == Key.Ctrl) return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            return Input.GetKey(Map(k));
        }

        public static string Typed => Input.inputString ?? string.Empty;

        public static void Poll() { }

        public static Vector2 PointerPosition => Input.mousePosition;
#endif

        /// <summary>Space or Enter.</summary>
        public static bool Confirm => Down(Key.Enter) || Down(Key.Space);
    }
}
