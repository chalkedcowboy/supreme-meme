using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// X-ray vision ability: press the toggle key to see every XRayTarget through walls
/// for a limited time, then wait out a cooldown.
/// </summary>
public class XRayVision : MonoBehaviour
{
    [Tooltip("The Custom/XRay shader. Assign it so it is included in builds.")]
    public Shader xrayShader;
#if ENABLE_INPUT_SYSTEM
    public Key toggleKey = Key.X;
#else
    public KeyCode toggleKey = KeyCode.X;
#endif
    [Tooltip("Seconds the ability lasts. 0 = until toggled off.")]
    public float duration = 5f;
    [Tooltip("Seconds before it can be used again after it ends.")]
    public float cooldown = 3f;
    [Tooltip("Max distance at which targets are revealed. 0 = unlimited.")]
    public float range = 50f;
    public bool showStatus = true;

    public bool Active { get; private set; }
    public float TimeLeft { get; private set; }
    public float CooldownLeft { get; private set; }

    Material material;

    void Awake()
    {
        if (xrayShader == null) xrayShader = Shader.Find("Custom/XRay");
        if (xrayShader == null)
        {
            Debug.LogError("XRayVision: assign the Custom/XRay shader.", this);
            enabled = false;
            return;
        }
        material = new Material(xrayShader);
    }

    void Update()
    {
        CooldownLeft = Mathf.Max(CooldownLeft - Time.deltaTime, 0f);
        if (TogglePressed())
        {
            if (Active) SetActive(false);
            else if (CooldownLeft <= 0f) SetActive(true);
        }
        if (Active && duration > 0f)
        {
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f) SetActive(false);
        }

        float rangeSqr = range * range;
        Vector3 pos = transform.position;
        foreach (var t in XRayTarget.All)
            t.SetVisible(Active && (range <= 0f || (t.transform.position - pos).sqrMagnitude <= rangeSqr), material);
    }

    void SetActive(bool on)
    {
        Active = on;
        TimeLeft = on ? duration : 0f;
        if (!on) CooldownLeft = cooldown;
    }

    void OnDisable()
    {
        Active = false;
        foreach (var t in XRayTarget.All) t.SetVisible(false, material);
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }

#if ENABLE_INPUT_SYSTEM
    bool TogglePressed() => Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame;
#else
    bool TogglePressed() => Input.GetKeyDown(toggleKey);
#endif

    void OnGUI()
    {
        if (!showStatus) return;
        string status = Active ? (duration > 0f ? $"X-Ray: {TimeLeft:F1}s" : "X-Ray: ON")
            : CooldownLeft > 0f ? $"X-Ray: cooldown {CooldownLeft:F1}s"
            : $"X-Ray: ready [{toggleKey}]";
        GUI.Label(new Rect(10, 30, 250, 25), status);
    }
}
