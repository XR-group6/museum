using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public static class CenserScenePlaybackEditor
{
    const string ScenePath = "Assets/Project3D/Scenes/scene_01.unity";

    [MenuItem("Museum/Effects/Install Scene 01 Censer Interaction")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before installing the visual replacement.");
        Directory.CreateDirectory("Library/CenserScenePlayback");
        var active = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var roots = scene.GetRootGameObjects();
        var controller = roots.SelectMany(x => x.GetComponentsInChildren<RightController>(true)).Single();
        var collider = controller.censerColliders.First(x => x);
        var target = collider.transform;
        while (target && !target.CompareTag("Censer")) target = target.parent;
        if (!target) throw new Exception("Interactive censer target was not found.");
        string before = EditorJsonUtility.ToJson(controller);
        var component = target.GetComponent<CenserScenePlayback>();
        if (!component) component = Undo.AddComponent<CenserScenePlayback>(target.gameObject);
        var original = target.GetComponent<MeshRenderer>();
        if (!original) throw new Exception("Original censer renderer is missing.");
        if (!component.visualRoot)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Effect/Resources/CenserInteractionEffect.prefab");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(visual, "Install censer visual replacement");
            visual.name = "CenserInteractionEffect";
            visual.SetActive(true);
            var proxy = visual.GetComponentInChildren<MeshRenderer>(true);
            visual.transform.localScale *= original.bounds.size.magnitude / Mathf.Max(proxy.bounds.size.magnitude, 0.0001f);
            visual.transform.position += original.bounds.center - proxy.bounds.center;
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
            component.visualRoot = visual;
            component.director = visual.GetComponentInChildren<PlayableDirector>(true);
        }
        component.originalRenderer = original;
        component.playOnStart = false;
        Undo.RecordObject(original, "Replace censer visuals");
        original.enabled = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(original);
        component.director.playOnAwake = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(component.director);
        EditorUtility.SetDirty(component);
        if (EditorJsonUtility.ToJson(controller) != before || !collider.enabled)
            throw new Exception("Interaction bindings changed unexpectedly.");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save scene_01.");
        File.WriteAllText("Library/CenserScenePlayback/result.txt",
            "PASS scene_01 saved with active CenserInteractionEffect prefab; interaction playback enabled; original censer renderer hidden; original Censer tag, Rigidbody, collider and RightController serialization preserved.\n" +
            "Visual position: " + component.visualRoot.transform.position + "; scale: " + component.visualRoot.transform.localScale);
        Debug.Log("[Museum] Scene 01 censer replacement installed: playback waits for the narration and right-hand interaction.");
        if (opened) EditorSceneManager.CloseScene(scene, true);
        if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
    }
}
