using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class Scene01Setup
{
    const string Dir = "Assets/Scene01Animation";
    static Scene01Setup() { EditorApplication.delayCall += Auto; }
    static void Auto()
    {
        if (File.Exists("Temp/Scene01.build.done")) return;
        if (SceneManager.GetActiveScene().name != "scene_04_01" || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { Build(); File.WriteAllText("Temp/Scene01.build.done", "OK"); }
        catch(Exception ex) { File.WriteAllText("Temp/Scene01.build.error", ex.ToString()); Debug.LogException(ex); }
    }
    static Transform Find(string name) { return SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).First(t=>t.name==name); }
    static Transform Bone(Transform root,string name) { return root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name); }
    static float S(float a,float b,float t) { return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t)); }
    static void Pose(Transform actor,string name,float x,float y=0,float z=0) { Bone(actor,name).localRotation *= Quaternion.Euler(x,y,z); }
    static GameObject Prop(string path,Transform parent,Vector3 pos)
    {
        var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        obj.transform.SetParent(parent,true); obj.transform.position=pos; return obj;
    }
    [MenuItem("Tools/Incense Story/Build Scene01 Morning")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir);
        var scene=SceneManager.GetActiveScene();
        if(scene.name!="scene_04_01") throw new Exception("Open scene_04_01 first");
        if (GameObject.Find("Scene01_Morning")) throw new Exception("Scene01 already exists; edit its generated clips in Timeline.");
        Directory.CreateDirectory("Logs");
        // Preserve all currently loaded character placements, including unsaved imports.
        EditorSceneManager.SaveScene(scene,"Logs/scene_04_01_before_scene01.unity",true);
        Transform house=Find("scene_09_models"), father=Find("Father_Young"), daughter=Find("Daughter_Child_Schoolbag"), mother=Find("Mother_Young");
        var director=Find("TimeLine").GetComponent<PlayableDirector>();
        string original=AssetDatabase.GetAssetPath(director.playableAsset);
        string timelinePath=Dir+"/Scene04_WithScene01.playable";
        if (!AssetDatabase.CopyAsset(original,timelinePath)) throw new Exception("Cannot copy timeline");
        var timeline=AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
        var originalTimeline=director.playableAsset as TimelineAsset;
        var oldTracks=originalTimeline.GetOutputTracks().ToArray();
        var newTracks=timeline.GetOutputTracks().ToArray();
        for(int i=0;i<oldTracks.Length;i++) director.SetGenericBinding(newTracks[i],director.GetGenericBinding(oldTracks[i]));
        director.playableAsset=timeline;
        // Give the morning scene its own 26 seconds before the existing carousel rotation.
        foreach(var track in timeline.GetOutputTracks())
        {
            foreach(var c in track.GetClips()) c.start+=26;
            var anim=track as AnimationTrack;
            if(anim && anim.infiniteClip)
            {
                var copy=anim.infiniteClip; copy.name=anim.infiniteClip.name+" delayed";
                foreach(var binding in AnimationUtility.GetCurveBindings(copy))
                {
                    var curve=AnimationUtility.GetEditorCurve(copy,binding);
                    var keys=curve.keys; for(int k=0;k<keys.Length;k++) keys[k].time+=26;
                    curve.keys=keys; AnimationUtility.SetEditorCurve(copy,binding,curve);
                }
                EditorUtility.SetDirty(copy);
            }
        }
        var group=new GameObject("Scene01_Morning"); group.transform.SetParent(house,false);
        // World-authored positions become children of the rotating room, so all props follow it.
        father.SetParent(group.transform,true); daughter.SetParent(group.transform,true); mother.SetParent(group.transform,true);
        father.position=new Vector3(.52f,.05f,-3.16f); father.rotation=Quaternion.Euler(0,270,0);
        daughter.position=new Vector3(1.15f,.05f,-4.20f); daughter.rotation=Quaternion.Euler(0,320,0);
        mother.position=new Vector3(-1.94f,.05f,-.67f); mother.rotation=Quaternion.Euler(0,90,0);
        foreach(var actor in new[]{father,daughter,mother}) foreach(var animator in actor.GetComponentsInChildren<Animator>()) animator.enabled=false;
        var box=Prop("Assets/Project3D/Prefabs/prop/suitcase_open.prefab",house,new Vector3(.69f,.05f,-2.35f)); box.name="Scene01_UnpackedCase";
        var burner=Prop("Assets/Project3D/Prefabs/prop/incense_burner_tripod.prefab",house,new Vector3(.20f,1.04f,-3.16f)); burner.name="Scene01_Burner";
        // A small folded garment gives the mother's hand movement a visible target.
        var cloth=GameObject.CreatePrimitive(PrimitiveType.Cube); cloth.name="Scene01_FoldedClothes"; cloth.transform.SetParent(group.transform,true);
        cloth.transform.position=new Vector3(-1.56f,.65f,-.67f); cloth.transform.localScale=new Vector3(.32f,.045f,.25f);
        UnityEngine.Object.DestroyImmediate(cloth.GetComponent<Collider>());
        var mat=new Material(Shader.Find("Standard")); mat.color=new Color(.40f,.47f,.49f); AssetDatabase.CreateAsset(mat,Dir+"/Clothes.mat"); cloth.GetComponent<Renderer>().sharedMaterial=mat;
        var actors=group.GetComponentsInChildren<Transform>().Where(t=>t!=group.transform).ToArray();
        var rest=actors.ToDictionary(t=>t,t=>t.localRotation);
        var restPos=actors.ToDictionary(t=>t,t=>t.localPosition);
        var baseF=father.localPosition; var baseD=daughter.localPosition;
        Vector3 destination=house.InverseTransformPoint(new Vector3(1.25f,.05f,-5.70f));
        var curves=new Dictionary<Transform,AnimationCurve[]>();
        foreach(var tr in actors) curves[tr]=Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray();
        var burnerCurves=Enumerable.Range(0,3).Select(_=>new AnimationCurve()).ToArray();
        Vector3 put=house.InverseTransformPoint(new Vector3(-.30f,.84f,-3.20f));
        Vector3 carry=house.InverseTransformPoint(new Vector3(.13f,1.04f,-3.16f));
        Vector3 inBox=house.InverseTransformPoint(new Vector3(.69f,.34f,-2.35f));
        var chime=MakeChime();
        for(int frame=0;frame<=780;frame++)
        {
            float t=frame/30f;
            foreach(var tr in actors) { tr.localRotation=rest[tr]; tr.localPosition=restPos[tr]; }
            float take=1-S(0,3,t), place=S(8,11,t), release=S(11,12.5f,t);
            father.localPosition=baseF + new Vector3(.15f*take,0,.40f*take);
            Pose(father,"Spine",18*take+9*(place-release));
            Pose(father,"Head",12*take, -18*S(12.5f,14,t));
            float hold=1-release;
            Pose(father,"LeftUpperArm",-56*hold,0,-12*hold); Pose(father,"RightUpperArm",-56*hold,0,12*hold);
            Pose(father,"LeftLowerArm",-55*hold); Pose(father,"RightLowerArm",-55*hold);
            float speak=Mathf.Sin(t*3.4f)*2*(S(8,8.5f,t)-S(17.5f,18,t)); Pose(father,"Head",speak);
            float ask=S(4,4.7f,t)-S(7.2f,8,t); Pose(daughter,"Head",-12*ask,-20*ask);
            Pose(daughter,"RightUpperArm",-20*ask,0,10*ask); Pose(daughter,"RightLowerArm",-35*ask);
            float walk=S(18,24,t); daughter.localPosition=Vector3.Lerp(baseD,destination,walk);
            daughter.localRotation=Quaternion.Slerp(rest[daughter],Quaternion.Euler(0,180,0),S(17.5f,18.5f,t));
            float gait=Mathf.Sin((t-18)*8.5f)*(S(18,18.5f,t)-S(23.5f,24,t));
            Pose(daughter,"LeftUpperLeg",24*gait); Pose(daughter,"RightUpperLeg",-24*gait);
            Pose(daughter,"LeftLowerLeg",Mathf.Max(0,-gait)*30); Pose(daughter,"RightLowerLeg",Mathf.Max(0,gait)*30);
            Pose(daughter,"LeftUpperArm",-14*gait); Pose(daughter,"RightUpperArm",14*gait);
            Bone(mother,"Hips").localPosition+=new Vector3(0,-.43f,0);
            Pose(mother,"LeftUpperLeg",-88); Pose(mother,"RightUpperLeg",-88);
            Pose(mother,"LeftLowerLeg",88); Pose(mother,"RightLowerLeg",88);
            Pose(mother,"Spine",10); Pose(mother,"Head",18);
            float fold=Mathf.Sin(t*1.7f);
            Pose(mother,"LeftUpperArm",-35+5*fold,0,-14); Pose(mother,"RightUpperArm",-35-5*fold,0,14);
            Pose(mother,"LeftLowerArm",-70+10*fold); Pose(mother,"RightLowerArm",-70-10*fold);
            foreach(var tr in actors)
            {
                var c=curves[tr]; var p=tr.localPosition; var q=tr.localRotation;
                float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w}; for(int k=0;k<7;k++) c[k].AddKey(t,values[k]);
            }
            Vector3 bp=t<3?Vector3.Lerp(inBox,carry,S(0,3,t)):Vector3.Lerp(carry,put,place);
            for(int k=0;k<3;k++) burnerCurves[k].AddKey(t,bp[k]);
        }
        foreach(var tr in actors) { tr.localRotation=rest[tr]; tr.localPosition=restPos[tr]; }
        var clip=new AnimationClip{name="Scene01_清晨上学_26s",frameRate=30};
        string[] props={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
        foreach(var pair in curves) for(int k=0;k<7;k++) clip.SetCurve(AnimationUtility.CalculateTransformPath(pair.Key,group.transform),typeof(Transform),props[k],pair.Value[k]);
        clip.EnsureQuaternionContinuity(); AssetDatabase.CreateAsset(clip,Dir+"/Scene01_Characters.anim");
        AddAnimation(timeline,director,group,clip,"Scene01 · 父亲取炉 / 女儿上学 / 母亲理衣");
        var burnerClip=new AnimationClip{name="Scene01_取炉放桌",frameRate=30};
        // Parent binding animates the burner's own local coordinates without mixing axis conversion.
        var propRoot=new GameObject("Scene01_PropMotion"); propRoot.transform.SetParent(house,false);
        burner.transform.SetParent(propRoot.transform,false);
        for(int k=0;k<3;k++) burnerClip.SetCurve(burner.name,typeof(Transform),props[k],burnerCurves[k]);
        AssetDatabase.CreateAsset(burnerClip,Dir+"/Scene01_Burner.anim"); AddAnimation(timeline,director,propRoot,burnerClip,"Scene01 · 香炉离箱 → 圆桌");
        var activation=timeline.CreateTrack<ActivationTrack>(null,"Scene01 · 人物显示 0–26s"); var active=activation.CreateDefaultClip(); active.start=0; active.duration=26; activation.postPlaybackState=ActivationTrack.PostPlaybackState.Inactive; director.SetGenericBinding(activation,group);
        var textObj=new GameObject("Scene01_Dialogue",typeof(TextMesh)); textObj.transform.SetParent(house,true); textObj.transform.position=new Vector3(0,1.15f,-2.02f); textObj.transform.rotation=Quaternion.Euler(0,180,0);
        var text=textObj.GetComponent<TextMesh>(); text.anchor=TextAnchor.MiddleCenter; text.fontSize=48; text.characterSize=.024f; text.color=new Color(1,.94f,.80f);
        var font=AssetDatabase.FindAssets("t:Font").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith(".ttf",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".otf",StringComparison.OrdinalIgnoreCase));
        if(font!=null) { text.font=AssetDatabase.LoadAssetAtPath<Font>(font); text.GetComponent<MeshRenderer>().sharedMaterial=text.font.material; }
        var dialogue=textObj.AddComponent<Scene01Dialogue>(); dialogue.director=director; dialogue.caption=text; dialogue.chime=chime;
        var title=Find("Tips").GetComponentsInChildren<UnityEngine.UI.Text>(true).First(t=>t.text.StartsWith("Scene 1"));
        title.text="约1951年｜清晨\n许多人渡海来到台湾，仍盼着早日回乡。\n一家人在港口边安顿下来，也渐渐适应了这里的生活。";
        if(text.font) title.font=text.font;
        EditorUtility.SetDirty(timeline); AssetDatabase.SaveAssets(); director.time=0; director.Evaluate();
        Selection.activeGameObject=group; EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/Scene01-validation.txt","Scene01: 26 seconds / 781 samples; 3 existing actors; native character and burner animation tracks; actor activation; original carousel delayed by 26 seconds; original bindings retained.\nVoice files were not supplied; dialogue has assignable voice slots.\n");
        Debug.Log("[Scene01] Build complete and scene saved.");
    }
    static void AddAnimation(TimelineAsset timeline,PlayableDirector director,GameObject obj,AnimationClip clip,string name)
    {
        var track=timeline.CreateTrack<AnimationTrack>(null,name); track.trackOffset=TrackOffset.ApplySceneOffsets;
        var tc=track.CreateDefaultClip(); ((AnimationPlayableAsset)tc.asset).clip=clip; tc.start=0; tc.duration=26;  tc.displayName=clip.name;
        var anim=obj.GetComponent<Animator>()??obj.AddComponent<Animator>(); director.SetGenericBinding(track,anim);
    }
    static AudioClip MakeChime()
    {
        int rate=44100,n=rate; byte[] data=new byte[44+n*2];
        using(var ms=new MemoryStream(data)) using(var w=new BinaryWriter(ms))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36+n*2); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate*2); w.Write((short)2); w.Write((short)16); w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(n*2);
            for(int i=0;i<n;i++){ double t=i/(double)rate; w.Write((short)(2600*Math.Sin(t*2*Math.PI*1568)*Math.Exp(-t*7)*Math.Min(1,t*100))); }
        }
        File.WriteAllBytes(Dir+"/航海顺利_轻叮.wav",data); AssetDatabase.ImportAsset(Dir+"/航海顺利_轻叮.wav"); return AssetDatabase.LoadAssetAtPath<AudioClip>(Dir+"/航海顺利_轻叮.wav");
    }
}

