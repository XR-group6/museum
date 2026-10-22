using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Timeline;
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
    GameObject explorationChoice;
    BoxCollider finishHit, continueHit;
    Image finishImage, continueImage;
    LineRenderer confirmationPointer;
    Material confirmationPointerMaterial;
    Font confirmationFont;
    PermitInspection pausedInspection;
    public bool ShowingExplorationChoice => explorationChoice && explorationChoice.activeSelf;

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
    bool[] videoStarted, videoFinished;
    Collider[] videoHits;
    double[] videoReadyAt, videoPlaybackSeconds, videoEntranceAt;
    bool[] videoPreviewRequested, videoPreviewReady;
    readonly List<Material> videoScreenMaterials = new List<Material>();
    VRSubtitle[] videoHints;
    bool awaitingSail, sailConfirmed;


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
        videoStarted = new bool[timelineVideos.Length]; videoFinished = new bool[timelineVideos.Length];
        videoReadyAt = new double[timelineVideos.Length];
        videoPlaybackSeconds = new double[timelineVideos.Length];
        videoEntranceAt = Enumerable.Repeat(double.PositiveInfinity, timelineVideos.Length).ToArray();
        videoPreviewRequested = new bool[timelineVideos.Length]; videoPreviewReady = new bool[timelineVideos.Length];
        videoHits = new Collider[timelineVideos.Length]; videoHints = new VRSubtitle[timelineVideos.Length];
        foreach (var video in timelineVideos)
        {
            video.Stop(); video.playOnAwake = false; video.isLooping = false;
            video.loopPointReached += VideoCompleted;
            video.prepareCompleted += VideoPrepared;
            video.frameReady += VideoPreviewFrame;
            video.sendFrameReadyEvents = true;
            var screen = video.targetMaterialRenderer ? video.targetMaterialRenderer : video.GetComponent<Renderer>();
            if (!screen) { Fail("Video screen renderer missing: " + video.name); return; }
            int index = System.Array.IndexOf(timelineVideos, video);
            var previewMaterial = new Material(screen.sharedMaterial);
            previewMaterial.SetTexture(video.targetMaterialProperty, Texture2D.blackTexture);
            screen.material = previewMaterial; videoScreenMaterials.Add(previewMaterial);
            var target = screen.gameObject.AddComponent<BoxCollider>();
            var mesh = screen.GetComponent<MeshFilter>();
            if (mesh && mesh.sharedMesh) { target.center = mesh.sharedMesh.bounds.center; target.size = mesh.sharedMesh.bounds.size + Vector3.one * .01f; }
            target.isTrigger = true; videoHits[index] = target;
            // Let the original screen entrance animation reach its final key
            // before pausing the Timeline to await a click.
            var timeline = oceanTimeline.playableAsset as TimelineAsset;
            if (timeline)
                foreach (var track in timeline.GetOutputTracks())
                {
                    var activation = track as ActivationTrack;
                    var binding = oceanTimeline.GetGenericBinding(track) as GameObject;
                    if (activation && binding && (video.transform == binding.transform || video.transform.IsChildOf(binding.transform)))
                        foreach (var clip in activation.GetClips())
                        {
                            videoPlaybackSeconds[index] = System.Math.Max(videoPlaybackSeconds[index], clip.duration);
                            videoEntranceAt[index] = System.Math.Min(videoEntranceAt[index], clip.start);
                        }
                    var animation = track as AnimationTrack;
                    if (!animation || !animation.infiniteClip) continue;
                    var animator = oceanTimeline.GetGenericBinding(track) as Animator;
                    if (animator && (screen.transform == animator.transform || screen.transform.IsChildOf(animator.transform)))
                        videoReadyAt[index] = System.Math.Max(videoReadyAt[index], animation.infiniteClip.length);
                }
            var hint = CreateSubtitle(false); videoHints[index] = hint;
            hint.transform.position = screen.bounds.center + Vector3.up * (screen.bounds.extents.y + .25f);
            hint.subtitleWidth = 1400; hint.fontSize = 34;
            hint.ShowText("");
            // Activation tracks will reveal the screens; the playback loop starts
            // each player on its activation edge, including on repeated visits.
            video.gameObject.SetActive(false);
            video.enabled = true;
        }
        foreach (var text in SceneObjects<Text>())
            if (!text.GetComponentInParent<VRSubtitle>()) text.gameObject.SetActive(false);
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
        if (ShowingExplorationChoice) { UpdateExplorationChoice(); return; }
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
        if (sceneName == "scene_07") ShowExplorationChoice();
        else StartCoroutine(RevealOcean());
        return true;
    }

    void ShowExplorationChoice()
    {
        if (ShowingExplorationChoice || loading) return;
        pausedInspection = SceneObjects<PermitInspection>().FirstOrDefault(p => p.enabled);
        if (pausedInspection) pausedInspection.enabled = false;
        if (!explorationChoice)
        {
            explorationChoice = new GameObject("Exploration completion confirmation", typeof(RectTransform), typeof(Canvas));
            explorationChoice.transform.SetParent(transform, false);
            explorationChoice.transform.localScale = Vector3.one * .0015f;
            explorationChoice.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 280);
            var canvas = explorationChoice.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = viewer; canvas.sortingOrder = 200;
            confirmationFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 42);
            ConfirmationLabel(explorationChoice.transform, "是否已经探索完毕", "Have you finished exploring?", new Vector2(0, 70), new Vector2(900, 100));
            finishHit = ConfirmationButton("是", "Yes", -160, out finishImage);
            continueHit = ConfirmationButton("否", "No", 160, out continueImage);
            var pointer = new GameObject("Exploration confirmation right pointer"); pointer.transform.SetParent(transform, false);
            confirmationPointer = pointer.AddComponent<LineRenderer>(); confirmationPointer.positionCount = 2;
            confirmationPointer.startWidth = .004f; confirmationPointer.endWidth = .002f;
            confirmationPointerMaterial = new Material(Shader.Find("Sprites/Default"));
            confirmationPointer.sharedMaterial = confirmationPointerMaterial;
        }
        // Reuse the censer instruction anchor without attaching UI to the HMD.
        var anchor = subtitle ? subtitle.transform.position : censer.position + Vector3.up * .4f;
        explorationChoice.transform.position = anchor;
        Vector3 facing = anchor - viewer.transform.position;
        explorationChoice.transform.rotation = facing.sqrMagnitude > .001f
            ? Quaternion.LookRotation(facing, Vector3.up) : viewer.transform.rotation;
        explorationChoice.SetActive(true);
    }

    void ConfirmationLabel(Transform parent, string chinese, string english, Vector2 position, Vector2 size)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
        var label = go.GetComponent<Text>(); label.font = confirmationFont; label.fontSize = 42;
        label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
        go.AddComponent<LocalizedUIText>().BindBilingual(chinese, english);
    }

    BoxCollider ConfirmationButton(string chinese, string english, float x, out Image image)
    {
        var go = new GameObject(english, typeof(RectTransform), typeof(Image), typeof(BoxCollider));
        go.transform.SetParent(explorationChoice.transform, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = new Vector2(x, -45); rect.sizeDelta = new Vector2(240, 90);
        image = go.GetComponent<Image>(); image.color = new Color(.12f, .36f, .3f);
        var hit = go.GetComponent<BoxCollider>(); hit.size = new Vector3(240, 90, 16);
        ConfirmationLabel(go.transform, chinese, english, Vector2.zero, rect.sizeDelta);
        return hit;
    }

    void UpdateExplorationChoice()
    {
        finishImage.color = new Color(.12f, .36f, .3f);
        if (continueImage) continueImage.color = finishImage.color;
        confirmationPointer.enabled = false;
        if (rightHand && rightHand.isActiveAndEnabled && rightHand.isValid && trigger != null)
        {
            var ray = new Ray(rightHand.transform.position, rightHand.transform.forward);
            ProcessExplorationChoice(ray, trigger.GetStateDown(SteamVR_Input_Sources.RightHand), true);
        }
        if (Application.isEditor && Input.GetMouseButtonDown(0))
            ProcessExplorationChoice(viewer.ScreenPointToRay(Input.mousePosition), true, false);
    }

    void ProcessExplorationChoice(Ray ray, bool pressed, bool draw)
    {
        RaycastHit hit; Vector3 end = ray.GetPoint(5); bool hovered = false, finish = false;
        if (finishHit.Raycast(ray, out hit, 5)) { hovered = finish = true; end = hit.point; finishImage.color = new Color(.2f, .65f, .55f); }
        else if (continueHit && continueHit.Raycast(ray, out hit, 5)) { hovered = true; end = hit.point; continueImage.color = new Color(.2f, .65f, .55f); }
        if (draw)
        {
            confirmationPointer.enabled = true; confirmationPointer.SetPosition(0, ray.origin); confirmationPointer.SetPosition(1, end);
            confirmationPointer.startColor = confirmationPointer.endColor = hovered ? Color.green : Color.white;
        }
        if (hovered && pressed) ConfirmExplorationFinished(finish);
    }

    public void ConfirmExplorationFinished(bool finished)
    {
        if (!ShowingExplorationChoice) return;
        explorationChoice.SetActive(false);
        confirmationPointer.enabled = false;
        if (awaitingSail) { sailConfirmed = true; awaitingSail = false; return; }
        if (pausedInspection) pausedInspection.enabled = true;
        pausedInspection = null;
        if (finished) Load("scene_06_01");
        else
        {
            if (subtitle) subtitle.ShowText(ExplorePrompt);
            WaitingForCenser = true;
        }
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
            yield return InteractWithVideos();
            if (timelineVideos.Length > 0 && videoFinished.All(finished => finished)) break;
            OceanRevealed = !blackEnvironment || !blackEnvironment.activeInHierarchy;
            yield return null;
        }
        // Hold and evaluate the actual final frame before beginning the whiteout.
        oceanTimeline.Pause();
        if (!videoFinished.All(finished => finished))
        {
            oceanTimeline.time = duration;
            oceanTimeline.Evaluate();
        }
        // Every clip must actually finish, even if its authored activation window was missed.
        for (int i = 0; i < timelineVideos.Length; i++)
            if (timelineVideos[i] && !videoFinished[i]) timelineVideos[i].gameObject.SetActive(true);
        yield return InteractWithVideos();
        foreach (var video in timelineVideos) if (video) video.Pause();
        ShowSailChoice();
        while (!sailConfirmed) yield return null;
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

    void VideoPrepared(UnityEngine.Video.VideoPlayer video)
    {
        int index = System.Array.IndexOf(timelineVideos, video);
        if (index >= 0 && !videoStarted[index]) { video.time = 0; video.Play(); }
    }

    void VideoPreviewFrame(UnityEngine.Video.VideoPlayer video, long frame)
    {
        int index = System.Array.IndexOf(timelineVideos, video);
        if (index < 0 || videoStarted[index] || videoPreviewReady[index]) return;
        video.Pause(); videoPreviewReady[index] = true;
    }

    void VideoCompleted(UnityEngine.Video.VideoPlayer video)
    {
        int index = System.Array.IndexOf(timelineVideos, video);
        if (index >= 0) videoFinished[index] = true;
    }

    IEnumerator InteractWithVideos()
    {
        bool pending = false;
        for (int i = 0; i < timelineVideos.Length; i++)
            if (timelineVideos[i] && timelineVideos[i].isActiveAndEnabled && !videoFinished[i] && oceanTimeline.time + .0001 >= videoReadyAt[i]) pending = true;
        if (!pending) yield break;
        bool resume = oceanTimeline.state == PlayState.Playing;
        oceanTimeline.Pause();
        EnsureVideoPointer();
        while (pending)
        {
            pending = false;
            bool tracked = rightHand && rightHand.isActiveAndEnabled && rightHand.isValid && trigger != null;
            var ray = tracked ? new Ray(rightHand.transform.position, rightHand.transform.forward) : new Ray();
            int selected = -1; float nearest = 5f; Vector3 end = ray.GetPoint(5);
            for (int i = 0; i < timelineVideos.Length; i++)
            {
                var video = timelineVideos[i];
                if (!video || !video.isActiveAndEnabled || videoFinished[i] || oceanTimeline.time + .0001 < videoReadyAt[i]) continue;
                if (videoStarted[i] && video.time >= videoPlaybackSeconds[i] && videoPlaybackSeconds[i] > 0)
                { video.Pause(); videoFinished[i] = true; videoHints[i].ShowText(""); continue; }
                pending = true;
                var screen = video.targetMaterialRenderer ? video.targetMaterialRenderer : video.GetComponent<Renderer>();
                OrientVideo(i);
                Physics.SyncTransforms();
                if (!videoStarted[i])
                {
                    videoHints[i].ShowBilingualText("用右摇杆激光点击视频开始播放", "Point the right controller laser at the video and press the trigger to play.");
                    RaycastHit hit;
                    if (tracked && videoHits[i].Raycast(ray, out hit, nearest)) { selected = i; nearest = hit.distance; end = hit.point; }
                    if (Application.isEditor && Input.GetMouseButtonDown(0) && videoHits[i].Raycast(viewer.ScreenPointToRay(Input.mousePosition), out hit, 20)) StartVideo(i);
                }
                else videoHints[i].ShowText("");
            }
            confirmationPointer.enabled = tracked && pending;
            if (tracked)
            {
                confirmationPointer.SetPosition(0, ray.origin); confirmationPointer.SetPosition(1, end);
                confirmationPointer.startColor = confirmationPointer.endColor = selected >= 0 ? Color.green : Color.white;
                if (selected >= 0 && trigger.GetStateDown(SteamVR_Input_Sources.RightHand)) StartVideo(selected);
            }
            yield return null;
        }
        confirmationPointer.enabled = false;
        foreach (var hint in videoHints) if (hint) hint.ShowText("");
        if (resume)
        {
            // Remove only the idle gap before the next screen; play its original
            // entrance animation at the authored speed after a brief .2s beat.
            double nextEntrance = double.PositiveInfinity;
            for (int i = 0; i < timelineVideos.Length; i++)
                if (!videoFinished[i] && videoEntranceAt[i] > oceanTimeline.time)
                    nextEntrance = System.Math.Min(nextEntrance, videoEntranceAt[i]);
            if (!double.IsInfinity(nextEntrance) && nextEntrance - oceanTimeline.time > .2)
            {
                oceanTimeline.time = nextEntrance - .2;
                oceanTimeline.Evaluate();
            }
            oceanTimeline.Play();
        }
    }

    void LateUpdate()
    {
        if (sceneName != "scene_06_01" || !viewer || videoReadyAt == null) return;
        for (int i = 0; i < timelineVideos.Length; i++)
        {
            var video = timelineVideos[i];
            if (video && video.isActiveAndEnabled && !videoPreviewRequested[i])
            {
                videoPreviewRequested[i] = true;
                for (ushort track = 0; track < video.controlledAudioTrackCount; track++) video.SetDirectAudioMute(track, true);
                video.Prepare();
            }
            OrientVideo(i);
        }
    }

    void OrientVideo(int index)
    {
        var video = timelineVideos[index];
        if (!video || !video.isActiveAndEnabled || oceanTimeline.time + .0001 < videoReadyAt[index]) return;
        var screen = video.targetMaterialRenderer ? video.targetMaterialRenderer : video.GetComponent<Renderer>();
        if (!screen) return;
        Vector3 away = screen.bounds.center - viewer.transform.position;
        if (away.sqrMagnitude > .001f)
        {
            // Unity's Quad front points along local -Z, like the subtitle UI.
            screen.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
        if (videoHints[index])
        {
            videoHints[index].transform.position = screen.bounds.center + Vector3.up * (screen.bounds.extents.y + .25f);
            videoHints[index].vrCamera = viewer;
        }
    }

    void StartVideo(int index)
    {
        if (videoStarted[index] || !videoPreviewReady[index]) return;
        videoStarted[index] = true;
        var video = timelineVideos[index];
        video.sendFrameReadyEvents = false;
        for (ushort track = 0; track < video.controlledAudioTrackCount; track++) video.SetDirectAudioMute(track, false);
        video.time = 0; video.Play();
        videoHints[index].ShowText("");
    }

    void EnsureVideoPointer()
    {
        if (confirmationPointer) return;
        var go = new GameObject("Boat right controller pointer"); go.transform.SetParent(transform, false);
        confirmationPointer = go.AddComponent<LineRenderer>(); confirmationPointer.positionCount = 2;
        confirmationPointer.startWidth = .004f; confirmationPointer.endWidth = .002f;
        confirmationPointerMaterial = new Material(Shader.Find("Sprites/Default"));
        confirmationPointer.sharedMaterial = confirmationPointerMaterial;
    }

    void ShowSailChoice()
    {
        awaitingSail = true;
        explorationChoice = new GameObject("Sail to museum", typeof(RectTransform), typeof(Canvas));
        explorationChoice.transform.SetParent(transform, false);
        var boat = SceneObjects<Transform>().FirstOrDefault(t => t.name == "fishing_boat");
        if (!boat) { Fail("Fishing boat missing for return button"); Destroy(explorationChoice); return; }
        var renderers = boat.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(boat.position, Vector3.zero);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        }
        // Keep the return button in the clear space between the viewer and
        // the near side of the boat, biased toward the boat for visibility.
        Vector3 nearBoat = bounds.ClosestPoint(viewer.transform.position);
        Vector3 anchor = Vector3.Lerp(viewer.transform.position, nearBoat, .95f);
        anchor.y = viewer.transform.position.y - .2f;
        explorationChoice.transform.position = anchor;
        Vector3 facing = anchor - viewer.transform.position;
        explorationChoice.transform.rotation = facing.sqrMagnitude > .001f
            ? Quaternion.LookRotation(facing, Vector3.up) : viewer.transform.rotation;
        explorationChoice.transform.localScale = Vector3.one * .0015f;
        var canvas = explorationChoice.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = viewer; canvas.sortingOrder = 200;
        confirmationFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 42);
        finishHit = ConfirmationButton("驶向博物馆", "Sail to the museum", 0, out finishImage);
        finishHit.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        finishHit.GetComponent<RectTransform>().sizeDelta = new Vector2(600, 110);
        finishHit.size = new Vector3(600, 110, 16);
        var label = finishHit.GetComponentInChildren<Text>(); label.rectTransform.sizeDelta = new Vector2(600, 110);
        EnsureVideoPointer();
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
        foreach (var video in timelineVideos) if (video)
        {
            video.loopPointReached -= VideoCompleted;
            video.prepareCompleted -= VideoPrepared;
            video.frameReady -= VideoPreviewFrame;
        }
        foreach (var material in videoScreenMaterials) if (material) Destroy(material);
        if (explorationChoice) Destroy(explorationChoice);
        if (confirmationFont) Destroy(confirmationFont);
        if (confirmationPointerMaterial) Destroy(confirmationPointerMaterial);
        if (fadeMaterial) Destroy(fadeMaterial);
        if (roomSubtitleMaterial) Destroy(roomSubtitleMaterial);
        foreach (var material in darknessMaterials) if (material) Destroy(material);
    }
}
