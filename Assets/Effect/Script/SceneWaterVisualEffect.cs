using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Preserve colliders, transforms and Timeline targets; replace sea visuals only.
public sealed class SceneWaterVisualEffect : MonoBehaviour
{
    static readonly HashSet<string> WaterNames = new HashSet<string>
    { "strait_water", "bay_water", "harbour_water" };
    readonly List<Renderer> originals = new List<Renderer>();
    readonly List<bool> visibility = new List<bool>();
    GameObject spawned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(SceneManager.GetActiveScene());
    }
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Apply(scene); }

    public static void Apply(Scene scene)
    {
        if (!scene.IsValid() || (scene.name != "scene_02" && scene.name != "scene_03")) return;
        GameObject waves = null;
        var water = new HashSet<Renderer>();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<SceneWaterVisualEffect>()) return;
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item.name == "VisualEffect_Waves") waves = item.gameObject;
                if (!WaterNames.Contains(item.name)) continue;
                foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
                    if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) water.Add(renderer);
            }
        }
        var prefab = Resources.Load<GameObject>("VisualEffectOcean");
        if (!waves && !prefab)
        {
            Debug.LogWarning("[Museum] VisualEffectOcean is missing; keeping the existing sea.");
            return;
        }
        var owner = new GameObject("SceneWaterVisualEffect").AddComponent<SceneWaterVisualEffect>();
        SceneManager.MoveGameObjectToScene(owner.gameObject, scene);
        if (!waves)
        {
            waves = Instantiate(prefab);
            waves.name = "VisualEffect_Waves";
            SceneManager.MoveGameObjectToScene(waves, scene);
            owner.spawned = waves;
            bool found = false;
            var bounds = new Bounds();
            foreach (var renderer in water)
            {
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (found)
            {
                waves.transform.position = bounds.center;
                var footprint = waves.GetComponent<VisualEffectOceanFootprint>();
                if (footprint)
                {
                    var scale = waves.transform.localScale;
                    scale.x *= bounds.size.x / Mathf.Max(footprint.width, 0.01f);
                    // The imported -90 degree X rotation maps local Y to world Z.
                    scale.y *= bounds.size.z / Mathf.Max(footprint.depth, 0.01f);
                    waves.transform.localScale = scale;
                    waves.transform.position -= Vector3.Scale(footprint.center,
                        new Vector3(scale.x, scale.z, scale.y));
                }
            }
        }
        foreach (var renderer in water)
        {
            owner.originals.Add(renderer);
            owner.visibility.Add(renderer.forceRenderingOff);
            renderer.forceRenderingOff = true;
        }
        waves.SetActive(true);
    }

    void OnDestroy()
    {
        for (int i = 0; i < originals.Count; i++)
            if (originals[i]) originals[i].forceRenderingOff = visibility[i];
        if (spawned) Destroy(spawned);
    }
}
