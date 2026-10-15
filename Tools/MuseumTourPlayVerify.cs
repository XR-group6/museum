using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Valve.VR;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Copy into the disposable verification project only; hardware stays disabled there.
public class MuseumTourPlayVerify : MonoBehaviour
{
    readonly List<string> report = new List<string>();
    void Awake() { DontDestroyOnLoad(gameObject); SteamVR.enabled = false; SteamVR_Actions.PreInitialize(); }
    VRSubtitle Hint() => FindObjectsOfType<VRSubtitle>().FirstOrDefault(s => s.name == "Exploration VR Hint");
    bool Check(bool value, string message) { if (!value) Fail(message); return value; }

    IEnumerator Start()
    {
        yield return SceneManager.LoadSceneAsync("scene_04");
        yield return null;
        // Scene-three audio/subtitles are unchanged; check its serialized transition binding separately.
        for (int number = 4; number <= 6; number++)
        {
            string name = "scene_" + number.ToString("00");
            if (!Check(SceneManager.GetActiveScene().name == name, "Unexpected view scene")) yield break;
            var flow = FindObjectOfType<MuseumSceneTour>();
            if (!Check(flow && !flow.movement && !flow.turning && !Hint(), "Exploration leaked into " + name)) yield break;
            if (!Check(FindObjectsOfType<MuseumLeftStickMove>(true).Length == 0 && FindObjectsOfType<MuseumRightStickTurn>(true).Length == 0,
                "Locomotion component outside scene seven")) yield break;
            double entered = Time.realtimeSinceStartupAsDouble;
            while (SceneManager.GetActiveScene().name == name && Time.realtimeSinceStartupAsDouble - entered < 10)
            {
                if (!Check(!Hint(), "Early hint in " + name)) yield break;
                yield return null;
            }
            double elapsed = Time.realtimeSinceStartupAsDouble - entered;
            if (!Check(SceneManager.GetActiveScene().name == "scene_" + (number + 1).ToString("00") && elapsed > 2.9 && elapsed < 8, "View duration/order " + name + ": " + elapsed)) yield break;
            report.Add(name + " -> next scene: " + elapsed.ToString("F3") + "s including loading; no controls/hint");
            yield return null;
        }
        yield return VerifySceneSeven();
        // Direct entry must not bypass the waiting period either.
        yield return SceneManager.LoadSceneAsync("scene_07");
        yield return null;
        yield return VerifySceneSeven();
        File.WriteAllLines("MuseumTourPlayValidation.txt", report);
        Debug.Log("MUSEUM_TOUR_PLAY_VALIDATION_PASS\n" + string.Join("\n", report));
#if UNITY_EDITOR
        EditorApplication.Exit(0);
#endif
    }

    IEnumerator VerifySceneSeven()
    {
        var flow = FindObjectOfType<MuseumSceneTour>();
        if (!Check(flow && flow.sceneNumber == 7 && flow.movement && flow.turning && flow.holdSeconds == 3, "Missing scene-seven setup")) yield break;
        if (!Check(flow.movement.leftHand.inputSource == SteamVR_Input_Sources.LeftHand && flow.turning.rightHand.inputSource == SteamVR_Input_Sources.RightHand,
            "Hand references mixed")) yield break;
        double entered = Time.realtimeSinceStartupAsDouble;
        while (!flow.ExplorationStarted && Time.realtimeSinceStartupAsDouble - entered < 6)
        {
            if (!Check(!flow.movement.enabled && !flow.turning.enabled && !Hint(), "Controls/hint enabled during scene-seven view")) yield break;
            yield return null;
        }
        double delay = Time.realtimeSinceStartupAsDouble - entered;
        if (!Check(delay > 2.9 && delay < 3.3 && flow.movement.enabled && flow.turning.enabled && Hint(), "Exploration start/delay/hint: " + delay)) yield break;
        var hint = Hint();
        var text = hint.GetComponentInChildren<Text>();
        if (!Check(text && text.text == SubtitleLanguage.Translate(flow.explorationHintText) && hint.followCamera, "Translated exploration hint content")) yield break;
        // The existing language switch must also update the moved hint.
        SubtitleLanguage.Select(true);
        if (!Check(text.text == SubtitleLanguage.Translate(flow.explorationHintText), "English exploration hint")) yield break;
        SubtitleLanguage.Select(false);
        report.Add("scene_07 controls and Chinese/English hint enabled after " + delay.ToString("F3") + "s; hand references PASS");
        yield return new WaitForSecondsRealtime(flow.explorationHintSeconds + .1f);
        if (!Check(SceneManager.GetActiveScene().name == "scene_07" && !Hint() && flow.movement.enabled && flow.turning.enabled && flow.movement.GetComponent<CharacterController>().enabled,
            "Hint expiry / scene-seven persistence / controller state")) yield break;
        report.Add("scene_07 stays for exploration; hint expires after configured duration; CharacterController remains enabled PASS");
    }

    void Fail(string message)
    {
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
        if (settings) { settings.InitManagerOnStart = false; EditorUtility.SetDirty(settings); }
        if (settings && settings.Manager) { settings.Manager.automaticLoading = false; settings.Manager.automaticRunning = false; EditorUtility.SetDirty(settings.Manager); }
        SteamVR_Settings.instance.autoEnableVR = false; EditorUtility.SetDirty(SteamVR_Settings.instance);
        EditorSceneManager.OpenScene("Assets/Project3D/Scenes/scene_03.unity");
        var thirdFlow = UnityEngine.Object.FindObjectOfType<MuseumSceneTour>();
        if (!thirdFlow || thirdFlow.sceneNumber != 3 || !thirdFlow.story || !thirdFlow.narration || MuseumSceneTour.NextScene(3) != 4)
            throw new Exception("Scene-three story/audio transition binding missing");
        for (int i = 4; i <= 7; i++)
        {
            var target = EditorSceneManager.OpenScene("Assets/Project3D/Scenes/scene_" + i.ToString("00") + ".unity");
            foreach (var pose in UnityEngine.Object.FindObjectsOfType<SteamVR_Behaviour_Pose>(true)) pose.enabled = false;
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<SteamVR_Camera>(true)) camera.enabled = false;
            EditorSceneManager.MarkSceneDirty(target); EditorSceneManager.SaveScene(target);
        }
        AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Automated no-headset scene-seven test").AddComponent<MuseumTourPlayVerify>();
        EditorSceneManager.SaveScene(scene, "Assets/TourVerificationBoot.unity");
        EditorApplication.EnterPlaymode();
    }
#endif
}
