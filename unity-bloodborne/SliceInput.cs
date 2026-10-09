using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Input wrapper working with both input backends.</summary>
public static class SliceInput
{
#if ENABLE_INPUT_SYSTEM
    static bool K(Key k) => Keyboard.current != null && Keyboard.current[k].isPressed;
    static bool KD(Key k) => Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame;
    public static Vector2 Move() => new Vector2((K(Key.D) ? 1 : 0) - (K(Key.A) ? 1 : 0), (K(Key.W) ? 1 : 0) - (K(Key.S) ? 1 : 0));
    public static Vector2 Look() => Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.1f : Vector2.zero;
    public static bool Attack() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    public static bool Dodge() => KD(Key.Space);
    public static bool Fire() => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    public static bool Heal() => KD(Key.F);
    public static bool LockOn() => KD(Key.Q);
    public static bool Restart() => KD(Key.R);
#else
    public static Vector2 Move() => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    public static Vector2 Look() => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 2f;
    public static bool Attack() => Input.GetMouseButtonDown(0);
    public static bool Dodge() => Input.GetKeyDown(KeyCode.Space);
    public static bool Fire() => Input.GetMouseButtonDown(1);
    public static bool Heal() => Input.GetKeyDown(KeyCode.F);
    public static bool LockOn() => Input.GetKeyDown(KeyCode.Q);
    public static bool Restart() => Input.GetKeyDown(KeyCode.R);
#endif
}
