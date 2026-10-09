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
    public float camDistance = 6f, camHeight = 2f;

    public BloodborneCombat Combat { get; private set; }
    public SliceEnemy Target { get; private set; }

    CharacterController cc;
    Transform cam;
    float yaw, pitch = 15f, vy, swingStart = -1f;
    bool hitDone;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        Combat = GetComponent<BloodborneCombat>();
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

        if (SliceInput.Dodge() && Combat.Quickstep(dir.sqrMagnitude > 0.01f ? dir : -transform.forward)) swingStart = -1f;
        if (SliceInput.Attack() && !swinging) { swingStart = Time.time; hitDone = false; }

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
