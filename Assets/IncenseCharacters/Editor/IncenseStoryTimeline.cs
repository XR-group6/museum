#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace IncenseCharacters.Editor
{
    /// <summary>
    /// 把分镜「父亲敬香 → 女儿叫父 → 父亲回头 → 父亲让位 → 女儿近看香炉」一键铺成 Unity Timeline。
    /// 在 scene_02 中运行；生成后打开 Timeline 窗口即可拖动关键帧 / 调时长自己修细节。
    /// 依赖 com.unity.timeline 包。
    ///
    /// 移动靠动画「Root」骨骼（与转头同机制），不靠绑定 GameObject Transform，
    /// 因为这套角色是 Generic 原地动画，绑定到 Transform 的位移轨不会被应用。
    /// </summary>
    public static class IncenseStoryTimeline
    {
        const string FatherPrefab   = "Assets/IncenseCharacters/Generated/Father_Young/Father_Young.prefab";
        const string DaughterPrefab = "Assets/IncenseCharacters/Generated/Daughter_Child/Daughter_Child.prefab";
        const string FatherAnimDir   = "Assets/IncenseCharacters/Generated/Father_Young/Animations/";
        const string DaughterAnimDir = "Assets/IncenseCharacters/Generated/Daughter_Child/Animations/";
        const string MotionDir       = "Assets/IncenseCharacters/Generated/Motion/";
        const string TimelinePath    = "Assets/IncenseCharacters/Generated/IncenseStory.playable";
        const string IncensePrefab   = "Assets/IncenseCharacters/Props/HandIncense.prefab";

        const string RootPath = "Root"; // 骨架里最顶层的骨骼名（相对 Animator 的路径）

        // —— 分镜时间（秒）——
        const float T_PRAY          = 0f;    // 父亲敬香
        const float T_INCENSE_END   = IncensePrayAnimation.ReleaseTime; // 香脚插入香灰、松手后隐藏
        const float T_FATHER_IDLE   = 5f;    // 敬香结束，站直（女儿开始叫）
        const float T_HEAD_TURN     = 9f;    // 父亲回头
        const float T_STEP_ASIDE    = 11.2f; // 父亲转身让位
        const float T_FATHER_IDLE2  = 12.8f; // 让位后站定看女儿（此后的站定 clip 固定在新位置）
        const float T_DAUGHTER_WALK = 12.5f; // 女儿走进
        const float T_PEEK          = 15f;   // 女儿近看香炉
        const float T_DAUGHTER_IDLE = 23f;   // 女儿打量结束，回 Idle
        const float T_END           = 28f;

        // —— 空间参数（世界坐标，香炉 x=0）——
        const float FatherDist   = 1.2f;  // 父亲在香炉前方距离
        const float SideStepX    = 1.2f;  // 让位横向移动量
        const float DaughterDist = 4.5f;  // 门口(女儿)距离
        const float DaughterNear = 0.95f; // 与香炉前沿镜头留出空间，探头时不进入近裁剪面

        // —— 回头参数（角度方向反了就改符号）——
        const float HeadTurnDeg = 35f; // 微微偏头
        const float NeckTurnDeg = 15f;

        // —— 女儿上下打量的头部俯仰幅度（角度方向反了就改符号）——
        const float ExaminePitchDeg  = 22f;
        const float ExaminePitchNeck = 10f;

        // —— 女儿最后一幕侧头看向父亲的偏转（方向反了就改符号）——
        const float LookFatherHeadDeg = 45f;
        const float LookFatherNeckDeg = 20f;

        [MenuItem("Tools/Incense Story/Build Timeline in scene_02")]
        public static void Build()
        {
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.name.StartsWith("scene_02"))
                {
                    EditorUtility.DisplayDialog("Incense Story", "请先打开 scene_02.unity（香炉所在场景）再运行。", "OK");
                    return;
                }

                var burner = GameObject.Find("incense_burner");
                if (burner == null)
                {
                    EditorUtility.DisplayDialog("Incense Story", "场景中找不到 incense_burner。", "OK");
                    return;
                }
                Vector3 burnerPos = burner.transform.position;

                // 摆放坐标
                Vector3 fatherPrayPos   = new Vector3(burnerPos.x, 0f, burnerPos.z - FatherDist);
                Vector3 daughterDoorPos = new Vector3(burnerPos.x, 0f, burnerPos.z - DaughterDist);
                Quaternion faceBurner = FaceTowards(burnerPos, fatherPrayPos);

                // 清理上一次生成的场景对象（可重复运行）
                DestroyByName("Father_Young");
                DestroyByName("Daughter_Child");
                DestroyByName("IncenseStoryDirector");

                // 角色
                var father = InstantiatePrefab(FatherPrefab, "Father_Young", fatherPrayPos, faceBurner);
                var daughter = InstantiatePrefab(DaughterPrefab, "Daughter_Child", daughterDoorPos, FaceTowards(burnerPos, daughterDoorPos));
                if (father == null || daughter == null)
                {
                    EditorUtility.DisplayDialog("Incense Story", "找不到角色 Prefab。请先运行 Tools/Incense Characters/1 Build Characters。", "OK");
                    return;
                }

                // 放大包围盒：骨骼位移后避免被视锥剔除而闪烁
                EnlargeBounds(father);
                EnlargeBounds(daughter);

                // 隐藏香炉前面的蜡烛（按名字包含 "candle" 递归隐藏所有相关对象），避免挡住第一视角
                HideBySubstring("candle");

                // 生成辅助 clip（转头 / 让位 / 走进）—— 都作用于骨骼，放在 Animator 轨上
                EnsureFolder(MotionDir);
                var fatherPrayClip = IncensePrayAnimation.Bake(father, burnerPos);
                var headRoot = FindChildRecursive(father.transform, "Root");
                var headBone = FindChildRecursive(father.transform, "Head");
                var neckBone = FindChildRecursive(father.transform, "Neck");

                AnimationClip headTurnClip = CreateHeadTurnClip(
                    BonePath(headRoot, headBone), BonePath(headRoot, neckBone),
                    HeadTurnDeg, NeckTurnDeg, 1.2f, 1.0f);

                AnimationClip daughterWalk = AssetDatabase.LoadAssetAtPath<AnimationClip>(DaughterAnimDir + "Walk.anim");

                // 父亲让位（撤步转身）：干净地侧移 + 转身，不带原地走步（避免滑步/月亮步）
                AnimationClip stepAsideClip = CreateWalkingMoveClip(
                    "Father_StepAside", null,
                    Vector3.zero, new Vector3(SideStepX, 0f, 0f),
                    Quaternion.identity, Quaternion.Euler(0f, 180f, 0f),
                    1.6f);

                // 女儿走进：向 +Z 前进 (DaughterDist - DaughterNear)
                AnimationClip walkInClip = CreateWalkingMoveClip(
                    "Daughter_WalkIn", daughterWalk,
                    Vector3.zero, new Vector3(0f, 0f, DaughterDist - DaughterNear),
                    Quaternion.identity, Quaternion.identity,
                    DaughterDist - DaughterNear > 0 ? 2.5f : 1.6f);

                // 父亲让位后站定在新位置（不再弹回敬香位）：Idle 姿态 + Root 固定 + 转身 180°
                // 头部视线完全交给 IncenseLookAt 组件，避免与动画烘焙的头转角打架导致闪烁
                AnimationClip fatherIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(FatherAnimDir + "Idle.anim");
                AnimationClip idleAsideClip = CreateHeldClip(
                    "Father_IdleAside", fatherIdle,
                    new Vector3(SideStepX, 0f, 0f), Quaternion.Euler(0f, 180f, 0f),
                    null, null, 0f, 0f,
                    T_END - T_FATHER_IDLE2);

                // 女儿近看/站定：Root 固定在近处（避免走进后又弹回门口）
                float walkDist = DaughterDist - DaughterNear;
                Vector3 daughterRoot = new Vector3(0f, 0f, walkDist);
                AnimationClip daughterPeek = AssetDatabase.LoadAssetAtPath<AnimationClip>(DaughterAnimDir + "PeekTable.anim");
                AnimationClip daughterIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(DaughterAnimDir + "Idle.anim");
                var daughterHeadRoot = FindChildRecursive(daughter.transform, "Root");
                var daughterHeadBone = FindChildRecursive(daughter.transform, "Head");
                var daughterNeckBone = FindChildRecursive(daughter.transform, "Neck");
                AnimationClip peekNearClip = CreateExamineClip(
                    "Daughter_PeekNear", daughterPeek,
                    daughterRoot,
                    BonePath(daughterHeadRoot, daughterHeadBone), BonePath(daughterHeadRoot, daughterNeckBone),
                    ExaminePitchDeg, ExaminePitchNeck,
                    T_DAUGHTER_IDLE - T_PEEK);
                AnimationClip idleNearClip = CreateHeldClip(
                    "Daughter_LookFather", daughterIdle,
                    daughterRoot, Quaternion.identity,
                    BonePath(daughterHeadRoot, daughterHeadBone), BonePath(daughterHeadRoot, daughterNeckBone),
                    LookFatherHeadDeg, LookFatherNeckDeg,
                    T_END - T_DAUGHTER_IDLE);

                // Timeline 资产（覆盖旧文件）
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TimelinePath) != null)
                    AssetDatabase.DeleteAsset(TimelinePath);
                var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, TimelinePath);

                // 父亲动画轨（含让位 + 让位后站定）
                var fatherAnim = timeline.CreateTrack<AnimationTrack>(null, "Father_Animation");
                AddClip(fatherAnim, fatherPrayClip, T_PRAY);
                AddAnimClip(fatherAnim, FatherAnimDir, "Idle", T_FATHER_IDLE);
                AddClip(fatherAnim, headTurnClip, T_HEAD_TURN);
                AddClip(fatherAnim, stepAsideClip, T_STEP_ASIDE);
                AddClip(fatherAnim, idleAsideClip, T_FATHER_IDLE2);

                // 女儿动画轨（含走进 + 近看打量 + 站定，Root 都固定在近处）
                var daughterAnim = timeline.CreateTrack<AnimationTrack>(null, "Daughter_Animation");
                AddAnimClip(daughterAnim, DaughterAnimDir, "Idle", T_PRAY);
                AddAnimClip(daughterAnim, DaughterAnimDir, "CallFather", T_FATHER_IDLE);
                AddClip(daughterAnim, walkInClip, T_DAUGHTER_WALK);
                AddClip(daughterAnim, peekNearClip, T_PEEK);
                AddClip(daughterAnim, idleNearClip, T_DAUGHTER_IDLE);

                // Director + 绑定（只绑 Animator）
                var directorGo = new GameObject("IncenseStoryDirector");
                var director = directorGo.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;
                director.playOnAwake = true;
                director.extrapolationMode = DirectorWrapMode.Hold;

                director.SetGenericBinding(fatherAnim, father.GetComponent<Animator>());
                director.SetGenericBinding(daughterAnim, daughter.GetComponent<Animator>());

                // The previous director was destroyed above; reconnect narration to this one.
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var narration in root.GetComponentsInChildren<SceneTwoNarration>(true))
                    {
                        narration.storyDirector = director;
                        EditorUtility.SetDirty(narration);
                        if (narration.isActiveAndEnabled) director.playOnAwake = false;
                    }
                }

                // 使用原生显隐轨，播放、回拖和重播时都由 Timeline 确定状态。
                var grip = FindChildRecursive(father.transform, "Grip_R");
                var incenseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(IncensePrefab);
                if (grip == null || incenseAsset == null)
                    throw new System.InvalidOperationException("Missing Grip_R or " + IncensePrefab);
                var incense = (GameObject)PrefabUtility.InstantiatePrefab(incenseAsset, grip);
                incense.name = "Father_HandIncense";
                incense.transform.localPosition = IncensePrayAnimation.GripOffset;
                incense.transform.localRotation = IncensePrayAnimation.GripRotation;
                incense.SetActive(false);
                var incenseTrack = timeline.CreateTrack<ActivationTrack>(null, "Father_Incense");
                incenseTrack.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
                var incenseClip = incenseTrack.CreateDefaultClip();
                incenseClip.displayName = "Hold incense";
                incenseClip.start = T_PRAY;
                incenseClip.duration = T_INCENSE_END - T_PRAY;
                director.SetGenericBinding(incenseTrack, incense);

                // 精确视线跟随：让位后父亲的头持续看向女儿（目标用女儿的 Head 骨骼，随她移动，而不是固定在门口的根物体）
                var lookAt = father.AddComponent<IncenseLookAt>();
                lookAt.target = daughterHeadBone;
                lookAt.director = director;
                lookAt.lookFrom = T_FATHER_IDLE2;

                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                Selection.activeGameObject = directorGo;
                EditorGUIUtility.PingObject(directorGo);
                EditorUtility.DisplayDialog("Incense Story",
                    "Timeline 已生成：\n" + TimelinePath +
                    "\n\n选中 IncenseStoryDirector，打开 Window > Sequencing > Timeline 拖关键帧修细节。" +
                    "\n\n若回头/转身角度方向反了，改脚本里 HeadTurnDeg / NeckTurnDeg 或让位旋转角度后重跑。",
                    "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Incense Story", "生成失败：\n" + e.Message, "OK");
            }
        }

        // ---------- helpers ----------

        static GameObject InstantiatePrefab(string path, string name, Vector3 pos, Quaternion rot)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetPositionAndRotation(pos, rot);
            return go;
        }

        static Quaternion FaceTowards(Vector3 target, Vector3 from)
        {
            var d = target - from; d.y = 0f;
            return d.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(d, Vector3.up);
        }

        static void DestroyByName(string name)
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == name) UnityEngine.Object.DestroyImmediate(go);
        }

        static void EnlargeBounds(GameObject go)
        {
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr) smr.localBounds = new Bounds(Vector3.zero, Vector3.one * 20f);
        }

        static void HideBySubstring(string substr)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.IndexOf(substr, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        t.gameObject.SetActive(false);
                }
            }
        }

        static Transform FindChildRecursive(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        static string BonePath(Transform root, Transform bone)
        {
            if (root == null || bone == null) return "";
            var names = new List<string>();
            var t = bone;
            while (t != null && t != root) { names.Add(t.name); t = t.parent; }
            if (t != root) return bone.name;
            names.Reverse();
            return root.name + "/" + string.Join("/", names);
        }

        static void EnsureFolder(string fullPath)
        {
            if (AssetDatabase.IsValidFolder(fullPath)) return;
            int slash = fullPath.LastIndexOf('/');
            string parent = fullPath.Substring(0, slash);
            string leaf = fullPath.Substring(slash + 1).TrimEnd('/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static AnimationClip SaveClip(AnimationClip clip)
        {
            string path = MotionDir + clip.name + ".anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static void AddAnimClip(AnimationTrack track, string dir, string name, float start)
        {
            AddClip(track, AssetDatabase.LoadAssetAtPath<AnimationClip>(dir + name + ".anim"), start);
        }

        static void AddClip(AnimationTrack track, AnimationClip clip, float start, float duration = -1f)
        {
            if (clip == null) return;
            var tc = track.CreateDefaultClip();
            var asset = tc.asset as AnimationPlayableAsset;
            if (asset == null) return;
            asset.clip = clip;
            tc.displayName = clip.name;
            tc.start = start;
            tc.duration = duration > 0f ? duration : clip.length;
        }

        // 纯转头：只动 Head / Neck 的 localRotation
        static AnimationClip CreateHeadTurnClip(string headPath, string neckPath, float headDeg, float neckDeg, float rise, float fall)
        {
            var clip = new AnimationClip { name = "Father_TurnHead", frameRate = 30 };
            SetBoneYawSwing(clip, headPath, headDeg, rise, fall);
            if (!string.IsNullOrEmpty(neckPath)) SetBoneYawSwing(clip, neckPath, neckDeg, rise, fall);
            clip.EnsureQuaternionContinuity();
            return SaveClip(clip);
        }

        static void SetBoneYawSwing(AnimationClip clip, string path, float maxDeg, float rise, float fall)
        {
            if (string.IsNullOrEmpty(path)) return;
            var q0 = Quaternion.Euler(0f, 0f, 0f);
            var q1 = Quaternion.Euler(0f, maxDeg, 0f);
            string[] c = { "x", "y", "z", "w" };
            for (int i = 0; i < 4; i++)
            {
                var curve = new AnimationCurve(
                    new Keyframe(0f, q0[i]),
                    new Keyframe(rise, q1[i]),
                    new Keyframe(rise + fall, q0[i]));
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + c[i], curve);
            }
        }

        // 走路 + 移动：复制 Walk 步态（平铺到整个时长），并在 Root 骨骼上叠加位移/旋转
        static AnimationClip CreateWalkingMoveClip(string name, AnimationClip walkClip, Vector3 fromPos, Vector3 toPos, Quaternion fromRot, Quaternion toRot, float dur)
        {
            var clip = new AnimationClip { name = name, frameRate = 30 };
            if (walkClip != null && walkClip.length > 0f)
            {
                float cycle = walkClip.length;
                foreach (var b in AnimationUtility.GetCurveBindings(walkClip))
                {
                    if (b.path == RootPath) continue; // Root 由下方覆盖
                    var src = AnimationUtility.GetEditorCurve(walkClip, b);
                    clip.SetCurve(b.path, b.type, b.propertyName, TileCurve(src, cycle, dur));
                }
            }
            SetBonePositionCurve(clip, RootPath, fromPos, toPos, dur);
            SetBoneRotationCurve(clip, RootPath, fromRot, toRot, dur);
            clip.EnsureQuaternionContinuity();
            return SaveClip(clip);
        }

        // 站定 clip：复制源动画，把 Root 固定到指定位置/朝向，并把头/颈转到目标角度。
        // loop=true 时把源动画平铺到整个时长（用于 Idle 循环）；false 时只复制一次（用于非循环动作如 PeekTable，超出部分自然保持末帧）。
        static AnimationClip CreateHeldClip(string name, AnimationClip sourceClip, Vector3 rootPos, Quaternion rootRot, string headPath, string neckPath, float headDeg, float neckDeg, float dur, bool loop = true)
        {
            var clip = new AnimationClip { name = name, frameRate = 30 };
            if (sourceClip != null && sourceClip.length > 0f)
            {
                float cycle = sourceClip.length;
                foreach (var b in AnimationUtility.GetCurveBindings(sourceClip))
                {
                    if (b.path == RootPath || b.path == headPath || b.path == neckPath) continue;
                    var src = AnimationUtility.GetEditorCurve(sourceClip, b);
                    clip.SetCurve(b.path, b.type, b.propertyName, loop ? TileCurve(src, cycle, dur) : src);
                }
            }
            SetBonePositionCurve(clip, RootPath, rootPos, rootPos, dur);
            SetBoneRotationCurve(clip, RootPath, rootRot, rootRot, dur);
            if (!string.IsNullOrEmpty(headPath)) SetBoneRotationCurve(clip, headPath, Quaternion.Euler(0f, headDeg, 0f), Quaternion.Euler(0f, headDeg, 0f), dur);
            if (!string.IsNullOrEmpty(neckPath)) SetBoneRotationCurve(clip, neckPath, Quaternion.Euler(0f, neckDeg, 0f), Quaternion.Euler(0f, neckDeg, 0f), dur);
            clip.EnsureQuaternionContinuity();
            return SaveClip(clip);
        }

        // 打量 clip：复制源动画一次 + Root 固定 + 头/颈做上下俯仰摆动（上下打量的感觉）
        static AnimationClip CreateExamineClip(string name, AnimationClip sourceClip, Vector3 rootPos, string headPath, string neckPath, float headPitchDeg, float neckPitchDeg, float dur)
        {
            var clip = new AnimationClip { name = name, frameRate = 30 };
            if (sourceClip != null && sourceClip.length > 0f)
            {
                foreach (var b in AnimationUtility.GetCurveBindings(sourceClip))
                {
                    if (b.path == RootPath || b.path == headPath || b.path == neckPath) continue;
                    var src = AnimationUtility.GetEditorCurve(sourceClip, b);
                    clip.SetCurve(b.path, b.type, b.propertyName, src); // 非循环，只复制一次
                }
            }
            SetBonePositionCurve(clip, RootPath, rootPos, rootPos, dur);
            SetBoneRotationCurve(clip, RootPath, Quaternion.identity, Quaternion.identity, dur);
            if (!string.IsNullOrEmpty(headPath)) SetBonePitchSwing(clip, headPath, headPitchDeg, dur);
            if (!string.IsNullOrEmpty(neckPath)) SetBonePitchSwing(clip, neckPath, neckPitchDeg, dur);
            clip.EnsureQuaternionContinuity();
            return SaveClip(clip);
        }

        // 上下打量：头/颈绕 X 轴俯仰摆动（0 → 低头 → 抬头 → 低头 → 0）
        static void SetBonePitchSwing(AnimationClip clip, string path, float deg, float dur)
        {
            if (string.IsNullOrEmpty(path)) return;
            var q0 = Quaternion.Euler(0f, 0f, 0f);
            var qDown = Quaternion.Euler(-deg, 0f, 0f);
            var qUp = Quaternion.Euler(deg, 0f, 0f);
            string[] c = { "x", "y", "z", "w" };
            for (int i = 0; i < 4; i++)
            {
                var curve = new AnimationCurve(
                    new Keyframe(0f, q0[i]),
                    new Keyframe(dur * 0.25f, qDown[i]),
                    new Keyframe(dur * 0.5f, qUp[i]),
                    new Keyframe(dur * 0.75f, qDown[i]),
                    new Keyframe(dur, q0[i]));
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + c[i], curve);
            }
        }

        static AnimationCurve TileCurve(AnimationCurve src, float cycle, float total)
        {
            if (src == null || src.keys.Length == 0) return new AnimationCurve();
            var keys = src.keys;
            float start = keys[0].time;
            float span = keys[keys.Length - 1].time - start;
            if (span <= 0f) span = cycle;
            var list = new List<Keyframe>();
            float offset = 0f;
            int guard = 0;
            while (offset < total && guard++ < 1000)
            {
                foreach (var k in keys)
                {
                    float nt = k.time - start + offset;
                    if (nt > total + 0.0001f) break;
                    // Adjacent cycles share an endpoint; duplicate times produce invalid samples.
                    if (list.Count > 0 && nt <= list[list.Count - 1].time + 0.00001f) continue;
                    list.Add(new Keyframe(nt, k.value, k.inTangent, k.outTangent, k.inWeight, k.outWeight));
                }
                offset += span;
            }
            return new AnimationCurve(list.ToArray());
        }

        static void SetBonePositionCurve(AnimationClip clip, string path, Vector3 a, Vector3 b, float dur)
        {
            string[] axis = { "x", "y", "z" };
            for (int i = 0; i < 3; i++)
            {
                var curve = new AnimationCurve(
                    new Keyframe(0f, a[i]),
                    new Keyframe(dur, b[i]));
                clip.SetCurve(path, typeof(Transform), "m_LocalPosition." + axis[i], curve);
            }
        }

        static void SetBoneRotationCurve(AnimationClip clip, string path, Quaternion a, Quaternion b, float dur)
        {
            string[] comp = { "x", "y", "z", "w" };
            for (int i = 0; i < 4; i++)
            {
                var curve = new AnimationCurve(
                    new Keyframe(0f, a[i]),
                    new Keyframe(dur, b[i]));
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + comp[i], curve);
            }
            clip.EnsureQuaternionContinuity();
        }
    }
}
#endif
