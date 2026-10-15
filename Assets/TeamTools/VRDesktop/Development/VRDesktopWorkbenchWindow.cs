#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XRTeam.DebugTools.Editor
{
    public sealed class VRDesktopWorkbenchWindow : EditorWindow
    {
        readonly string[] tabs={"控制台","场景与视角","对象检查","光效与语言","记录与验收","帮助"};
        int tab;Vector2 scroll;SceneAsset scene;
        WorkbenchSettings settings;
        List<WorkbenchTargetInfo> targets=new List<WorkbenchTargetInfo>();
        readonly WorkbenchVisualOverride visual=new WorkbenchVisualOverride();
        string filter="",bookmarkName="观察位置",manualTask="",note="";
        double nextRepaint;
        GUIStyle title,subtitle,card;
        DesktopSimulatorHost Host=>DesktopSimulatorHost.Current;

        [MenuItem("XR Team/VR 桌面调试工作台")]
        public static void Open(){GetWindow<VRDesktopWorkbenchWindow>("VR 桌面工作台").Show();}
        void OnEnable()
        {
            minSize=new Vector2(680,600);settings=WorkbenchSettings.Load();
            if(!scene)scene=AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkbenchSceneSession.SourceFor(SceneManager.GetActiveScene().path));
            EditorApplication.update+=Refresh;WorkbenchJournal.Changed+=Repaint;
        }
        void OnDisable(){EditorApplication.update-=Refresh;WorkbenchJournal.Changed-=Repaint;visual.Restore();}
        void Refresh()
        {
            if(Host)visual.Apply();
            if(EditorApplication.timeSinceStartup>nextRepaint){nextRepaint=EditorApplication.timeSinceStartup+.15;Repaint();}
        }
        void Styles()
        {
            if(title!=null)return;
            title=new GUIStyle(EditorStyles.boldLabel){fontSize=22,normal={textColor=new Color(.65f,.9f,1)}};
            subtitle=new GUIStyle(EditorStyles.wordWrappedLabel){normal={textColor=new Color(.7f,.76f,.8f)}};
            card=new GUIStyle(EditorStyles.helpBox){padding=new RectOffset(14,14,12,12)};
        }
        void OnGUI()
        {
            Styles();settings=Host?Host.Settings:settings??WorkbenchSettings.Load();
            EditorGUI.DrawRect(new Rect(0,0,position.width,94),new Color(.055f,.105f,.15f));
            GUILayout.Space(14);GUILayout.Label("  VR DESKTOP WORKBENCH",title);
            GUILayout.Label("    团队桌面调试 · 模拟头显 / 双手 / 交互 / 测试记录",subtitle);GUILayout.Space(18);
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(Host?"● 桌面模拟运行中":"● 未启用 · 正式VR路线不受影响",GUILayout.Width(270));
                if(Host)
                {
                    Host.Frozen=GUILayout.Toggle(Host.Frozen,"冻结模拟输入",EditorStyles.toolbarButton);
                    if(GUILayout.Button("复位",EditorStyles.toolbarButton)){Host.Pilot.Reset();Host.ResetHands();}
                    if(GUILayout.Button("截图",EditorStyles.toolbarButton))WorkbenchJournal.ChooseExportFolder(true);
                    if(GUILayout.Button("停止并恢复",EditorStyles.toolbarButton))WorkbenchSceneSession.Stop();
                }
            }
            tab=GUILayout.Toolbar(tab,tabs);scroll=EditorGUILayout.BeginScrollView(scroll);
            try{switch(tab){case 0:Controls();break;case 1:Scenes();break;case 2:Targets();break;case 3:Visuals();break;case 4:Journal();break;default:Help();break;}}
            catch(Exception e){EditorGUILayout.HelpBox(e.Message,MessageType.Error);}
            EditorGUILayout.EndScrollView();
            if(GUI.changed)settings.Save();
        }
        void Card(string heading,Action body){GUILayout.Space(10);using(new EditorGUILayout.VerticalScope(card)){GUILayout.Label(heading,EditorStyles.boldLabel);GUILayout.Space(5);body();}}
        void Controls()
        {
            Card("会话与安全",()=>{
                scene=(SceneAsset)EditorGUILayout.ObjectField("启动场景",scene,typeof(SceneAsset),false);
                EditorGUILayout.HelpBox("手动启用后复制场景运行，原场景和项目XR配置不变。停止后恢复原场景；正式构建不包含工作台UI。",MessageType.Info);
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !scene || WorkbenchSceneSession.HasRecovery))
                    if(GUILayout.Button("启动桌面模拟",GUILayout.Height(34)))WorkbenchSceneSession.Start(AssetDatabase.GetAssetPath(scene));
                if(WorkbenchSceneSession.HasRecovery && !EditorApplication.isPlayingOrWillChangePlaymode)
                    if(GUILayout.Button("恢复原场景（上次会话恢复）"))WorkbenchSceneSession.Restore();
            });
            Card("头显与控制器",()=>{
                settings.cameraSpeed=EditorGUILayout.Slider("移动速度 m/s",settings.cameraSpeed,.1f,8);
                settings.lookSpeed=EditorGUILayout.Slider("视角速度 °/s",settings.lookSpeed,10,180);
                settings.handSpeed=EditorGUILayout.Slider("手柄移动 m/s",settings.handSpeed,.05f,3);
                settings.handTurnSpeed=EditorGUILayout.Slider("手柄转速 °/s",settings.handTurnSpeed,10,180);
                settings.defaultEyeHeight=EditorGUILayout.Slider("无追踪默认眼高 m",settings.defaultEyeHeight,.6f,2.2f);
                settings.collisionMovement=EditorGUILayout.Toggle("摄像机简易碰撞",settings.collisionMovement);
                settings.muteAudio=EditorGUILayout.Toggle("暂停场景音频",settings.muteAudio);
                settings.showOverlay=EditorGUILayout.Toggle("Game视图操作提示",settings.showOverlay);
                if(!Host)return;
                bool right=GUILayout.Toolbar(settings.rightHand?1:0,new[]{"左手（通用目标）","右手（团队交互）"})==1;Host.SetRightHand(right);
                EditorGUILayout.HelpBox("键盘输入只在Game视图获得焦点时读取。当前焦点："+(Host.HasGameFocus?"Game视图":"编辑器面板"),MessageType.None);
                Host.TriggerOverride=GUILayout.Toggle(Host.TriggerOverride,"模拟扳机保持（再次点击松开）","Button");
                Host.GripOverride=GUILayout.Toggle(Host.GripOverride,"模拟握持保持（再次点击松开）","Button");
            });
            if(Host)Card("实时输入",()=>{
                EditorGUILayout.LabelField("输入来源","桌面模拟，不是SteamVR追踪");
                EditorGUILayout.LabelField("当前路由",Host.Router.Route);
                EditorGUILayout.LabelField("命中目标",Host.Router.TargetName);
                EditorGUILayout.LabelField("扳机",Host.ActiveHand.Down?"按下":Host.ActiveHand.Held?"保持":Host.ActiveHand.Released?"释放":"松开");
                EditorGUILayout.Vector3Field("模拟头显",Host.View.transform.position);
                Vector3 p=EditorGUILayout.Vector3Field("手柄位置",Host.ActiveHand.Transform.position);
                Vector3 r=EditorGUILayout.Vector3Field("手柄旋转",Host.ActiveHand.Transform.eulerAngles);
                Host.ActiveHand.Transform.SetPositionAndRotation(p,Quaternion.Euler(r));
            });
        }
        void Scenes()
        {
            Card("场景浏览",()=>{
                foreach(var entry in EditorBuildSettings.scenes)
                {
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(Path.GetFileNameWithoutExtension(entry.path)+(entry.enabled?"":"（构建中未启用）"));
                        using(new EditorGUI.DisabledScope(!Host || !entry.enabled))
                            if(GUILayout.Button("调试加载",GUILayout.Width(85))){visual.Restore();WorkbenchJournal.Add("调试跳转",entry.path,"人为跳过剧情，不能作为自然流程测试证据");WorkbenchSceneSession.Load(entry.path,LoadSceneMode.Single);}
                    }
                }
            });
            Card("个人视角书签（不上传）",()=>{
                bookmarkName=EditorGUILayout.TextField("书签名称",bookmarkName);
                using(new EditorGUI.DisabledScope(!Host))if(GUILayout.Button("保存当前观察位置")){
                    var data=WorkbenchBookmarks.Load();data.items.Add(new ViewBookmark{label=bookmarkName,scene=WorkbenchSceneSession.SourceFor(SceneManager.GetActiveScene().path),position=Host.View.transform.position,rotation=Host.View.transform.rotation});WorkbenchBookmarks.Save(data);
                }
                var bookmarks=WorkbenchBookmarks.Load();int remove=-1;
                for(int i=0;i<bookmarks.items.Count;i++){
                    var b=bookmarks.items[i];using(new EditorGUILayout.HorizontalScope()){
                        GUILayout.Label(b.label+" · "+Path.GetFileNameWithoutExtension(b.scene));
                        using(new EditorGUI.DisabledScope(!Host || b.scene!=WorkbenchSceneSession.SourceFor(SceneManager.GetActiveScene().path)))if(GUILayout.Button("恢复",GUILayout.Width(60))){Host.Pilot.SetPose(b.position,b.rotation);Host.ResetHands();}
                        if(GUILayout.Button("移除",GUILayout.Width(50)))remove=i;
                    }
                }
                if(remove>=0){bookmarks.items.RemoveAt(remove);WorkbenchBookmarks.Save(bookmarks);}
            });
        }
        void Targets()
        {
            Card("可交互对象与引用检查",()=>{
                using(new EditorGUILayout.HorizontalScope()){
                    filter=EditorGUILayout.TextField("筛选",filter);
                    if(GUILayout.Button("刷新列表",GUILayout.Width(85)))targets=WorkbenchValidation.ScanTargets();
                    if(GUILayout.Button("检查场景",GUILayout.Width(85)))WorkbenchValidation.RunSceneChecks();
                }
                if(targets.Count==0)EditorGUILayout.HelpBox("刷新后显示已适配物体、按钮、光效和内置交互入口。不是所有模型都会自动变成可交互物体。",MessageType.Info);
                foreach(var target in targets){
                    if(!target.gameObject || (!string.IsNullOrEmpty(filter) && !target.gameObject.name.ToLowerInvariant().Contains(filter.ToLowerInvariant())))continue;
                    using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)){
                        GUILayout.Label(target.kind+" / "+target.gameObject.name,EditorStyles.boldLabel);
                        EditorGUILayout.HelpBox(target.detail,target.warning?MessageType.Warning:MessageType.None);
                        using(new EditorGUILayout.HorizontalScope()){
                            if(GUILayout.Button("选中"))Selection.activeGameObject=target.gameObject;
                            using(new EditorGUI.DisabledScope(!Host)){
                                if(GUILayout.Button("观察"))Host.FocusTarget(target.gameObject);
                                if(GUILayout.Button("手柄靠近"))Host.HandNear(target.gameObject);
                                if(GUILayout.Button("手柄瞄准"))Host.ActiveHand.AimAt(WorkbenchValidation.BoundsOf(target.gameObject).center);
                            }
                        }
                    }
                }
                EditorGUILayout.HelpBox("新物体：在原场景选中物体，通过下方按钮显式添加适配器，再在Inspector连接真实业务事件。此操作会修改该场景，需要单独保存与审查。",MessageType.Info);
                using(new EditorGUI.DisabledScope(EditorApplication.isPlaying || !Selection.activeGameObject))
                    if(GUILayout.Button("给所选物体添加通用适配器（Undo可撤销）")){
                        if(!Selection.activeGameObject.GetComponent<DebugInteractionTarget>())Undo.AddComponent<DebugInteractionTarget>(Selection.activeGameObject);
                    }
            });
        }
        void Visuals()
        {
            Card("柔光状态比较（仅模拟会话）",()=>{
                using(new EditorGUI.DisabledScope(!Host)){
                    var target=(SoftInteractionHighlight)EditorGUILayout.ObjectField("高亮组件",visual.Target,typeof(SoftInteractionHighlight),true);visual.Select(target);
                    if(visual.Target){
                        visual.Mode=GUILayout.Toolbar(visual.Mode,new[]{"默认","靠近","持有隐藏"});
                        visual.Target.color=EditorGUILayout.ColorField("光晕颜色",visual.Target.color);
                        visual.Target.width=EditorGUILayout.Slider("宽度 m",visual.Target.width,.001f,.1f);
                        visual.Target.idleStrength=EditorGUILayout.Slider("默认强度",visual.Target.idleStrength,0,1);
                        visual.Target.nearbyStrength=EditorGUILayout.Slider("靠近强度",visual.Target.nearbyStrength,0,1);
                        if(GUILayout.Button("结束强制状态并恢复参数"))visual.Restore();
                    }
                }
            });
            Card("语言与界面预览",()=>{
                EditorGUILayout.HelpBox("这里直接设置调试语言，不代表完成了语言按钮操作。按钮能否点中，请回到Game视图用模拟扳机测试。",MessageType.Info);
                using(new EditorGUI.DisabledScope(!Host))using(new EditorGUILayout.HorizontalScope()){
                    if(GUILayout.Button("预览中文")){SubtitleLanguage.Select(false);WorkbenchJournal.Add("调试语言","中文","通过工具直接设置，非按钮操作验收");}
                    if(GUILayout.Button("预览英文")){SubtitleLanguage.Select(true);WorkbenchJournal.Add("调试语言","English","通过工具直接设置，非按钮操作验收");}
                }
                if(GUILayout.Button("检查文字裁切和场景引用"))WorkbenchValidation.RunSceneChecks();
            });
        }
        void Journal()
        {
            Card("验证与导出",()=>{
                using(new EditorGUILayout.HorizontalScope()){
                    if(GUILayout.Button("运行工具自检"))WorkbenchSelfTests.Run();
                    if(GUILayout.Button("检查当前场景"))WorkbenchValidation.RunSceneChecks();
                    if(GUILayout.Button("导出JSON报告"))WorkbenchJournal.ChooseExportFolder(false);
                    if(GUILayout.Button("清空本次记录"))WorkbenchJournal.Clear();
                }
                foreach(var check in WorkbenchJournal.Checks)EditorGUILayout.LabelField(check.status+" · "+check.name,check.detail,EditorStyles.wordWrappedLabel);
            });
            Card("人工验收（明确标记来源）",()=>{
                manualTask=EditorGUILayout.TextField("检查内容",manualTask);
                using(new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(manualTask)))using(new EditorGUILayout.HorizontalScope()){
                    if(GUILayout.Button("人工确认通过"))WorkbenchJournal.Manual(manualTask,true);
                    if(GUILayout.Button("人工记录失败"))WorkbenchJournal.Manual(manualTask,false);
                }
                note=EditorGUILayout.TextField("补充备注",note);if(GUILayout.Button("记录备注") && !string.IsNullOrWhiteSpace(note)){WorkbenchJournal.Add("备注","人工",note);note="";}
            });
            Card("事件记录（最多1500条）",()=>{
                var items=WorkbenchJournal.Events;for(int i=items.Count-1;i>=Mathf.Max(0,items.Count-100);i--){var e=items[i];GUILayout.Label($"{e.seconds:F1}s | {e.scene} | {e.category} | {e.target}",EditorStyles.boldLabel);GUILayout.Label(e.detail,EditorStyles.wordWrappedLabel);}
            });
        }
        void Help()
        {
            Card("操作速查",()=>{
                foreach(string line in new[]{"先在控制台选择场景并启动模拟，再点击Game视图。","WASD：摄像机平移；Q/E：升降；方向键/按住鼠标右键：查看。","IJKL：手柄平移；U/O：升降；按住鼠标中键：手柄旋转。","数字小键盘8/2、4/6、1/3：手柄俯仰、偏航和翻滚。","Space：扳机按下/保持/释放；F：通用目标握持；Tab：切换左右手。","R：手柄复位；C：观察位置复位；Esc：关闭证件预览。","右手未命中时白色射线保持5米，命中目标变绿。","语言选择、记忆确认和入台证按团队约定仅支持右手。","入台证和香炉仍受真实剧情条件限制，工具不会强制跳过等待。"})GUILayout.Label(line,EditorStyles.wordWrappedLabel);
            });
            Card("能力边界",()=>{
                EditorGUILayout.HelpBox("工具支持已提供输入入口的团队交互、显式配置的通用事件目标，以及世界空间uGUI。并不自动兼容任意SteamVR抓取脚本、UI Toolkit或屏幕空间UI。握持事件本身不会自动改变物体位置。",MessageType.Warning);
                GUILayout.Label("简易摄像机碰撞只是桌面查看辅助，不是最终玩家碰撞验收。截图导出仅保存相机画面，不包含编辑器工作台窗口。",EditorStyles.wordWrappedLabel);
                GUILayout.Label("停止模拟后自动回到原始场景；异常中断后，可通过控制台的恢复按钮恢复。个人设置与书签保存在本机EditorPrefs，不上传。",EditorStyles.wordWrappedLabel);
            });
        }
    }
}
#endif
