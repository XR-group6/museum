using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

// Recreates the VisualEffect scene_01 global Bloom on the current VR camera.
public static class SceneBloomVisualEffect
{
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
        if (!scene.IsValid() || scene.name != "scene_01") return;

        var profile = Resources.Load<PostProcessProfile>("VisualEffectBloom");
        if (!profile)
        {
            Debug.LogWarning("[Museum] VisualEffect Bloom profile is unavailable.");
            return;
        }

        Camera camera = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var candidate in root.GetComponentsInChildren<Camera>(true))
            {
                if (!candidate.enabled) continue;
                camera = candidate;
                break;
            }
            if (camera) break;
        }
        if (!camera)
        {
            Debug.LogWarning("[Museum] VisualEffect Bloom could not find the scene_01 camera.");
            return;
        }

        var layer = camera.GetComponent<PostProcessLayer>();
        if (!layer) layer = camera.gameObject.AddComponent<PostProcessLayer>();
        layer.volumeTrigger = camera.transform;
        layer.volumeLayer = 1 << 0;

        var volumeObject = new GameObject("VisualEffect Bloom Volume");
        SceneManager.MoveGameObjectToScene(volumeObject, scene);
        var volume = volumeObject.AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }
}
