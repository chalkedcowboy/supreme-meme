using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Source-style first-person controller with bunny hopping and air strafing.
/// Hold jump to hop on every landing. Gain speed in the air by holding A or D
/// while turning the mouse the same way.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class BhopController : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Child transform holding the camera. Defaults to the first child Camera.")]
    public Transform cameraPivot;
    public float mouseSensitivity = 2f;
    public float maxPitch = 89f;

    // Defaults are Source engine values scaled to meters (1 unit = 0.028 m).
    [Header("Ground")]
    public float moveSpeed = 7f;
    public float groundAccel = 10f;
    public float friction = 5f;
    public float stopSpeed = 2.8f;
    [Tooltip("Downward speed kept while grounded so the controller stays snapped to slopes.")]
    public float groundStick = 2f;

    [Header("Air")]
    public float airAccel = 12f;
    [Tooltip("Max speed gained along the wish direction per air tick. Low cap + high accel = air strafing.")]
    public float airSpeedCap = 0.84f;
    public float gravity = 22f;
    public float jumpSpeed = 8.4f;

    [Header("Bhop")]
    [Tooltip("On: hold jump to hop every landing. Off: press per hop; a press in the air is buffered until landing.")]
    public bool autoBhop = true;
    public bool showSpeed = true;

    public Vector3 Velocity => velocity;
    public float HorizontalSpeed => new Vector3(velocity.x, 0f, velocity.z).magnitude;
    public bool IsGrounded => grounded;

    CharacterController cc;
    Vector3 velocity;
    float pitch;
    bool jumpQueued;
    bool grounded;
    bool hitGround; // set by OnControllerColliderHit during Move

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cameraPivot == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraPivot = cam.transform;
        }
        SetCursorLocked(true);
    }

    void Update()
    {
        if (EscapePressed()) SetCursorLocked(false);
        else if (ClickPressed()) SetCursorLocked(true);

        Look();
        UpdateJumpQueue();

        Vector2 input = Vector2.ClampMagnitude(ReadMove(), 1f);
        Vector3 wishDir = transform.right * input.x + transform.forward * input.y;
        float wishSpeed = moveSpeed * input.magnitude;
        if (wishDir.sqrMagnitude > 0f) wishDir.Normalize();

        // Jumping before friction is what preserves speed between hops.
        if (grounded && jumpQueued)
        {
            velocity.y = jumpSpeed;
            grounded = false;
            jumpQueued = false;
        }

        if (grounded)
        {
            ApplyFriction();
            Accelerate(wishDir, wishSpeed, wishSpeed, groundAccel);
            velocity.y = -groundStick;
        }
        else
        {
            Accelerate(wishDir, wishSpeed, airSpeedCap, airAccel);
            velocity.y -= gravity * Time.deltaTime;
        }

        hitGround = false;
        cc.Move(velocity * Time.deltaTime);
        grounded = hitGround && velocity.y <= 0f;
    }

    void Look()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;
        Vector2 look = ReadLook() * mouseSensitivity;
        transform.Rotate(0f, look.x, 0f);
        pitch = Mathf.Clamp(pitch - look.y, -maxPitch, maxPitch);
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void UpdateJumpQueue()
    {
        if (autoBhop) jumpQueued = JumpHeld();
        else if (JumpPressed()) jumpQueued = true;
        else if (JumpReleased()) jumpQueued = false;
    }

    // Source/Quake acceleration: only the speed along wishDir is capped, so
    // turning while strafing keeps adding speed perpendicular to the velocity.
    void Accelerate(Vector3 wishDir, float wishSpeed, float speedCap, float accel)
    {
        float current = Vector3.Dot(velocity, wishDir);
        float add = Mathf.Min(wishSpeed, speedCap) - current;
        if (add <= 0f) return;
        float accelSpeed = Mathf.Min(accel * wishSpeed * Time.deltaTime, add);
        velocity += wishDir * accelSpeed;
    }

    void ApplyFriction()
    {
        float speed = HorizontalSpeed;
        if (speed < 0.001f)
        {
            velocity.x = velocity.z = 0f;
            return;
        }
        float drop = Mathf.Max(speed, stopSpeed) * friction * Time.deltaTime;
        float scale = Mathf.Max(speed - drop, 0f) / speed;
        velocity.x *= scale;
        velocity.z *= scale;
    }

    // Walkable surfaces count as ground; anything steeper (walls, ceilings,
    // surf ramps) clips velocity so the player slides along it.
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Vector3 n = hit.normal;
        if (Vector3.Angle(n, Vector3.up) <= cc.slopeLimit)
        {
            hitGround = true;
            return;
        }
        float into = Vector3.Dot(velocity, n);
        if (into < 0f) velocity -= n * into;
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void OnGUI()
    {
        if (!showSpeed) return;
        GUI.Label(new Rect(10, 10, 200, 25), $"Speed: {HorizontalSpeed:F1} m/s");
    }

#if ENABLE_INPUT_SYSTEM
    static Vector2 ReadMove()
    {
        var k = Keyboard.current;
        if (k == null) return Vector2.zero;
        return new Vector2(
            (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f),
            (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f));
    }

    // 0.1 matches the legacy "Mouse X/Y" axis scale.
    static Vector2 ReadLook() => Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.1f : Vector2.zero;
    static bool JumpHeld() => Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
    static bool JumpPressed() => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    static bool JumpReleased() => Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame;
    static bool EscapePressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    static bool ClickPressed() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
    static Vector2 ReadMove() => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    static Vector2 ReadLook() => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
    static bool JumpHeld() => Input.GetButton("Jump");
    static bool JumpPressed() => Input.GetButtonDown("Jump");
    static bool JumpReleased() => Input.GetButtonUp("Jump");
    static bool EscapePressed() => Input.GetKeyDown(KeyCode.Escape);
    static bool ClickPressed() => Input.GetMouseButtonDown(0);
#endif
}
