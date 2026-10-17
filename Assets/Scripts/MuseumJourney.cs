using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Valve.VR;

// Owns the new 03 -> 04_01 -> 07 -> 06_01 -> 01 loop.
// References and timings remain editable on the scene's Museum journey object.
[DefaultExecutionOrder(-10000)]
public sealed class MuseumJourney : MonoBehaviour
{
    public const string ExplorePrompt = "请用右手触碰并握住香炉，继续我们的旅程。";
    public const string OceanPrompt = "请向前走到香炉前，用右手触碰握住香炉。";
    public const string RoomTurnPrompt = "可以使用右摇杆跟随灯光转动视角";
    public Transform censer;
    [Tooltip("Clockwise order, starting with the 1951 sector.")]
    public Light[] eraLights = new Light[0];
    public AudioClip regionCue;
    public AudioClip oceanAmbience;
    public Shader coverShader;
    [Min(0)] public float roomSubtitleHeight = 3.2f;
    public Shader roomSubtitleShader;
    public PlayableDirector roomTimeline;
    public PlayableDirector oceanTimeline;
    [Min(0)] public float finalRegionSeconds = 10;
    public Transform guideLine;
    [Min(.1f)] public float whiteFadeSeconds = 1.5f;
    [Min(0)] public float touchDistance = .025f;
    public Vector3 roomCentre = new Vector3(0, .116f, 3.17f);
    public GameObject blackEnvironment;
    public GameObject[] oceanSurfaces = new GameObject[0];
    public bool WaitingForCenser { get; private set; }
    public int ActiveEra { get; private set; } = -1;
    public bool OceanRevealed { get; private set; }
    public bool Returning { get; private set; }

    Camera viewer;
    Transform rig;
    SteamVR_Behaviour_Pose rightHand;
    SteamVR_Action_Boolean trigger;
    Collider[] targets;
    VRSubtitle subtitle;
    AudioSource sound;
    MuseumSceneTour tour;
    MuseumLeftStickMove movement;
    MuseumRightStickTurn turning;
    public double FinalRegionLitAt { get; private set; } = -1;
    Renderer[] guideRenderers = new Renderer[0];
    bool loading;
    string sceneName;
    Material fadeMaterial;
    Material roomSubtitleMaterial;
    readonly List<Material> darknessMaterials = new List<Material>();
    Image fadeImage;
    Renderer[] hiddenEnvironment = new Renderer[0];
    Transform guideParent;
    Vector3 guidePosition, guideScale;
    Quaternion guideRotation;
    UnityEngine.Video.VideoPlayer[] timelineVideos = new UnityEngine.Video.VideoPlayer[0];
    bool[] videoWasActive = new bool[0];

    void Awake()
    {
        sceneName = gameObject.scene.name;
        tour = SceneObjects<MuseumSceneTour>().FirstOrDefault();
        if (sceneName == "scene_07") return;
        foreach (var oldTour in SceneObjects<MuseumSceneTour>()) oldTour.enabled = false;
        if (sceneName == "scene_04_01") return;
        // The original authored Timelines are retained as assets but must not
        // animate the room, camera, light states or censer during this sequence.
        foreach (var director in SceneObjects<PlayableDirector>())
        { director.playOnAwake = false; director.Stop(); director.enabled = false; }
        foreach (var fader in SceneObjects<LightGroupFader>()) fader.enabled = false;
        foreach (var oldTour in SceneObjects<MuseumSceneTour>()) oldTour.enabled = false;
        foreach (var audio in SceneObjects<AudioSource>()) audio.Stop();
    }

    T[] SceneObjects<T>() where T : Component => gameObject.scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    IEnumerator Start()
    {
        // Activate the authored SteamVR rig before looking for its tracked camera.
        var pose = SceneObjects<SteamVR_Behaviour_Pose>().FirstOrDefault();
        if (!pose) { Fail("SteamVR rig missing"); yield break; }
        rig = pose.transform.parent;
        rig.gameObject.SetActive(true);
        viewer = rig.GetComponentInChildren<Camera>(true);
        rightHand = rig.GetComponentsInChildren<SteamVR_Behaviour_Pose>(true)
            .FirstOrDefault(p => p.inputSource == SteamVR_Input_Sources.RightHand);
        if (!viewer || !rightHand) { Fail("Tracked camera or right controller missing"); yield break; }
        viewer.enabled = true;
        viewer.tag = "MainCamera";
        foreach (var camera in SceneObjects<Camera>())
            if (camera != viewer && !camera.transform.IsChildOf(rig)) camera.enabled = false;
        foreach (var listener in SceneObjects<AudioListener>()) listener.enabled = listener.gameObject == viewer.gameObject;
        if (!viewer.GetComponent<AudioListener>()) viewer.gameObject.AddComponent<AudioListener>();
        movement = rig.GetComponent<MuseumLeftStickMove>();
        turning = rig.GetComponent<MuseumRightStickTurn>();
        if (sceneName != "scene_07")
        {
            if (movement) movement.enabled = false;
            if (turning) turning.enabled = false;
        }
        if (!censer) censer = SceneObjects<Transform>().FirstOrDefault(t => t.name == "censer");
        if (!censer) { Fail("Censer missing"); yield break; }
        if (sceneName != "scene_07")
            foreach (var animator in censer.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        sound = new GameObject("Journey directional sound").AddComponent<AudioSource>();
        sound.transform.SetParent(transform, false);
        sound.playOnAwake = false;
        sound.spatialBlend = 1;
        sound.minDistance = 1; sound.maxDistance = 15;
        sound.rolloffMode = AudioRolloffMode.Linear;
        if (sceneName == "scene_04_01")
        {
            yield return WatchEras();
            yield break;
        }
        // Only the censer mesh defines contact; imported parent/table colliders
        // must never expand the touch target to the nearby permit.
        targets = new Collider[0];
        if (censer.GetComponent<MeshFilter>())
        {
            // Fit a solid interaction target to the actual imported censer mesh.
            var mesh = censer.GetComponent<MeshFilter>();
            if (!mesh || !mesh.sharedMesh) { Fail("Censer mesh missing"); yield break; }
            var box = censer.gameObject.AddComponent<BoxCollider>();
            box.center = mesh.sharedMesh.bounds.center; box.size = mesh.sharedMesh.bounds.size;
            box.isTrigger = true;
            targets = new Collider[] { box };
        }
        if (targets.Length == 0) { Fail("Censer needs its own mesh for contact detection"); yield break; }
        trigger = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("GrabPinch");
        if (trigger != null) trigger.actionSet.Activate(SteamVR_Input_Sources.RightHand);
        if (sceneName == "scene_07")
        {
            while (tour && !tour.ExplorationStarted) yield return null;
            if (!tour) { Fail("Exploration controller missing"); yield break; }
            subtitle = CreateSubtitle(false);
            var renderer = censer.GetComponentInChildren<Renderer>();
            subtitle.transform.position = renderer ? renderer.bounds.center + Vector3.up * .4f : censer.position + Vector3.up * .4f;
            subtitle.ShowText(ExplorePrompt);
        }
        else
        {
            PrepareOcean();
            subtitle = CreateSubtitle(true);
            subtitle.ShowText(OceanPrompt);
        }
        // A press held during a scene transition never activates the next censer.
        yield return null;
        WaitingForCenser = true;
    }

    VRSubtitle CreateSubtitle(bool follow)
    {
        var result = new GameObject("Journey bilingual subtitle").AddComponent<VRSubtitle>();
        result.transform.SetParent(transform, false);
        result.vrCamera = viewer; result.followCamera = follow;
        result.cameraOffset = new Vector3(0, -.28f, 1.6f);
        result.fontSize = 38;
        return result;
    }

    IEnumerator WatchEras()
    {
        if (!roomTimeline) { Fail("Authored room Timeline missing"); yield break; }
        if (eraLights.Length != 4 || eraLights.Any(l => !l)) { Fail("Four era lights required"); yield break; }
        if (roomSubtitleShader) roomSubtitleMaterial = new Material(roomSubtitleShader);
        // Keep the original anchors/orientations with the clearer subtitle styling.
        foreach (var text in SceneObjects<Text>())
        {
            string content = text.text.Replace("\r\n", "\n");
            int chineseStart = content.IndexOf("场景", System.StringComparison.Ordinal);
            if (chineseStart < 0) continue;
            var panel = CreateSubtitle(false);
            panel.name = "Room restored subtitle";
            panel.transform.SetPositionAndRotation(text.transform.position, text.transform.rotation);
            panel.faceCamera = false;
            panel.fontSize = 34; panel.subtitleWidth = 1100; panel.canvasScale = .002f;
            if (roomSubtitleMaterial) panel.SetMaterial(roomSubtitleMaterial);
            panel.ShowBilingualText(content.Substring(chineseStart).Trim(), content.Substring(0, chineseStart).Trim());
            text.gameObject.SetActive(false);
        }
        if (!turning)
        {
            rig.gameObject.SetActive(false);
            turning = rig.gameObject.AddComponent<MuseumRightStickTurn>();
            turning.headset = viewer.transform; turning.rightHand = rightHand;
            rig.gameObject.SetActive(true);
        }
        turning.snapTurn = true; turning.snapDegrees = 90; turning.enabled = true;
        var turnHint = CreateSubtitle(true);
        turnHint.name = "Room headset turn guide";
        turnHint.cameraOffset = new Vector3(0, .35f, 1.8f);
        if (roomSubtitleMaterial) turnHint.SetMaterial(roomSubtitleMaterial);
        turnHint.ShowText(RoomTurnPrompt);
        roomTimeline.enabled = true;
        roomTimeline.time = 0;
        roomTimeline.extrapolationMode = DirectorWrapMode.Hold;
        roomTimeline.Evaluate();
        roomTimeline.Play();
        while (FinalRegionLitAt < 0 || Time.realtimeSinceStartupAsDouble - FinalRegionLitAt < finalRegionSeconds)
        {
            ActiveEra = -1;
            for (int i = 0; i < eraLights.Length; i++)
                if (eraLights[i].enabled && eraLights[i].intensity > .01f) ActiveEra = i;
            if (FinalRegionLitAt < 0 && eraLights[3].enabled && eraLights[3].intensity > .01f)
                FinalRegionLitAt = Time.realtimeSinceStartupAsDouble;
            yield return null;
        }
        Load("scene_07");
    }

    void FaceDirection(Vector3 direction)
    {
        // Rotate the rig around the tracked head: never write to the HMD pose,
        // never translate the seated viewer, and only snap the horizontal heading.
        float delta = Mathf.DeltaAngle(viewer.transform.eulerAngles.y, Quaternion.LookRotation(direction, Vector3.up).eulerAngles.y);
        var body = rig.GetComponent<CharacterController>();
        bool wasEnabled = body && body.enabled;
        if (wasEnabled) body.enabled = false;
        rig.RotateAround(viewer.transform.position, Vector3.up, delta);
        if (wasEnabled) body.enabled = true;
    }

    void MoveRig(Vector3 position)
    {
        var body = rig.GetComponent<CharacterController>();
        bool wasEnabled = body && body.enabled;
        if (wasEnabled) body.enabled = false;
        rig.position = position;
        if (wasEnabled) body.enabled = true;
    }

    void PrepareOcean()
    {
        if (!oceanTimeline) oceanTimeline = SceneObjects<PlayableDirector>().FirstOrDefault(d => d.playableAsset);
        if (!oceanTimeline || !oceanTimeline.playableAsset)
        { Fail("Original ocean Timeline missing"); return; }
        hiddenEnvironment = SceneObjects<Renderer>().Where(r => r.enabled &&
            !r.transform.IsChildOf(censer) && !r.transform.IsChildOf(rig) &&
            (!guideLine || !r.transform.IsChildOf(guideLine)) &&
            (!blackEnvironment || !r.transform.IsChildOf(blackEnvironment.transform))).ToArray();
        foreach (var renderer in hiddenEnvironment) renderer.enabled = false;
        viewer.clearFlags = CameraClearFlags.SolidColor;
        viewer.backgroundColor = Color.black;
        if (blackEnvironment) blackEnvironment.SetActive(true);
        if (!guideLine) guideLine = SceneObjects<Transform>().FirstOrDefault(t => t.name == "line");
        if (guideLine)
        {
            guideParent = guideLine.parent;
            guidePosition = guideLine.localPosition; guideRotation = guideLine.localRotation; guideScale = guideLine.localScale;
            // Keep the authored line visible until interaction, then restore its Timeline hierarchy.
            guideLine.SetParent(transform, true); guideLine.gameObject.SetActive(true);
            foreach (var animator in guideLine.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            guideRenderers = guideLine.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in guideRenderers)
            {
                renderer.enabled = true;
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetFloat("_MainAlpha", 1);
                renderer.SetPropertyBlock(properties);
            }
        }
        if (blackEnvironment)
        {
            // These are the authored M_BlackSphere / M_BlackPlane materials.
            // TL_scene06_01 animates their _Dissolve_Scale from 0 to 1.
            foreach (var renderer in blackEnvironment.GetComponentsInChildren<Renderer>(true))
            {
                var copies = renderer.sharedMaterials.Select(source =>
                {
                    if (!source) return source;
                    var material = new Material(source);
                    darknessMaterials.Add(material);
                    if (material.HasProperty("_Dissolve_Scale")) material.SetFloat("_Dissolve_Scale", 0);
                    return material;
                }).ToArray();
                renderer.sharedMaterials = copies;
            }
        }
        foreach (var surface in oceanSurfaces) if (surface) surface.SetActive(false);
        timelineVideos = SceneObjects<UnityEngine.Video.VideoPlayer>();
        videoWasActive = new bool[timelineVideos.Length];
        foreach (var video in timelineVideos)
        {
            video.Stop(); video.playOnAwake = false;
            // Activation tracks will reveal the screens; the playback loop starts
            // each player on its activation edge, including on repeated visits.
            video.gameObject.SetActive(false);
            video.enabled = true;
        }
        foreach (var text in SceneObjects<Text>()) text.gameObject.SetActive(false);
        foreach (var item in SceneObjects<Transform>())
            if (item.name == "slide" || item.name == "Smoke_guide" || item.name == "P_VFX") item.gameObject.SetActive(false);
        // The black shell is a visual cover, not a barrier around the viewer.
        if (blackEnvironment)
            foreach (var collider in blackEnvironment.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        MoveRig(new Vector3(censer.position.x, 0, censer.position.z + 2.3f));
        FaceDirection(censer.position - viewer.transform.position);
        var floor = new GameObject("Journey approach floor");
        floor.transform.SetParent(transform, false);
        floor.transform.position = new Vector3(censer.position.x, -.06f, censer.position.z);
        var ground = floor.AddComponent<BoxCollider>(); ground.size = new Vector3(12, .1f, 12);
        if (!movement)
        {
            // Configure while inactive so OnEnable cannot run with missing bindings.
            rig.gameObject.SetActive(false);
            movement = rig.gameObject.AddComponent<MuseumLeftStickMove>();
            movement.headset = viewer.transform;
            movement.leftHand = rig.GetComponentsInChildren<SteamVR_Behaviour_Pose>(true)
                .FirstOrDefault(p => p.inputSource == SteamVR_Input_Sources.LeftHand);
            rig.gameObject.SetActive(true);
        }
        movement.forwardOnly = false; movement.forwardBackwardOnly = true; movement.enabled = true;
        if (turning) turning.enabled = false;
        var body = rig.GetComponent<CharacterController>();
        body.radius = .2f; body.skinWidth = .02f; body.minMoveDistance = 0;
        var glow = new GameObject("Approach censer light").AddComponent<Light>();
        glow.transform.SetParent(transform, false); glow.transform.position = censer.position + Vector3.up * .6f;
        glow.type = LightType.Point; glow.range = 2; glow.intensity = 1;
    }

    void Update()
    {
        if (!WaitingForCenser || !rightHand || !rightHand.isActiveAndEnabled || !rightHand.isValid || trigger == null) return;
        TryGrab(rightHand.transform.position, trigger.GetStateDown(SteamVR_Input_Sources.RightHand));
    }

    public bool TryGrab(Vector3 handPosition, bool pressedThisFrame)
    {
        if (!WaitingForCenser || !pressedThisFrame || targets == null) return false;
        if (sceneName == "scene_07")
        {
            var inspection = SceneObjects<PermitInspection>().FirstOrDefault();
            if (inspection && (inspection.IsOpen || inspection.TriggerConsumedFrame == Time.frameCount ||
                (rightHand && inspection.OwnsTrigger(new Ray(handPosition, rightHand.transform.forward))))) return false;
        }
        bool touching = targets.Any(c => c && c.enabled && c.gameObject.activeInHierarchy &&
            Vector3.Distance(handPosition, c.ClosestPoint(handPosition)) <= touchDistance);
        if (!touching) return false;
        WaitingForCenser = false;
        if (subtitle) subtitle.ShowText("");
        if (sceneName == "scene_07") Load("scene_06_01");
        else StartCoroutine(RevealOcean());
        return true;
    }

    IEnumerator RevealOcean()
    {
        if (movement) movement.enabled = false;
        if (turning) turning.enabled = false;
        if (!oceanTimeline || !oceanTimeline.playableAsset)
        { Fail("Original ocean Timeline missing"); yield break; }
        viewer.clearFlags = CameraClearFlags.Skybox;
        foreach (var renderer in hiddenEnvironment) if (renderer) renderer.enabled = true;
        foreach (var surface in oceanSurfaces) if (surface) surface.SetActive(true);
        foreach (var item in SceneObjects<Transform>())
            if (item.name == "slide" || item.name == "Smoke_guide" || item.name == "P_VFX") item.gameObject.SetActive(true);
        if (guideLine)
        {
            guideLine.SetParent(guideParent, false);
            guideLine.localPosition = guidePosition; guideLine.localRotation = guideRotation; guideLine.localScale = guideScale;
            foreach (var renderer in guideRenderers) renderer.SetPropertyBlock(null);
        }
        // Re-enable only the Animators referenced by this Timeline. Keep the HMD
        // and controller transforms under SteamVR tracking throughout the show.
        foreach (var output in oceanTimeline.playableAsset.outputs)
        {
            var animator = oceanTimeline.GetGenericBinding(output.sourceObject) as Animator;
            if (animator) animator.enabled = true;
        }
        oceanTimeline.enabled = true;
        oceanTimeline.extrapolationMode = DirectorWrapMode.Hold;
        oceanTimeline.time = 0;
        oceanTimeline.Evaluate();
        double duration = oceanTimeline.duration;
        if (duration <= 0 || double.IsNaN(duration) || double.IsInfinity(duration))
        { Fail("Original ocean Timeline has an invalid duration"); yield break; }
        oceanTimeline.Play();
        while (oceanTimeline.time < duration - .001)
        {
            SyncTimelineVideos();
            OceanRevealed = !blackEnvironment || !blackEnvironment.activeInHierarchy;
            yield return null;
        }
        // Hold and evaluate the actual final frame before beginning the whiteout.
        oceanTimeline.Pause();
        oceanTimeline.time = duration;
        oceanTimeline.Evaluate();
        foreach (var video in timelineVideos) if (video) video.Stop();
        yield return null;
        Returning = true;
        CreateWhiteCover(new Color(1, 1, 1, 0), "Ocean light cover");
        float elapsed = 0;
        while (elapsed < whiteFadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeImage.color = new Color(1, 1, 1, Mathf.Clamp01(elapsed / whiteFadeSeconds));
            yield return null;
        }
        // Render the fully white frame before loading the museum again.
        fadeImage.color = Color.white;
        yield return null;
        SubtitleLanguage.BeginNewVisit();
        Load("scene_01");
    }

    void SyncTimelineVideos()
    {
        for (int i = 0; i < timelineVideos.Length; i++)
        {
            var video = timelineVideos[i];
            bool active = video && video.isActiveAndEnabled;
            if (active && !videoWasActive[i]) { video.time = 0; video.Play(); }
            else if (!active && videoWasActive[i] && video) video.Stop();
            videoWasActive[i] = active;
        }
    }

    void CreateWhiteCover(Color initial, string objectName)
    {
        var cover = new GameObject(objectName, typeof(RectTransform), typeof(Canvas));
        cover.transform.SetParent(viewer.transform, false);
        cover.transform.localPosition = Vector3.forward * (viewer.nearClipPlane + .05f);
        cover.transform.localScale = Vector3.one * .01f;
        cover.GetComponent<RectTransform>().sizeDelta = new Vector2(2000, 2000);
        var canvas = cover.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = viewer; canvas.sortingOrder = 1000;
        fadeImage = cover.AddComponent<Image>(); fadeImage.raycastTarget = false;
        fadeMaterial = new Material(coverShader ? coverShader : Shader.Find("TeamPrototype/JourneyCover"));
        fadeImage.material = fadeMaterial; fadeImage.color = initial;
    }

    void Load(string target)
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(target)) { Fail("Scene absent from Build Settings: " + target); return; }
        loading = true;
        SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
    }

    void Fail(string reason) { Debug.LogError("[Museum journey] " + reason, this); enabled = false; }
    void OnDestroy()
    {
        if (fadeMaterial) Destroy(fadeMaterial);
        if (roomSubtitleMaterial) Destroy(roomSubtitleMaterial);
        foreach (var material in darknessMaterials) if (material) Destroy(material);
    }
}
