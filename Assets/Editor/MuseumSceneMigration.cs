using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps museum-specific bindings outside the pipeline-owned migration tool.
public static class MuseumSceneMigration
{
    public static void Run()
    {
        try
        {
            var migrate = typeof(Project3D.MigrateScene).GetMethod("MigrateOne", BindingFlags.NonPublic | BindingFlags.Static);
            if (migrate == null) throw new Exception("Project3D migration entry is unavailable.");
            foreach (var name in new[] { "scene_01", "scene_02" })
            {
                var path = "Assets/Project3D/Scenes/" + name + ".unity";
                var before = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var ownedRoots = before.GetRootGameObjects().Where(IsConsumerRoot).ToArray();
                var rootNames = ownedRoots.Select(x => x.name).ToArray();
                var objectCount = ownedRoots.Sum(x => x.GetComponentsInChildren<Transform>(true).Length);
                EditorSceneManager.CloseScene(before, true);

                var args = new object[] { name, false, null, false };
                if (!(bool)migrate.Invoke(null, args)) throw new Exception(name + ": " + args[2]);
                var scene = SceneManager.GetActiveScene();
                var room = scene.GetRootGameObjects().Single(x =>
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(x) ==
                    "Assets/Project3D/Prefabs/scenes/" + name + ".prefab");
                var preserved = scene.GetRootGameObjects().Where(x => rootNames.Contains(x.name)).ToArray();
                if (preserved.Length != rootNames.Length ||
                    preserved.Sum(x => x.GetComponentsInChildren<Transform>(true).Length) != objectCount)
                    throw new Exception(name + ": a consumer object was lost during migration.");

                var flow = preserved.SelectMany(x => x.GetComponentsInChildren<SceneAudioTimeline>(true)).FirstOrDefault();
                var firstNarration = preserved.SelectMany(x => x.GetComponentsInChildren<SceneOneNarration>(true)).FirstOrDefault();
                var secondNarration = preserved.SelectMany(x => x.GetComponentsInChildren<SceneTwoNarration>(true)).FirstOrDefault();
                if (flow) { flow.nextScene = name == "scene_01" ? "scene_02" : "scene_03"; EditorUtility.SetDirty(flow); }
                if (firstNarration) { firstNarration.nextScene = "scene_02"; EditorUtility.SetDirty(firstNarration); }
                if (secondNarration) { secondNarration.nextScene = "scene_03"; EditorUtility.SetDirty(secondNarration); }
                if (!flow && !firstNarration && !secondNarration) throw new Exception(name + ": no sequence component survived.");
                if (name == "scene_01")
                {
                    var censer = room.GetComponentsInChildren<Transform>(true).Single(x => x.name == "censer");
                    censer.gameObject.tag = "Censer";
                    PrefabUtility.RecordPrefabInstancePropertyModifications(censer.gameObject);
                    var colliders = censer.GetComponentsInChildren<Collider>(true).Where(x => x.enabled).ToArray();
                    if (colliders.Length == 0) throw new Exception("New censer has no interaction collider.");
                    var controller = preserved.SelectMany(x => x.GetComponentsInChildren<RightController>(true)).Single();
                    controller.censerColliders = colliders;
                    if (firstNarration) { controller.narration = firstNarration; controller.sequence = null; }
                    else controller.sequence = flow;
                    EditorUtility.SetDirty(controller);
                    Debug.Log("[Museum migration] censer=" + censer.position + "; controller colliders=" + colliders.Length);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[Museum migration] " + name + ": preserved " + objectCount +
                    " consumer objects under " + rootNames.Length + " roots; " + args[2]);
            }
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(x => System.IO.File.Exists(x.path)).ToArray();
            AssetDatabase.SaveAssets();
            MuseumRemovalValidation.Run();
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    static bool IsConsumerRoot(GameObject root)
    {
        var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
        return !path.StartsWith("Assets/Project3D/Prefabs/", StringComparison.Ordinal) &&
            root.name != "Project3D Lighting" && root.name != "Project3D Probes" && root.name != "Project3D Post";
    }
}
