using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class MuseumRemovalValidation
{
    public static void Run()
    {
        try
        {
            foreach (var name in new[] { "scene_01", "scene_02" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Project3D/Scenes/" + name + ".unity", OpenSceneMode.Single);
                int missing = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                        foreach (var component in transform.GetComponents<Component>())
                        {
                            if (!component) continue;
                            var serialized = new SerializedObject(component);
                            var property = serialized.GetIterator();
                            while (property.NextVisible(true))
                                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                                    !property.objectReferenceValue && property.objectReferenceInstanceIDValue != 0)
                                    throw new Exception(name + ": broken reference on " + transform.name +
                                        "." + component.GetType().Name + "." + property.propertyPath);
                        }
                    }
                if (missing != 0) throw new Exception(name + " contains missing scripts: " + missing);
                {
                    var flow = UnityEngine.Object.FindObjectOfType<SceneAudioTimeline>();
                    if (!flow || (!flow.timeline && !flow.voiceOver)) throw new Exception(name + " audio sequence references missing");
                    if (flow.nextScene != (name == "scene_01" ? "scene_02" : "")) throw new Exception(name + " has an invalid next scene");
                    if (name == "scene_01")
                    {
                        var controller = UnityEngine.Object.FindObjectOfType<RightController>();
                        if (!flow.ambience || !controller || !controller.rightHand || !controller.sequence || controller.censerColliders.Length == 0)
                            throw new Exception("scene_01 interaction references missing");
                        foreach (var collider in controller.censerColliders) if (!collider) throw new Exception("Missing censer collider");
                    }
                    else
                    {
                        if (!flow.completionDirector || !flow.completionDirector.playableAsset) throw new Exception("scene_02 animation director missing");
                        var timeline = flow.completionDirector.playableAsset as TimelineAsset;
                        if (!timeline) throw new Exception("scene_02 animation Timeline missing");
                        foreach (var track in timeline.GetOutputTracks())
                            if (track is AnimationTrack && !flow.completionDirector.GetGenericBinding(track))
                                throw new Exception("Missing animation binding: " + track.name);
                        var subtitle = UnityEngine.Object.FindObjectOfType<VRSubtitle>();
                        if (!subtitle || !subtitle.subtitleFile || string.IsNullOrEmpty(subtitle.subtitleFile.text))
                            throw new Exception("scene_02 subtitle missing");
                    }
                }
                Debug.Log("[Removal validation] " + name + " loaded; scripts and sequence references valid.");
            }
            foreach (var entry in EditorBuildSettings.scenes)
                if (!System.IO.File.Exists(entry.path)) throw new Exception("Build scene is missing: " + entry.path);
            Debug.Log("[Removal validation] PASS: both scenes load with interaction, audio, animation and subtitle bindings.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
