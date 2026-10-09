using UnityEngine;

/// <summary>Third-person hunter: move, lock-on camera, melee swing, quickstep.</summary>
[RequireComponent(typeof(CharacterController), typeof(BloodborneCombat))]
public class SlicePlayer : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float gravity = 25f;
    public float damage = 25f;
    public float reach = 2.4f;
    public float swingHitDelay = 0.2f, swingTime = 0.55f;
    public float lockRange = 25f;
    public float maxStamina = 100f, attackCost = 20f, dodgeCost = 15f, staminaRegen = 35f;
    public int vials = 5;
    public float vialHeal = 40f;
    public float gunRange = 20f, gunCooldown = 0.8f;
    public float visceralDamage = 90f, visceralHeal = 35f;
    public float camDistance = 6f, camHeight = 2f;

    public BloodborneCombat Combat { get; private set; }
    public SliceEnemy Target { get; private set; }
    public float Stamina { get; private set; }
    float lastSpend, nextShot;

    CharacterController cc;
    Transform cam;
    float yaw, pitch = 15f, vy, swingStart = -1f;
    bool hitDone;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        Combat = GetComponent<BloodborneCombat>();
        Stamina = maxStamina;
        cam = Camera.main.transform;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (Combat.Health <= 0f) return;
        if (SliceInput.LockOn()) Target = Target ? null : Nearest();
        if (Target == null || !Target.Alive) Target = null;

        var look = SliceInput.Look();
        if (Target) yaw = Quaternion.LookRotation(Flat(Target.transform.position - transform.position)).eulerAngles.y;
        else yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, -10f, 60f);

        var m = SliceInput.Move();
        var dir = Quaternion.Euler(0, yaw, 0) * new Vector3(m.x, 0, m.y);
        bool swinging = Time.time - swingStart < swingTime;

        if (Time.time - lastSpend > 0.8f) Stamina = Mathf.Min(maxStamina, Stamina + staminaRegen * Time.deltaTime);

        if (SliceInput.Dodge() && Stamina >= dodgeCost && Combat.Quickstep(dir.sqrMagnitude > 0.01f ? dir : -transform.forward))
        { Spend(dodgeCost); swingStart = -1f; }
        if (SliceInput.Attack() && !swinging && Stamina > 0f) { Spend(attackCost); swingStart = Time.time; hitDone = false; }
        if (SliceInput.Fire() && Time.time >= nextShot) { nextShot = Time.time + gunCooldown; Shoot(); }
        if (SliceInput.Heal() && vials > 0 && Combat.Health < Combat.maxHealth) { vials--; Combat.Heal(vialHeal); }

        vy = cc.isGrounded ? -1f : vy - gravity * Time.deltaTime;
        cc.Move((dir.normalized * (swinging ? moveSpeed * 0.3f : moveSpeed) + Vector3.up * vy) * Time.deltaTime);

        var face = Target ? Flat(Target.transform.position - transform.position) : dir;
        if (face.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 15f * Time.deltaTime);

        if (swinging && !hitDone && Time.time - swingStart >= swingHitDelay) { hitDone = true; Swing(); }
    }

    void Swing()
    {
        foreach (var e in FindObjectsByType<SliceEnemy>(FindObjectsSortMode.None))
        {
            var to = e.transform.position - transform.position;
            if (e.Alive && to.magnitude <= reach + 0.8f && Vector3.Angle(transform.forward, Flat(to)) < 70f)
            {
                if (e.Staggered)
                {
                    e.TakeHit(visceralDamage);
                    Combat.Heal(visceralHeal);
                    e.Stagger(0f);
                    continue;
                }
                e.TakeHit(damage);
                Combat.RegisterHit(damage);
            }
        }
    }

    void LateUpdate()
    {
        var rot = Quaternion.Euler(pitch, yaw, 0);
        var pivot = transform.position + Vector3.up * camHeight;
        cam.position = pivot - rot * Vector3.forward * camDistance;
        cam.LookAt(Target ? Vector3.Lerp(pivot, Target.transform.position, 0.3f) : pivot);
    }

    void Spend(float amount) { Stamina = Mathf.Max(0f, Stamina - amount); lastSpend = Time.time; }

    // Pistol: a shot landing while the beast winds up staggers it for a visceral.
    void Shoot()
    {
        foreach (var e in FindObjectsByType<SliceEnemy>(FindObjectsSortMode.None))
        {
            var to = e.transform.position - transform.position;
            if (e.Alive && to.magnitude <= gunRange && Vector3.Angle(transform.forward, Flat(to)) < 12f)
            {
                if (e.Winding) e.Stagger(2.5f);
                else e.TakeHit(8f);
            }
        }
    }

    SliceEnemy Nearest()
    {
        SliceEnemy best = null; float bd = lockRange;
        foreach (var e in FindObjectsByType<SliceEnemy>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(e.transform.position, transform.position);
            if (e.Alive && d < bd) { bd = d; best = e; }
        }
        return best;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
}
