using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.Video;
using Valve.VR;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Isolated Play Mode verification; never opens or writes the user's authored scenes.
public sealed class MuseumOceanTimelineVerify : MonoBehaviour
{
    readonly List<string> report = new List<string>();
    void Awake() { DontDestroyOnLoad(gameObject); SteamVR.enabled=false; SteamVR_Actions.PreInitialize(); }
    void Check(bool ok,string message)
    {
        if(ok)return;
        File.WriteAllText("../result.txt","FAIL: "+message);Debug.LogError("OCEAN_TIMELINE_FAIL "+message);
#if UNITY_EDITOR
        EditorApplication.Exit(1);
#endif
        throw new Exception(message);
    }
    IEnumerator Start()
    {
        for(int loop=0;loop<2;loop++)
        {
            SubtitleLanguage.Select(true);
            yield return SceneManager.LoadSceneAsync("scene_06_01");yield return null;yield return null;
            var flow=FindObjectOfType<MuseumJourney>();
            Check(flow && flow.WaitingForCenser && !flow.oceanTimeline.enabled,"Must wait for physical censer input");
            var move=FindObjectOfType<MuseumLeftStickMove>();
            Check(move && move.forwardBackwardOnly && move.enabled,"Approach movement");
            var boat=GameObject.Find("Boat");var start=boat.transform.position;
            var videos=Resources.FindObjectsOfTypeAll<VideoPlayer>().Where(v=>v.gameObject.scene.name=="scene_06_01").OrderBy(v=>v.name).ToArray();
            Check(videos.Length==2 && videos.All(v=>v.clip && v.enabled && !v.gameObject.activeInHierarchy),"Both original clips must be armed but inactive");
            int[] starts=new int[2];for(int i=0;i<2;i++){int index=i;videos[i].started+=v=>starts[index]++;}
            Check(flow.TryGrab(flow.censer.position,true),"Censer should start original Timeline");
            Check(!move.enabled && flow.oceanTimeline.enabled,"Show must lock movement and enable Timeline");
            yield return new WaitForSecondsRealtime(2.2f);
            Check(Vector3.Distance(boat.transform.position,start)>1,"Boat animation must run");
            Check(!flow.Returning && SceneManager.GetActiveScene().name=="scene_06_01","Must not leave during boat entry");
            yield return new WaitForSecondsRealtime(1.7f);
            Check(videos[0].gameObject.activeInHierarchy && !videos[1].gameObject.activeInHierarchy,"First video activation timing");
            yield return new WaitForSecondsRealtime(6.3f);
            Check(videos.All(v=>v.gameObject.activeInHierarchy),"Both video activation tracks must run");
            Check(!flow.Returning,"Legacy five-second timeout must not truncate the show");
            double deadline=Time.realtimeSinceStartupAsDouble+20;
            while(!flow.Returning)
            {
                Check(Time.realtimeSinceStartupAsDouble<deadline,"Timeline failed to finish");yield return null;
            }
            Check(flow.oceanTimeline.time>=flow.oceanTimeline.duration-.001,"Whiteout must wait for Timeline end");
            var cover=GameObject.Find("Ocean light cover");Check(cover,"Whiteout cover missing");
            var image=cover.GetComponent<UnityEngine.UI.Image>();
            yield return new WaitForSecondsRealtime(.7f);
            Check(SceneManager.GetActiveScene().name=="scene_06_01" && image.color.a>0 && image.color.a<1,"No transition before full white");
            yield return new WaitForSecondsRealtime(1f);
            Check(SceneManager.GetActiveScene().name=="scene_01" && !SubtitleLanguage.HasSelected && FindObjectOfType<InitialLanguageSelector>(),"Return must restart language selection");
            report.Add("Loop "+loop+": censer gate, boat animation, both video activation windows, full 24.93s Timeline, whiteout and language reset PASS. Video start callbacks="+string.Join(",",starts));
        }
        File.WriteAllLines("../result.txt",report.Concat(new[]{"OCEAN_TIMELINE_VERIFY_PASS; null graphics device: headset image/video frame appearance not tested."}));
#if UNITY_EDITOR
        EditorApplication.Exit(0);
#endif
    }
#if UNITY_EDITOR
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').EndsWith("/Temp/OceanTimelineVerification/Project/Assets"))throw new Exception("Isolated project only");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene,"Assets/scene_01.unity");
        scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var rig=new GameObject("[CameraRig]");rig.SetActive(false);
        var head=new GameObject("Head");head.transform.SetParent(rig.transform,false);head.transform.localPosition=Vector3.up*1.2f;head.AddComponent<Camera>();head.AddComponent<AudioListener>();
        foreach(var side in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
        {var hand=new GameObject(side.ToString());hand.transform.SetParent(rig.transform,false);var pose=hand.AddComponent<SteamVR_Behaviour_Pose>();pose.inputSource=side;pose.enabled=false;}
        var censer=GameObject.CreatePrimitive(PrimitiveType.Cube);censer.name="censer";censer.transform.position=Vector3.up;censer.transform.localScale=Vector3.one*.3f;
        var flow=new GameObject("Journey").AddComponent<MuseumJourney>();flow.censer=censer.transform;flow.coverShader=Shader.Find("TeamPrototype/JourneyCover");
        flow.blackEnvironment=new GameObject("blackenvironment");flow.oceanSurfaces=new[]{new GameObject("Sea")};
        var smoke=new GameObject("Smoke_guide");var line=GameObject.CreatePrimitive(PrimitiveType.Plane);line.name="line";line.transform.SetParent(smoke.transform,false);flow.guideLine=line.transform;
        var director=new GameObject("Original ocean Timeline").AddComponent<PlayableDirector>();director.playOnAwake=false;director.timeUpdateMode=DirectorUpdateMode.UnscaledGameTime;flow.oceanTimeline=director;
        var timeline=ScriptableObject.CreateInstance<TimelineAsset>();AssetDatabase.DeleteAsset("Assets/Ocean.playable");AssetDatabase.CreateAsset(timeline,"Assets/Ocean.playable");director.playableAsset=timeline;
        var boat=new GameObject("Boat");var animator=boat.AddComponent<Animator>();var motion=timeline.CreateTrack<AnimationTrack>(null,"Boat entrance");motion.CreateInfiniteClip("Boat motion");motion.infiniteClip.SetCurve("",typeof(Transform),"localPosition.z",AnimationCurve.Linear(0,0,2,20));director.SetGenericBinding(motion,animator);
        var slide=new GameObject("slide");
        for(int i=0;i<2;i++)
        {
            var screen=new GameObject("Video "+i);screen.transform.SetParent(slide.transform,false);var video=screen.AddComponent<VideoPlayer>();video.clip=AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Video"+i+".mp4");video.renderMode=VideoRenderMode.APIOnly;video.audioOutputMode=VideoAudioOutputMode.None;video.playOnAwake=true;
            var track=timeline.CreateTrack<ActivationTrack>(null,"Video "+i);track.postPlaybackState=ActivationTrack.PostPlaybackState.Inactive;var clip=track.CreateDefaultClip();clip.start=i==0?3.566666666666667:9.883333333333333;clip.duration=24.933333333333334-clip.start;director.SetGenericBinding(track,screen);
        }
        AssetDatabase.SaveAssets();rig.SetActive(true);EditorSceneManager.SaveScene(scene,"Assets/scene_06_01.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/scene_01.unity",true),new EditorBuildSettingsScene("Assets/scene_06_01.unity",true)};
        scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Ocean timeline verify").AddComponent<MuseumOceanTimelineVerify>();EditorSceneManager.SaveScene(scene,"Assets/Verify.unity");EditorApplication.isPlaying=true;
    }
#endif
}
