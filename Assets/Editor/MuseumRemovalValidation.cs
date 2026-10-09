using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MuseumRemovalValidation
{
    public static void Run()
    {
        try
        {
            foreach (var name in new[] { "scene_01", "scene_02", "scene_03" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Project3D/Scenes/" + name + ".unity", OpenSceneMode.Single);
                int missing = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (missing != 0) throw new Exception(name + " contains missing scripts: " + missing);
                if (name != "scene_03")
                {
                    var flow = UnityEngine.Object.FindObjectOfType<SceneAudioTimeline>();
                    if (!flow || !flow.timeline || string.IsNullOrEmpty(flow.nextScene)) throw new Exception(name + " audio sequence references missing");
                    if (name == "scene_01")
                    {
                        var controller = UnityEngine.Object.FindObjectOfType<RightController>();
                        if (!flow.ambience || !controller || !controller.rightHand || !controller.sequence || controller.censerColliders.Length == 0)
                            throw new Exception("scene_01 interaction references missing");
                        foreach (var collider in controller.censerColliders) if (!collider) throw new Exception("Missing censer collider");
                    }
                }
                Debug.Log("[Removal validation] " + name + " loaded; scripts and sequence references valid.");
            }
            Debug.Log("[Removal validation] PASS: museum project loads without nested My project (2).");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
