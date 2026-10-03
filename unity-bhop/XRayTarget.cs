using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Marks an object (and its child meshes) as visible through walls while XRayVision is active.
/// Ghost copies of each mesh are created on first use and drawn with the X-ray shader.
/// </summary>
public class XRayTarget : MonoBehaviour
{
    public static readonly List<XRayTarget> All = new List<XRayTarget>();
    static readonly int ColorId = Shader.PropertyToID("_Color");

    public Color color = new Color(1f, 0.3f, 0.2f, 0.8f);

    readonly List<Renderer> sources = new List<Renderer>();
    readonly List<Renderer> ghosts = new List<Renderer>();
    MaterialPropertyBlock block;
    bool built, visible, shown;

    void OnEnable() => All.Add(this);

    void OnDisable()
    {
        All.Remove(this);
        visible = false;
        Sync();
    }

    public void SetVisible(bool on, Material material)
    {
        if (on && !built) Build(material);
        if (on && !visible) ApplyColor();
        visible = on;
    }

    void LateUpdate()
    {
        if (visible || shown) Sync();
    }

    // Ghosts follow their source renderer's enabled state (e.g. hidden on death).
    void Sync()
    {
        for (int i = 0; i < ghosts.Count; i++)
            if (ghosts[i] != null) ghosts[i].enabled = visible && sources[i] != null && sources[i].enabled;
        shown = visible;
    }

    void ApplyColor()
    {
        if (block == null) block = new MaterialPropertyBlock();
        block.SetColor(ColorId, color);
        foreach (var g in ghosts)
            if (g != null) g.SetPropertyBlock(block);
    }

    void Build(Material material)
    {
        built = true;

        // Only ghost LOD0 so lower LODs don't stack on top of it.
        var skip = new HashSet<Renderer>();
        foreach (var group in GetComponentsInChildren<LODGroup>())
        {
            var lods = group.GetLODs();
            for (int i = 1; i < lods.Length; i++)
                foreach (var r in lods[i].renderers)
                    if (r != null) skip.Add(r);
        }

        foreach (var src in GetComponentsInChildren<Renderer>())
        {
            if (skip.Contains(src)) continue;
            var skin = src as SkinnedMeshRenderer;
            Mesh mesh = null;
            if (skin != null) mesh = skin.sharedMesh;
            else if (src is MeshRenderer && src.TryGetComponent(out MeshFilter filter)) mesh = filter.sharedMesh;
            if (mesh == null) continue;

            var go = new GameObject(src.name + " (X-Ray)");
            go.layer = src.gameObject.layer;
            go.transform.SetParent(src.transform, false);

            Renderer ghost;
            if (skin != null)
            {
                var g = go.AddComponent<SkinnedMeshRenderer>();
                g.sharedMesh = mesh;
                g.bones = skin.bones;
                g.rootBone = skin.rootBone;
                g.localBounds = skin.localBounds;
                g.updateWhenOffscreen = skin.updateWhenOffscreen;
                ghost = g;
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                ghost = go.AddComponent<MeshRenderer>();
            }

            var mats = new Material[mesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            ghost.sharedMaterials = mats;
            ghost.shadowCastingMode = ShadowCastingMode.Off;
            ghost.receiveShadows = false;
            ghost.enabled = false;

            sources.Add(src);
            ghosts.Add(ghost);
        }
    }
}
