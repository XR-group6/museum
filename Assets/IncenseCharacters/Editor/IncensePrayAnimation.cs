#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IncenseCharacters.Editor
{
    /// <summary>Bakes the approach, insertion and release into ordinary Generic bone curves.</summary>
    public static class IncensePrayAnimation
    {
        public const float ReleaseTime = 3.7f;
        public const float Duration = 5f;
        public const string ClipPath = "Assets/IncenseCharacters/Generated/Motion/Father_PrayInsert.anim";
        // Grip_R is a wrist-relative helper, not the centre of the curled fingers.
        // Raise the bundle through the closed hands: right grip is 9 cm above the feet.
        const float BottomGripRaise = .11f;
        public static readonly Vector3 GripOffset = new Vector3(-.002f + BottomGripRaise, -.016f, -.005f);
        public static readonly Quaternion GripRotation = Quaternion.Euler(0, 0, -90);
        static readonly Vector3 PinchInHand = new Vector3(-.012f + BottomGripRaise, -.084f, .028f);

        static float Ease(float time, float a, float b)
        {
            float t = Mathf.Clamp01((time - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        static float Step(float time, float a, float b, float backA, float backB, float distance)
        { return distance * (Ease(time, a, b) - Ease(time, backA, backB)); }

        static float Lift(float time, float a, float b)
        {
            if (time <= a || time >= b) return 0;
            return .065f * Mathf.Pow(Mathf.Sin(Mathf.PI * (time - a) / (b - a)), 2);
        }

        // Uses the rig's actual segment lengths; end position and wrist orientation are independent.
        static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole, Quaternion rotation)
        {
            Vector3 origin = upper.position;
            float a = Vector3.Distance(origin, lower.position), b = Vector3.Distance(lower.position, end.position);
            Vector3 axis = (target - origin).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(origin, target), Mathf.Abs(a - b) + .0001f, a + b - .0001f);
            Vector3 bend = Vector3.ProjectOnPlane(pole - origin, axis).normalized;
            float along = (a * a - b * b + distance * distance) / (2 * distance);
            Vector3 elbow = origin + along * axis + Mathf.Sqrt(Mathf.Max(0, a * a - along * along)) * bend;
            upper.rotation = Quaternion.FromToRotation(lower.position - origin, elbow - origin) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, origin + axis * distance - lower.position) * lower.rotation;
            end.rotation = rotation;
        }

        public static AnimationClip Bake(GameObject father, Vector3 burnerPosition)
        {
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/IncenseCharacters/Generated/Father_Young/Animations/Idle.anim");
            if (!idle) throw new InvalidOperationException("Father Idle animation is missing.");
            idle.SampleAnimation(father, 0);
            var bones = father.GetComponentsInChildren<Transform>(true).Where(t => t != father.transform)
                .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            string[] animated = { "Root", "Hips", "Spine", "Chest", "Head", "Neck",
                "RightUpperArm", "RightLowerArm", "RightHand", "LeftUpperArm", "LeftLowerArm", "LeftHand",
                "RightUpperLeg", "RightLowerLeg", "RightFoot", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                "RightThumb1", "RightThumb2", "LeftThumb1", "LeftThumb2" };
            var moving = new List<string>(animated);
            foreach (string side in new[] { "Right", "Left" })
                foreach (string finger in new[] { "Index", "Middle", "Ring", "Little" })
                    for (int joint = 1; joint <= 3; joint++) moving.Add(side + finger + joint);
            var initialRotations = moving.ToDictionary(n => n, n => bones[n].localRotation);
            var initialPositions = moving.ToDictionary(n => n, n => bones[n].localPosition);
            var curves = moving.ToDictionary(n => n, n => Enumerable.Range(0, 4).Select(i => new List<Keyframe>()).ToArray());
            var positions = new[] { "Root", "Hips" }.ToDictionary(n => n, n => Enumerable.Range(0, 3).Select(i => new List<Keyframe>()).ToArray());
            Vector3 rightFoot = father.transform.InverseTransformPoint(bones["RightFoot"].position);
            Vector3 leftFoot = father.transform.InverseTransformPoint(bones["LeftFoot"].position);
            // The model's existing incense starts at ash height + .125 m. Use the near side of the bowl.
            Vector3 ash = father.transform.InverseTransformPoint(burnerPosition + Vector3.up * .125f) - Vector3.forward * .05f;
            Vector3 inserted = ash + Vector3.up * .184f; // Prop origin is .20 m above its feet; embed .016 m.
            Quaternion rightPalm = father.transform.rotation * Quaternion.Euler(0, 0, 90);
            Quaternion leftPalm = father.transform.rotation * Quaternion.Euler(0, 0, -90);
            var clip = UnityEngine.Object.Instantiate(idle);
            clip.name = "Father_PrayInsert";
            clip.frameRate = 30;
            for (int frame = 0; frame <= 150; frame++)
            {
                float time = frame / 30f;
                foreach (string n in moving) { bones[n].localPosition = initialPositions[n]; bones[n].localRotation = initialRotations[n]; }
                float rightStep = Step(time, 1.75f, 2.55f, 4.15f, 5f, .65f);
                float leftStep = Step(time, 2.15f, 2.95f, 3.85f, 4.55f, .22f);
                float advance = Ease(time, 1.8f, 2.95f) * (1 - Ease(time, 3.85f, 5f));
                float rootForward = (rightStep + leftStep) * .5f + .165f * advance;
                bones["Root"].localPosition += Vector3.forward * rootForward;
                bones["Hips"].localPosition -= Vector3.up * .1f * advance;
                bones["Spine"].localRotation = Quaternion.Euler(18 * advance, 0, 0);
                float bow = Ease(time, .7f, 1.2f) * (1 - Ease(time, 1.5f, 2f));
                bones["Head"].localRotation = Quaternion.Euler(8 + 13 * bow, 0, 0);
                bones["Neck"].localRotation = Quaternion.Euler(3 * bow, 0, 0);
                float relax = Ease(time, 4.15f, 5f);
                bones["Head"].localRotation = Quaternion.Slerp(bones["Head"].localRotation, initialRotations["Head"], relax);
                foreach (string side in new[] { "Right", "Left" })
                {
                    bool right = side == "Right";
                    Vector3 foot = right ? rightFoot : leftFoot;
                    foot.z += right ? rightStep : leftStep;
                    foot.y += right ? Lift(time, 1.75f, 2.55f) + Lift(time, 4.15f, 5f) : Lift(time, 2.15f, 2.95f) + Lift(time, 3.85f, 4.55f);
                    Solve(bones[side + "UpperLeg"], bones[side + "LowerLeg"], bones[side + "Foot"], father.transform.TransformPoint(foot),
                        father.transform.TransformPoint(new Vector3(right ? -.2f : .2f, .5f, rootForward + .7f)), father.transform.rotation);
                }
                Vector3 hold = Vector3.Lerp(new Vector3(0, 1.2f, .24f), new Vector3(0, 1.32f, .3f), Ease(time, 0, .7f));
                hold.y += BottomGripRaise;
                hold.y -= .025f * bow;
                hold = Vector3.Lerp(hold, inserted + Vector3.up * .14f, Ease(time, 1.9f, 2.95f));
                hold = Vector3.Lerp(hold, inserted, Ease(time, 2.95f, 3.5f));
                hold = Vector3.Lerp(hold, new Vector3(-.04f, 1.15f, rootForward + .25f), Ease(time, ReleaseTime, 4.2f));
                Vector3 wrist = father.transform.TransformPoint(hold) - rightPalm * PinchInHand;
                Solve(bones["RightUpperArm"], bones["RightLowerArm"], bones["RightHand"], wrist,
                    father.transform.TransformPoint(new Vector3(-.55f, 1.04f, rootForward)), rightPalm);
                // The supporting hand encloses the same bundle, just below the right hand.
                Vector3 leftPinch = new Vector3(.012f, -.084f, .028f);
                Vector3 support = hold - Vector3.up * (BottomGripRaise + .075f) - Quaternion.Euler(0, 0, -90) * leftPinch;
                support = Vector3.Lerp(support, new Vector3(.22f, 1.05f, rootForward + .22f), Ease(time, 1.75f, 2.55f));
                Solve(bones["LeftUpperArm"], bones["LeftLowerArm"], bones["LeftHand"], father.transform.TransformPoint(support),
                    father.transform.TransformPoint(new Vector3(.55f, 1.02f, rootForward)), leftPalm);
                foreach (string side in new[] { "Right", "Left" })
                    foreach (string finger in new[] { "Index", "Middle", "Ring", "Little" })
                        for (int joint = 1; joint <= 3; joint++)
                        {
                            string n = side + finger + joint;
                            float opening = side == "Right" ? Ease(time, 3.5f, ReleaseTime) : 0;
                            bones[n].localRotation = Quaternion.Slerp(Quaternion.Euler(Mathf.Lerp(-50, -12, opening), 0, 0), initialRotations[n], relax);
                        }
                bones["RightThumb1"].localRotation = Quaternion.FromToRotation(new Vector3(.023f, -.042f, .025f), new Vector3(-.008f, -.051f, .019f));
                bones["RightThumb1"].localRotation = Quaternion.Slerp(bones["RightThumb1"].localRotation, initialRotations["RightThumb1"], Ease(time, 3.5f, ReleaseTime));
                bones["LeftThumb1"].localRotation = Quaternion.Slerp(
                    Quaternion.FromToRotation(new Vector3(-.023f, -.042f, .025f), new Vector3(.008f, -.051f, .019f)),
                    initialRotations["LeftThumb1"], relax);
                foreach (string n in new[] { "RightUpperArm", "RightLowerArm", "RightHand", "LeftUpperArm", "LeftLowerArm", "LeftHand" })
                    bones[n].localRotation = Quaternion.Slerp(bones[n].localRotation, initialRotations[n], relax);
                foreach (string n in moving)
                {
                    Quaternion q = bones[n].localRotation;
                    // Keep quaternion signs continuous before interpolating baked samples.
                    if (frame > 0 && Enumerable.Range(0, 4).Sum(i => curves[n][i][frame - 1].value * q[i]) < 0)
                        q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    for (int i = 0; i < 4; i++) curves[n][i].Add(new Keyframe(time, q[i]));
                    if (positions.ContainsKey(n)) for (int i = 0; i < 3; i++) positions[n][i].Add(new Keyframe(time, bones[n].localPosition[i]));
                }
            }
            foreach (string n in moving)
            {
                string path = AnimationUtility.CalculateTransformPath(bones[n], father.transform);
                for (int i = 0; i < 4; i++) Set(clip, path, "m_LocalRotation." + "xyzw"[i], curves[n][i]);
                if (positions.ContainsKey(n)) for (int i = 0; i < 3; i++) Set(clip, path, "m_LocalPosition." + "xyz"[i], positions[n][i]);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false; settings.startTime = 0; settings.stopTime = Duration;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (existing) { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); clip = existing; }
            else AssetDatabase.CreateAsset(clip, ClipPath);
            idle.SampleAnimation(father, 0);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static void Set(AnimationClip clip, string path, string property, List<Keyframe> keys)
        {
            var curve = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }
    }
}
#endif
