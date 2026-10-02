#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

namespace IncenseCharacters.Editor
{
    public static class IncenseCharacterBuilder
    {
        const string Source = "Assets/IncenseCharacters/Source";
        [Serializable] class ModelData { public string name; public MaterialData[] materials; public BoneData[] bones; public float[] positions, normals, weights; public int[] joints; public SubmeshData[] submeshes; public ClipData[] clips; }
        [Serializable] class MaterialData { public string name; public float[] color; }
        [Serializable] class BoneData { public string name; public int parent; public float[] position, localPosition; }
        [Serializable] class SubmeshData { public int[] indices; }
        [Serializable] class ClipData { public string name,description; public float duration; public bool loop; public FrameData[] keys; }
        [Serializable] class FrameData { public float time; public float[] rotations,positions; }
        static Vector3 V(float[] a,int i=0) { return new Vector3(a[i],a[i+1],a[i+2]); }
        static Shader ChooseShader()
        {
            var rp=GraphicsSettings.currentRenderPipeline;
            Shader s=null;
            if(rp!=null) s=Shader.Find(rp.GetType().Name.Contains("HD")?"HDRP/Lit":"Universal Render Pipeline/Lit");
            return s!=null?s:Shader.Find("Standard");
        }
        static Material MakeMaterial(string name,Color color,string path)
        {
            var shader=ChooseShader(); if(shader==null) throw new Exception("No supported Lit shader. Use Built-in or URP project.");
            var m=new Material(shader){name=name};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_Color"))m.SetColor("_Color",color);
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.12f);
            if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",.12f);
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",0);
            AssetDatabase.CreateAsset(m,path);return m;
        }
        [MenuItem("Tools/Incense Characters/1 Build Characters")]
        public static void Build()
        {
            if(!Directory.Exists(Source)){EditorUtility.DisplayDialog("Source missing","Copy the IncenseCharacters folder into your Unity project's Assets folder first.","OK");return;}
            // A new folder per build preserves previous prefabs and scene references.
            string output=AssetDatabase.GenerateUniqueAssetPath("Assets/IncenseCharacters/Generated");
            Directory.CreateDirectory(output);AssetDatabase.Refresh();
            try
            {
                string[] files=Directory.GetFiles(Source,"*.json");Array.Sort(files);
                for(int i=0;i<files.Length;i++)
                {
                    var data=JsonUtility.FromJson<ModelData>(File.ReadAllText(files[i]));
                    EditorUtility.DisplayProgressBar("Building incense story cast",data.name,(float)i/files.Length);
                    BuildOne(data,output);
                }
                AssetDatabase.SaveAssets();AssetDatabase.Refresh();
                EditorPrefs.SetString("IncenseCharacters.LastBuild",output);
                Selection.activeObject=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(output);
                EditorUtility.DisplayDialog("Characters ready","4 prefabs, skinned meshes, materials and 17 animation clips per character were created in:\n"+output+"\n\nNext: Tools > Incense Characters > 2 Open Demo Scene","OK");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }
        static void BuildOne(ModelData d,string output)
        {
            var folder=output+"/"+d.name;Directory.CreateDirectory(folder+"/Animations");AssetDatabase.Refresh();
            GameObject go=new GameObject(d.name);
            try
            {
                var bones=new Transform[d.bones.Length];var paths=new string[bones.Length];
                for(int i=0;i<bones.Length;i++)
                {
                    var b=d.bones[i];var t=new GameObject(b.name).transform;
                    t.SetParent(b.parent<0?go.transform:bones[b.parent],false);t.localPosition=V(b.localPosition);bones[i]=t;
                    paths[i]=(b.parent<0?"":paths[b.parent]+"/")+b.name;
                }
                int count=d.positions.Length/3;var vs=new Vector3[count];var normals=new Vector3[count];var weights=new BoneWeight[count];
                for(int i=0;i<count;i++)
                {
                    vs[i]=V(d.positions,i*3);normals[i]=V(d.normals,i*3);
                    weights[i]=new BoneWeight{boneIndex0=d.joints[i*4],boneIndex1=d.joints[i*4+1],boneIndex2=d.joints[i*4+2],boneIndex3=d.joints[i*4+3],weight0=d.weights[i*4],weight1=d.weights[i*4+1],weight2=d.weights[i*4+2],weight3=d.weights[i*4+3]};
                }
                var mesh=new Mesh{name=d.name+"_Mesh",indexFormat=IndexFormat.UInt32};mesh.vertices=vs;mesh.normals=normals;mesh.boneWeights=weights;
                var bind=new Matrix4x4[bones.Length];for(int i=0;i<bones.Length;i++)bind[i]=bones[i].worldToLocalMatrix*go.transform.localToWorldMatrix;
                mesh.bindposes=bind;mesh.subMeshCount=d.submeshes.Length;
                for(int i=0;i<d.submeshes.Length;i++)mesh.SetTriangles(d.submeshes[i].indices,i);
                mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/Mesh.asset");
                var materials=new Material[d.materials.Length];
                for(int i=0;i<materials.Length;i++){var md=d.materials[i];materials[i]=MakeMaterial(md.name,new Color(md.color[0],md.color[1],md.color[2],md.color[3]),folder+"/"+md.name+".mat");}
                var meshObj=new GameObject("CharacterMesh");meshObj.transform.SetParent(go.transform,false);
                var smr=meshObj.AddComponent<SkinnedMeshRenderer>();smr.sharedMesh=mesh;smr.bones=bones;smr.rootBone=bones[1];smr.sharedMaterials=materials;
                smr.localBounds=new Bounds(new Vector3(0,.85f,0),new Vector3(2.6f,2.8f,2.6f));smr.updateWhenOffscreen=true;
                var controller=AnimatorController.CreateAnimatorControllerAtPath(folder+"/Character.controller");var sm=controller.layers[0].stateMachine;
                foreach(var cd in d.clips)
                {
                    var clip=new AnimationClip{name=cd.name,frameRate=30};
                    for(int b=0;b<bones.Length;b++)
                    {
                        for(int axis=0;axis<4;axis++) AddCurve(clip,paths[b],"m_LocalRotation."+"xyzw"[axis],cd,b*4+axis,true);
                        for(int axis=0;axis<3;axis++) AddCurve(clip,paths[b],"m_LocalPosition."+"xyz"[axis],cd,b*3+axis,false);
                    }
                    clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=cd.loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
                    AssetDatabase.CreateAsset(clip,folder+"/Animations/"+cd.name+".anim");
                    var state=sm.AddState(cd.name);state.motion=clip;state.writeDefaultValues=true;if(cd.name=="Idle")sm.defaultState=state;
                }
                var animator=go.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var avatar=AvatarBuilder.BuildGenericAvatar(go,"Root");avatar.name=d.name+"_GenericAvatar";AssetDatabase.CreateAsset(avatar,folder+"/GenericAvatar.asset");animator.avatar=avatar;
                var player=go.AddComponent<IncenseCharacterPlayer>();player.animator=animator;player.actionNames=Array.ConvertAll(d.clips,c=>c.name);
                PrefabUtility.SaveAsPrefabAsset(go,folder+"/"+d.name+".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        static void AddCurve(AnimationClip clip,string path,string property,ClipData cd,int offset,bool rotation)
        {
            var keys=new Keyframe[cd.keys.Length];
            for(int j=0;j<keys.Length;j++)keys[j]=new Keyframe(cd.keys[j].time,rotation?cd.keys[j].rotations[offset]:cd.keys[j].positions[offset]);
            var curve=new AnimationCurve(keys);
            for(int j=0;j<keys.Length;j++){AnimationUtility.SetKeyLeftTangentMode(curve,j,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,j,AnimationUtility.TangentMode.Linear);}
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
        }
        [MenuItem("Tools/Incense Characters/2 Open Demo Scene")]
        public static void Demo()
        {
            var output=EditorPrefs.GetString("IncenseCharacters.LastBuild","Assets/IncenseCharacters/Generated");
            if(!Directory.Exists(output)){EditorUtility.DisplayDialog("Build first","Run 1 Build Characters first.","OK");return;}
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var names=new[]{"Father_Young","Father_Elder","Daughter_Child","Daughter_Adult"};
            for(int i=0;i<names.Length;i++)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(output+"/"+names[i]+"/"+names[i]+".prefab");if(prefab==null)continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.position=new Vector3((i-1.5f)*1.05f,0,0);
            }
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Preview floor";floor.transform.localScale=Vector3.one*.65f;
            var floorPath=AssetDatabase.GenerateUniqueAssetPath(output+"/PreviewFloor.mat");floor.GetComponent<Renderer>().sharedMaterial=MakeMaterial("Preview floor",new Color(.72f,.71f,.67f),floorPath);
            var light=new GameObject("Key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(38,-30,0);light.shadows=LightShadows.Soft;
            RenderSettings.ambientLight=new Color(.55f,.55f,.52f);
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,1.6f,5.8f);camera.transform.LookAt(new Vector3(0,.9f,0));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.19f,.20f);camera.fieldOfView=36;camera.nearClipPlane=.05f;camera.gameObject.AddComponent<AudioListener>();
            new GameObject("Desktop Preview Controls").AddComponent<IncenseCastPreview>();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),AssetDatabase.GenerateUniqueAssetPath(output+"/CastDemo.unity"));
            AssetDatabase.SaveAssets();
        }
    }
    [CustomEditor(typeof(IncenseCharacterPlayer))]
    public class CharacterPlayerInspector:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var p=(IncenseCharacterPlayer)target;
            EditorGUILayout.HelpBox("Enter Play mode, then press an action button. These are Generic transform animations, not a Humanoid retargeting rig.",MessageType.Info);
            GUI.enabled=Application.isPlaying;
            if(p.actionNames!=null)foreach(var n in p.actionNames)if(GUILayout.Button(n))p.PlayAction(n);
            GUI.enabled=true;
        }
    }
}
#endif
