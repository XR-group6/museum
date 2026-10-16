using UnityEngine;
using UnityEngine.UI;
using Valve.VR;

// Scene-seven-only inspection. One right-hand ray owns all permit input.
public sealed class PermitInspection : MonoBehaviour
{
    public GameObject closedPrefab, openPrefab;
    public Transform tableModel;
    public Collider tableCollider;
    public SoftInteractionHighlight tableGlow;
    public MuseumSceneTour tour;
    public Camera viewer;
    public float nearbyDistance = .35f;
    public bool IsOpen { get; private set; }
    public bool IsUnfolded { get; private set; }
    public bool PreviewReadyForCapture;
    SteamVR_Behaviour_Pose hand;
    SteamVR_Action_Boolean trigger;
    LineRenderer pointer;
    Material pointerMaterial, uiMaterial, composite;
    GameObject panel, container, pivot, clone;
    Camera previewCamera;
    RenderTexture texture;
    Font font;
    Text title, instruction, toggleLabel, closeLabel;
    Image toggleImage, closeImage;
    BoxCollider previewHit, toggleHit, closeHit;
    MuseumLeftStickMove movement; MuseumRightStickTurn turning;
    bool restoreMove, restoreTurn, rotating, mouseDragging;
    Vector2 lastMouse;
    Quaternion handStart, objectStart;
    int previewLayer;
    Camera[] maskedCameras; int[] oldMasks;
    Light[] maskedLights; int[] oldLightMasks;
    public bool Ready => tour && tour.ExplorationStarted;

    void OnEnable()
    {
        BindInput();
        SubtitleLanguage.Changed += RefreshText;
    }
    void BindInput()
    {
        previewLayer=LayerMask.NameToLayer("ObjectPreview");
        trigger=SteamVR_Input.GetAction<SteamVR_Action_Boolean>("GrabPinch");
        if(trigger!=null) trigger.actionSet.Activate(SteamVR_Input_Sources.RightHand);
        foreach(var p in FindObjectsOfType<SteamVR_Behaviour_Pose>())
            if(p.inputSource==SteamVR_Input_Sources.RightHand) { hand=p; break; }
        if(!pointer) {
            var go=new GameObject("Permit Right-Hand Pointer (5m)"); go.transform.SetParent(transform,false);
            pointer=go.AddComponent<LineRenderer>();pointer.useWorldSpace=true;pointer.positionCount=2;
            pointer.startWidth=.003f;pointer.endWidth=.0015f;
            pointerMaterial=new Material(Shader.Find("Sprites/Default"));pointer.sharedMaterial=pointerMaterial;pointer.enabled=false;
        }
    }
    Camera Viewer() {
        if(viewer && viewer.isActiveAndEnabled) return viewer;
        if(Camera.main && Camera.main!=previewCamera) return viewer=Camera.main;
        foreach(var c in Camera.allCameras) if(c!=previewCamera && c.isActiveAndEnabled) return viewer=c;
        return null;
    }
    void Update()
    {
        if(pointer) pointer.enabled=false;
        if(!Ready || !Viewer()) {if(IsOpen) Close();return;}
        bool tracked=hand && hand.isActiveAndEnabled && hand.isValid && trigger!=null;
        if(tracked) {
            var ray=new Ray(hand.transform.position,hand.transform.forward);
            bool down=trigger.GetStateDown(SteamVR_Input_Sources.RightHand);
            bool held=trigger.GetState(SteamVR_Input_Sources.RightHand);
            ProcessRay(ray,down,held,hand.transform.rotation);
        } else { rotating=false; if(tableGlow) tableGlow.SetNearby(false); }
        // Editor-only convenience; does not replace the SteamVR input path.
        if(Application.isEditor) {
            if(Input.GetKeyDown(KeyCode.Escape)) Close();
            if(Input.GetMouseButtonDown(0)) {
                var ray=viewer.ScreenPointToRay(Input.mousePosition);
                ProcessRay(ray,true,true,Quaternion.identity);
                RaycastHit hit;
                mouseDragging=IsOpen && previewHit && previewHit.Raycast(ray,out hit,5);
                lastMouse=Input.mousePosition;
            }
            if(mouseDragging && Input.GetMouseButton(0) && pivot) {
                Vector2 current=Input.mousePosition; var delta=current-lastMouse;lastMouse=current;
                pivot.transform.localRotation=Quaternion.Euler(delta.y*.4f,-delta.x*.4f,0)*pivot.transform.localRotation;
            }
            if(Input.GetMouseButtonUp(0)) mouseDragging=false;
        }
    }
    void ProcessRay(Ray ray,bool down,bool held,Quaternion rotation)
    {
        Vector3 end=ray.GetPoint(5);bool hovered=false;RaycastHit hit;
        if(!IsOpen) {
            bool near=tableCollider && Vector3.Distance(ray.origin,tableCollider.ClosestPoint(ray.origin))<=nearbyDistance;
            // The nearest hit must be this permit, not a wall or the table.
            hovered=Physics.Raycast(ray,out hit,5,~(1<<previewLayer),QueryTriggerInteraction.Ignore) && hit.collider==tableCollider;
            if(hovered) end=hit.point;
            if(tableGlow) tableGlow.SetNearby(near || hovered);
            DrawPointer(ray,end,hovered);
            if(hovered && down) Open();
            return;
        }
        if(toggleImage) toggleImage.color=new Color(.15f,.30f,.32f);
        if(closeImage) closeImage.color=new Color(.35f,.18f,.12f);
        if(closeHit && closeHit.Raycast(ray,out hit,5)) {
            hovered=true;end=hit.point;closeImage.color=new Color(.2f,.65f,.55f);
            if(down) { Close();DrawPointer(ray,end,true);return; }
        } else if(toggleHit && toggleHit.Raycast(ray,out hit,5)) {
            hovered=true;end=hit.point;toggleImage.color=new Color(.2f,.65f,.55f);
            if(down) ToggleFold();
        } else if(previewHit && previewHit.Raycast(ray,out hit,5)) {
            hovered=true;end=hit.point;
            if(down) {rotating=true;handStart=rotation;objectStart=pivot.transform.localRotation;}
        }
        if(!held) rotating=false;
        if(rotating && held && pivot) {
            Quaternion delta=Quaternion.Inverse(viewer.transform.rotation)*rotation*Quaternion.Inverse(handStart)*viewer.transform.rotation;
            pivot.transform.localRotation=delta*objectStart;
        }
        DrawPointer(ray,end,hovered);
    }
    void DrawPointer(Ray ray,Vector3 end,bool hit) {
        pointer.enabled=true;pointer.SetPosition(0,ray.origin);pointer.SetPosition(1,end);
        pointer.startColor=pointer.endColor=hit?Color.green:Color.white;
    }
    public void Open()
    {
        if(IsOpen || (!Ready && !PreviewReadyForCapture) || !Viewer() || !closedPrefab || !openPrefab) return;
        previewLayer=LayerMask.NameToLayer("ObjectPreview");
        if(previewLayer<0) {Debug.LogError("Permit: ObjectPreview layer missing.",this);return;}
        // Bind again at interface creation, matching the repaired language selector.
        BindInput();
        maskedCameras=FindObjectsOfType<Camera>();oldMasks=new int[maskedCameras.Length];
        for(int i=0;i<maskedCameras.Length;i++) {oldMasks[i]=maskedCameras[i].cullingMask;maskedCameras[i].cullingMask &= ~(1<<previewLayer);}
        // New scene lighting must not darken or tint the isolated document preview.
        maskedLights=FindObjectsOfType<Light>();oldLightMasks=new int[maskedLights.Length];
        for(int i=0;i<maskedLights.Length;i++){oldLightMasks[i]=maskedLights[i].cullingMask;maskedLights[i].cullingMask &= ~(1<<previewLayer);}
        movement=tour?tour.movement:null;turning=tour?tour.turning:null;
        restoreMove=movement && movement.enabled;restoreTurn=turning && turning.enabled;
        if(movement) movement.enabled=false;if(turning) turning.enabled=false;
        container=new GameObject("PreviewCameraContainer");container.transform.position=new Vector3(0,-1000,0);
        PreviewLight("Permit Preview Key",Quaternion.Euler(20,-25,0),1.0f);
        PreviewLight("Permit Preview Fill",Quaternion.Euler(-20,145,0),.45f);
        pivot=new GameObject("PermitRotationPivot");pivot.transform.SetParent(container.transform,false);
        var cam=new GameObject("PermitPreviewCamera");cam.transform.SetParent(container.transform,false);cam.transform.localPosition=new Vector3(0,0,-3);
        previewCamera=cam.AddComponent<Camera>();previewCamera.orthographic=true;previewCamera.orthographicSize=.88f;
        previewCamera.nearClipPlane=.05f;previewCamera.farClipPlane=8;previewCamera.cullingMask=1<<previewLayer;
        previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=Color.clear;previewCamera.stereoTargetEye=StereoTargetEyeMask.None;
        texture=new RenderTexture(1024,1024,24,RenderTextureFormat.ARGB32);texture.Create();previewCamera.targetTexture=texture;
        IsOpen=true;IsUnfolded=false;BuildPanel();BuildClone();
        if(tableModel) tableModel.gameObject.SetActive(false);
    }
    void PreviewLight(string name,Quaternion rotation,float intensity)
    {
        var go=new GameObject(name);go.transform.SetParent(container.transform,false);go.transform.localRotation=rotation;
        var light=go.AddComponent<Light>();light.type=LightType.Directional;light.color=Color.white;light.intensity=intensity;
        light.cullingMask=1<<previewLayer;light.shadows=LightShadows.None;
    }
    void BuildClone()
    {
        if(clone) {clone.SetActive(false);Dispose(clone);}
        clone=Instantiate(IsUnfolded?openPrefab:closedPrefab,pivot.transform);
        clone.transform.localPosition=Vector3.zero;clone.transform.localRotation=Quaternion.identity;clone.transform.localScale=Vector3.one;
        var bounds=BoundsOf(clone);float longest=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
        clone.transform.localScale=Vector3.one*(1.35f/Mathf.Max(longest,.0001f));
        bounds=BoundsOf(clone);clone.transform.position+=pivot.transform.position-bounds.center;
        foreach(var c in clone.GetComponentsInChildren<Collider>()) c.enabled=false;
        SetLayer(clone,previewLayer);
        var glow=clone.AddComponent<SoftInteractionHighlight>();glow.width=.025f;glow.idleStrength=.25f;
        glow.EnsureBuilt();glow.SetNearby(false);
        // OnEnable creates the shells before fields are assigned; layer is inherited.
    }
    public static Bounds BoundsOf(GameObject go) {
        var rs=go.GetComponentsInChildren<Renderer>();var b=new Bounds(go.transform.position,Vector3.zero);bool first=true;
        foreach(var r in rs) {if(first) {b=r.bounds;first=false;}else b.Encapsulate(r.bounds);}return b;
    }
    static void SetLayer(GameObject go,int layer) {foreach(var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=layer;}
    void BuildPanel()
    {
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","PingFang SC","Arial Unicode MS","Arial"},28);
        uiMaterial=new Material(Shader.Find("LocalPrototype/ReadableVRUI"));composite=new Material(Shader.Find("LocalPrototype/ObjectPreviewComposite"));
        panel=new GameObject("PermitInspectionPanel",typeof(RectTransform),typeof(Canvas),typeof(Image));panel.transform.SetParent(transform,false);
        var canvas=panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=viewer;canvas.sortingOrder=200;
        panel.GetComponent<RectTransform>().sizeDelta=new Vector2(6000,5000);panel.transform.localScale=Vector3.one*.001f;
        panel.transform.SetPositionAndRotation(viewer.transform.TransformPoint(new Vector3(0,0,1.2f)),viewer.transform.rotation);
        panel.GetComponent<Image>().material=uiMaterial;panel.GetComponent<Image>().color=new Color(0,0,0,.28f);
        var image=Element("ObjectPreviewRawImage",new Vector2(0,35),new Vector2(620,620));
        var raw=image.AddComponent<RawImage>();raw.texture=texture;raw.material=composite;raw.raycastTarget=false;
        previewHit=image.AddComponent<BoxCollider>();previewHit.size=new Vector3(620,620,8);
        title=Label(new Vector2(0,-285),new Vector2(850,40),26);
        instruction=Label(new Vector2(0,-325),new Vector2(1000,40),21);
        toggleHit=Button("ToggleFold",new Vector2(-140,-380),out toggleImage);toggleLabel=Label(new Vector2(-140,-380),new Vector2(230,48),23);
        closeHit=Button("Close",new Vector2(140,-380),out closeImage);closeLabel=Label(new Vector2(140,-380),new Vector2(230,48),23);
        toggleImage.color=new Color(.15f,.30f,.32f);closeImage.color=new Color(.35f,.18f,.12f);
        RefreshText();
    }
    GameObject Element(string name,Vector2 pos,Vector2 size) {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(panel.transform,false);
        var rect=go.GetComponent<RectTransform>();rect.anchoredPosition=pos;rect.sizeDelta=size;return go;
    }
    BoxCollider Button(string name,Vector2 pos,out Image image) {
        var go=Element(name,pos,new Vector2(230,48));image=go.AddComponent<Image>();image.material=uiMaterial;
        var col=go.AddComponent<BoxCollider>();col.size=new Vector3(230,48,10);return col;
    }
    Text Label(Vector2 pos,Vector2 size,int fontSize) {
        var t=Element("PermitLabel",pos,size).AddComponent<Text>();t.font=font;t.fontSize=fontSize;t.material=uiMaterial;
        t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;
    }
    void RefreshText() {
        if(!title) return;bool en=SubtitleLanguage.English;
        title.text=en?"Travel permit · Narrative reconstruction":"入台证 · 叙事重构道具";
        instruction.text=en?"Aim at the document; hold the right trigger and rotate your controller":"指向证件，按住右手扳机并转动手柄查看";
        toggleLabel.text=IsUnfolded?(en?"Fold":"合拢"):(en?"Unfold":"展开");closeLabel.text=en?"Return to table":"放回桌面";
    }
    public void ToggleFold() {if(!IsOpen)return;IsUnfolded=!IsUnfolded;rotating=false;BuildClone();RefreshText();}
    public void SetPreviewRotation(Quaternion rotation) {if(pivot)pivot.transform.localRotation=rotation;}
    public void RenderPreview() {if(previewCamera){Canvas.ForceUpdateCanvases();previewCamera.Render();}}
    public void Close() {
        if(!IsOpen) return;IsOpen=false;IsUnfolded=false;rotating=mouseDragging=false;
        if(tableModel)tableModel.gameObject.SetActive(true);if(tableGlow)tableGlow.SetNearby(false);
        if(previewCamera)previewCamera.targetTexture=null;
        if(texture){texture.Release();Dispose(texture);}if(panel){panel.SetActive(false);Dispose(panel);}if(container){container.SetActive(false);Dispose(container);}
        if(maskedCameras!=null)for(int i=0;i<maskedCameras.Length;i++)if(maskedCameras[i])maskedCameras[i].cullingMask=oldMasks[i];
        if(maskedLights!=null)for(int i=0;i<maskedLights.Length;i++)if(maskedLights[i])maskedLights[i].cullingMask=oldLightMasks[i];
        if(movement && restoreMove)movement.enabled=true;if(turning && restoreTurn)turning.enabled=true;
        if(font)Dispose(font);if(uiMaterial)Dispose(uiMaterial);if(composite)Dispose(composite);
    }
    void OnDisable() {SubtitleLanguage.Changed-=RefreshText;Close();if(pointer)pointer.enabled=false;}
    void OnDestroy() {if(pointerMaterial)Dispose(pointerMaterial);}
    static void Dispose(Object obj) {if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
}
