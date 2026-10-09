using UnityEngine;

/// <summary>
/// Bloodborne-style health: damage taken leaves a rally pool that is
/// regained by landing hits before it decays. Quickstep dodge with i-frames.
/// </summary>
public class BloodborneCombat : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    [Tooltip("Seconds after a hit before the rally pool starts to drain.")]
    public float rallyWindow = 4f;
    public float rallyDecay = 10f;
    [Tooltip("Fraction of damage dealt that is regained from the rally pool.")]
    public float rallyRegain = 0.6f;

    [Header("Quickstep")]
    public float stepSpeed = 14f;
    public float stepTime = 0.2f;
    public float iFrames = 0.25f;
    public float stepCooldown = 0.6f;

    public float Health { get; private set; }
    public float Rally { get; private set; }
    public bool Invulnerable => Time.time < invulnUntil;
    public event System.Action Died;

    float lastHit = -999f, invulnUntil, stepUntil, nextStep;
    Vector3 stepDir;
    CharacterController cc;

    void Awake()
    {
        Health = maxHealth;
        cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (Rally > 0f && Time.time - lastHit > rallyWindow)
            Rally = Mathf.Max(0f, Rally - rallyDecay * Time.deltaTime);

        if (Time.time < stepUntil && cc)
            cc.Move(stepDir * stepSpeed * Time.deltaTime);
    }

    public void TakeDamage(float amount)
    {
        if (Invulnerable || Health <= 0f) return;
        float dealt = Mathf.Min(amount, Health);
        Health -= dealt;
        Rally = Mathf.Min(maxHealth - Health, Rally + dealt);
        lastHit = Time.time;
        if (Health <= 0f) { Rally = 0f; Died?.Invoke(); }
    }

    /// <summary>Call when this entity lands a hit; converts rally into health.</summary>
    public void RegisterHit(float damageDealt)
    {
        float heal = Mathf.Min(Rally, damageDealt * rallyRegain);
        Rally -= heal;
        Health = Mathf.Min(maxHealth, Health + heal);
    }

    public void Heal(float amount) { if (Health > 0f) Health = Mathf.Min(maxHealth, Health + amount); }

    public bool Quickstep(Vector3 direction)
    {
        if (Time.time < nextStep) return false;
        direction.y = 0f;
        stepDir = direction.sqrMagnitude > 0.01f ? direction.normalized : -transform.forward;
        stepUntil = Time.time + stepTime;
        invulnUntil = Time.time + iFrames;
        nextStep = Time.time + stepCooldown;
        return true;
    }
}
