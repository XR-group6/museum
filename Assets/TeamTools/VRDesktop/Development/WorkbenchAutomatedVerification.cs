#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Security.Cryptography;
using System.Collections.Generic;
namespace XRTeam.DebugTools.Editor
{
    /// <summary>Batch and menu acceptance runner. Executes actual Play Mode on disposable scene copies.</summary>
    [InitializeOnLoad]
    public static class WorkbenchAutomatedVerification
    {
        static int phase;
        static double timeout,waitUntil;
        static bool failed;
        static string reportFolder;
        const string Key="XRTeam.Workbench.Verify";
        static WorkbenchAutomatedVerification()
        {
            if(!SessionState.GetBool(Key,false))return;
            phase=SessionState.GetInt(Key+".Phase",0);failed=SessionState.GetBool(Key+".Failed",false);
            reportFolder=SessionState.GetString(Key+".Folder","");timeout=EditorApplication.timeSinceStartup+150;
            Application.logMessageReceived+=OnLog;EditorApplication.update+=Step;
        }
        [MenuItem("XR Team/验证桌面工作台（独立测试会话）")]
        public static void Begin()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the current session first.");
            reportFolder=Path.GetFullPath("WorkbenchValidationResults/Run_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));Directory.CreateDirectory(reportFolder);
            phase=0;failed=false;timeout=EditorApplication.timeSinceStartup+150;
            WriteSourceHashes(Path.Combine(reportFolder,"Sources_Before.txt"));
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+".Phase",0);SessionState.SetBool(Key+".Failed",false);SessionState.SetString(Key+".Folder",reportFolder);
            Application.logMessageReceived+=OnLog;
            EditorApplication.update+=Step;
            if(WorkbenchSceneSession.HasRecovery)WorkbenchSceneSession.Restore();
            WorkbenchSceneSession.Start("Assets/Project3D/Scenes/scene_01.unity");
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert)return;
            failed=true;SessionState.SetBool(Key+".Failed",true);File.AppendAllText(Path.Combine(reportFolder,"Errors.txt"),message+"\n"+stack+"\n");
        }
        static void Step()
        {
            try{
                if(EditorApplication.timeSinceStartup>timeout)throw new TimeoutException("Workbench acceptance timeout, phase="+phase);
                var host=DesktopSimulatorHost.Current;
                if(phase==0 && host)
                {
                    WorkbenchSelfTests.Run();host.Frozen=true;phase=1;waitUntil=EditorApplication.timeSinceStartup+.5;
                }
                else if(phase==1 && host && EditorApplication.timeSinceStartup>waitUntil)
                {
                    var language=UnityEngine.Object.FindObjectOfType<InitialLanguageSelector>();WorkbenchSelfTests.Require(language && language.Built,"Language UI not created");
                    var button=GameObject.Find("English");WorkbenchSelfTests.Require(button,"English button missing");
                    host.Right.Transform.SetPositionAndRotation(host.View.transform.TransformPoint(new Vector3(.2f,-.18f,.4f)),host.View.transform.rotation);
                    host.Right.AimAt(button.transform.position);host.Right.InputFrame(false,false);host.Router.Process(host.Right,host.View,false);
                    WorkbenchSelfTests.Require(host.Router.Hit,"Language ray miss");
                    host.Right.InputFrame(true,false);host.Router.Process(host.Right,host.View,false);
                    WorkbenchSelfTests.Require(SubtitleLanguage.English && SubtitleLanguage.HasSelected,"Shared language handler failed");
                    host.Right.InputFrame(false,false);WorkbenchJournal.Add("验收断言","语言选择","真实共享处理函数通过模拟射线选择英文");
                    WorkbenchSceneSession.Load("scene_07",LoadSceneMode.Single);phase=2;waitUntil=EditorApplication.timeSinceStartup+4;
                }
                else if(phase==2 && host && SceneManager.GetActiveScene().name=="scene_07" && EditorApplication.timeSinceStartup>waitUntil)
                {
                    var permit=UnityEngine.Object.FindObjectOfType<PermitInspection>();WorkbenchSelfTests.Require(permit && permit.Ready,"Exploration did not unlock");
                    WorkbenchSelfTests.Require(SubtitleLanguage.English,"Language did not persist");
                    host.FocusTarget(permit.gameObject);host.HandNear(permit.gameObject);
                    host.Right.Transform.position=permit.transform.position+Vector3.up*.2f;host.Right.AimAt(permit.transform.position);
                    host.Right.InputFrame(false,false);Physics.SyncTransforms();host.Router.Process(host.Right,host.View,false);
                    WorkbenchSelfTests.Require(host.Router.Hit,"Permit ray miss");
                    host.Right.InputFrame(true,false);host.Router.Process(host.Right,host.View,false);
                    WorkbenchSelfTests.Require(permit.IsOpen,"Permit did not open through shared handler");
                    host.Right.InputFrame(false,false);phase=3;waitUntil=EditorApplication.timeSinceStartup+.5;
                }
                else if(phase==3 && host && EditorApplication.timeSinceStartup>waitUntil)
                {
                    var permit=UnityEngine.Object.FindObjectOfType<PermitInspection>();
                    permit.RenderPreview();WorkbenchJournal.Capture(host.View,reportFolder);
                    var image=GameObject.Find("ObjectPreviewRawImage");WorkbenchSelfTests.Require(image,"Preview collider missing");
                    host.Right.AimAt(image.transform.position);host.Right.InputFrame(true,false);host.Router.Process(host.Right,host.View,false);
                    host.Right.Transform.Rotate(0,45,15);host.Right.InputFrame(true,false);host.Router.Process(host.Right,host.View,false);
                    var pivot=GameObject.Find("PermitRotationPivot");WorkbenchSelfTests.Require(pivot && Quaternion.Angle(pivot.transform.localRotation,Quaternion.identity)>20,"Controller rotation not applied");
                    host.Right.InputFrame(false,false);host.Router.Process(host.Right,host.View,false);
                    var toggle=GameObject.Find("ToggleFold");host.Right.AimAt(toggle.transform.position);host.Right.InputFrame(true,false);host.Router.Process(host.Right,host.View,false);
                    WorkbenchSelfTests.Require(permit.IsUnfolded,"Unfold button failed");
                    host.Right.InputFrame(false,false);permit.RenderPreview();WorkbenchJournal.Capture(host.View,reportFolder);
                    var close=GameObject.Find("Close");host.Right.AimAt(close.transform.position);host.Right.InputFrame(true,false);host.Router.Process(host.Right,host.View,false);
                    WorkbenchSelfTests.Require(!permit.IsOpen && permit.tableModel.gameObject.activeSelf,"Return-to-table failed");
                    WorkbenchSelfTests.Require(permit.tour.movement.enabled && permit.tour.turning.enabled,"Locomotion not restored");
                    WorkbenchJournal.Add("验收断言","入台证","选取、手柄旋转、展开按钮、放回桌面、控制恢复通过");
                    VerifyGenericAdapters(host);
                    WorkbenchValidation.RunSceneChecks();WorkbenchJournal.Export(reportFolder);
                    foreach(var check in WorkbenchJournal.Checks)if(check.status=="FAIL")failed=true;
                    phase=4;SessionState.SetInt(Key+".Phase",4);SessionState.SetBool(Key+".Failed",failed);WorkbenchSceneSession.Stop();
                }
                else if(phase==4 && !EditorApplication.isPlayingOrWillChangePlaymode && !WorkbenchSceneSession.HasRecovery)
                {
                    WorkbenchSelfTests.Require(!VRDebugSession.Active,"Simulation flag not reset");
                    string after=Path.Combine(reportFolder,"Sources_After.txt");WriteSourceHashes(after);
                    WorkbenchSelfTests.Require(File.ReadAllText(after)==File.ReadAllText(Path.Combine(reportFolder,"Sources_Before.txt")),"Original scenes or XR settings were modified");
                    foreach(var check in WorkbenchJournal.Checks)if(check.status=="FAIL")failed=true;
                    Finish();
                }
            }catch(Exception e){failed=true;File.AppendAllText(Path.Combine(reportFolder,"Errors.txt"),e+"\n");if(EditorApplication.isPlaying)WorkbenchSceneSession.Stop();Finish();}
        }
        static void VerifyGenericAdapters(DesktopSimulatorHost host)
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Generic Acceptance Target";cube.transform.position=new Vector3(1000,500,10);cube.transform.localScale=Vector3.one*.2f;
            var target=cube.AddComponent<DebugInteractionTarget>();target.hitCollider=cube.GetComponent<Collider>();target.displayName="通用验收物体";
            int selected=0,held=0,released=0;target.onSelect.AddListener(()=>selected++);target.onHeld.AddListener(()=>held++);target.onReleased.AddListener(()=>released++);
            host.Right.Transform.SetPositionAndRotation(cube.transform.position-Vector3.forward,Quaternion.identity);Physics.SyncTransforms();
            host.Right.InputFrame(false,false);host.Router.Process(host.Right,host.View,false);
            host.Right.InputFrame(true,true);host.Router.Process(host.Right,host.View,false);
            host.Right.InputFrame(true,true);host.Router.Process(host.Right,host.View,false);
            WorkbenchSelfTests.Require(selected==1 && held==1 && target.Hovered,"Generic target duplicated or missing events");
            host.Right.InputFrame(false,false);host.Router.Process(host.Right,host.View,false);
            WorkbenchSelfTests.Require(released==1 && !target.Held,"Generic release missing");
            target.allowLeftHand=false;host.Left.Transform.SetPositionAndRotation(host.Right.Transform.position,host.Right.Transform.rotation);
            host.Left.InputFrame(true,false);host.Router.Process(host.Left,host.View,true);WorkbenchSelfTests.Require(selected==1,"Left-hand restriction failed");
            cube.SetActive(false);UnityEngine.Object.Destroy(cube);
            var canvasObject=new GameObject("Generic UI Acceptance",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=host.View;
            canvasObject.transform.position=new Vector3(1000,500,12);canvasObject.transform.localScale=Vector3.one*.001f;canvasObject.GetComponent<RectTransform>().sizeDelta=new Vector2(600,400);
            var buttonObject=new GameObject("Generic Test Button",typeof(RectTransform),typeof(Image),typeof(Button));buttonObject.transform.SetParent(canvasObject.transform,false);
            buttonObject.GetComponent<RectTransform>().sizeDelta=new Vector2(300,100);int clicked=0;buttonObject.GetComponent<Button>().onClick.AddListener(()=>clicked++);
            Canvas.ForceUpdateCanvases();host.Right.Transform.SetPositionAndRotation(canvasObject.transform.position-Vector3.forward,Quaternion.identity);
            host.Right.InputFrame(false,false);host.Router.Process(host.Right,host.View,false);
            host.Right.InputFrame(true,false);host.Router.Process(host.Right,host.View,false);
            host.Right.InputFrame(false,false);host.Router.Process(host.Right,host.View,false);
            WorkbenchSelfTests.Require(clicked==1,"Generic world-space uGUI click failed");
            host.Right.Transform.rotation=Quaternion.Euler(0,180,0);host.Router.Process(host.Right,host.View,false);
            WorkbenchSelfTests.Require(!host.Router.Hit && Mathf.Abs(Vector3.Distance(host.Right.Transform.position,host.Router.End)-5)<.001f,"Miss ray must remain 5m without a selection");
            canvasObject.SetActive(false);UnityEngine.Object.Destroy(canvasObject);host.Router.Reset();
            WorkbenchJournal.Add("验收断言","通用适配器","选择去重、握持释放、左右手限制、世界空间uGUI按钮、未命中5米射线通过");
        }
        static void WriteSourceHashes(string path)
        {
            var files=new List<string>();foreach(var scene in EditorBuildSettings.scenes)if(scene.enabled)files.Add(scene.path);
            foreach(var file in Directory.GetFiles("ProjectSettings"))files.Add(file);
            if(Directory.Exists("Assets/XR"))foreach(var file in Directory.GetFiles("Assets/XR","*",SearchOption.AllDirectories))if(!file.EndsWith(".meta"))files.Add(file);
            files.Sort(StringComparer.Ordinal);var lines=new List<string>();
            using(var hash=SHA256.Create())foreach(string file in files)lines.Add(file+" "+BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(file))).Replace("-",""));
            File.WriteAllLines(path,lines);
        }
        static void Finish()
        {
            EditorApplication.update-=Step;Application.logMessageReceived-=OnLog;
            SessionState.SetBool(Key,false);
            File.WriteAllText(Path.Combine(reportFolder,"Result.txt"),failed?"FAIL: inspect Errors.txt and JSON report":"PASS: Play Mode desktop language/permit adapters, state tests and original scene recovery. Hardware NOT tested.");
            Debug.Log(failed?"WORKBENCH_ACCEPTANCE_FAIL":"WORKBENCH_ACCEPTANCE_PASS");
            if(Application.isBatchMode)EditorApplication.Exit(failed?1:0);
        }
    }
}
#endif
