using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Applies the imported animated water shader to the current ocean meshes.
// The Project3D meshes and scene placements remain the source of geometry.
public sealed class SceneWaterVisualEffect : MonoBehaviour
{
    static readonly HashSet<string> WaterNames = new HashSet<string>
    {
        "strait_water", "bay_water", "harbour_water"
    };

    readonly List<Material> instances = new List<Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Apply(scene); }

    static void Apply(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "scene_03") return;
        var template = Resources.Load<Material>("VisualEffectWater");
        if (!template || !template.shader || !template.shader.isSupported)
        {
            Debug.LogWarning("[Museum] VisualEffect water shader is unavailable.");
            return;
        }
        var owner = new GameObject("SceneWaterVisualEffect").AddComponent<SceneWaterVisualEffect>();
        SceneManager.MoveGameObjectToScene(owner.gameObject, scene);
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (!WaterNames.Contains(transform.name)) continue;
                foreach (var renderer in transform.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var source = renderer.sharedMaterials;
                    var replacement = new Material[source.Length];
                    for (int i = 0; i < source.Length; i++)
                    {
                        var material = new Material(template);
                        if (source[i] && source[i].mainTexture && material.HasProperty("_Main_Tex"))
                            material.SetTexture("_Main_Tex", source[i].mainTexture);
                        replacement[i] = material;
                        owner.instances.Add(material);
                    }
                    renderer.sharedMaterials = replacement;
                }
            }
        }
        if (owner.instances.Count == 0)
            Destroy(owner.gameObject);
    }

    void OnDestroy()
    {
        foreach (var material in instances)
            if (material) Destroy(material);
    }
}
