using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Blood resource for BhopController: fills while airborne above a speed
/// threshold (clean strafing), drains on a surge. Press E to surge forward.
/// </summary>
[RequireComponent(typeof(BhopController))]
public class Bloodborn : MonoBehaviour
{
    public float maxBlood = 100f;
    [Tooltip("Horizontal speed above which airtime fills blood.")]
    public float fillSpeed = 9f;
    public float fillRate = 12f;
    public float surgeCost = 40f;
    public float surgeSpeed = 10f;
    public float surgeCooldown = 0.5f;
    public bool showHud = true;

    public float Blood { get; private set; }

    BhopController bhop;
    float nextSurge;

    void Awake() => bhop = GetComponent<BhopController>();

    void Update()
    {
        if (!bhop.IsGrounded && bhop.HorizontalSpeed > fillSpeed)
            Blood = Mathf.Min(maxBlood, Blood + fillRate * Time.deltaTime);

        if (SurgePressed() && Blood >= surgeCost && Time.time >= nextSurge)
        {
            Blood -= surgeCost;
            nextSurge = Time.time + surgeCooldown;
            Vector3 dir = bhop.Velocity; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            bhop.AddVelocity(dir.normalized * surgeSpeed);
        }
    }

#if ENABLE_INPUT_SYSTEM
    static bool SurgePressed() => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
    static bool SurgePressed() => Input.GetKeyDown(KeyCode.E);
#endif

    void OnGUI()
    {
        if (!showHud) return;
        GUI.Label(new Rect(10, 35, 200, 25), $"Blood: {Blood:F0}/{maxBlood:F0}  [E] surge");
    }
}
