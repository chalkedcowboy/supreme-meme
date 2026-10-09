using System;
using System.Collections.Generic;
using UnityEngine;

// Generational respawn: each death starts a new generation that inherits a
// fraction of the previous run's best speed as a starting bonus.
public class Bloodline : MonoBehaviour
{
    [Serializable]
    public struct Generation
    {
        public int index;
        public float bestSpeed;
        public float lifetime;
    }

    [SerializeField] Transform spawnPoint;
    [SerializeField, Range(0f, 1f)] float inheritance = 0.25f;
    [SerializeField] float maxBonus = 8f;
    [SerializeField] float killY = -50f;

    public IReadOnlyList<Generation> History => history;
    public int Current => history.Count;
    public float StartBonus { get; private set; }

    readonly List<Generation> history = new List<Generation>();
    Rigidbody body;
    CharacterController cc;
    float bestSpeed, born;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        cc = GetComponent<CharacterController>();
        born = Time.time;
    }

    void Update()
    {
        Vector3 v = body ? body.linearVelocity : cc ? cc.velocity : Vector3.zero;
        bestSpeed = Mathf.Max(bestSpeed, new Vector3(v.x, 0f, v.z).magnitude);
        if (transform.position.y < killY) Die();
    }

    public void Die()
    {
        history.Add(new Generation { index = history.Count + 1, bestSpeed = bestSpeed, lifetime = Time.time - born });
        StartBonus = Mathf.Min(bestSpeed * inheritance, maxBonus);
        bestSpeed = 0f;
        born = Time.time;
        Respawn();
    }

    void Respawn()
    {
        if (!spawnPoint) return;
        if (cc) cc.enabled = false;
        transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        if (cc) cc.enabled = true;
        if (body)
        {
            body.linearVelocity = spawnPoint.forward * StartBonus;
            body.angularVelocity = Vector3.zero;
        }
    }
}
