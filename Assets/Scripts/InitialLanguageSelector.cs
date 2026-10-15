using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Valve.VR;

public sealed class InitialLanguageSelector : MonoBehaviour
{
    public bool Built { get; private set; }
    Camera viewer; Font font; Material material; LineRenderer ray;
    SteamVR_Action_Boolean trigger;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if(SceneManager.GetActiveScene().name=="scene_01" && !SubtitleLanguage.HasSelected && !FindObjectOfType<InitialLanguageSelector>())
            new GameObject("Initial Subtitle Language Choice").AddComponent<InitialLanguageSelector>();
    }
    public void Build(Camera camera)
    {
        if(Built) return;
        viewer=camera; Built=true;
        material=new Material(Shader.Find("TeamPrototype/LanguagePanelUI"));
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","PingFang SC","Arial Unicode MS","Arial"},40);
        var canvas=gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=camera; canvas.sortingOrder=150;
        GetComponent<RectTransform>().sizeDelta=new Vector2(820,360);
        transform.localScale=Vector3.one*0.0014f;
        var bg=gameObject.AddComponent<Image>(); bg.material=material; bg.color=new Color(0.02f,0.055f,0.075f,0.95f);
        Label("请选择字幕语言\nChoose subtitle language",new Vector2(0,95),new Vector2(780,120),42);
        Choice("中文",false,new Vector2(-195,-25)); Choice("English",true,new Vector2(195,-25));
        Label("中文配音保持不变 / Audio remains in Chinese",new Vector2(0,-130),new Vector2(780,50),25);
        Position();
        if(!XRTeam.DebugTools.VRDebugSession.Active) trigger=SteamVR_Input.GetAction<SteamVR_Action_Boolean>("GrabPinch");
        if(trigger!=null) trigger.actionSet.Activate(SteamVR_Input_Sources.RightHand);
        var pointer=new GameObject("Language Choice Pointer"); pointer.transform.SetParent(transform,false);
        ray=pointer.AddComponent<LineRenderer>(); ray.positionCount=2; ray.startWidth=0.004f; ray.endWidth=0.002f;
        ray.material=new Material(Shader.Find("Sprites/Default")); ray.startColor=ray.endColor=Color.cyan; ray.enabled=false;
    }
    void Position() { transform.position=viewer.transform.TransformPoint(new Vector3(0,0,1.65f)); transform.rotation=viewer.transform.rotation; }
    void Label(string text,Vector2 pos,Vector2 size,int fontSize)
    {
        var go=new GameObject("LanguageLabel",typeof(RectTransform),typeof(Text)); go.transform.SetParent(transform,false);
        var rect=go.GetComponent<RectTransform>(); rect.anchoredPosition=pos; rect.sizeDelta=size;
        var label=go.GetComponent<Text>(); label.font=font; label.fontSize=fontSize; label.text=text;
        label.color=Color.white; label.alignment=TextAnchor.MiddleCenter; label.material=material; label.raycastTarget=false;
    }
    void Choice(string label,bool english,Vector2 pos)
    {
        var go=new GameObject(label,typeof(RectTransform),typeof(Image)); go.transform.SetParent(transform,false);
        var rect=go.GetComponent<RectTransform>(); rect.anchoredPosition=pos; rect.sizeDelta=new Vector2(330,95);
        var image=go.GetComponent<Image>(); image.material=material; image.color=new Color(0.05f,0.36f,0.43f,1);
        var hit=go.AddComponent<BoxCollider>(); hit.size=new Vector3(330,95,12);
        go.AddComponent<InitialLanguageChoice>().english=english;
        Label(label,pos,new Vector2(330,95),42);
    }
    public void Select(bool english)
    {
        SubtitleLanguage.Select(english); gameObject.SetActive(false); Destroy(gameObject);
    }
    void Update()
    {
        if(XRTeam.DebugTools.VRDebugSession.Active) {
#if UNITY_EDITOR
            if(!Built && XRTeam.DebugTools.VRDebugSession.View) Build(XRTeam.DebugTools.VRDebugSession.View);
#endif
            return;
        }
        if(!Built) {
            Camera main=Camera.main;
            if(!main) foreach(var camera in Camera.allCameras) if(camera.enabled) {main=camera; break;}
            if(!main) return;
            // Wait for actual tracking before placing the headset's entry UI.
            bool tracked=false;
            foreach(var pose in FindObjectsOfType<SteamVR_Behaviour_Pose>()) if(pose.isValid) tracked=true;
            if(!tracked) return;
            Build(main);
        }
        ray.enabled=false;
        foreach(var choice in GetComponentsInChildren<InitialLanguageChoice>())
            choice.GetComponent<Image>().color=new Color(0.05f,0.36f,0.43f,1);
        foreach(var pose in FindObjectsOfType<SteamVR_Behaviour_Pose>()) {
            if(!pose.isActiveAndEnabled || !pose.isValid || trigger==null) continue;
            if(pose.inputSource!=SteamVR_Input_Sources.RightHand) continue;
            var aim=new Ray(pose.transform.position,pose.transform.forward);
            ray.enabled=true; ray.SetPosition(0,aim.origin); ray.SetPosition(1,aim.GetPoint(5));
            ray.startColor=ray.endColor=Color.white;
            ProcessPointer(aim,trigger.GetStateDown(pose.inputSource),true);
            break;
        }
        if(Input.GetMouseButtonDown(0)) foreach(var choice in GetComponentsInChildren<InitialLanguageChoice>()) {
            RaycastHit hit;
            if(choice.GetComponent<Collider>().Raycast(viewer.ScreenPointToRay(Input.mousePosition),out hit,5)) {Select(choice.english); return;}
        }
    }
    // Both real SteamVR and desktop simulation use this exact choice handler.
    public bool ProcessPointer(Ray aim,bool pressed,bool drawRay=false)
    {
        if(!Built)return false;
        if(ray)ray.enabled=drawRay;
        if(drawRay){ray.SetPosition(0,aim.origin);ray.SetPosition(1,aim.GetPoint(5));ray.startColor=ray.endColor=Color.white;}
        bool hitAny=false;
        foreach(var choice in GetComponentsInChildren<InitialLanguageChoice>()) {
            RaycastHit hit;bool hovered=choice.GetComponent<Collider>().Raycast(aim,out hit,5);
            choice.GetComponent<Image>().color=hovered?new Color(.2f,.65f,.55f):new Color(.05f,.36f,.43f,1);
            if(!hovered)continue;hitAny=true;
            if(drawRay){ray.SetPosition(1,hit.point);ray.startColor=ray.endColor=Color.green;}
            if(pressed){Select(choice.english);return true;}
        }
        return hitAny;
    }
    void LateUpdate() { if(Built && viewer) Position(); }
    void OnDestroy() { if(font) Destroy(font); if(material) Destroy(material); if(ray && ray.sharedMaterial) Destroy(ray.sharedMaterial); }
}
public sealed class InitialLanguageChoice : MonoBehaviour { public bool english; }
