using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
[InitializeOnLoad] public static class Scene01Revision {
 static Scene01Revision(){EditorApplication.delayCall+=Run;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Run;};}
 static Transform Bone(Transform root,string n)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==n);
 static void Solve(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 pole,Quaternion palm){
 Vector3 a=upper.position;float ab=Vector3.Distance(a,lower.position),bc=Vector3.Distance(lower.position,hand.position);Vector3 axis=(target-a).normalized;float distance=Mathf.Clamp(Vector3.Distance(a,target),Mathf.Abs(ab-bc)+.001f,ab+bc-.001f);Vector3 side=Vector3.ProjectOnPlane(pole-a,axis).normalized;float along=(ab*ab-bc*bc+distance*distance)/(2*distance);Vector3 elbow=a+axis*along+side*Mathf.Sqrt(Mathf.Max(0,ab*ab-along*along));upper.rotation=Quaternion.FromToRotation(lower.position-a,elbow-a)*upper.rotation;lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,a+axis*distance-lower.position)*lower.rotation;hand.rotation=palm;
 }
 static void Run(){ File.WriteAllText("Temp/Scene01.revision.state",SceneManager.GetActiveScene().path+" playing="+EditorApplication.isPlaying+" changing="+EditorApplication.isPlayingOrWillChangePlaymode);
 if(File.Exists("Temp/Scene01.revision.done"))return;
 if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;} if(SceneManager.GetActiveScene().name!="scene_04_01")return;
 try{
 var scene=SceneManager.GetActiveScene();var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();Func<string,Transform> find=n=>all.First(t=>t.name==n);
 var director=find("TimeLine").GetComponent<PlayableDirector>();var motion=director.GetComponent<Scene01Motion>();var view=director.GetComponent<Scene01CenserView>();
 var father=find("Father_Young");var mother=find("Mother_Young");var daughter=find("Daughter_Child_Schoolbag");
 EditorSceneManager.SaveScene(scene,"Logs/scene01_before_held_opening.unity",true);
 foreach(var clip in new[]{motion.characters,motion.burner}){
 foreach(var binding in AnimationUtility.GetCurveBindings(clip)){
 if(clip==motion.characters&&!binding.path.StartsWith("Father_Young"))continue;
 var curve=AnimationUtility.GetEditorCurve(clip,binding);var keys=curve.keys;float v=curve.Evaluate(6);
 for(int i=0;i<keys.Length;i++)if(keys[i].time<=3.01f){keys[i].value=v;keys[i].inTangent=keys[i].outTangent=0;}
 curve.keys=keys;AnimationUtility.SetEditorCurve(clip,binding,curve);
 }
 EditorUtility.SetDirty(clip);
 }
 // Explicit hold keys prevent quaternion interpolation across the long talking plateau.
 foreach(var binding in AnimationUtility.GetCurveBindings(motion.characters)){
 if(!binding.path.StartsWith("Daughter_Child_Schoolbag/Root/Hips"))continue;
 if(!(binding.path.EndsWith("/Hips")||binding.path.Contains("UpperLeg")||binding.path.Contains("LowerLeg")||binding.path.Contains("Foot")||binding.path.Contains("Toes")))continue;
 var c=AnimationUtility.GetEditorCurve(motion.characters,binding);float value=c.Evaluate(0);
 var keys=new List<Keyframe>{new Keyframe(0,value,0,0),new Keyframe(18,value,0,0)};
 keys.AddRange(c.keys.Where(k=>k.time>18.00001f));c.keys=keys.ToArray();AnimationUtility.SetEditorCurve(motion.characters,binding,c);
 }
 motion.Sample(6);
 Bone(mother,"Spine").localRotation=Quaternion.identity;
 Bone(mother,"Chest").localRotation=Quaternion.Euler(0,30,0);
 Bone(mother,"Neck").localRotation=Quaternion.identity;
 foreach(string side in new[]{"Left","Right"}){
 var u=Bone(mother,side+"UpperArm");var l=Bone(mother,side+"LowerArm");var h=Bone(mother,side+"Hand");
 u.localRotation=l.localRotation=h.localRotation=Quaternion.identity;
 var thigh=Bone(mother,side+"UpperLeg");var knee=Bone(mother,side+"LowerLeg");
 Vector3 lap=Vector3.Lerp(thigh.position,knee.position,.65f)+Vector3.up*.065f;
 Solve(u,l,h,lap,mother.TransformPoint(new Vector3(side=="Left"?.4f:-.4f,.60f,.10f)),mother.rotation*Quaternion.Euler(85,0,0));
 }
 string[] names={"Spine","Chest","Neck","Head","LeftUpperArm","LeftLowerArm","LeftHand","RightUpperArm","RightLowerArm","RightHand"};
 var bones=names.Select(n=>Bone(mother,n)).ToArray();var rest=bones.ToDictionary(t=>t,t=>t.localRotation);
 var curves=bones.ToDictionary(t=>t,t=>Enumerable.Range(0,4).Select(_=>new AnimationCurve()).ToArray());
 for(int frame=0;frame<=780;frame++){
 float t=frame/30f;motion.characters.SampleAnimation(motion.actors,t);
 foreach(var pair in rest)pair.Key.localRotation=pair.Value;
 var head=Bone(mother,"Head");var fhead=Bone(father,"Head");var dhead=Bone(daughter,"Head");
 float speak=Mathf.SmoothStep(0,1,Mathf.InverseLerp(7.6f,8.4f,t));
 // After the father's line, continue watching him; the later line is the burner's offscreen voice.
 Vector3 target=Vector3.Lerp(dhead.position,fhead.position,speak);
 head.rotation=Quaternion.LookRotation(target-head.position,Vector3.up);
 foreach(var bone in bones){var q=bone.localRotation;float[] values={q.x,q.y,q.z,q.w};for(int k=0;k<4;k++)curves[bone][k].AddKey(t,values[k]);}
 }
 string[] props={"m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
 foreach(var pair in curves)for(int k=0;k<4;k++){
 var c=pair.Value[k];if(pair.Key.name!="Head")c=AnimationCurve.Constant(0,26,c.Evaluate(0));
 AnimationUtility.SetEditorCurve(motion.characters,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(pair.Key,motion.actors.transform),typeof(Transform),props[k]),c);
 }
 motion.characters.EnsureQuaternionContinuity();EditorUtility.SetDirty(motion.characters);
 var cloth=find("Scene01_FoldedClothes");cloth.gameObject.SetActive(false);
 AssetDatabase.SaveAssets();director.RebuildGraph();director.time=0;director.Evaluate();
 var eye=view.EyeAt(0);var direction=view.daughterHead.position-eye;
 foreach(var camera in new[]{view.headsetCamera,view.desktopCamera}){
 var rig=camera==view.headsetCamera?view.headsetRig:view.desktopRig;rig.Rotate(Vector3.up,Mathf.DeltaAngle(camera.transform.eulerAngles.y,Quaternion.LookRotation(direction).eulerAngles.y),Space.World);rig.position+=eye-camera.transform.position;if(camera==view.desktopCamera)camera.transform.rotation=Quaternion.LookRotation(direction);
 }
 view.BeginView();string report="";Quaternion lastHand=Quaternion.identity;Vector3 firstBurner=Vector3.zero;
 foreach(float t in new[]{0f,2f,6f,8f,11f,16f,21f}){
 director.time=t;director.Evaluate();view.ApplyView();var cam=view.headsetCamera.isActiveAndEnabled?view.headsetCamera:view.desktopCamera;
 var head=Bone(mother,"Head");Vector3 gazeTarget=Vector3.Lerp(Bone(daughter,"Head").position,Bone(father,"Head").position,Mathf.SmoothStep(0,1,Mathf.InverseLerp(7.6f,8.4f,t)));var hand=Bone(mother,"LeftHand");
 if(t==0){firstBurner=view.storyBurner.position;lastHand=hand.rotation;}
 float eyeError=Vector3.Distance(cam.transform.position,view.EyeAt(t));float gaze=Vector3.Angle(head.forward,gazeTarget-head.position);float handDelta=Quaternion.Angle(lastHand,hand.rotation); float legAngle=Mathf.Max(Quaternion.Angle(Quaternion.identity,Bone(daughter,"LeftUpperLeg").localRotation),Quaternion.Angle(Quaternion.identity,Bone(daughter,"RightUpperLeg").localRotation));
 report+="t="+t+" burner="+view.storyBurner.position+" eye="+cam.transform.position+" eyeError="+eyeError+" motherGazeError="+gaze+" handRotationDelta="+handDelta+" daughterLegAngle="+legAngle+"\n";
 if((t<18&&legAngle>.01f)||eyeError>.0001f||gaze>5||handDelta>.1f||(t<=8&&Vector3.Distance(firstBurner,view.storyBurner.position)>.001f))throw new Exception("Sample validation failed at "+t+"\n"+report);
 }
 view.RestoreRenderers();director.time=0;director.Evaluate();view.ApplyView();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 File.WriteAllText("Logs/Scene01-held-opening-validation.txt",report);File.WriteAllText("Temp/Scene01.revision.done","OK");
 }catch(Exception e){File.WriteAllText("Temp/Scene01.revision.error",e.ToString());Debug.LogException(e);}
 }
}



