using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>
/// One-off editor tool that replaces the hand-cut "4in1" house mesh in
/// scene_04_01 with the four pipeline era prefabs (scene_04..07), parented
/// under a single Stage, and re-binds the PlayableDirector so the existing
/// rotating-room timeline drives the new prefabs instead of the cut mesh.
///
/// Nothing under Assets/Project3D/ is modified: only the demo scene's wiring
/// changes, and every model stays the pipeline's own.
///
/// Run from the menu (Project3D > Rebuild Carousel), or headless:
///   Unity -batchmode -nographics -quit -projectPath shared \
///         -executeMethod RebuildCarousel.Rebuild
/// </summary>
public static class RebuildCarousel
{
    const string ScenePath = "Assets/Project3D/Scenes/scene_04_01.unity";

    // Oldest era to newest. One entry per hand-cut "4in1" instance, in the order
    // they are laid out around the stage (matched by object name).
    static readonly (string path, string label)[] Eras =
    {
        ("Assets/Project3D/Prefabs/scenes/scene_04.prefab", "era_1950s"),
        ("Assets/Project3D/Prefabs/scenes/scene_05.prefab", "era_1960s"),
        ("Assets/Project3D/Prefabs/scenes/scene_06.prefab", "era_1980s"),
        ("Assets/Project3D/Prefabs/scenes/scene_07.prefab", "era_1990s"),
    };

    [MenuItem("Project3D/Rebuild Carousel (4in1 -> era prefabs)")]
    public static void Run() => Rebuild();

    public static void Rebuild()
    {
        // 1. Back up the scene before touching it.
        string projRoot = Directory.GetParent(Application.dataPath).FullName;
        string abs = Path.Combine(projRoot, ScenePath);
        if (File.Exists(abs))
        {
            string bak = abs + ".bak";
            File.Copy(abs, bak, true);
            Debug.Log("[RebuildCarousel] backed up to " + bak);
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 2. Locate the hand-cut roots, in a stable name order.
        List<GameObject> fourInOne = FindNamed(scene, "4in1");
        fourInOne = fourInOne.OrderBy(go => go.name, System.StringComparer.Ordinal).ToList();

        if (fourInOne.Count == 0)
        {
            Debug.LogError("[RebuildCarousel] no '4in1' objects found -- already rebuilt?");
            return;
        }
        Debug.Log("[RebuildCarousel] found " + fourInOne.Count + " 4in1 roots: "
            + string.Join(", ", fourInOne.Select(go => go.name)));

        // 3. Record the director and which 4in1 index each track binds to (before deletion).
        PlayableDirector director = Object.FindObjectOfType<PlayableDirector>();
        TimelineAsset timeline = director != null ? director.playableAsset as TimelineAsset : null;
        Dictionary<TrackAsset, int> bindingIndex = new Dictionary<TrackAsset, int>();
        Dictionary<TrackAsset, bool> bindingIsTransform = new Dictionary<TrackAsset, bool>();

        if (timeline != null)
        {
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                Object bound = director.GetGenericBinding(track);
                GameObject boundGo = bound as GameObject ?? (bound as Component)?.gameObject;
                int idx = boundGo == null ? -1 : fourInOne.IndexOf(boundGo);
                bindingIndex[track] = idx;
                bindingIsTransform[track] = bound is Transform;
                if (idx >= 0)
                    Debug.Log("[RebuildCarousel] track '" + track.name + "' ("
                        + track.GetType().Name + ") bound to 4in1[" + idx + "]");
            }
        }
        else
        {
            Debug.LogWarning("[RebuildCarousel] no PlayableDirector/Timeline; mesh swap only.");
        }

        // 4. Build the Stage and instantiate the era prefabs at the captured transforms.
        GameObject stage = new GameObject("Stage");
        stage.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        GameObject[] newRoots = new GameObject[Eras.Length];

        for (int i = 0; i < Eras.Length && i < fourInOne.Count; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Eras[i].path);
            if (prefab == null)
            {
                Debug.LogError("[RebuildCarousel] missing prefab " + Eras[i].path);
                continue;
            }

            // The 4in1 cut mesh is a Blender/Z-up FBX: its instance rotation carries a
            // -90deg-about-X axis conversion (to stand it up) plus a per-quarter yaw. The
            // era prefabs are already Y-up, so we keep only the yaw. The cut mesh's pivot
            // is its centre (y ~ 1.56) while an era prefab's floor sits on its local y=0,
            // so we land it at y=0 rather than copying the 4in1's y.
            Transform src = fourInOne[i].transform;
            Vector3 fwd = -src.up;               // Blender "-Y" is the house's forward axis
            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(stage.transform, false);
            inst.transform.localPosition = new Vector3(src.position.x, 0f, src.position.z);
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            inst.transform.localScale = Vector3.one;
            inst.name = Eras[i].label;
            newRoots[i] = inst;

            Debug.Log("[RebuildCarousel] " + Eras[i].label + " <- " + fourInOne[i].name
                + " at (" + src.position.x + ", 0, " + src.position.z + ") yaw " + yaw + "deg");
        }

        // 5. Delete the hand-cut roots.
        foreach (GameObject go in fourInOne)
            Object.DestroyImmediate(go);

        // 6. Re-bind tracks that pointed at a 4in1 root.
        if (timeline != null && director != null)
        {
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (!bindingIndex.TryGetValue(track, out int idx)
                    || idx < 0 || idx >= newRoots.Length || newRoots[idx] == null)
                    continue;

                Object target = bindingIsTransform[track]
                    ? (Object)newRoots[idx].transform
                    : (Object)newRoots[idx];
                director.SetGenericBinding(track, target);
                Debug.Log("[RebuildCarousel] re-bound track '" + track.name + "' -> " + newRoots[idx].name);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[RebuildCarousel] done: scene saved.");
    }

    static List<GameObject> FindNamed(Scene scene, string prefix)
    {
        List<GameObject> result = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
            Collect(root, prefix, result);
        return result;
    }

    static void Collect(GameObject go, string prefix, List<GameObject> into)
    {
        if (go.name.StartsWith(prefix))
            into.Add(go);
        for (int i = 0; i < go.transform.childCount; i++)
            Collect(go.transform.GetChild(i).gameObject, prefix, into);
    }
}
