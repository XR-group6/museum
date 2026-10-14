using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valve.VR;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class MuseumTourPlayVerify : MonoBehaviour
{
    readonly List<string> report = new List<string>();
    void Awake() { DontDestroyOnLoad(gameObject); SteamVR.enabled = false; SteamVR_Actions.PreInitialize(); }
    IEnumerator Start()
    {
        yield return SceneManager.LoadSceneAsync("scene_03");
        yield return null;
        var story = FindObjectOfType<IncenseCharacters.Scene03Story>();
        if (!story || !story.director) { Fail("Missing story"); yield break; }
        // Exercise normal autoplay from zero; never seek the director to completion.
        Debug.Log("[Tour test] natural autoplay initial time=" + story.director.time + ", clock=" + story.director.timeUpdateMode);
        int[] order = { 4, 5, 6, 7, 5 };
        double entered = Time.realtimeSinceStartupAsDouble;
        string previous = "scene_03";
        for (int i = 0; i < order.Length; i++)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + (i == 0 ? 40 : 15);
            while (SceneManager.GetActiveScene().name == previous && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            string current = SceneManager.GetActiveScene().name;
            if (current != "scene_" + order[i].ToString("00")) { Fail("Unexpected scene " + current); yield break; }
            double elapsed = Time.realtimeSinceStartupAsDouble - entered;
            if (i == 0 && (elapsed < 29.8 || elapsed > 35)) { Fail("Natural story duration " + elapsed); yield break; }
            if (i > 0 && (elapsed < 2.95 || elapsed > 8)) { Fail("Residence " + elapsed); yield break; }
            report.Add(previous + " -> " + current + " residence=" + elapsed.ToString("F3") + "s (includes scene load)");
            previous = current; entered = Time.realtimeSinceStartupAsDouble;
            yield return null;
            var flow = FindObjectOfType<MuseumSceneTour>();
            if (!flow || flow.holdSeconds != 3) { Fail("Missing 3s flow"); yield break; }
            if (order[i] == 5)
            {
                bool enabled = flow.movement && flow.turning && flow.movement.enabled && flow.turning.enabled;
                if (enabled != (i == order.Length - 1)) { Fail("Exploration gate mismatch"); yield break; }
                if (flow.movement.leftHand.inputSource != SteamVR_Input_Sources.LeftHand || flow.turning.rightHand.inputSource != SteamVR_Input_Sources.RightHand)
                { Fail("Input hand reference mismatch"); yield break; }
            }
        }
        yield return new WaitForSecondsRealtime(4);
        if (SceneManager.GetActiveScene().name != "scene_05") { Fail("Final scene looped"); yield break; }
        report.Add("PASS: scene three natural autoplay from zero (no seek), real Play Mode scene loads 3->4->5->6->7->5, 3s holds, first scene-five movement disabled, final scene-five controls enabled, final scene remains; no hardware input used.");
        File.WriteAllLines("MuseumTourPlayValidation.txt", report);
        Debug.Log("MUSEUM_TOUR_PLAY_VALIDATION_PASS\n" + string.Join("\n", report));
#if UNITY_EDITOR
        EditorApplication.Exit(0);
#endif
    }
    void Fail(string message) {
        Debug.LogError("MUSEUM_TOUR_PLAY_VALIDATION_FAIL " + message);
#if UNITY_EDITOR
        EditorApplication.Exit(1);
#endif
    }
#if UNITY_EDITOR
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Temp/ExplorationVerification/Assets"))
            throw new InvalidOperationException("Run only in the disposable Temp/ExplorationVerification project.");
        var settings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
        if (settings && settings.Manager) { settings.Manager.automaticLoading = false; settings.Manager.automaticRunning = false; }
        SteamVR_Settings.instance.autoEnableVR = false;
        // Only the disposable verification copy has its hardware behaviours disabled.
        for (int i = 3; i <= 7; i++)
        {
            var target = EditorSceneManager.OpenScene("Assets/Project3D/Scenes/scene_" + i.ToString("00") + ".unity");
            foreach (var pose in UnityEngine.Object.FindObjectsOfType<SteamVR_Behaviour_Pose>(true)) pose.enabled = false;
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<SteamVR_Camera>(true)) camera.enabled = false;
            EditorSceneManager.MarkSceneDirty(target); EditorSceneManager.SaveScene(target);
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Automated no-headset tour test").AddComponent<MuseumTourPlayVerify>();
        EditorSceneManager.SaveScene(scene, "Assets/TourVerificationBoot.unity");
        EditorApplication.EnterPlaymode();
    }
#endif
}
