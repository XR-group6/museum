using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Valve.VR;

// Scene five owns its story and hands control to exploration only after the door closes.
[DefaultExecutionOrder(21000)]
public sealed class Scene05HomecomingStory : MonoBehaviour
{
    public PlayableDirector director;
    public Transform father, burner, luggage, luggageHandle, door;
    public Transform rig, desktopRig;
    public Camera headsetCamera, desktopCamera;
    public MuseumLeftStickMove movement;
    public MuseumRightStickTurn turning;
    public DesktopMuseumDriver desktopMovement;
    public PermitInspection permit;
    public Transform[] hands;
    public Transform[] poseBones;
    public Vector3[] restPositions;
    public Quaternion[] restRotations;
    public Vector3 standPosition, luggagePosition;
    public Quaternion standRotation, luggageRotation;
    public Vector3 doorway = new Vector3(0,.15f,-5.4f), outside = new Vector3(0,.15f,-6.6f);
    public Vector3 explorationEye = new Vector3(.25f,1.72f,-1.45f);
    public Vector3 touchPoint;
    public Vector3[] exitWaypoints;
    public AudioClip prayer, expectation, leave, home, finalVoice, chime, doorSound, ticking, footsteps;
    public float prayerAt=2, chimeAt=4.6f, expectationAt=7, leaveAt=10, homeAt=15;
    public float withdrawAt=23, pickupAt=25, walkAt=28, outsideAt=34, closeAt=35, closedAt=37, exploreAt=39;
    public bool ExplorationStarted { get; private set; }
    public bool FinalNarrationPlayed { get; private set; }
    public VRSubtitle dialogue, history, hint;
    public Shader subtitleShader;
    Material subtitleMaterial;
    public Camera Viewer => headsetCamera && headsetCamera.isActiveAndEnabled ? headsetCamera : desktopCamera;
    public Transform ActiveRig => Viewer==headsetCamera?rig:desktopRig;
    AudioSource voice, effects, clock, steps;
    double previous=-1;
    bool started;
    Renderer[] burnerRenderers;
    Vector3 trackingOffset;
    bool[] originalBurnerStates;
    bool finalSubscribed;
    public const string History = "1988年｜返乡探亲\n两岸开放探亲后，许多离乡多年的人终于有机会回去看看。\n离开故乡近四十年，父亲也等到了自己的归期。";
    public const string ExploreHint = "现在，你可以在屋里自由走动，看看桌上留下的证件。";

    public Transform Bone(string name) => father.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
    public void CapturePose()
    {
        poseBones=father.GetComponentsInChildren<Transform>(true);
        restPositions=poseBones.Select(t=>t.localPosition).ToArray();restRotations=poseBones.Select(t=>t.localRotation).ToArray();
        standPosition=father.position;standRotation=father.rotation;
        luggagePosition=luggage.position;luggageRotation=luggage.rotation;
    }
    static float S(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
    public void Sample(double time)
    {
        if(!father || poseBones==null || poseBones.Length==0)return;
        float t=(float)time;
        for(int i=0;i<poseBones.Length;i++){poseBones[i].localPosition=restPositions[i];poseBones[i].localRotation=restRotations[i];}
        father.gameObject.SetActive(t<outsideAt+.4f);
        Vector3[] route=exitWaypoints!=null&&exitWaypoints.Length>0?exitWaypoints:new[]{outside};
        float length=0;Vector3 last=standPosition;
        foreach(var point in route){length+=Vector3.Distance(last,point);last=point;}
        float distance=Mathf.Clamp01((t-walkAt)/(outsideAt-walkAt))*length;
        Vector3 position=standPosition,direction=route[0]-standPosition;last=standPosition;
        foreach(var point in route){float segment=Vector3.Distance(last,point);direction=point-last;if(distance<=segment){position=Vector3.Lerp(last,point,segment>0?distance/segment:1);break;}distance-=segment;position=point;last=point;}
        father.position=t<walkAt?standPosition:position;
        Quaternion walkingRotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(direction,Vector3.up));
        father.rotation=Quaternion.Slerp(standRotation,walkingRotation,S(pickupAt+1,walkAt,t));
        float gait= t>=walkAt&&t<outsideAt?Mathf.Sin((t-walkAt)*Mathf.PI*2*1.25f):0;
        Bone("LeftUpperLeg").localRotation*=Quaternion.Euler(gait*18,0,0);
        Bone("RightUpperLeg").localRotation*=Quaternion.Euler(-gait*18,0,0);
        Bone("LeftLowerLeg").localRotation*=Quaternion.Euler(Mathf.Max(0,-gait)*22,0,0);
        Bone("RightLowerLeg").localRotation*=Quaternion.Euler(Mathf.Max(0,gait)*22,0,0);
        Bone("LeftUpperArm").localRotation*=Quaternion.Euler(-gait*12,0,0);
        float bend=12*(S(pickupAt,pickupAt+.8f,t)-S(pickupAt+1,pickupAt+2,t));
        float touch=S(leaveAt-1,leaveAt+.5f,t)*(1-S(withdrawAt,withdrawAt+1.5f,t));
        Bone("Spine").localRotation*=Quaternion.Euler(bend+32*touch,0,0);
        if(t<walkAt){var head=Bone("Head");head.rotation=Quaternion.LookRotation(Vector3.Lerp(touchPoint,head.position+father.forward*2,S(withdrawAt,pickupAt,t))-head.position,Vector3.up);}
        StraightenWrist("Left");
        StraightenWrist("Right");
        var right=Bone("RightHand");
        if(touch>0){
            Vector3 fingertipTarget=Vector3.Lerp(Bone("RightMiddle3").position,touchPoint,touch);
            // Reach with the arm while keeping the wrist straight, including at contact.
            for(int i=0;i<4;i++){
                Vector3 fingerOffset=Bone("RightMiddle3").position-right.position;
                Arm("Right",fingertipTarget-fingerOffset);
                StraightenWrist("Right");
            }
        }
        luggage.position=luggagePosition;luggage.rotation=luggageRotation;
        float pick=S(pickupAt+.6f,pickupAt+1.7f,t);
        if(pick>0){
            var hand=Bone("LeftHand");
            Vector3 carry=father.TransformPoint(new Vector3(.38f,.72f,.04f));
            Vector3 handle=Vector3.Lerp(luggageHandle.position,carry,pick);
            Arm("Left",handle);
            StraightenWrist("Left");
            luggage.rotation=Quaternion.Slerp(luggageRotation,father.rotation*Quaternion.Euler(-90,0,0),pick);
            luggage.position+=Bone("LeftHand").position-luggageHandle.position;
        }
        door.localRotation=Quaternion.Euler(0,85*(1-S(closeAt,closedAt,t)),0);
    }
    void StraightenWrist(string side)
    {
        // This rig's palm runs along local -Y. A knuckle's position includes
        // lateral offsets, so it is not a reliable axis for straightening the wrist.
        var wrist=Bone(side+"Hand");
        if(wrist.localPosition.sqrMagnitude>.000001f)
            wrist.localRotation=Quaternion.FromToRotation(Vector3.down,wrist.localPosition.normalized);
    }
    void Arm(string side,Vector3 target)
    {
        var upper=Bone(side+"UpperArm");var lower=Bone(side+"LowerArm");var hand=Bone(side+"Hand");
        Vector3 a=upper.position;float ab=Vector3.Distance(a,lower.position),bc=Vector3.Distance(lower.position,hand.position);
        Vector3 axis=(target-a).normalized;float d=Mathf.Clamp(Vector3.Distance(a,target),Mathf.Abs(ab-bc)+.001f,ab+bc-.001f);
        Vector3 pole=father.TransformPoint(new Vector3(side=="Left"?.7f:-.7f,.8f,.05f));
        Vector3 across=Vector3.ProjectOnPlane(pole-a,axis).normalized;float along=(ab*ab-bc*bc+d*d)/(2*d);
        Vector3 elbow=a+axis*along+across*Mathf.Sqrt(Mathf.Max(0,ab*ab-along*along));
        upper.rotation=Quaternion.FromToRotation(lower.position-a,elbow-a)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,a+axis*d-lower.position)*lower.rotation;
        // Wrist orientation is corrected by the caller after solving the arm.
    }
    void Awake()
    {
        foreach(var tour in gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MuseumSceneTour>(true)))tour.enabled=false;
        SetMovement(false);
        if(permit) permit.enabled=false;
    }
    IEnumerator Start()
    {
        bool vr=UnityEngine.XR.XRSettings.isDeviceActive;
        rig.gameObject.SetActive(vr);desktopRig.gameObject.SetActive(!vr);
        headsetCamera.enabled=vr;desktopCamera.enabled=!vr;
        foreach(var listener in new[]{headsetCamera.GetComponent<AudioListener>(),desktopCamera.GetComponent<AudioListener>()})if(listener)listener.enabled=listener.gameObject==Viewer.gameObject;
        Viewer.tag="MainCamera";permit.viewer=Viewer;
        trackingOffset=ActiveRig.InverseTransformPoint(Viewer.transform.position);
        // Align the origin once; head tracking continues throughout the seated view.
        Vector3 toward=Bone("Head").position-PermitInspection.BoundsOf(burner.gameObject).center;
        ActiveRig.Rotate(Vector3.up,Mathf.DeltaAngle(Viewer.transform.eulerAngles.y,Mathf.Atan2(toward.x,toward.z)*Mathf.Rad2Deg),Space.World);
        burnerRenderers=burner.GetComponentsInChildren<Renderer>(true);originalBurnerStates=burnerRenderers.Select(r=>r.forceRenderingOff).ToArray();
        foreach(var r in burnerRenderers)r.forceRenderingOff=true;
        foreach(var h in hands)h.gameObject.SetActive(false);
        if(subtitleShader)subtitleMaterial=new Material(subtitleShader);
        // A narrow column beside the actor leaves the central performance unobstructed.
        // Match scene 04's apparent text size at this panel's longer viewing distance.
        dialogue=Subtitle("Scene05 dialogue",new Vector3(0,.75f,2.4f),56);
        dialogue.subtitleWidth=580;
        history=Subtitle("Scene05 history",new Vector3(1.4f,.85f,2.4f),52);
        history.subtitleWidth=1100;
        history.followCamera=false; history.faceCamera=true;
        var sofa=gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name=="wood_sofa" || t.name=="sofa");
        history.transform.position=sofa ? PermitInspection.BoundsOf(sofa.gameObject).center + Vector3.up*1.4f : new Vector3(1.6f,2.2f,-2.5f);
        hint=Subtitle("Scene05 exploration hint",new Vector3(0,-.28f,1.8f),34);
        history.ShowText(History);
        voice=Source("Scene05 voices",burner.position,false,.85f);effects=Source("Scene05 effects",burner.position,false,.35f);
        clock=Source("Scene05 clock only",new Vector3(-2.3f,2.45f,-2.2f),true,.22f);clock.clip=ticking;
        steps=Source("Scene05 footsteps",standPosition,true,.18f);steps.clip=footsteps;
        yield return null;
        started=true;director.time=0;director.Play();
    }
    VRSubtitle Subtitle(string name,Vector3 offset,int size){var go=new GameObject(name);go.transform.SetParent(transform,false);var s=go.AddComponent<VRSubtitle>();s.vrCamera=Viewer;s.cameraOffset=offset;s.fontSize=size;s.subtitleWidth=1100;s.canvasScale=.002f;if(subtitleMaterial)s.SetMaterial(subtitleMaterial);return s;}
    AudioSource Source(string name,Vector3 at,bool spatial,float volume){var go=new GameObject(name);go.transform.SetParent(transform,true);go.transform.position=at;var a=go.AddComponent<AudioSource>();a.playOnAwake=false;a.spatialBlend=spatial?1:0;a.minDistance=2;a.maxDistance=12;a.volume=volume;return a;}
    public string CaptionAt(float t)=>t<prayerAt?"":t<expectationAt-1?"父亲：请妈祖保佑我们这次航海顺利。":t<expectationAt?"":t<leaveAt-.5f?"香炉：那我……":t<homeAt-.8f?"父亲：这次，我就不带你了。":t<homeAt?"":t<withdrawAt?"父亲：当年我把你带过来，现在这里也是你的家了。":"";
    void Cue(float at,AudioClip clip,AudioSource source){double t=director.time;if(previous<at&&t>=at&&clip)source.PlayOneShot(clip);}
    void Update()
    {
        if(!started||!director)return;
        float t=(float)director.time;
        if(!ExplorationStarted){
            Cue(prayerAt,prayer,voice);Cue(chimeAt,chime,effects);Cue(expectationAt,expectation,voice);Cue(leaveAt,leave,voice);Cue(homeAt,home,voice);Cue(closedAt,doorSound,effects);
            if(previous<walkAt&&t>=walkAt&&footsteps){steps.loop=true;steps.Play();}
            if(t>=outsideAt&&steps.isPlaying)steps.Stop();
            if(previous<closedAt&&t>=closedAt&&ticking){clock.loop=true;clock.Play();}
            dialogue.ShowText(CaptionAt(t));
            if(t>=exploreAt){SceneManager.LoadScene("scene_07");return;}
        }
        if(desktopMovement && ExplorationStarted)desktopMovement.enabled=!permit.IsOpen;
        previous=t;
    }
    void LateUpdate()
    {
        if(!started||ExplorationStarted||!Viewer)return;
        float t=(float)director.time;
        Vector3 eye=PermitInspection.BoundsOf(burner.gameObject).center+Vector3.up*.035f;
        eye += Vector3.down * 1.1f;
        var body=ActiveRig.GetComponent<CharacterController>();bool enabledBody=body&&body.enabled;if(enabledBody)body.enabled=false;
        ActiveRig.position=eye-ActiveRig.TransformVector(trackingOffset);
        if(enabledBody)body.enabled=true;
        if(Viewer==desktopCamera){Vector3 look=Vector3.Lerp(Bone("Head").position,new Vector3(-.72f,1.3f,-3.0f),S(closedAt,exploreAt,t));Viewer.transform.rotation=Quaternion.LookRotation(look-eye,Vector3.up);}
        if(t>=closedAt){float p=S(closedAt+.2f,exploreAt,t);foreach(var r in burnerRenderers)r.forceRenderingOff=p<.7f;}
        // The seated camera moves late; keep captions aligned in this same frame.
        dialogue.UpdatePose();history.UpdatePose();hint.UpdatePose();
    }
    public void BeginExploration() { SceneManager.LoadScene("scene_07"); }
    IEnumerator HideHint(){yield return new WaitForSeconds(14);if(hint)hint.ShowText("");}
    void SetMovement(bool enabled){if(movement)movement.enabled=enabled;if(turning)turning.enabled=enabled;if(desktopMovement)desktopMovement.enabled=enabled;}
    void OnReadAndReturned(){if(!ExplorationStarted||FinalNarrationPlayed)return;FinalNarrationPlayed=true;StartCoroutine(FinalNarration());}
    IEnumerator FinalNarration(){hint.ShowText("");dialogue.ShowText("香炉：看来你终于要回家了。我也知道，你还会回家。");if(finalVoice)voice.PlayOneShot(finalVoice);yield return new WaitForSeconds(finalVoice?finalVoice.length+1:8);dialogue.ShowText("");}
    void RestoreBurner(){if(burnerRenderers!=null)for(int i=0;i<burnerRenderers.Length;i++)if(burnerRenderers[i])burnerRenderers[i].forceRenderingOff=originalBurnerStates[i];}
    void OnDisable(){RestoreBurner();if(finalSubscribed&&permit){permit.ReadAndReturned-=OnReadAndReturned;finalSubscribed=false;}}
    void OnDestroy(){if(subtitleMaterial)Destroy(subtitleMaterial);}
}





