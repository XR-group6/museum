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
            foreach (var name in new[] { "scene_01", "scene_02", "scene_03" })
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
                    if (name == "scene_01")
                    {
                        var narration = UnityEngine.Object.FindObjectOfType<SceneOneNarration>();
                        if (!narration || narration.nextScene != "scene_02" || !narration.introSubtitles ||
                            !narration.narrationSubtitle || !narration.interactionSubtitle)
                            throw new Exception("scene_01 narration or subtitle missing");
                        foreach (var clip in new[] { narration.museumEcho, narration.censerIntro,
                            narration.censerAfterGrab, narration.censerFinal })
                            if (!clip) throw new Exception("scene_01 narration audio clip missing");
                        var controller = UnityEngine.Object.FindObjectOfType<RightController>();
                        if (!controller || !controller.rightHand || controller.narration != narration || controller.censerColliders.Length == 0)
                            throw new Exception("scene_01 interaction references missing");
                        foreach (var collider in controller.censerColliders) if (!collider) throw new Exception("Missing censer collider");
                    }
                    else if (name == "scene_02")
                    {
                        var narration = UnityEngine.Object.FindObjectOfType<SceneTwoNarration>();
                        if (!narration || narration.nextScene != "scene_03" || !narration.storyDirector ||
                            !narration.storyDirector.playableAsset || !narration.dialogueSubtitles)
                            throw new Exception("scene_02 narration, animation or next scene missing");
                        foreach (var clip in new[] { narration.footsteps, narration.luggage, narration.harbour,
                            narration.seagulls, narration.fabric, narration.shipHorn, narration.fatherVoice,
                            narration.daughterVoice, narration.censerVoice })
                            if (!clip) throw new Exception("scene_02 narration audio clip missing");
                        var timeline = narration.storyDirector.playableAsset as TimelineAsset;
                        if (!timeline) throw new Exception("scene_02 animation Timeline missing");
                        foreach (var track in timeline.GetOutputTracks())
                            if (track is AnimationTrack && !narration.storyDirector.GetGenericBinding(track))
                                throw new Exception("Missing animation binding: " + track.name);
                        var subtitle = UnityEngine.Object.FindObjectOfType<VRSubtitle>();
                        if (!subtitle || narration.subtitles != subtitle || string.IsNullOrEmpty(narration.dialogueSubtitles.text))
                            throw new Exception("scene_02 subtitle missing");
                    }
                    else
                    {
                        var ambience = UnityEngine.Object.FindObjectsOfType<AudioSource>();
                        if (ambience.Length < 2) throw new Exception("scene_03 ocean ambience missing");
                        foreach (var audio in ambience)
                            if (!audio.clip || !audio.loop) throw new Exception("scene_03 ambience clip missing");
                    }
                }
                if (UnityEngine.Object.FindObjectsOfType<Camera>().Length == 0) throw new Exception(name + " has no active camera");
                Debug.Log("[Removal validation] " + name + " loaded; scripts and sequence references valid.");
            }
            foreach (var entry in EditorBuildSettings.scenes)
                if (!System.IO.File.Exists(entry.path)) throw new Exception("Build scene is missing: " + entry.path);
            Debug.Log("[Removal validation] PASS: three scenes load with interaction, audio, animation, subtitle bindings and cameras.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
