using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class SoftInteractionHighlight : MonoBehaviour
{
    public Color color = new Color(1f, 0.91f, 0.65f, 1f);
    [Range(0.005f, 0.1f)] public float width = 0.028f;
    [Range(1, 4)] public float nearbyWidthMultiplier = 2.6f;
    [Range(0, 1)] public float idleStrength = 0.22f;
    [Range(0, 1)] public float nearbyStrength = 0.7f;
    [Range(8, 32)] public int glowLayers = 24;
    public bool IsNearby { get; private set; }
    public bool IsHeld { get; private set; }
    private readonly List<GameObject> shells = new List<GameObject>();
    private readonly List<Material> glowMaterials = new List<Material>();
    private readonly List<Mesh> glowMeshes = new List<Mesh>();
    public void SetNearby(bool value) { IsNearby = value; Refresh(); }
    public void SetHeld(bool value) { IsHeld = value; Refresh(); }
    void OnEnable() { EnsureBuilt(); }
    public void EnsureBuilt()
    {
        if (shells.Count > 0) return;
        var shader = Shader.Find("LocalPrototype/SoftOutline");
        if (!shader) { Debug.LogError("Soft outline shader missing", this); return; }
        for (int i = 0; i < glowLayers; i++) glowMaterials.Add(new Material(shader));
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var skin = renderer as SkinnedMeshRenderer;
            if (!filter && !skin) continue;
            var mesh = filter ? filter.sharedMesh : skin.sharedMesh;
            if (!mesh) continue;
            if (mesh.isReadable) {
                var copyMesh = Instantiate(mesh);
                var positions = copyMesh.vertices; var normals = copyMesh.normals;
                var sums = new Dictionary<Vector3, Vector3>();
                for (int v = 0; v < positions.Length; v++) {
                    Vector3 sum; sums.TryGetValue(positions[v], out sum);
                    sums[positions[v]] = sum + renderer.localToWorldMatrix.inverse.transpose.MultiplyVector(normals[v]).normalized;
                }
                for (int v = 0; v < positions.Length; v++) normals[v] = renderer.localToWorldMatrix.transpose.MultiplyVector(sums[positions[v]].normalized).normalized;
                copyMesh.normals = normals; glowMeshes.Add(copyMesh); mesh = copyMesh;
            }
            var shell = new GameObject("[Highlight] " + renderer.name);
            shell.transform.SetParent(renderer.transform, false);
            shell.layer = renderer.gameObject.layer;
            Renderer copy;
            if (skin) {
                var skinned = shell.AddComponent<SkinnedMeshRenderer>();
                skinned.sharedMesh = mesh; skinned.bones = skin.bones;
                skinned.rootBone = skin.rootBone; skinned.localBounds = skin.localBounds;
                copy = skinned;
            } else {
                shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                copy = shell.AddComponent<MeshRenderer>();
            }
            // Extra materials redraw the last submesh only. Create a renderer for each
            // layer instead so every submesh participates in the complete glow.
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = glowMaterials[0];
            copy.sharedMaterials = materials;
            copy.shadowCastingMode = ShadowCastingMode.Off; copy.receiveShadows = false;
            shells.Add(shell);
            for (int layer = 1; layer < glowMaterials.Count; layer++) {
                var extra = Instantiate(shell, renderer.transform);
                extra.name = "[Highlight] Glow " + layer;
                var layerMaterials = new Material[mesh.subMeshCount];
                for (int j = 0; j < layerMaterials.Length; j++) layerMaterials[j] = glowMaterials[layer];
                extra.GetComponent<Renderer>().sharedMaterials = layerMaterials;
                shells.Add(extra);
            }
        }
        Refresh();
    }
    void LateUpdate() { Refresh(); }
    void Refresh()
    {
        if (glowMaterials.Count == 0) return;
        float strength = IsNearby ? nearbyStrength : idleStrength;
        float radius = width * (IsNearby ? nearbyWidthMultiplier : 1);
        for (int i = 0; i < glowMaterials.Count; i++) {
            float t = (i + 1f) / glowMaterials.Count;
            // Nested transparent shells accumulate near the edge and smoothly
            // diminish outward. No global Bloom or scene-material edits required.
            float opacity = strength * Mathf.Pow(1 - t, 1.4f) * 0.12f;
            glowMaterials[i].SetColor("_Color", new Color(color.r,color.g,color.b,opacity));
            glowMaterials[i].SetFloat("_Width", radius*t);
        }
        foreach (var shell in shells) if (shell) shell.SetActive(!IsHeld);
    }
    void OnDisable()
    {
        foreach (var shell in shells) if (shell) Dispose(shell);
        shells.Clear();
        foreach (var material in glowMaterials) if (material) Dispose(material);
        glowMaterials.Clear();
        foreach (var mesh in glowMeshes) if (mesh) Dispose(mesh);
        glowMeshes.Clear();
    }
    static void Dispose(Object obj) { if(Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
}
