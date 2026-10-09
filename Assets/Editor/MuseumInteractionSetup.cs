using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Valve.VR;

[InitializeOnLoad]
public static class MuseumInteractionSetup
{
    const string Marker = "Assets/Scripts/MuseumInteractionSetup.complete";
    static MuseumInteractionSetup() { EditorApplication.delayCall += AutoSetup; }
    static void AutoSetup()
    {
        if (File.Exists(Marker) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists("Assets/SteamVR_Input/SteamVR_Input_Initialization.cs"))
        {
            var actionPath = SteamVR_Input.GetActionsFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(actionPath));
            if (!File.Exists(actionPath))
            {
                foreach (var file in Directory.GetFiles("Assets/SteamVR/Input/ExampleJSON", "*.json"))
                    File.Copy(file, Path.Combine(Path.GetDirectoryName(actionPath) ?? "", Path.GetFileName(file)), false);
            }
            SteamVR_Input.InitializeFile(true);
            SteamVR_Input_Generator.BeginGeneration();
            return;
        }
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
        { Debug.LogWarning("[Museum] Save open scene changes; setup will retry automatically."); EditorApplication.delayCall += AutoSetup; return; }
        try { Install(); } catch (Exception e) { Debug.LogException(e); }
    }

    [MenuItem("Museum/Install interaction sequence")]
    public static void Install()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var paths = new[] { "Assets/Project3D/Scenes/scene_01.unity", "Assets/Project3D/Scenes/scene_02.unity", "Assets/Project3D/Scenes/scene_03.unity" };
        var ambience = RequiredAudio("博物馆氛围音");
        var cicadas = RequiredAudio("蝉鸣");
        var voices = RequiredAudio("博物馆回音+人声");
        if (cicadas.length < 15 || voices.length < 15) throw new Exception("[Museum] Sequence audio must be at least 15 seconds.");
        Directory.CreateDirectory("Assets/Timelines"); AssetDatabase.Refresh();
        var first = MakeTimeline("CicadasTimeline", cicadas);
        var second = MakeTimeline("MuseumVoicesTimeline", voices);
        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                var scene = EditorSceneManager.OpenScene(paths[i], OpenSceneMode.Single);
                var poses = UnityEngine.Object.FindObjectsOfType<SteamVR_Behaviour_Pose>(true);
                var right = poses.FirstOrDefault(p => p.inputSource == SteamVR_Input_Sources.RightHand);
                if (i == 0 && !right) throw new Exception("[Museum] No existing SteamVR right-hand tracking object in scene_01.");
                var listeners = UnityEngine.Object.FindObjectsOfType<AudioListener>().Where(x => x.enabled).ToArray();
                var keep = listeners.FirstOrDefault(x => x.GetComponentInParent<SteamVR_Camera>()) ?? listeners.FirstOrDefault();
                if (!keep)
                {
                    var camera = Camera.main ?? UnityEngine.Object.FindObjectsOfType<Camera>().FirstOrDefault();
                    if (!camera) throw new Exception("[Museum] Missing active camera for AudioListener.");
                    keep = camera.gameObject.AddComponent<AudioListener>();
                }
                foreach (var listener in listeners) if (listener != keep) listener.enabled = false;
                if (i < 2)
                {
                    var obj = GameObject.Find("SceneAudioTimeline") ?? new GameObject("SceneAudioTimeline");
                    var flow = obj.GetComponent<SceneAudioTimeline>() ?? obj.AddComponent<SceneAudioTimeline>();
                    flow.timeline = i == 0 ? first : second; flow.ambience = i == 0 ? ambience : null;
                    flow.playOnStart = i == 1; flow.nextScene = i == 0 ? "scene_02" : "scene_03";
                    var director = obj.GetComponent<PlayableDirector>();
                    director.playableAsset = flow.timeline; director.playOnAwake = false;
                    director.timeUpdateMode = DirectorUpdateMode.DSPClock; director.extrapolationMode = DirectorWrapMode.Hold;
                    if (i == 0)
                    {
                        var censer = UnityEngine.Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == "censer");
                        if (!censer) throw new Exception("[Museum] Missing censer model.");
                        censer.gameObject.tag = "Censer";
                        var colliders = censer.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToArray();
                        if (colliders.Length == 0)
                        {
                            foreach (var filter in censer.GetComponentsInChildren<MeshFilter>())
                                if (filter.sharedMesh) { var collider = filter.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = filter.sharedMesh; }
                            colliders = censer.GetComponentsInChildren<Collider>();
                        }
                        if (colliders.Length == 0) throw new Exception("[Museum] Censer has no usable mesh or collider.");
                        var dummy = GameObject.Find("RightControllerDummy") ?? new GameObject("RightControllerDummy");
                        var controller = dummy.GetComponent<RightController>() ?? dummy.AddComponent<RightController>();
                        controller.rightHand = right; controller.sequence = flow; controller.censerColliders = colliders;
                    }
                }
                EditorSceneManager.SaveScene(scene);
            }
            var builds = EditorBuildSettings.scenes.ToList();
            foreach (var path in paths)
            {
                var existing = builds.FirstOrDefault(s => s.path == path);
                if (existing != null) existing.enabled = true; else builds.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = builds.ToArray(); AssetDatabase.SaveAssets();
            File.WriteAllText(Marker, "scene_01 ambience > censer right trigger > cicadas 15s > scene_02 voices 15s > scene_03\nUnity editor scripts compiled and scene setup completed.\nAudio lengths: " + ambience.length + ", " + cicadas.length + ", " + voices.length + "\nActual VR controller and headset output require hardware verification.");
            AssetDatabase.Refresh(); Debug.Log("[Museum] Interaction scenes and timelines saved successfully.");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    static AudioClip RequiredAudio(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MP3/" + name + ".mp3");
        if (!clip) throw new Exception("[Museum] Missing audio: " + name); return clip;
    }
    static TimelineAsset MakeTimeline(string name, AudioClip audio)
    {
        string path = "Assets/Timelines/" + name + ".playable";
        var existing = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
        if (existing) return existing;
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, path);
        var track = timeline.CreateTrack<AudioTrack>(null, name);
        var clip = track.CreateClip<AudioPlayableAsset>();
        ((AudioPlayableAsset)clip.asset).clip = audio; ((AudioPlayableAsset)clip.asset).loop = false;
        clip.start = 0; clip.duration = 15; clip.clipIn = 0; clip.timeScale = 1;
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = 15;
        EditorUtility.SetDirty(timeline); return timeline;
    }
}
