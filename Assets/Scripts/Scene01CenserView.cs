using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// Move the tracking origin, leaving the headset's tracked local pose untouched.
[DefaultExecutionOrder(20000)]
public sealed class Scene01CenserView : MonoBehaviour
{
    public PlayableDirector director;
    public Transform storyBurner, carouselBurner, daughterHead;
    public Camera headsetCamera, desktopCamera;
    public Transform headsetRig, desktopRig, scene01Title;
    public TextMesh dialogue;
    public Scene02Story scene02;
    public Scene03DinnerStory scene03;
    public Scene04HarborStory scene04;
    public float eyeLift = .035f;
    readonly Dictionary<Transform, Vector3> trackingOffsets = new Dictionary<Transform, Vector3>();
    readonly Dictionary<Renderer, bool> renderStates = new Dictionary<Renderer, bool>();
    bool oriented;
    public float scene02TurnDegrees = 90;
    float appliedScene02Turn;
    public float scene03TurnDegrees = -146;
    float appliedScene03Turn;
    public float scene04TurnDegrees = -110;
    float appliedScene04Turn;
    Vector3 titlePosition;
    Quaternion titleRotation;

    public Vector3 EyeAt(double time)
    {
        Vector3 eye = Centre(storyBurner) + Vector3.up * eyeLift;
        if (time > 26 && carouselBurner)
            eye = Vector3.Lerp(eye, Centre(carouselBurner) + Vector3.up * eyeLift,
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(26, 28, (float)time)));
        if (scene02 && scene02.burner)
        {
            float enter = Mathf.SmoothStep(0, 1, Mathf.InverseLerp((float)scene02.startTime - 2.4f, (float)scene02.startTime, (float)time));
            float leave = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp((float)scene02.EndTime, (float)scene02.EndTime + 2.4f, (float)time));
            eye = Vector3.Lerp(eye, Centre(scene02.burner) + Vector3.up * eyeLift, enter * leave);
        }
        if (scene03 && scene03.burner)
        {
            float enter = Mathf.SmoothStep(0, 1, Mathf.InverseLerp((float)scene03.startTime - 2.4f, (float)scene03.startTime, (float)time));
            float leave = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp((float)scene03.EndTime, (float)scene03.EndTime + 2.4f, (float)time));
            eye = Vector3.Lerp(eye, Centre(scene03.burner) + Vector3.up * (eyeLift + (RestyledRoom ? .3f : 0)), enter * leave);
        }
        if (scene04 && scene04.burner)
        {
            float enter = Mathf.SmoothStep(0, 1, Mathf.InverseLerp((float)scene04.startTime - 2.4f, (float)scene04.startTime, (float)time));
            eye = Vector3.Lerp(eye, Centre(scene04.burner) + Vector3.up * eyeLift, enter);
        }
        return eye;
    }

    static Vector3 Centre(Transform root)
    {
        if (!root) return Vector3.zero;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return root.position;
        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds.center;
    }

    readonly Dictionary<Transform, VRSubtitle> scenePanels = new Dictionary<Transform, VRSubtitle>();
    readonly Dictionary<Transform, Vector3> wallAnchors = new Dictionary<Transform, Vector3>();
    bool RestyledRoom => gameObject.scene.name == "scene_04_01";

    void PresentPanel(Transform source, Camera view, bool history, bool windowWall = false)
    {
        if (!source) return;
        var text = source.GetComponent<TextMesh>();
        var uiText = source.GetComponent<UnityEngine.UI.Text>();
        if (!text && !uiText) return;
        if (!scenePanels.TryGetValue(source, out var panel))
        {
            var go = new GameObject(source.name + " formatted subtitle");
            // Legacy title canvases have very small scales; do not inherit them.
            go.transform.SetParent(transform, false);
            panel = go.AddComponent<VRSubtitle>();
            panel.subtitleFont = text ? text.font : uiText.font;
            panel.fontSize = 38; panel.subtitleWidth = 1100; panel.canvasScale = .002f;
            if (history && source == scene01Title) panel.subtitleWidth = 550;
            panel.followCamera = !history; panel.faceCamera = true;
            panel.cameraOffset = new Vector3(0, -.55f, 1.8f);
            scenePanels[source] = panel;
            // Use authored wall coordinates, never a stair or railing pivot.
            Vector3 anchor = source.position;
            if (history && !windowWall)
            {
                // Fixed world positions copied from the user's temporary placement cubes.
                anchor = source == scene01Title ? new Vector3(.833f, 2.168f, -4.107f) :
                    scene02 && source == scene02.title ? new Vector3(-6.339f, 2.469f, 1.389f) :
                    new Vector3(6.228f, 2.079f, 4.786f);
            }
            else if (history)
            {
                // Keep the existing window-wall anchor for rotation three.
                foreach (var root in gameObject.scene.GetRootGameObjects())
                    foreach (var child in root.GetComponentsInChildren<Transform>(true))
                        if (child.name == "1980s_window_papercut_0")
                            {
                            anchor = child.position + Vector3.up * 1.2f;
                            // Pull the entire billboard into the room, clear of the window/wall.
                            Vector3 inward = scene03 && scene03.burner ? scene03.burner.position - anchor : view.transform.position - anchor;
                            inward.y = 0;
                            anchor += inward.normalized * .9f;
                        }
            }
            wallAnchors[source] = anchor;
        }
        var renderer = source.GetComponent<MeshRenderer>();
        if (renderer) renderer.enabled = false;
        panel.gameObject.SetActive(source.gameObject.activeInHierarchy);
        panel.vrCamera = view;
        if (history) panel.transform.position = wallAnchors[source];
        if (uiText) uiText.enabled = false;
        panel.ShowText(text ? text.text : uiText.text);
        panel.UpdatePose();
    }

    bool headsetPoseReady;
    void OnEnable()
    {
        headsetPoseReady = false;
        trackingOffsets.Clear();
        Valve.VR.SteamVR_Events.NewPosesApplied.Listen(HeadsetPosesApplied);
    }
    void HeadsetPosesApplied()
    {
        if (!headsetCamera || !headsetRig || !headsetCamera.isActiveAndEnabled) return;
        var tracked = headsetCamera.GetComponent<Valve.VR.SteamVR_TrackedObject>();
        var pose = headsetCamera.GetComponent<Valve.VR.SteamVR_Behaviour_Pose>();
        if (tracked && !tracked.isValid || pose && !pose.isValid) return;
        if (!headsetPoseReady)
        {
            trackingOffsets[headsetRig] = headsetRig.InverseTransformPoint(headsetCamera.transform.position);
            headsetPoseReady = true;
        }
        // SteamVR can apply poses after LateUpdate; finish positioning in the same frame.
        ApplyView();
    }

    void Start() { BeginView(); }
    public void BeginView()
    {
        if (scene01Title) { titlePosition = scene01Title.position; titleRotation = scene01Title.rotation; }
        // The original central censer is not part of the 1951 room being held.
        foreach (var root in new[] { storyBurner, carouselBurner, scene02 ? scene02.burner : null, scene03 ? scene03.burner : null, scene04 ? scene04.burner : null })
            if (root) foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            { renderStates[r] = r.forceRenderingOff; r.forceRenderingOff = true; }
    }

    void LateUpdate() { ApplyView(); }
    public void ApplyView()
    {
        if (!director || !storyBurner) return;
        double time = director.time;
        var motion = GetComponent<Scene01Motion>();
        if (motion) motion.StabilizeStandingPose(time);
        Camera view = headsetCamera && headsetCamera.isActiveAndEnabled ? headsetCamera : desktopCamera;
        Transform rig = view == headsetCamera ? headsetRig : desktopRig;
        if (!view || !rig || !view.isActiveAndEnabled) return;
        if (Application.isPlaying && view == headsetCamera && !headsetPoseReady) return;
        if (!trackingOffsets.ContainsKey(rig)) trackingOffsets[rig] = rig.InverseTransformPoint(view.transform.position);
        if (!oriented)
        {
            Vector3 direction = (daughterHead ? daughterHead.position : storyBurner.position + Vector3.back) - EyeAt(time);
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            rig.Rotate(Vector3.up, Mathf.DeltaAngle(view.transform.eulerAngles.y, yaw), Space.World);
            // Desktop view has no tracked headset to supply pitch.
            if (view == desktopCamera) view.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            oriented = true;
        }
        if (scene02)
        {
            float enter = Mathf.SmoothStep(0, 1, Mathf.InverseLerp((float)scene02.startTime - 2.4f, (float)scene02.startTime, (float)time));
            float leave = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp((float)scene02.EndTime, (float)scene02.EndTime + 2.4f, (float)time));
            float turn = scene02TurnDegrees * enter * leave;
            rig.RotateAround(view.transform.position, Vector3.up, turn - appliedScene02Turn);
            appliedScene02Turn = turn;
        }
        if (scene03)
        {
            float enter = Mathf.SmoothStep(0, 1, Mathf.InverseLerp((float)scene03.startTime - 2.4f, (float)scene03.startTime, (float)time));
            float leave = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp((float)scene03.EndTime, (float)scene03.EndTime + 2.4f, (float)time));
            float turn = scene03TurnDegrees * enter * leave;
            rig.RotateAround(view.transform.position, Vector3.up, turn - appliedScene03Turn);
            appliedScene03Turn = turn;
        }
        if (scene04)
        {
            float enter = Mathf.SmoothStep(0, 1, Mathf.InverseLerp((float)scene04.startTime - 2.4f, (float)scene04.startTime, (float)time));
            float turn = scene04TurnDegrees * enter;
            rig.RotateAround(view.transform.position, Vector3.up, turn - appliedScene04Turn);
            appliedScene04Turn = turn;
        }
        var body = rig.GetComponent<CharacterController>();
        bool bodyEnabled = body && body.enabled;
        if (bodyEnabled) body.enabled = false;
        Vector3 trackingOffset = rig.TransformVector(trackingOffsets[rig]);
        if (RestyledRoom)
        {
            // All entrances use the same authored eye height. SteamVR may update
            // the tracked local height after scene loading or after LateUpdate.
            // Resolve vertical compensation from the current pose, not a cached
            // startup height; retain horizontal room-scale head movement.
            trackingOffset.y = view.transform.position.y - rig.position.y;
        }
        rig.position = EyeAt(time) - trackingOffset;
        if (bodyEnabled) body.enabled = true;
        if (time < 26)
        {
            // Caption and historical title stay readable from the burner, including while carried.
            if (dialogue)
            {
                dialogue.transform.position = view.transform.TransformPoint(new Vector3(0, -.19f, 1.6f));
                dialogue.transform.rotation = view.transform.rotation;
                dialogue.characterSize = .014f;
            }
            if (scene01Title)
            {
                scene01Title.position = view.transform.TransformPoint(new Vector3(0, .50f, 2.4f));
                scene01Title.rotation = view.transform.rotation;
            }
        }
        else if (scene01Title) scene01Title.SetPositionAndRotation(titlePosition, titleRotation);
        if (scene02) scene02.Present(view);
        if (scene03) scene03.Present(view);
        if (scene04) scene04.Present(view);
        if (RestyledRoom)
        {
            PresentPanel(scene01Title, view, true);
            PresentPanel(dialogue ? dialogue.transform : null, view, false);
            if (scene02) { PresentPanel(scene02.title, view, true); PresentPanel(scene02.caption ? scene02.caption.transform : null, view, false); }
            if (scene03) { PresentPanel(scene03.title, view, true, true); PresentPanel(scene03.caption ? scene03.caption.transform : null, view, false); }
            if (scene04) { PresentPanel(scene04.title, view, true); PresentPanel(scene04.caption ? scene04.caption.transform : null, view, false); }
        }
    }

    void OnDisable()
    {
        Valve.VR.SteamVR_Events.NewPosesApplied.Remove(HeadsetPosesApplied);
        RestoreRenderers();
    }
    public void RestoreRenderers()
    {
        foreach (var pair in renderStates) if (pair.Key) pair.Key.forceRenderingOff = pair.Value;
    }
}

