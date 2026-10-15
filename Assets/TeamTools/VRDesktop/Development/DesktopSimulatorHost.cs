#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace XRTeam.DebugTools.Editor
{
    [DefaultExecutionOrder(15000)]
    public sealed class DesktopSimulatorHost : MonoBehaviour
    {
        public static DesktopSimulatorHost Current {get;private set;}
        public WorkbenchSettings Settings {get;private set;}
        public DesktopCameraPilot Pilot {get;private set;}
        public SimulatedHand Left {get;private set;}
        public SimulatedHand Right {get;private set;}
        public WorkbenchInteractionRouter Router {get;private set;}
        public SimulatedHand ActiveHand=>Settings.rightHand?Right:Left;
        public Camera View=>Pilot.View;
        public bool Frozen;
        public bool MouseLook;
        public bool TriggerOverride, GripOverride;
        public bool HasGameFocus=>EditorWindow.focusedWindow && EditorWindow.focusedWindow.GetType().Name=="GameView";
        LineRenderer ray;Material rayMaterial;
        bool originalMute;

        void Awake()
        {
            Current=this;Settings=WorkbenchSettings.Load();Pilot=new DesktopCameraPilot(transform);
            Left=new SimulatedHand(transform,true,Settings.markerColor);Right=new SimulatedHand(transform,false,Settings.markerColor);
            Router=new WorkbenchInteractionRouter();ray=gameObject.AddComponent<LineRenderer>();ray.useWorldSpace=true;ray.positionCount=2;ray.startWidth=.003f;ray.endWidth=.0015f;
            rayMaterial=new Material(Shader.Find("Sprites/Default"));ray.sharedMaterial=rayMaterial;
            originalMute=AudioListener.pause;SceneManager.sceneLoaded+=OnScene;SetSceneHome();VRDebugSession.View=View;
            WorkbenchJournal.Add("会话","桌面模拟","使用临时场景，不代表头显验证");
        }
        void OnScene(Scene scene,LoadSceneMode mode){Router.Reset();SetSceneHome();WorkbenchJournal.Add("场景就绪",scene.name,"模拟相机及双手复位");}
        public void SetSceneHome()
        {
            var spawn=GameObject.Find("__VRDesktopSpawn");
            Pilot.SetHome(spawn?spawn.transform.position:new Vector3(0,1.65f,0),spawn?spawn.transform.rotation:Quaternion.identity);
            ResetHands();
        }
        public void ResetHands(){Left.Place(View,true);Right.Place(View,false);Router?.Reset();}
        public void SetRightHand(bool right)
        {
            if(right==Settings.rightHand)return;Left.InputFrame(false,false);Right.InputFrame(false,false);Router.Reset();Settings.rightHand=right;Settings.Save();
        }
        public void FocusTarget(GameObject target)
        {
            if(!target)return;var bounds=WorkbenchValidation.BoundsOf(target);
            Pilot.SetPose(bounds.center+new Vector3(0,.25f,-Mathf.Max(.6f,bounds.size.magnitude)),Quaternion.identity);
            View.transform.LookAt(bounds.center);Pilot.SetPose(View.transform.position,View.transform.rotation);ResetHands();ActiveHand.AimAt(bounds.center);
            WorkbenchJournal.Add("定位",target.name,"改变模拟观察位置，不移动目标");
        }
        public void HandNear(GameObject target)
        {
            if(!target)return;var bounds=WorkbenchValidation.BoundsOf(target);ActiveHand.Transform.position=bounds.center+Vector3.back*Mathf.Max(.08f,bounds.extents.z+.05f);ActiveHand.AimAt(bounds.center);
        }
        static float Axis(KeyCode positive,KeyCode negative)=>(Input.GetKey(positive)?1:0)-(Input.GetKey(negative)?1:0);
        void Update()
        {
            AudioListener.pause=Settings.muteAudio;
            bool keys=HasGameFocus && !Frozen;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            if(keys)
            {
                var move=new Vector3(Axis(KeyCode.D,KeyCode.A),Axis(KeyCode.E,KeyCode.Q),Axis(KeyCode.W,KeyCode.S));
                var look=new Vector2(Axis(KeyCode.RightArrow,KeyCode.LeftArrow),Axis(KeyCode.DownArrow,KeyCode.UpArrow));
                if(Input.GetMouseButton(1)){look.x+=Input.GetAxis("Mouse X")*2;look.y-=Input.GetAxis("Mouse Y")*2;}
                Pilot.Step(move,look,dt,Settings);
                var handMove=new Vector3(Axis(KeyCode.L,KeyCode.J),Axis(KeyCode.O,KeyCode.U),Axis(KeyCode.I,KeyCode.K));
                var turn=new Vector3(Axis(KeyCode.Keypad2,KeyCode.Keypad8),Axis(KeyCode.Keypad6,KeyCode.Keypad4),Axis(KeyCode.Keypad3,KeyCode.Keypad1));
                if(Input.GetMouseButton(2))turn+=new Vector3(-Input.GetAxis("Mouse Y"),Input.GetAxis("Mouse X"),0)*2;
                ActiveHand.Move(handMove,turn,View,Settings.handSpeed,Settings.handTurnSpeed,dt);
                if(Input.GetKeyDown(KeyCode.C)){Pilot.Reset();ResetHands();}
                if(Input.GetKeyDown(KeyCode.R))ResetHands();
                if(Input.GetKeyDown(KeyCode.Tab))SetRightHand(!Settings.rightHand);
                if(Input.GetKeyDown(KeyCode.Escape)){var p=FindObjectOfType<PermitInspection>();if(p)p.Close();TriggerOverride=GripOverride=false;}
            }
            bool trigger=!Frozen && ((keys && Input.GetKey(KeyCode.Space)) || TriggerOverride);
            bool grip=!Frozen && ((keys && Input.GetKey(KeyCode.F)) || GripOverride);
            ActiveHand.InputFrame(trigger,grip);
            (Settings.rightHand?Left:Right).InputFrame(false,false);
            Left.SetActive(!Settings.rightHand);Right.SetActive(Settings.rightHand);
            if(!Frozen)Router.Process(ActiveHand,View,!Settings.rightHand);
            else Router.Reset();
            ray.enabled=!Frozen;ray.SetPosition(0,ActiveHand.Transform.position);ray.SetPosition(1,Router.End);
            ray.startColor=ray.endColor=Router.Hit?Color.green:Color.white;
        }
        void OnGUI()
        {
            if(!Settings.showOverlay)return;
            var old=GUI.color;GUI.color=new Color(.05f,.12f,.17f,.95f);GUI.Box(new Rect(12,12,610,104),GUIContent.none);GUI.color=old;
            GUI.Label(new Rect(26,20,580,22),"VR 桌面调试工作台 · 模拟模式 · 非头显验收");
            GUI.Label(new Rect(26,44,580,22),"WASD移动 · Q/E升降 · 右键看向 · IJKL/UO手柄 · 中键旋转");
            GUI.Label(new Rect(26,67,580,22),"Space扳机 · F握持 · Tab左右手 · R手柄复位 · C视角复位");
            GUI.Label(new Rect(26,90,580,22),$"{(Settings.rightHand?"右手":"左手")} | {Router.TargetName} | 扳机 {(ActiveHand.Held?"保持":"松开")}");
        }
        void OnDestroy()
        {
            SceneManager.sceneLoaded-=OnScene;Router?.Dispose();Left?.Dispose();Right?.Dispose();if(rayMaterial)Destroy(rayMaterial);
            AudioListener.pause=originalMute;VRDebugSession.View=null;if(Current==this)Current=null;
        }
    }
}
#endif
