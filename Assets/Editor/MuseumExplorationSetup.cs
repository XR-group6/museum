using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valve.VR;

public static class MuseumExplorationSetup
{
    sealed class ExplorationSettings
    {
        public float speed = 1.2f, moveDeadzone = .2f, gravity = 9.81f, fallReset = 5;
        public float turnSpeed = 60, turnDeadzone = .2f, radius = .2f, skin = .02f, step = .15f, slope = 45;
        public float forward = .35f, hintSeconds = 10;
        public string hint = "可以使用左摇杆移动，右摇杆可以辅助您调整视角（请不要调整过快以免出现眩晕）。试试用右摇杆的激光指向桌上的台胞证并按下扳机。";
        public void Read(MuseumSceneTour flow)
        {
            if (!flow || !flow.movement || !flow.turning) return;
            var move = flow.movement; var turn = flow.turning;
            speed = move.speed; moveDeadzone = move.deadzone; gravity = move.gravity; fallReset = move.fallResetDistance;
            turnSpeed = turn.degreesPerSecond; turnDeadzone = turn.deadzone;
            var body = move.GetComponent<CharacterController>();
            if (body) { radius = body.radius; skin = body.skinWidth; step = body.stepOffset; slope = body.slopeLimit; }
            forward = flow.explorationSpawnForward; hintSeconds = flow.explorationHintSeconds; hint = flow.explorationHintText;
        }
    }

    [MenuItem("Museum/Configure tour and scene seven exploration")]
    public static void Run()
    {
        try
        {
            var report = new List<string>();
            var config = new ExplorationSettings();
            EditorSceneManager.OpenScene("Assets/Project3D/Scenes/scene_07.unity");
            config.Read(UnityEngine.Object.FindObjectOfType<MuseumSceneTour>());
            var oldScene = EditorSceneManager.OpenScene("Assets/Project3D/Scenes/scene_05.unity");
            var oldFlow = UnityEngine.Object.FindObjectOfType<MuseumSceneTour>();
            config.Read(oldFlow);
            foreach (var move in UnityEngine.Object.FindObjectsOfType<MuseumLeftStickMove>(true))
            {
                var body = move.GetComponent<CharacterController>();
                UnityEngine.Object.DestroyImmediate(move);
                if (body) UnityEngine.Object.DestroyImmediate(body);
            }
            foreach (var turn in UnityEngine.Object.FindObjectsOfType<MuseumRightStickTurn>(true)) UnityEngine.Object.DestroyImmediate(turn);
            int removed = 0;
            foreach (var root in oldScene.GetRootGameObjects())
            {
                if (root.name == "Exploration edge guards") { UnityEngine.Object.DestroyImmediate(root); continue; }
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) != "Assets/Project3D/Prefabs/scenes/scene_05.prefab") continue;
                foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
                    if (!collider.isTrigger && PrefabUtility.IsAddedComponentOverride(collider))
                    { UnityEngine.Object.DestroyImmediate(collider); removed++; }
            }
            if (oldFlow) { oldFlow.movement = null; oldFlow.turning = null; oldFlow.explorationHintText = ""; oldFlow.explorationHintSeconds = 0; oldFlow.explorationSpawnForward = 0; EditorUtility.SetDirty(oldFlow); }
            EditorSceneManager.MarkSceneDirty(oldScene); EditorSceneManager.SaveScene(oldScene);
            report.Add("scene_05 exploration components, hint settings and edge guards removed; removed scene-added mesh colliders=" + removed);
            for (int number = 3; number <= 7; number++)
            {
                string path = "Assets/Project3D/Scenes/scene_" + number.ToString("00") + ".unity";
                if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path)) throw new Exception("Scene not enabled: " + path);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var flow = UnityEngine.Object.FindObjectOfType<MuseumSceneTour>();
                if (!flow) flow = new GameObject("Museum tour progression").AddComponent<MuseumSceneTour>();
                flow.sceneNumber = number; flow.holdSeconds = 3;
                if (number == 3) flow.story = UnityEngine.Object.FindObjectOfType<IncenseCharacters.Scene03Story>();
                if (number == 3 && (!flow.story || !flow.story.director)) throw new Exception("Scene three story missing");
                if (number == 7)
                {
                    var left = UnityEngine.Object.FindObjectsOfType<SteamVR_Behaviour_Pose>(true).Single(p => p.inputSource == SteamVR_Input_Sources.LeftHand);
                    var right = UnityEngine.Object.FindObjectsOfType<SteamVR_Behaviour_Pose>(true).Single(p => p.inputSource == SteamVR_Input_Sources.RightHand);
                    var rig = left.transform.parent;
                    var camera = rig.GetComponentInChildren<Camera>(true);
                    if (!camera || !right.transform.IsChildOf(rig)) throw new Exception("Shared SteamVR rig missing");
                    if ((rig.lossyScale - Vector3.one).sqrMagnitude > .001f) throw new Exception("Rig must use metre scale");
                    var body = rig.GetComponent<CharacterController>();
                    if (!body) body = rig.gameObject.AddComponent<CharacterController>();
                    body.radius = config.radius; body.height = 1.7f; body.center = new Vector3(0, .85f, 0);
                    body.skinWidth = config.skin; body.minMoveDistance = 0; body.stepOffset = config.step; body.slopeLimit = config.slope;
                    flow.movement = rig.GetComponent<MuseumLeftStickMove>();
                    if (!flow.movement) flow.movement = rig.gameObject.AddComponent<MuseumLeftStickMove>();
                    flow.movement.headset = camera.transform; flow.movement.leftHand = left;
                    flow.turning = rig.GetComponent<MuseumRightStickTurn>();
                    if (!flow.turning) flow.turning = rig.gameObject.AddComponent<MuseumRightStickTurn>();
                    flow.turning.headset = camera.transform; flow.turning.rightHand = right;
                    flow.movement.speed = config.speed; flow.movement.deadzone = config.moveDeadzone;
                    flow.movement.gravity = config.gravity; flow.movement.fallResetDistance = config.fallReset;
                    flow.turning.degreesPerSecond = config.turnSpeed; flow.turning.deadzone = config.turnDeadzone;
                    flow.explorationSpawnForward = config.forward; flow.explorationHintText = config.hint; flow.explorationHintSeconds = config.hintSeconds;
                    // Scene seven unlocks controls and the hint only after its three-second view.
                    flow.movement.enabled = flow.turning.enabled = false;
                    int added = 0;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) != "Assets/Project3D/Prefabs/scenes/scene_07.prefab") continue;
                        foreach (var mesh in root.GetComponentsInChildren<MeshFilter>(true))
                        {
                            // The sea is a visual surface, not walkable ground.
                            if (mesh.GetComponentsInParent<Transform>().Any(t => t.name == "sea")) continue;
                            if (!mesh.sharedMesh || mesh.GetComponent<Collider>() || mesh.GetComponentInParent<Rigidbody>()) continue;
                            var collider = mesh.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = mesh.sharedMesh; added++;
                        }
                    }
                    Physics.SyncTransforms();
                    var groundHits = Physics.RaycastAll(rig.position + Vector3.up * .5f, Vector3.down, 5)
                        .Where(h => !h.collider.isTrigger && !(h.collider is CharacterController)).OrderBy(h => h.distance).ToArray();
                    if (groundHits.Length == 0) throw new Exception("No floor under scene-seven spawn");
                    body.enabled = false;
                    rig.position = new Vector3(rig.position.x, groundHits[0].point.y + .01f, rig.position.z);
                    body.enabled = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
                    report.Add("scene_07 rig=" + rig.position + ", floor=" + groundHits[0].collider.name + ", new mesh colliders=" + added);
                    // Keep people inside the main room's ground extents, including any open outer edges.
                    var floor = groundHits[0].collider.bounds;
                    report.Add("Spawn floor bounds=" + floor);
                    var terrain = UnityEngine.Object.FindObjectsOfType<MeshCollider>().Where(c => c.GetComponentsInParent<Transform>().Any(t => t.name == "ground")).ToArray();
                    if (terrain.Length == 0) throw new Exception("Outdoor exploration ground missing");
                    var terrainBounds = terrain[0].bounds;
                    foreach (var collider in terrain.Skip(1)) terrainBounds.Encapsulate(collider.bounds);
                    AddBoundary(scene, terrainBounds, groundHits[0].point.y);
                    report.Add("Outdoor edge guards bounds=" + terrainBounds + "; sea excluded from walkable collision; fall recovery enabled");
                    var stairMesh = UnityEngine.Object.FindObjectsOfType<MeshFilter>().FirstOrDefault(m => m.name == "stair");
                    if (stairMesh && stairMesh.sharedMesh)
                    {
                        var vertices = stairMesh.sharedMesh.vertices; var triangles = stairMesh.sharedMesh.triangles;
                        var heights = new List<float>();
                        for (int i = 0; i < triangles.Length; i += 3)
                        {
                            var a = stairMesh.transform.TransformPoint(vertices[triangles[i]]);
                            var b = stairMesh.transform.TransformPoint(vertices[triangles[i + 1]]);
                            var c = stairMesh.transform.TransformPoint(vertices[triangles[i + 2]]);
                            if (Vector3.Cross(b - a, c - a).normalized.y > .95f) heights.Add(Mathf.Round(a.y * 1000) / 1000);
                        }
                        var levels = heights.Distinct().OrderBy(y => y).ToArray();
                        var rises = levels.Skip(1).Select((y, i) => y - levels[i]).Where(y => y > .08f && y < .3f).OrderBy(y => y).ToArray();
                        if (rises.Length > 0) body.stepOffset = Mathf.Max(body.stepOffset, rises[rises.Length - 1] + .02f);
                        report.Add("Stair horizontal levels=" + string.Join(",", levels.Select(y => y.ToString("F3"))) + "; controller stepOffset=" + body.stepOffset);
                    }
                    EditorUtility.SetDirty(flow.movement); EditorUtility.SetDirty(flow.turning);
                }
                int missing = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true)
                    .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
                if (missing != 0) throw new Exception(path + " missing scripts=" + missing);
                EditorUtility.SetDirty(flow);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                report.Add(path + ": references and missing-script check PASS");
            }
            Validate(report);
            File.WriteAllLines("MuseumExplorationValidation.txt", report);
            AssetDatabase.SaveAssets();
            Debug.Log("MUSEUM_EXPLORATION_VALIDATION_PASS\n" + string.Join("\n", report));
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
    }

    static void AddBoundary(UnityEngine.SceneManagement.Scene scene, Bounds bounds, float groundHeight)
    {
        var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Exploration edge guards");
        if (root) return;
        root = new GameObject("Exploration edge guards");
        for (int i = 0; i < 4; i++)
        {
            var wall = new GameObject("Edge " + i); wall.transform.SetParent(root.transform);
            bool x = i < 2; float sign = i % 2 == 0 ? -1 : 1;
            wall.transform.position = new Vector3(x ? bounds.center.x + sign * bounds.extents.x : bounds.center.x,
                groundHeight + 1.5f, x ? bounds.center.z : bounds.center.z + sign * bounds.extents.z);
            var box = wall.AddComponent<BoxCollider>();
            box.size = x ? new Vector3(.1f, 3, bounds.size.z) : new Vector3(bounds.size.x, 3, .1f);
        }
    }

    static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    static void Validate(List<string> report)
    {
        Check(MuseumSceneTour.NextScene(3) == 4 && MuseumSceneTour.NextScene(4) == 5 && MuseumSceneTour.NextScene(5) == 6 &&
              MuseumSceneTour.NextScene(6) == 7 && MuseumSceneTour.NextScene(7) == 0, "Tour order");
        Check(!MuseumSceneTour.CanExplore(5) && MuseumSceneTour.CanExplore(7) && !MuseumSceneTour.CanExplore(4), "Tour exploration gate");
        Check(MuseumLeftStickMove.CalculateVelocity(new Vector2(.1f, 0), Vector3.forward, 1.2f, .2f) == Vector3.zero, "Move deadzone");
        Check(Vector3.Distance(MuseumLeftStickMove.CalculateVelocity(Vector2.up, new Vector3(0, -.8f, .6f), 1.2f, .2f), Vector3.forward * 1.2f) < .001f, "Horizontal move");
        Check(MuseumLeftStickMove.CalculateVelocity(Vector2.one, Vector3.forward, 1.2f, .2f).magnitude <= 1.201f, "Diagonal limit");
        Check(Vector3.Distance(MuseumLeftStickMove.CalculateVelocity(Vector2.down, Vector3.forward, 1.2f, .2f), Vector3.back * 1.2f) < .001f &&
              Vector3.Distance(MuseumLeftStickMove.CalculateVelocity(Vector2.left, Vector3.forward, 1.2f, .2f), Vector3.left * 1.2f) < .001f &&
              Vector3.Distance(MuseumLeftStickMove.CalculateVelocity(Vector2.right, Vector3.right, 1.2f, .2f), Vector3.back * 1.2f) < .001f, "Backward and strafe directions");
        var testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var rig = new GameObject("ValidationRig"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig, testScene);
            rig.transform.position = new Vector3(1000, 0, 1000);
            var body = rig.AddComponent<CharacterController>(); body.radius = .2f; body.skinWidth = .02f; body.minMoveDistance = 0;
            var head = new GameObject("OffsetHead"); head.transform.SetParent(rig.transform, false); head.transform.localPosition = new Vector3(.6f, 1.7f, .4f);
            // Add disabled, so editor tests exercise math/physics without starting hardware input.
            var move = rig.AddComponent<MuseumLeftStickMove>(); move.enabled = false; move.headset = head.transform; move.gravity = 0;
            var turn = rig.AddComponent<MuseumRightStickTurn>(); turn.enabled = false; turn.headset = head.transform;
            var position = head.transform.position;
            turn.TurnAxis(new Vector2(.1f, 1), 1);
            Check(Quaternion.Angle(rig.transform.rotation, Quaternion.identity) < .01f, "Turn deadzone/Y ignored");
            for (int i = 0; i < 60; i++) turn.TurnAxis(Vector2.right, 1f / 60);
            Check(Mathf.Abs(Mathf.DeltaAngle(rig.transform.eulerAngles.y, 60)) < .02f && Vector3.Distance(position, head.transform.position) < .003f && body.enabled, "Turn pivot/controller restore");
            var rotation = rig.transform.rotation; turn.TurnAxis(Vector2.zero, 1);
            Check(Quaternion.Angle(rotation, rig.transform.rotation) < .01f, "Immediate stop");
            for (int i = 0; i < 30; i++) turn.TurnAxis(Vector2.left, 1f / 30);
            Check(Quaternion.Angle(rig.transform.rotation, Quaternion.identity) < .02f, "Frame rate independence");
            turn.TurnAxis(new Vector2(.5f, 0), 1);
            Check(Mathf.Abs(Mathf.DeltaAngle(rig.transform.eulerAngles.y, 22.5f)) < .02f, "Analog turn");
            var velocity = MuseumLeftStickMove.CalculateVelocity(Vector2.up, head.transform.forward, 1.2f, .2f);
            Check(Vector3.Distance(velocity, head.transform.forward * 1.2f) < .001f, "Move after turn");
            rig.transform.rotation = Quaternion.identity; head.transform.localPosition = new Vector3(0, 1.7f, 0);
            rig.transform.position = new Vector3(1000, 0, 1000);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(wall, testScene);
            wall.transform.position = new Vector3(1000, 1, 1002); wall.transform.localScale = new Vector3(5, 3, 1);
            Physics.SyncTransforms();
            for (int i = 0; i < 5; i++) move.MoveAxis(Vector2.up, .1f);
            Check(Mathf.Abs(rig.transform.position.z - 1000.6f) < .02f, "Real CharacterController free displacement");
            for (int i = 0; i < 30; i++) move.MoveAxis(Vector2.up, .1f);
            float distance = rig.transform.position.z - 1000;
            Check(distance > 1.1f && distance < 1.4f, "Real CharacterController wall blocking");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor, testScene);
            floor.transform.position = new Vector3(1000, -.05f, 1000); floor.transform.localScale = new Vector3(10, .1f, 10);
            body.enabled = false; rig.transform.position = new Vector3(1000, 1, 1000); body.enabled = true;
            move.gravity = 9.81f; Physics.SyncTransforms();
            for (int i = 0; i < 120; i++) move.MoveAxis(Vector2.zero, 1f / 60);
            Check(rig.transform.position.y > -.05f && rig.transform.position.y < .05f && body.isGrounded, "Gravity and actual floor contact");
            report.Add("Unity simulated input PASS: deadzones, horizontal/diagonal movement, 30/60 Hz continuous turn, analog rate, immediate stop, offset pivot, controller restore, heading after turn, actual CharacterController displacement and wall blocking=" + distance);
            report.Add("Actual CharacterController gravity and floor grounding PASS");
        }
        finally { EditorSceneManager.CloseScene(testScene, true); }
        report.Add("Tour order and exploration gate PASS. Headset/controllers, visual comfort and original right-hand interactions still require hardware testing.");
    }
}
