using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drop this on nothing: it auto-builds the arena, player, camera, enemy and HUD
/// on play in any scene. Controls: WASD move, mouse look, LMB attack,
/// Space quickstep, RMB pistol, F vial, Q lock-on, R restart.
/// </summary>
public class SliceBootstrap : MonoBehaviour
{
    SlicePlayer player;
    SliceEnemy[] enemies;
    GUIStyle big;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init() => new GameObject("Slice").AddComponent<SliceBootstrap>();

    void Start()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.localScale = new Vector3(5, 1, 5);
        floor.GetComponent<Renderer>().material.color = new Color(0.15f, 0.13f, 0.14f);

        var cam = Camera.main;
        if (!cam) cam = new GameObject("Cam", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
        cam.clearFlags = CameraClearFlags.SolidColor;

        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);
        var light = new GameObject("Moon").AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.6f, 0.7f, 1f);
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        p.name = "Hunter";
        p.transform.position = new Vector3(0, 1.1f, -8);
        p.GetComponent<Renderer>().material.color = new Color(0.25f, 0.25f, 0.3f);
        Destroy(p.GetComponent<CapsuleCollider>());
        p.AddComponent<CharacterController>();
        p.AddComponent<BloodborneCombat>();
        player = p.AddComponent<SlicePlayer>();

        var e = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        e.name = "Beast";
        e.transform.position = new Vector3(0, 1.5f, 8);
        e.transform.localScale = new Vector3(1.6f, 1.5f, 1.6f);
        e.GetComponent<Renderer>().material.color = new Color(0.4f, 0.25f, 0.2f);
        e.AddComponent<SliceEnemy>();
    }

    void Update()
    {
        if (SliceInput.Restart()) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        enemies = FindObjectsByType<SliceEnemy>(FindObjectsSortMode.None);
    }

    void OnGUI()
    {
        if (!player) return;
        big ??= new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        var c = player.Combat;
        Bar(new Rect(20, 20, 300, 18), c.Health / c.maxHealth, new Color(0.7f, 0.1f, 0.1f), c.Rally / c.maxHealth, new Color(1f, 0.8f, 0.2f));
        GUI.color = new Color(0, 0, 0, 0.7f); GUI.DrawTexture(new Rect(20, 42, 240, 10), Texture2D.whiteTexture);
        GUI.color = new Color(0.3f, 0.8f, 0.3f); GUI.DrawTexture(new Rect(20, 42, 240 * player.Stamina / player.maxStamina, 10), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(20, 56, 300, 22), $"Blood vials: {player.vials}  [F]");
        foreach (var e in enemies)
            if (e && e.Alive) { Bar(new Rect(Screen.width / 2 - 200, Screen.height - 40, 400, 14), e.Health / e.Max, new Color(0.6f, 0.15f, 0.15f), 0, Color.clear); break; }
        GUI.Label(new Rect(20, 76, 600, 22), "WASD move | LMB attack | RMB pistol | Space quickstep | F vial | Q lock-on | R restart");
        var cr = new Rect(0, 0, Screen.width, Screen.height);
        if (c.Health <= 0f) { big.normal.textColor = new Color(0.8f, 0.1f, 0.1f); GUI.Label(cr, "YOU DIED\nR to restart", big); }
        else if (enemies.Length == 0) { big.normal.textColor = new Color(0.9f, 0.8f, 0.3f); GUI.Label(cr, "PREY SLAUGHTERED", big); }
    }

    // Health fills from the left; the rally pool is drawn right after it.
    static void Bar(Rect r, float hp, Color hpCol, float rally, Color rallyCol)
    {
        GUI.color = new Color(0, 0, 0, 0.7f); GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = rallyCol; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(hp + rally), r.height), Texture2D.whiteTexture);
        GUI.color = hpCol; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(hp), r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
