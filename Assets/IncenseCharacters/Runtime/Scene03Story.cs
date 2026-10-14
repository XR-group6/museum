using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace IncenseCharacters
{
    // All poses are sampled from absolute time; seeking never accumulates rotations.
    [DefaultExecutionOrder(10000)]
    public sealed class Scene03Story : MonoBehaviour
    {
        public Transform father, daughter, burner;
        public PlayableDirector director;
        public AudioClip daughterVoice, fatherVoice;
        public Shader coverShader;
        public Vector3 carryOffset = new Vector3(0, 1.13f, .27f);
        [Range(0, 30)] public float previewTime;
        public float Duration => 30f;
        readonly Dictionary<Transform, Quaternion> rotations = new Dictionary<Transform, Quaternion>();
        Transform fh, dh, leftUpper, leftLower, leftHand, rightUpper, rightLower, rightHand;
        Vector3 burnerPosition, burnerScale;
        Quaternion burnerRotation;
        Transform cameraTransform, rig;
        Vector3 rigPosition;
        Vector3 eyeRigPosition, initialCenter;
        Quaternion rigRotation;
        Quaternion cameraLocalRotation;
        float cameraFov;
        Mesh mouthF, mouthD;
        Mesh originalF, originalD;
        Vector3[] verticesF, verticesD;
        SkinnedMeshRenderer skinF, skinD;
        GameObject effects;
        Transform lidTop, lidBottom, clothTop, clothBottom;
        Material lidMaterial, clothMaterial;
        AudioSource voiceF, voiceD;
        bool ready, cameraPlaced;
        public float EyeOpening { get; private set; }
        public float ClothOpening { get; private set; }

        static Transform Bone(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            throw new System.InvalidOperationException(root.name + " missing bone " + name);
        }

        public void Prepare()
        {
            if (ready) return;
            if (!father || !daughter || !burner) throw new System.InvalidOperationException("Scene03 story references are missing.");
            foreach (var root in new[] { father, daughter })
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) rotations[t] = t.localRotation;
            fh = Bone(father, "Head"); dh = Bone(daughter, "Head");
            leftUpper = Bone(father, "LeftUpperArm"); leftLower = Bone(father, "LeftLowerArm"); leftHand = Bone(father, "LeftHand");
            rightUpper = Bone(father, "RightUpperArm"); rightLower = Bone(father, "RightLowerArm"); rightHand = Bone(father, "RightHand");
            burnerPosition = burner.position; burnerRotation = burner.rotation; burnerScale = burner.localScale;
            skinF = father.GetComponentInChildren<SkinnedMeshRenderer>();
            skinD = daughter.GetComponentInChildren<SkinnedMeshRenderer>();
            originalF = skinF.sharedMesh; originalD = skinD.sharedMesh;
            mouthF = Instantiate(originalF); mouthD = Instantiate(originalD);
            mouthF.hideFlags = mouthD.hideFlags = HideFlags.HideAndDontSave;
            verticesF = originalF.vertices; verticesD = originalD.vertices;
            skinF.sharedMesh = mouthF; skinD.sharedMesh = mouthD;
            ready = true;
        }

        void Start()
        {
            Prepare();
            voiceF = gameObject.AddComponent<AudioSource>(); voiceD = gameObject.AddComponent<AudioSource>();
            voiceF.playOnAwake = voiceD.playOnAwake = false;
            PlaceCamera();
            Sample(0);
        }

        void PlaceCamera()
        {
            // Move the tracking origin once. Headset tracking remains in control afterwards.
            var camera = Camera.main;
            if (!camera)
                foreach (var candidate in Camera.allCameras)
                    if (candidate.gameObject.scene == gameObject.scene) { camera = candidate; break; }
            if (!camera) return;
            cameraTransform = camera.transform;
            cameraLocalRotation = cameraTransform.localRotation; cameraFov = camera.fieldOfView;
            rig = cameraTransform;
            while (rig.parent && rig.parent.name != "scene_03") rig = rig.parent;
            rigPosition = rig.position; rigRotation = rig.rotation;
            Vector3 eye = father.TransformPoint(carryOffset + new Vector3(0, .20f, .10f));
            Vector3 target = Vector3.Lerp(dh.position, fh.position, .65f);
            Vector3 direction = target - eye;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            rig.Rotate(Vector3.up, Mathf.DeltaAngle(cameraTransform.eulerAngles.y, yaw), Space.World);
            rig.position += eye - cameraTransform.position;
            eyeRigPosition = rig.position; initialCenter = father.TransformPoint(carryOffset);
            if (!camera.stereoEnabled)
            {
                cameraTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                camera.fieldOfView = 105;
            }
            cameraPlaced = true;
            BuildEffects(cameraTransform);
        }

        public void BuildEffects(Transform view)
        {
            if (effects) return;
            cameraTransform = view;
            effects = new GameObject("Scene03 eye and woven cover");
            effects.hideFlags = HideFlags.HideAndDontSave;
            effects.transform.SetParent(view, false);
            var shader = coverShader ? coverShader : Shader.Find("Unlit/Color");
            lidMaterial = new Material(shader) { color = Color.black, hideFlags = HideFlags.HideAndDontSave };
            clothMaterial = new Material(shader) { color = new Color(.16f, .105f, .055f), hideFlags = HideFlags.HideAndDontSave };
            if (clothMaterial.HasProperty("_Weave")) clothMaterial.SetFloat("_Weave", 1);
            lidTop = Panel("Upper eyelid", lidMaterial); lidBottom = Panel("Lower eyelid", lidMaterial);
            clothTop = Panel("Woven cloth upper fold", clothMaterial); clothBottom = Panel("Woven cloth lower fold", clothMaterial);
        }

        Transform Panel(string name, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.hideFlags = HideFlags.HideAndDontSave; go.transform.SetParent(effects.transform, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            const int columns = 80;
            var v = new Vector3[(columns + 1) * 2]; var triangles = new int[columns * 6];
            var colors = new Color[v.Length];
            var uv = new Vector2[v.Length];
            for (int i = 0; i <= columns; i++)
            {
                float x = -1 + 2f * i / columns;
                float ripple = name.Contains("cloth") ? .013f * Mathf.Sin(i * .65f) : -.025f * x * x;
                v[i * 2] = new Vector3(x, ripple, 0); v[i * 2 + 1] = new Vector3(x, 2, .025f);
                colors[i * 2] = colors[i * 2 + 1] = Color.white;
                uv[i * 2] = new Vector2(i / (float)columns, 0); uv[i * 2 + 1] = new Vector2(i / (float)columns, 1);
                if (i == columns) continue;
                int k = i * 6, j = i * 2;
                triangles[k] = j; triangles[k + 1] = j + 1; triangles[k + 2] = j + 2;
                triangles[k + 3] = j + 2; triangles[k + 4] = j + 1; triangles[k + 5] = j + 3;
            }
            mesh.vertices = v; mesh.triangles = triangles; mesh.colors = colors; mesh.uv = uv; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            return go.transform;
        }

        static float Smooth(float t, float start, float end) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, t));
        static float Speech(float t, float start, float end) => Smooth(t, start, start + .25f) * (1 - Smooth(t, end - .25f, end));

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (!cameraPlaced) PlaceCamera();
            Sample(director ? (float)director.time : Mathf.Min(Time.timeSinceLevelLoad, 30));
        }

        public void Sample(float time)
        {
            Prepare();
            float t = Mathf.Clamp(time, 0, 30);
            previewTime = t;
            // Restore only bones, never character root transforms or their scene placement.
            foreach (var item in rotations) if (item.Key && item.Key != father && item.Key != daughter) item.Key.localRotation = item.Value;
            float pet = Smooth(t, 12, 13.8f) * (1 - Smooth(t, 18.3f, 20));
            bool petLeft = father.InverseTransformPoint(dh.position).x > 0;
            float lean = petLeft ? -1 : 1;
            Bone(father, "Spine").localRotation *= Quaternion.Euler(0, 0, lean * 30 * pet);
            Bone(father, "Chest").localRotation *= Quaternion.Euler(0, 0, lean * 12 * pet);
            float childLean = daughter.InverseTransformPoint(fh.position).x > 0 ? -1 : 1;
            Bone(daughter, "Spine").localRotation *= Quaternion.Euler(0, 0, childLean * 26 * pet);
            Bone(daughter, "Chest").localRotation *= Quaternion.Euler(0, 0, childLean * 14 * pet);
            var center = father.TransformPoint(carryOffset + new Vector3(-lean * .1f, .08f, 0) * pet);
            if (cameraPlaced && rig) rig.position = eyeRigPosition + center - initialCenter;
            burner.rotation = father.rotation * Quaternion.Euler(-90, 0, 0);
            burner.localScale = burnerScale;
            var bounds = new Bounds(); bool first = true;
            foreach (var r in burner.GetComponentsInChildren<Renderer>()) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
            burner.position += center - bounds.center;

            float toFather = Smooth(t, 6.5f, 9.2f);
            float forward = Smooth(t, 21, 23);
            Vector3 forwardDirection = (father.forward + daughter.forward).normalized;
            Vector3 daughterTarget = Vector3.Lerp(center, fh.position, toFather);
            Quaternion daughterLook = Quaternion.Slerp(rotations[dh], LocalLook(dh, daughterTarget), Smooth(t, 5, 6));
            dh.localRotation = Quaternion.Slerp(daughterLook, LocalLook(dh, dh.position + forwardDirection * 3), forward);
            Quaternion fatherLook = Quaternion.Slerp(rotations[fh], LocalLook(fh, dh.position), Smooth(t, 11, 12.3f));
            fh.localRotation = Quaternion.Slerp(fatherLook, LocalLook(fh, fh.position + forwardDirection * 3), forward);
            float daughterSpeech = Speech(t, 5, 10), fatherSpeech = Speech(t, 11, 20);
            dh.localRotation *= Quaternion.Euler(1.5f * Mathf.Sin(t * 5.1f) * daughterSpeech, 0, 0);
            fh.localRotation *= Quaternion.Euler(1.2f * Mathf.Sin(t * 4.3f) * fatherSpeech, 0, 0);

            Vector3 supportL = center + father.TransformDirection(new Vector3(.10f, -.15f, -.035f));
            Vector3 supportR = center + father.TransformDirection(new Vector3(-.10f, -.15f, -.035f));
            // Use the nearer arm for the child's actual placement; lean only the upper body.
            Vector3 crown = dh.position + dh.up * .20f + Vector3.up * (.022f + .008f * Mathf.Sin((t - 14) * 5)) + father.forward * .055f;
            SolveArm(leftUpper, leftLower, leftHand, Vector3.Lerp(supportL, crown, petLeft ? pet : 0), father.TransformPoint(new Vector3(.5f, 1.15f, .02f)), father.rotation * Quaternion.Euler(Mathf.Lerp(-90, 90, petLeft ? pet : 0), 0, 0));
            SolveArm(rightUpper, rightLower, rightHand, Vector3.Lerp(supportR, crown, petLeft ? 0 : pet), father.TransformPoint(new Vector3(-.5f, 1.15f, .02f)), father.rotation * Quaternion.Euler(Mathf.Lerp(-90, 90, petLeft ? 0 : pet), 0, 0));
            AnimateMouth(mouthF, verticesF, 1, 1, fatherSpeech * (.4f + .6f * Mathf.Abs(Mathf.Sin(t * 10))));
            AnimateMouth(mouthD, verticesD, .61f, .80f, daughterSpeech * (.4f + .6f * Mathf.Abs(Mathf.Sin(t * 11))));
            EyeOpening = Smooth(t, .3f, 2.5f) * (1 - .94f * (Smooth(t, 2.7f, 3.15f) - Smooth(t, 3.25f, 4.8f)));
            ClothOpening = 1 - .97f * Smooth(t, 23, 30);
            if (effects)
            {
                var camera = cameraTransform.GetComponent<Camera>();
                float z = Mathf.Max(.15f, camera.nearClipPlane + .04f);
                float h = z * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) * 1.25f;
                float w = Mathf.Max(h * camera.aspect * 1.5f, .5f);
                SetPanels(lidTop, lidBottom, EyeOpening, h, w, z);
                SetPanels(clothTop, clothBottom, ClothOpening, h, w, z + .01f);
                lidTop.gameObject.SetActive(t < 5); lidBottom.gameObject.SetActive(t < 5);
                clothTop.gameObject.SetActive(t >= 23); clothBottom.gameObject.SetActive(t >= 23);
            }
            if (Application.isPlaying) { SyncVoice(voiceD, daughterVoice, t, 5, 10); SyncVoice(voiceF, fatherVoice, t, 11, 20); }
        }

        static Quaternion LocalLook(Transform bone, Vector3 target) => Quaternion.Inverse(bone.parent.rotation) * Quaternion.LookRotation(target - bone.position, Vector3.up);

        static void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 pole, Quaternion palm)
        {
            Vector3 a = upper.position;
            float ab = Vector3.Distance(a, lower.position), bc = Vector3.Distance(lower.position, hand.position);
            Vector3 axis = (target - a).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(a, target), Mathf.Abs(ab - bc) + .001f, ab + bc - .001f);
            Vector3 side = Vector3.ProjectOnPlane(pole - a, axis).normalized;
            float along = (ab * ab - bc * bc + distance * distance) / (2 * distance);
            Vector3 elbow = a + axis * along + side * Mathf.Sqrt(Mathf.Max(0, ab * ab - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - a, elbow - a) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, a + axis * distance - lower.position) * lower.rotation;
            hand.rotation = palm;
        }

        static void AnimateMouth(Mesh mesh, Vector3[] rest, float bodyScale, float headScale, float amount)
        {
            // Match the generator's separate head/body proportions (the child is not uniformly scaled).
            var vertices = (Vector3[])rest.Clone();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = rest[i] / headScale;
                p.y = (rest[i].y - 1.465f * bodyScale) / headScale + 1.465f;
                if (Mathf.Abs(p.x) < .034f && p.y > 1.53f && p.y < 1.556f && p.z > .075f)
                    vertices[i].y -= amount * .012f * headScale * Mathf.Clamp01((1.555f - p.y) / .01f);
            }
            mesh.vertices = vertices;
        }

        static void SetPanels(Transform top, Transform bottom, float opening, float h, float w, float z)
        {
            top.localPosition = new Vector3(0, h * opening, z); top.localScale = new Vector3(w, h, 1);
            bottom.localPosition = new Vector3(0, -h * opening, z); bottom.localRotation = Quaternion.Euler(0, 0, 180); bottom.localScale = new Vector3(w, h, 1);
        }

        static void SyncVoice(AudioSource source, AudioClip clip, float t, float start, float end)
        {
            if (!source || !clip) return;
            float local = t - start;
            if (local < 0 || t >= end || local >= clip.length) { source.Stop(); return; }
            if (source.clip != clip) source.clip = clip;
            if (!source.isPlaying || Mathf.Abs(source.time - local) > .15f) { source.time = local; source.Play(); }
        }

        public void Restore()
        {
            if (!ready) return;
            foreach (var item in rotations) if (item.Key && item.Key != father && item.Key != daughter) item.Key.localRotation = item.Value;
            burner.SetPositionAndRotation(burnerPosition, burnerRotation); burner.localScale = burnerScale;
            if (skinF) skinF.sharedMesh = originalF; if (skinD) skinD.sharedMesh = originalD;
            if (cameraPlaced && rig) rig.SetPositionAndRotation(rigPosition, rigRotation);
            if (cameraPlaced && cameraTransform) { cameraTransform.localRotation = cameraLocalRotation; cameraTransform.GetComponent<Camera>().fieldOfView = cameraFov; }
            if (effects) foreach (var filter in effects.GetComponentsInChildren<MeshFilter>(true)) DestroyImmediate(filter.sharedMesh);
            if (effects) DestroyImmediate(effects);
            if (mouthF) DestroyImmediate(mouthF); if (mouthD) DestroyImmediate(mouthD);
            if (lidMaterial) DestroyImmediate(lidMaterial); if (clothMaterial) DestroyImmediate(clothMaterial);
            rotations.Clear(); ready = cameraPlaced = false;
        }
        void OnDisable() { Restore(); }
    }
}
