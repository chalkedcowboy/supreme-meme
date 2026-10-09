using UnityEngine;

/// <summary>Beast: chases, telegraphs (red) then strikes, recovers.</summary>
public class SliceEnemy : MonoBehaviour
{
    public float maxHealth = 120f, speed = 3.5f, range = 2.6f, damage = 30f;
    public float windup = 0.7f, recovery = 1.2f;

    public float Health { get; private set; }
    public bool Alive => Health > 0f;
    public float Max => maxHealth;
    public bool Staggered => state == S.Stagger;
    public bool Winding => state == S.Windup;

    enum S { Chase, Windup, Recover, Stagger }
    S state; float until;
    SlicePlayer player;
    Renderer rend;
    Color baseColor;

    void Start()
    {
        Health = maxHealth;
        player = FindFirstObjectByType<SlicePlayer>();
        rend = GetComponent<Renderer>();
        baseColor = rend.material.color;
    }

    void Update()
    {
        if (!Alive || !player || player.Combat.Health <= 0f) return;
        var to = player.transform.position - transform.position; to.y = 0;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 6f * Time.deltaTime);

        switch (state)
        {
            case S.Chase:
                rend.material.color = baseColor;
                if (to.magnitude > range) transform.position += to.normalized * speed * Time.deltaTime;
                else { state = S.Windup; until = Time.time + windup; }
                break;
            case S.Windup:
                rend.material.color = Color.red;
                if (Time.time >= until)
                {
                    if (to.magnitude <= range + 0.5f) player.Combat.TakeDamage(damage);
                    state = S.Recover; until = Time.time + recovery;
                }
                break;
            case S.Stagger:
                rend.material.color = Color.yellow;
                if (Time.time >= until) state = S.Chase;
                break;
            case S.Recover:
                rend.material.color = baseColor;
                if (Time.time >= until) state = S.Chase;
                break;
        }
    }

    public void Stagger(float time) { state = S.Stagger; until = Time.time + time; }

    public void TakeHit(float dmg)
    {
        Health -= dmg;
        if (state != S.Stagger) { state = S.Recover; until = Time.time + 0.4f; }
        if (!Alive) Destroy(gameObject, 0.3f);
    }
}
