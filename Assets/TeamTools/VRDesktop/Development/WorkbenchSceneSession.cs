#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;

namespace XRTeam.DebugTools.Editor
{
    [Serializable] public sealed class SceneSessionData
    {
        public SceneSetup[] original;
        public List<string> sources=new List<string>();
        public List<string> copies=new List<string>();
        public string folder;
        public bool xrAutomaticStart, xrCaptured;
    }

    [InitializeOnLoad]
    public static class WorkbenchSceneSession
    {
        public const string GeneratedRoot="Assets/TeamTools/VRDesktop/Generated";
        static string StateKey=>WorkbenchSettings.Key+".Recovery";
        const string ArmedKey="XRTeam.Workbench.Armed";
        public static bool HasRecovery=>!string.IsNullOrEmpty(EditorPrefs.GetString(StateKey,""));
        public static bool Armed=>SessionState.GetBool(ArmedKey,false);

        static WorkbenchSceneSession()
        {
            EditorApplication.playModeStateChanged+=OnPlayState;
            if(Armed)SuspendXR();
        }

        public static void Start(string source)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(HasRecovery){EditorUtility.DisplayDialog("需要恢复","上一次模拟的场景恢复尚未完成，请先点击恢复原场景。","知道了");return;}
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty){EditorUtility.DisplayDialog("请先保存场景","工作台不会替你保存团队场景。请手动保存或撤销未保存改动后再开始。","知道了");return;}
            if(string.IsNullOrEmpty(source) || !File.Exists(source))throw new ArgumentException("Select a saved scene.");
            var data=new SceneSessionData{original=EditorSceneManager.GetSceneManagerSetup(),folder=GeneratedRoot+"/Session_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff")};
            EditorPrefs.SetString(StateKey,JsonUtility.ToJson(data));
            try
            {
                if(!AssetDatabase.IsValidFolder(GeneratedRoot))AssetDatabase.CreateFolder("Assets/TeamTools/VRDesktop","Generated");
                AssetDatabase.CreateFolder(GeneratedRoot,Path.GetFileName(data.folder));
                var paths=new HashSet<string>();foreach(var entry in EditorBuildSettings.scenes)if(entry.enabled)paths.Add(entry.path);paths.Add(source);
                var names=new HashSet<string>();
                foreach(var path in paths)
                {
                    string name=Path.GetFileNameWithoutExtension(path);
                    if(!names.Add(name))throw new InvalidOperationException("Duplicate scene name: "+name);
                    string copy=data.folder+"/"+name+".unity";
                    if(!AssetDatabase.CopyAsset(path,copy))throw new IOException("Scene copy failed: "+path);
                    var scene=EditorSceneManager.OpenScene(copy,OpenSceneMode.Single);
                    Sanitize(scene);
                    EditorSceneManager.SaveScene(scene);
                    data.sources.Add(path);data.copies.Add(copy);
                    EditorPrefs.SetString(StateKey,JsonUtility.ToJson(data));
                }
                EditorSceneManager.OpenScene(Map(source,data),OpenSceneMode.Single);
                SessionState.SetBool(ArmedKey,true);
                SuspendXR();
                EditorApplication.EnterPlaymode();
            }
            catch {SessionState.SetBool(ArmedKey,false);Restore();throw;}
        }

        static void Sanitize(Scene scene)
        {
            Camera best=null;
            foreach(var root in scene.GetRootGameObjects())
            {
                foreach(var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if(best==null || camera.CompareTag("MainCamera") || camera.name=="Camera (eye)")best=camera;
                    camera.enabled=false;
                }
                foreach(var listener in root.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
                foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if(!component)continue;
                    string ns=component.GetType().Namespace??"";
                    if(ns.StartsWith("Valve.VR",StringComparison.Ordinal) || component is RightController)
                        component.enabled=false;
                }
            }
            var spawn=new GameObject("__VRDesktopSpawn");
            if(best)spawn.transform.SetPositionAndRotation(best.transform.position,best.transform.rotation);
            else spawn.transform.position=new Vector3(0,1.65f,0);
            // A camera at rig-floor level is a tracking placeholder, not an eye height.
            if(best && best.transform.localPosition.y<.3f)spawn.transform.position+=Vector3.up*WorkbenchSettings.Load().defaultEyeHeight;
        }

        static string Map(string name,SceneSessionData data)
        {
            for(int i=0;i<data.sources.Count;i++)
                if(data.sources[i]==name || Path.GetFileNameWithoutExtension(data.sources[i])==name)return data.copies[i];
            throw new InvalidOperationException("Scene not prepared for desktop simulation: "+name);
        }

        public static string SourceFor(string copy)
        {
            var data=Read();if(data==null)return copy;
            int i=data.copies.IndexOf(copy);return i>=0?data.sources[i]:copy;
        }

        public static AsyncOperation Load(string name,LoadSceneMode mode)
        {
            var data=Read();if(data==null)throw new InvalidOperationException("No active desktop scene session.");
            string path=Map(name,data);
            WorkbenchJournal.Add("场景加载",Path.GetFileNameWithoutExtension(name),"通过临时场景副本加载；非强制剧情跳转");
            return EditorSceneManager.LoadSceneAsyncInPlayMode(path,new LoadSceneParameters(mode));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeSceneLoad()
        {
            if(!Armed)return;
            VRDebugSession.Active=true;VRDebugSession.SceneLoader=Load;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterSceneLoad()
        {
            if(!Armed || DesktopSimulatorHost.Current)return;
            var root=new GameObject("[Editor Only] VR Desktop Workbench");
            UnityEngine.Object.DontDestroyOnLoad(root);root.AddComponent<DesktopSimulatorHost>();
        }

        static SceneSessionData Read()
        {
            string text=EditorPrefs.GetString(StateKey,"");return string.IsNullOrEmpty(text)?null:JsonUtility.FromJson<SceneSessionData>(text);
        }
        static void SuspendXR()
        {
            var data=Read();if(data==null)return;
            var xr=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if(!xr)return;
            if(!data.xrCaptured){data.xrAutomaticStart=xr.InitManagerOnStart;data.xrCaptured=true;EditorPrefs.SetString(StateKey,JsonUtility.ToJson(data));}
            // In-memory only: never SetDirty/SaveAssets on the XR configuration.
            xr.InitManagerOnStart=false;XRGeneralSettings.Instance=xr;
        }
        static void OnPlayState(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode || !HasRecovery)return;
            SessionState.SetBool(ArmedKey,false);EditorApplication.delayCall+=Restore;
        }
        public static void Stop(){if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();else Restore();}
        public static void Restore()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var data=Read();if(data==null)return;
            if(data.xrCaptured){var xr=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);if(xr)xr.InitManagerOnStart=data.xrAutomaticStart;}
            // Only the tool-owned, validated session path is eligible for automatic cleanup.
            if(data.original!=null && data.original.Length>0)EditorSceneManager.RestoreSceneManagerSetup(data.original);
            if(!string.IsNullOrEmpty(data.folder) && data.folder.StartsWith(GeneratedRoot+"/Session_",StringComparison.Ordinal))AssetDatabase.DeleteAsset(data.folder);
            EditorPrefs.DeleteKey(StateKey);SessionState.SetBool(ArmedKey,false);
            VRDebugSession.Active=false;VRDebugSession.SceneLoader=null;VRDebugSession.View=null;
        }
    }
}
#endif
