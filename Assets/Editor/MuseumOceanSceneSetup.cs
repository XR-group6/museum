using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valve.VR;

public static class MuseumOceanSceneSetup
{
    public static void Run()
    {
        try
        {
            const string path = "Assets/Project3D/Scenes/scene_03.unity";
            var source = EditorSceneManager.OpenScene("Assets/Project3D/Scenes/scene_02.unity", OpenSceneMode.Single);
            var narration = UnityEngine.Object.FindObjectOfType<SceneTwoNarration>();
            if (!narration) throw new Exception("scene_02 narration is missing.");
            narration.nextScene = "scene_03";
            EditorUtility.SetDirty(narration);
            EditorSceneManager.SaveScene(source);
            var sourceRig = source.GetRootGameObjects().Single(x => x.GetComponentInChildren<SteamVR_Behaviour_Pose>(true));
            var ocean = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(ocean);
            if (!ocean.GetRootGameObjects().Any(x => x.GetComponentInChildren<SteamVR_Behaviour_Pose>(true)))
            {
                var rig = UnityEngine.Object.Instantiate(sourceRig);
                rig.name = sourceRig.name;
                SceneManager.MoveGameObjectToScene(rig, ocean);
                // Manifest is Z-up; the published Unity room uses Y-up. Stand on the boat deck.
                rig.transform.SetPositionAndRotation(new Vector3(0f, 0.87f, 1.8f), Quaternion.identity);
            }
            if (!ocean.GetRootGameObjects().Any(x => x.name == "OceanAmbience"))
            {
                var audioRoot = new GameObject("OceanAmbience");
                AddLoop(audioRoot, "Harbour", narration.harbour, 0.25f);
                AddLoop(audioRoot, "Seagulls", narration.seagulls, 0.12f);
            }
            EditorSceneManager.MarkSceneDirty(ocean);
            EditorSceneManager.SaveScene(ocean);
            var builds = EditorBuildSettings.scenes.ToList();
            if (!builds.Any(x => x.path == path)) builds.Add(new EditorBuildSettingsScene(path, true));
            foreach (var build in builds) if (build.path == path) build.enabled = true;
            EditorBuildSettings.scenes = builds.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Museum ocean setup] scene_02 > scene_03 connected; VR rig and existing ocean sound clips installed.");
            EditorSceneManager.CloseScene(source, true);
            MuseumRemovalValidation.Run();
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    static void AddLoop(GameObject parent, string name, AudioClip clip, float volume)
    {
        if (!clip) throw new Exception("Missing ocean ambience clip: " + name);
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        var audio = child.AddComponent<AudioSource>();
        audio.clip = clip;
        audio.loop = true;
        audio.playOnAwake = true;
        audio.spatialBlend = 0;
        audio.volume = volume;
    }
}
