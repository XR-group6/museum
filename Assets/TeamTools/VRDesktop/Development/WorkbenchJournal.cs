#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace XRTeam.DebugTools.Editor
{
    [Serializable] public sealed class WorkbenchEvent {public string utc,scene,category,target,detail;public float seconds;}
    [Serializable] public sealed class CheckResult
    {
        public string name,status,detail;
        public CheckResult(string name,string status,string detail){this.name=name;this.status=status;this.detail=detail;}
    }
    [Serializable] public sealed class WorkbenchReport
    {
        public string toolVersion="1.0.0",unityVersion,platform,exportedUtc;
        public string scope="Desktop simulation only; native bindings, tracking, stereo and headset performance are NOT validated.";
        public List<WorkbenchEvent> events;public List<CheckResult> checks;
    }
    public static class WorkbenchJournal
    {
        public static readonly List<WorkbenchEvent> Events=new List<WorkbenchEvent>();
        public static readonly List<CheckResult> Checks=new List<CheckResult>();
        public static event Action Changed;
        public static void Add(string category,string target,string detail)
        {
            Events.Add(new WorkbenchEvent{utc=DateTime.UtcNow.ToString("o"),seconds=Time.realtimeSinceStartup,scene=SceneManager.GetActiveScene().name,category=category,target=target,detail=detail});
            if(Events.Count>1500)Events.RemoveAt(0);Changed?.Invoke();
        }
        public static void Clear(){Events.Clear();Checks.Clear();Changed?.Invoke();}
        public static void Manual(string task,bool pass){Checks.Add(new CheckResult(task,pass?"MANUAL_PASS":"MANUAL_FAIL","Human confirmation; not an automatic assertion."));Add("人工验收",task,pass?"通过":"失败");}
        public static string Export(string directory)
        {
            Directory.CreateDirectory(directory);
            var report=new WorkbenchReport{unityVersion=Application.unityVersion,platform=Application.platform.ToString(),exportedUtc=DateTime.UtcNow.ToString("o"),events=new List<WorkbenchEvent>(Events),checks=new List<CheckResult>(Checks)};
            string path=Path.Combine(directory,"VRDesktopReport_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff")+".json");File.WriteAllText(path,JsonUtility.ToJson(report,true));return path;
        }
        public static string Capture(Camera camera,string directory)
        {
            if(!camera)throw new InvalidOperationException("No simulation camera.");Directory.CreateDirectory(directory);
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;var rt=new RenderTexture(1600,1000,24);Texture2D texture=null;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();string path=Path.Combine(directory,"VRDesktop_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff")+".png");File.WriteAllBytes(path,texture.EncodeToPNG());Add("截图",camera.name,path);return path;}
            finally{camera.targetTexture=previousTarget;RenderTexture.active=previousActive;if(texture)UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);}
        }
        public static void ChooseExportFolder(bool screenshot)
        {
            string folder=EditorUtility.OpenFolderPanel(screenshot?"选择截图保存目录":"选择报告保存目录","","");if(string.IsNullOrEmpty(folder))return;
            try{string path=screenshot?Capture(DesktopSimulatorHost.Current?DesktopSimulatorHost.Current.View:null,folder):Export(folder);EditorUtility.RevealInFinder(path);}catch(Exception e){Debug.LogException(e);}
        }
    }
}
#endif
