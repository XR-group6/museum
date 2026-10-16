using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Valve.VR;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Run in the isolated Temp/JourneyVerification/Project, never the user's open scenes.
public sealed class MuseumJourneyPlayVerify : MonoBehaviour
{
    readonly List<string> report = new List<string>();
    void Awake() { DontDestroyOnLoad(gameObject); SteamVR.enabled = false; SteamVR_Actions.PreInitialize(); }
    void Check(bool condition, string message)
    {
        if (condition) return;
        File.WriteAllText("../result.txt", "FAIL: " + message);
        Debug.LogError("JOURNEY_VERIFY_FAIL: " + message);
#if UNITY_EDITOR
        EditorApplication.Exit(1);
#endif
        throw new Exception(message);
    }
    IEnumerator Start()
    {
        for (int loop = 0; loop < 2; loop++)
        {
            yield return SceneManager.LoadSceneAsync("scene_04_01");
            yield return null; yield return null;
            var journey = FindObjectOfType<MuseumJourney>();
            Check(journey && journey.roomTimeline && journey.finalRegionSeconds == 10, "Authored Timeline configuration");
            var viewer = Camera.main;
            var turn = viewer.transform.parent.GetComponent<MuseumRightStickTurn>();
            Check(turn && turn.snapTurn && turn.snapDegrees == 90, "Room snap-turn mode");
            Vector3 fixedHead = viewer.transform.position;
            float initialYaw = viewer.transform.eulerAngles.y;
            turn.TurnAxis(Vector2.right, .01f);
            Check(Mathf.Abs(Mathf.DeltaAngle(initialYaw, viewer.transform.eulerAngles.y) - 90) < .1f, "Right snap must be exactly 90 degrees");
            for(int i=0;i<30;i++) turn.TurnAxis(Vector2.right,.01f);
            Check(Mathf.Abs(Mathf.DeltaAngle(initialYaw, viewer.transform.eulerAngles.y) - 90) < .1f, "Held stick repeated snap");
            turn.TurnAxis(Vector2.zero,.01f); turn.TurnAxis(Vector2.left,.01f);
            Check(Mathf.Abs(Mathf.DeltaAngle(initialYaw,viewer.transform.eulerAngles.y)) < .1f && Vector3.Distance(fixedHead,viewer.transform.position)<.001f,"Neutral rearm / left snap / head pivot");
            var panel=FindObjectsOfType<VRSubtitle>().First(x=>x.name=="Room restored subtitle");
            Check(!panel.followCamera && !panel.faceCamera && panel.fontSize==34 && panel.canvasScale==.002f,"Original subtitle orientation and clear styling");
            Check(!FindObjectsOfType<VRSubtitle>().Any(x=>x.name.Contains("overhead subtitle")), "Room must use original panels");
            SubtitleLanguage.Select(false);
            Check(panel.GetComponentInChildren<UnityEngine.UI.Text>().text=="场景一：新的生活", "Chinese-only authored panel");
            SubtitleLanguage.Select(true);
            Check(panel.GetComponentInChildren<UnityEngine.UI.Text>().text=="Scene 1: A New Life", "English-only authored panel");
            int[] seen = new int[4];
            double start = Time.realtimeSinceStartupAsDouble, finalLit = -1;
            while (SceneManager.GetActiveScene().name == "scene_04_01")
            {
                Check(Time.realtimeSinceStartupAsDouble - start < 34, "Authored Timeline sequence stalled");
                if(journey.ActiveEra>=0) seen[journey.ActiveEra]++;
                if(journey.FinalRegionLitAt>=0) finalLit=journey.FinalRegionLitAt;
                yield return null;
            }
            Check(seen.All(x=>x>0) && finalLit>=0, "All authored sectors must light");
            Check(Time.realtimeSinceStartupAsDouble-finalLit>=10 && Time.realtimeSinceStartupAsDouble-finalLit<10.6, "Must switch 10s after final sector first lights");
            Check(SceneManager.GetActiveScene().name == "scene_07", "Room must lead directly to exploration");
            report.Add("Loop "+loop+": authored light curves, final-light +10s, exact 90-degree snap with neutral rearm, fixed head and original-position CN/EN panels PASS");
            yield return null; yield return null;
            journey=FindObjectOfType<MuseumJourney>();
            Check(journey && journey.WaitingForCenser && FindObjectOfType<MuseumSceneTour>().ExplorationStarted, "Scene seven must be immediately interactive");
            var inspection=FindObjectOfType<PermitInspection>();
            Physics.SyncTransforms();
            Check(!journey.TryGrab(inspection.tableCollider.bounds.center,true), "Touching the permit must not activate censer");
            Check(!journey.TryGrab(journey.censer.position,true), "Aiming at permit while touching censer must not change scene");
            inspection.tableCollider.gameObject.SetActive(false);
            Check(!journey.TryGrab(journey.censer.position+Vector3.right*.2f,true), "Near-but-not-touching controller accepted");
            Check(!journey.TryGrab(Vector3.one * 100, true), "Distant trigger accepted");
            Check(!journey.TryGrab(journey.censer.position, false), "Held trigger accepted without a new press");
            var label = FindObjectsOfType<VRSubtitle>().First(s => s.name == "Journey bilingual subtitle");
            SubtitleLanguage.Select(false);
            Check(label.GetComponentInChildren<UnityEngine.UI.Text>().text == MuseumJourney.ExplorePrompt, "Chinese world prompt");
            SubtitleLanguage.Select(true);
            Check(label.GetComponentInChildren<UnityEngine.UI.Text>().text == SubtitleLanguage.Translate(MuseumJourney.ExplorePrompt), "English world prompt");
            Check(!label.followCamera, "Scene seven hint must stay above censer");
            Check(journey.TryGrab(journey.censer.position, true), "Near right-trigger press rejected");
            Check(!journey.TryGrab(journey.censer.position, true), "Duplicate activation");
            yield return null;
            while (SceneManager.GetActiveScene().name != "scene_06_01") yield return null;
            yield return null; yield return null;
            journey = FindObjectOfType<MuseumJourney>();
            Check(journey && journey.WaitingForCenser && !journey.OceanRevealed && !journey.Returning, "Ocean entry must reset state and wait for grab");
            Check(journey.blackEnvironment.activeSelf && journey.oceanSurfaces.All(s => !s.activeSelf), "Ocean revealed before interaction");
            Check(SubtitleLanguage.English, "Language lost during transition");
            Check(journey.guideLine && journey.guideLine.gameObject.activeInHierarchy && journey.guideLine.GetComponent<Renderer>().enabled, "Ground guide line must remain visible before grabbing");
            label = FindObjectsOfType<VRSubtitle>().First(s => s.name == "Journey bilingual subtitle");
            Check(label.followCamera && label.GetComponentInChildren<UnityEngine.UI.Text>().text == SubtitleLanguage.Translate(MuseumJourney.OceanPrompt), "English headset approach prompt");
            var move = FindObjectOfType<MuseumLeftStickMove>();
            Check(move && move.enabled && !move.forwardOnly && move.forwardBackwardOnly, "Forward/back locomotion not available");
            Vector3 initial = move.transform.position;
            move.MoveAxis(Vector2.left, .1f);
            Check(Vector2.Distance(new Vector2(initial.x,initial.z), new Vector2(move.transform.position.x,move.transform.position.z)) < .001f, "Forward-only sideways input leaked");
            move.MoveAxis(Vector2.down, .1f);
            Check(Vector3.Dot(Vector3.ProjectOnPlane(move.transform.position-initial,Vector3.up),Vector3.ProjectOnPlane(move.headset.forward,Vector3.up)) < -.05f, "Backward movement failed");
            initial=move.transform.position;
            move.MoveAxis(Vector2.up, .1f);
            Check(Vector3.ProjectOnPlane(move.transform.position-initial,Vector3.up).magnitude > .05f, "Forward movement failed");
            Check(!journey.TryGrab(initial, true), "Remote ocean activation");
            Check(journey.TryGrab(journey.censer.position, true), "Ocean grab rejected");
            Check(!journey.OceanRevealed && journey.DarknessOpacity > .95f && !move.enabled && journey.blackEnvironment.activeSelf, "Reveal must start with authored black environment");
            yield return new WaitForSecondsRealtime(1.5f);
            Check(!journey.OceanRevealed && journey.DissolveProgress>.1f && journey.DissolveProgress<.9f && journey.blackEnvironment.activeSelf, "Darkness must dissolve gradually");
            Check(Mathf.Abs(journey.blackEnvironment.GetComponent<Renderer>().sharedMaterial.GetFloat("_Dissolve_Scale")-journey.DissolveProgress)<.01f,"Authored dissolve property not driven");
            Check(!GameObject.Find("Ocean darkness reveal"),"Screen alpha fade must not replace material dissolve");
            yield return new WaitForSecondsRealtime(1.7f);
            Check(journey.OceanRevealed && journey.DarknessOpacity==0 && journey.oceanSurfaces.All(x=>x.activeSelf), "Reveal completion");
            yield return new WaitForSecondsRealtime(4.6f);
            Check(!journey.Returning && SceneManager.GetActiveScene().name == "scene_06_01", "Ocean hold shorter than 5 seconds");
            yield return new WaitForSecondsRealtime(.3f);
            Check(journey.Returning && SceneManager.GetActiveScene().name == "scene_06_01", "White cover should begin after 5 seconds");
            yield return new WaitForSecondsRealtime(1.7f);
            Check(SceneManager.GetActiveScene().name == "scene_01", "Return to museum failed");
            Check(!SubtitleLanguage.HasSelected && FindObjectOfType<InitialLanguageSelector>(),"Return must restart at the language selector");
            report.Add("Loop " + loop + ": immediate exploration, permit isolation, physical censer contact, visible ground line, 3s authored material dissolve, forward/back movement, 5s sea and language-selection restart PASS");
        }
        File.WriteAllLines("../result.txt", report.Concat(new[]{"JOURNEY_VERIFY_PASS (simulated controllers; headset rendering still requires hardware)"}));
#if UNITY_EDITOR
        EditorApplication.Exit(0);
#endif
    }
#if UNITY_EDITOR
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').EndsWith("/Temp/JourneyVerification/Project/Assets")) throw new Exception("Isolated project only");
        var builds = new List<EditorBuildSettingsScene>();
        foreach (var name in new[]{"scene_04_01","scene_07","scene_06_01","scene_01"})
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); scene.name=name;
            if (name != "scene_01")
            {
                var rig = new GameObject("[CameraRig]"); rig.SetActive(false);
                var head = new GameObject("Camera"); head.transform.SetParent(rig.transform,false);head.transform.localPosition=new Vector3(.12f,1.2f,.08f);head.tag="MainCamera";head.AddComponent<Camera>();head.AddComponent<AudioListener>();
                SteamVR_Behaviour_Pose left=null,right=null;
                foreach (var side in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
                {
                    var child=new GameObject(side.ToString());child.transform.SetParent(rig.transform,false);
                    var pose=child.AddComponent<SteamVR_Behaviour_Pose>();pose.inputSource=side;pose.enabled=false;
                    if(side==SteamVR_Input_Sources.LeftHand)left=pose;else right=pose;
                }
                var censer=GameObject.CreatePrimitive(PrimitiveType.Cube);censer.name="censer";censer.transform.position=Vector3.up;censer.transform.localScale=Vector3.one*.3f;
                var go=new GameObject("Museum journey");var journey=go.AddComponent<MuseumJourney>();journey.censer=censer.transform;
                journey.coverShader=Shader.Find("TeamPrototype/JourneyCover");
                if(name=="scene_04_01")
                {
                    journey.eraLights=new Light[4];
                    var pos=new[]{new Vector3(.085f,4.349f,.611f),new Vector3(-2.35f,4.349f,2.74f),new Vector3(.166f,4.349f,5.731f),new Vector3(2.62f,4.349f,3.204f)};
                    var timeline=ScriptableObject.CreateInstance<TimelineAsset>();
                    string timelinePath="Assets/TestRoomTimeline.asset";
                    AssetDatabase.DeleteAsset(timelinePath);AssetDatabase.CreateAsset(timeline,timelinePath);
                    var director=new GameObject("Authored Timeline").AddComponent<PlayableDirector>();director.playOnAwake=false;director.timeUpdateMode=DirectorUpdateMode.UnscaledGameTime;director.playableAsset=timeline;
                    journey.roomTimeline=director;
                    float[][] times={new[]{0f,1f,4.983333f,5.983333f},new[]{6f,7.016667f,11.5f,12.5f},new[]{12.483334f,13.45f,18f,18.983334f},new[]{18.983334f,19.95f,25f,26.016666f}};
                    for(int i=0;i<4;i++)
                    {
                        var light=new GameObject("Region "+i).AddComponent<Light>();light.transform.position=pos[i];journey.eraLights[i]=light;
                        var animator=light.gameObject.AddComponent<Animator>();
                        var track=timeline.CreateTrack<AnimationTrack>(null,"Era "+i);track.CreateInfiniteClip("Era light "+i);
                        var t=times[i];track.infiniteClip.SetCurve("",typeof(Light),"m_Intensity",new AnimationCurve(new Keyframe(t[0],0),new Keyframe(t[1],1),new Keyframe(t[2],1),new Keyframe(t[3],0)));
                        director.SetGenericBinding(track,animator);
                    }
                    AssetDatabase.SaveAssets();
                    var panel=new GameObject("Authored bilingual panel",typeof(RectTransform),typeof(UnityEngine.UI.Text));panel.GetComponentInChildren<UnityEngine.UI.Text>().text="Scene 1: A New Life\n\n场景一：新的生活";
                }
                if(name=="scene_07")
                {
                    var move=rig.AddComponent<MuseumLeftStickMove>();move.enabled=false;move.headset=head.transform;move.leftHand=left;
                    var turn=rig.AddComponent<MuseumRightStickTurn>();turn.enabled=false;turn.headset=head.transform;turn.rightHand=right;
                    var flow=new GameObject("Tour").AddComponent<MuseumSceneTour>();flow.sceneNumber=7;flow.movement=move;flow.turning=turn;flow.explorationSpawnForward=0;flow.holdSeconds=0;
                    var permit=GameObject.CreatePrimitive(PrimitiveType.Cube);permit.name="Permit";permit.transform.position=new Vector3(0,1,1);permit.transform.localScale=Vector3.one*.1f;
                    var inspection=new GameObject("Permit inspection").AddComponent<PermitInspection>();inspection.tour=flow;inspection.tableCollider=permit.GetComponent<Collider>();inspection.enabled=false;
                    right.transform.rotation=Quaternion.identity;
                }
                if(name=="scene_06_01")
                {
                    journey.blackEnvironment=GameObject.CreatePrimitive(PrimitiveType.Sphere);journey.blackEnvironment.name="blackenvironment";journey.blackEnvironment.GetComponent<Renderer>().sharedMaterial=new Material(Shader.Find("Verification/AuthoredDissolve"));journey.oceanSurfaces=new[]{new GameObject("Sea")};
                    var smoke=new GameObject("Smoke_guide");var line=GameObject.CreatePrimitive(PrimitiveType.Plane);line.name="line";line.transform.SetParent(smoke.transform);journey.guideLine=line.transform;
                }
                rig.SetActive(true);
            }
            string path="Assets/"+name+".unity";EditorSceneManager.SaveScene(scene,path);builds.Add(new EditorBuildSettingsScene(path,true));
        }
        EditorBuildSettings.scenes=builds.ToArray();
        var initial=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Journey test runner").AddComponent<MuseumJourneyPlayVerify>();
        EditorSceneManager.SaveScene(initial,"Assets/Verification.unity");
        EditorApplication.isPlaying=true;
    }
#endif
}
