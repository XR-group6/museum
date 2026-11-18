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
            eye = Vector3.Lerp(eye, Centre(scene03.burner) + Vector3.up * eyeLift, enter * leave);
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
        rig.position = EyeAt(time) - rig.TransformVector(trackingOffsets[rig]);
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
    }

    void OnDisable() { RestoreRenderers(); }
    public void RestoreRenderers()
    {
        foreach (var pair in renderStates) if (pair.Key) pair.Key.forceRenderingOff = pair.Value;
    }
}

