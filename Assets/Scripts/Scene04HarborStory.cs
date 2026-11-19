using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public sealed class Scene04HarborStory : MonoBehaviour
{
    public PlayableDirector director;
    public Transform father, chair, burner, title;
    public TextMesh caption;
    public Vector3 seaTarget = new Vector3(10.5f, 1.35f, 2.8f);
    public double startTime = 88.0833333333333, duration = 22;
    public AudioClip fatherVoice, burnerVoice, clockBell;
    public Transform[] poseBones;
    public Vector3[] initialPositions;
    public Quaternion[] initialRotations;
    public double EndTime => startTime + duration;
    Dictionary<Transform, Vector3> positions;
    Dictionary<Transform, Quaternion> rotations;
    AudioSource voice;
    double previous = -1;

    public Transform Bone(string name)
    {
        foreach (var b in father.GetComponentsInChildren<Transform>(true)) if (b.name == name) return b;
        return null;
    }
    public void CapturePose()
    {
        poseBones = father.GetComponentsInChildren<Transform>(true);
        initialPositions = new Vector3[poseBones.Length]; initialRotations = new Quaternion[poseBones.Length];
        for (int i = 0; i < poseBones.Length; i++) { initialPositions[i] = poseBones[i].localPosition; initialRotations[i] = poseBones[i].localRotation; }
        positions = null; rotations = null;
    }
    void Prepare()
    {
        if (positions != null) return;
        if (poseBones == null || poseBones.Length == 0) CapturePose();
        positions = new Dictionary<Transform, Vector3>(); rotations = new Dictionary<Transform, Quaternion>();
        for (int i = 0; i < poseBones.Length; i++) { positions[poseBones[i]] = initialPositions[i]; rotations[poseBones[i]] = initialRotations[i]; }
    }
    public void Seat(float seatHeight)
    {
        var hips = Bone("Hips"); hips.position += Vector3.up * (seatHeight + .08f - hips.position.y);
        foreach (var side in new[] { "Left", "Right" })
        {
            Bone(side + "UpperLeg").localRotation = Quaternion.Euler(-88, 0, 0);
            Bone(side + "LowerLeg").localRotation = Quaternion.Euler(88, 0, 0);
            var thigh = Bone(side + "UpperLeg"); var knee = Bone(side + "LowerLeg");
            Vector3 target = Vector3.Lerp(thigh.position, knee.position, .82f) + Vector3.up * .065f;
            var upper = Bone(side + "UpperArm"); var lower = Bone(side + "LowerArm"); var hand = Bone(side + "Hand");
            Vector3 pole = father.TransformPoint(new Vector3(side == "Left" ? .43f : -.43f, .63f, .12f));
            SolveArm(upper, lower, hand, target, pole, father.rotation * Quaternion.Euler(85, 0, 0));
        }
    }
    static void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 pole, Quaternion palm)
    {
        Vector3 a = upper.position;
        float ab = Vector3.Distance(a, lower.position), bc = Vector3.Distance(lower.position, hand.position);
        Vector3 axis = (target - a).normalized;
        float d = Mathf.Clamp(Vector3.Distance(a, target), Mathf.Abs(ab - bc) + .001f, ab + bc - .001f);
        Vector3 side = Vector3.ProjectOnPlane(pole - a, axis).normalized;
        float along = (ab * ab - bc * bc + d * d) / (2 * d);
        Vector3 elbow = a + axis * along + side * Mathf.Sqrt(Mathf.Max(0, ab * ab - along * along));
        upper.rotation = Quaternion.FromToRotation(lower.position - a, elbow - a) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, a + axis * d - lower.position) * lower.rotation;
        hand.rotation = palm;
    }
    static float Smooth(float a, float b, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));
    public void Sample(double localTime)
    {
        if (!father) return;
        Prepare();
        foreach (var p in positions) if (p.Key) p.Key.localPosition = p.Value;
        foreach (var p in rotations) if (p.Key) p.Key.localRotation = p.Value;
        float t = Mathf.Clamp((float)localTime, 0, (float)duration);
        var head = Bone("Head");
        Vector3 target = seaTarget;
        // A small upward gaze, with the seated body and hands completely still.
        float lift = Smooth(1.4f, 3.4f, t);
        target.y = Mathf.Lerp(head.position.y - .18f, seaTarget.y, lift);
        head.rotation = Quaternion.LookRotation(target - head.position, Vector3.up);
        if (caption) caption.text = SyncedCaptionAt(t);
    }
    string SyncedCaptionAt(float t) => t >= 2f && t < 2f + (fatherVoice ? fatherVoice.length : 3.5f) ? "父亲：不知道那边的老屋，还在不在。" :
        t >= 14f && t < 14f + (burnerVoice ? burnerVoice.length : 2.168f) ? "香炉：我还在等。" : "";
    public static string CaptionAt(float t) => t < 2 || t >= 18 ? "" :
        t < 8.5f ? "父亲：不知道那边的老屋，还在不在。" :
        t < 14 ? "" : "香炉：我还在等。";
    public void Present(Camera camera)
    {
        if (!director || !camera) return;
        double t = director.time - startTime;
        if (t < 0 || t > duration + 1) { if (caption) caption.text = ""; return; }
        Sample(t);
        if (caption) { caption.transform.position = camera.transform.TransformPoint(new Vector3(0, -.20f, 1.65f)); caption.transform.rotation = camera.transform.rotation; }
        if (title) { title.position = camera.transform.TransformPoint(new Vector3(0, 1.65f, 2.6f)); title.rotation = camera.transform.rotation; }
    }
    void Update()
    {
        if (!Application.isPlaying || !director) return;
        double t = director.time - startTime;
        if (director.state == PlayState.Playing)
        {
            if (!voice) { voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false; voice.volume = .8f; }
            Cue(2, fatherVoice, t, 1); Cue(14, burnerVoice, t, .55f);
        }
        previous = t;
    }
    void Cue(double at, AudioClip clip, double t, float volume) { if (clip && previous < at && t >= at && t - at < .5) voice.PlayOneShot(clip, volume); }
}

