using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Makes the team's carousel scene (scene_04_01) viewable from a keyboard and mouse,
/// with no headset, and reports what the scene actually contains.
///
/// Three things stop it playing as authored, and this changes only those:
///   1. the scene's only camera is inside [CameraRig], and that instance is
///      deactivated -- so the Game view renders nothing at all;
///   2. the 4in1 houses carry no colliders, so a walker passes straight through them;
///   3. MuseumSceneTour advances the tour after 3 seconds (sceneNumber 4 -> scene_05),
///      which would eject you before you had looked at anything.
///
/// Everything it adds is named, so it is easy to strip back off: the player, the floor,
/// the colliders, and one raised holdSeconds.
///
///   Unity -batchmode -nographics -quit -projectPath shared \
///         -executeMethod AddDesktopTestPlayer.Add
/// </summary>
public static class AddDesktopTestPlayer
{
    const string ScenePath = "Assets/Project3D/Scenes/scene_04_01.unity";
    const string PlayerName = "Desktop Test Player";
    const string HeadName = "Head";
    const string FloorName = "Desktop Test Floor";
    const string HouseAsset = "Assets/Effect/Mesh/4in1.fbx";

    /// <summary>Meshes further than this from the middle of the plaza stay scenery, not floor.</summary>
    const float ColliderRange = 30f;

    [MenuItem("Project3D/View team carousel on desktop (scene_04_01)")]
    public static void Run() => Add();

    public static void Add()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // The player and the floor are made by this tool and nothing else, so a re-run
        // replaces them rather than leaving two of everything.
        foreach (Transform stale in Object.FindObjectsOfType<Transform>(true)
                     .Where(t => t.name == PlayerName || t.name == FloorName).ToArray())
            Object.DestroyImmediate(stale.gameObject);

        Inspect(scene);

        List<GameObject> houses = Houses();
        if (houses.Count == 0)
            Debug.LogError("[DesktopTest] no instances of " + HouseAsset + " in the scene.");

        Vector3 centre = houses.Count > 0 ? Centre(houses) : Vector3.zero;
        float baseY = houses.Count > 0 ? houses.Min(h => RenderBounds(h).min.y) : 0f;
        float ring = 6f;
        foreach (GameObject house in houses)
            ring = Mathf.Max(ring, Horizontal(RenderBounds(house).center - centre));
        // Stand outside the ring, not in the middle: the four houses are placed through
        // one another (see the report), so the centre of this plaza is inside a wall.
        Vector3 spawn = centre + Vector3.back * (ring + 6f);

        int solids = AddColliders(centre);
        Physics.SyncTransforms();
        bool floor = AddFloor(centre, spawn, baseY, ring);
        NeutraliseAutoAdvance();

        GameObject player = MakePlayer(houses, centre, spawn, baseY, ring);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DesktopTest] done: " + houses.Count + " houses, " + solids
            + " mesh colliders added near the plaza, floor " + (floor ? "added" : "already solid")
            + ", player at " + player.transform.position
            + ". Press Play: WASD walk, hold right mouse to look, 1-4 jump to a house.");
    }

    /// <summary>
    /// Says what the scene is, before touching it: which of the many objects the team
    /// placed are houses, whether those houses overlap each other, what the four
    /// captions say, and what the tour and the timeline do on their own.
    /// </summary>
    static void Inspect(Scene scene)
    {
        var log = new System.Text.StringBuilder();
        log.Append("\n[DesktopTest] ---------- what this scene is ----------\n");

        log.Append("cameras:\n");
        var cameras = Object.FindObjectsOfType<Camera>(true);
        if (cameras.Length == 0) log.Append("   (none)\n");
        foreach (Camera cam in cameras)
            log.Append("   " + Path(cam.transform) + "  enabled=" + cam.enabled
                + " rendering=" + (cam.enabled && cam.gameObject.activeInHierarchy)
                + "  at " + cam.transform.position + "\n");

        log.Append("prefab instances:\n");
        foreach (Transform t in Object.FindObjectsOfType<Transform>(true))
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
            log.Append("   " + t.name.PadRight(18)
                + Asset(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)).PadRight(30)
                + " active=" + t.gameObject.activeSelf + "  at " + t.transform.position
                + "  size " + Size(RenderBounds(t.gameObject)) + "\n");
        }

        List<GameObject> houses = Houses();
        log.Append("houses (" + houses.Count + ", in chapter order -- 1-4 jump to these):\n");
        for (int i = 0; i < houses.Count; i++)
        {
            GameObject house = houses[i];
            log.Append("   " + (i + 1) + ". " + house.name.PadRight(12) + " " + Size(RenderBounds(house))
                + "  at " + RenderBounds(house).center + "   caption: " + Caption(house) + "\n");
        }
        for (int i = 0; i < houses.Count; i++)
            for (int j = i + 1; j < houses.Count; j++)
            {
                Bounds a = RenderBounds(houses[i]), b = RenderBounds(houses[j]);
                float x = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
                float z = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
                log.Append("   overlap " + houses[i].name + " / " + houses[j].name
                    + ": x=" + x.ToString("F2") + " z=" + z.ToString("F2")
                    + (x > 0f && z > 0f ? "   <-- CLASH" : "   clear") + "\n");
            }

        log.Append("captions:\n");
        foreach (Text text in Object.FindObjectsOfType<Text>(true))
        {
            Canvas canvas = text.GetComponentInParent<Canvas>();
            log.Append("   [" + (canvas ? canvas.renderMode.ToString() : "no canvas")
                + "] at " + text.transform.position + " : " + Flat(text.text) + "\n");
        }

        MuseumSceneTour tour = Object.FindObjectsOfType<MuseumSceneTour>(true).FirstOrDefault();
        if (!tour) log.Append("tour: none\n");
        else
        {
            int next = MuseumSceneTour.NextScene(tour.sceneNumber);
            string name = "scene_" + next.ToString("00");
            log.Append("tour: sceneNumber=" + tour.sceneNumber + " holdSeconds=" + tour.holdSeconds
                + " -> then loads " + (next == 0 ? "(nothing, it is the last)" : name)
                + (next != 0 && !InBuildSettings(name) ? " -- not in build settings" : "")
                + "\n");
        }

        PlayableDirector director = Object.FindObjectsOfType<PlayableDirector>(true).FirstOrDefault();
        log.Append("timeline: " + (director
            ? (director.playableAsset ? director.playableAsset.name : "?")
              + "  duration=" + director.duration.ToString("F2") + "s"
              + "  wrap=" + director.extrapolationMode
              + "  playsOnAwake=" + director.playOnAwake
            : "none") + "\n");

        Debug.Log(log.ToString());
    }

    /// <summary>
    /// Every instance of the house FBX, ordered by the chapter its caption tells, so 1-4
    /// runs 1951, 1960, 1970, 1980 -- the order the story is told in. The houses are
    /// pinwheeled, so ordering by angle would walk you round the plaza in no useful order.
    /// </summary>
    static List<GameObject> Houses()
    {
        var found = new List<GameObject>();
        foreach (Transform t in Object.FindObjectsOfType<Transform>(true))
            if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)
                && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == HouseAsset)
                found.Add(t.gameObject);
        if (found.Count < 2) return found;
        return found.OrderBy(Chapter).ToList();
    }

    /// <summary>"Scene 3: ..." -> 3. Uncaptioned houses sort last rather than first.</summary>
    static int Chapter(GameObject house)
    {
        Text caption = NearestCaption(house);
        if (caption)
        {
            var match = System.Text.RegularExpressions.Regex.Match(caption.text, @"Scene\s+(\d+)");
            if (match.Success) return int.Parse(match.Groups[1].Value);
        }
        return 99;
    }

    static Vector3 Centre(List<GameObject> objects)
    {
        Vector3 sum = Vector3.zero;
        foreach (GameObject o in objects) sum += RenderBounds(o).center;
        return sum / Mathf.Max(1, objects.Count);
    }

    /// <summary>
    /// The houses arrive from the pipelinethe team's own FBX with no collision of any
    /// kind, so a CharacterController walks through the walls and off the edge. Collide
    /// what is near the plaza and leave the distant scenery alone.
    /// </summary>
    static int AddColliders(Vector3 centre)
    {
        int added = 0;
        foreach (MeshFilter filter in Object.FindObjectsOfType<MeshFilter>(true))
        {
            if (!filter.sharedMesh || !filter.gameObject.activeInHierarchy) continue;
            if (filter.GetComponent<Collider>()) continue;
            if (filter.GetComponentInParent<Rigidbody>()) continue;
            if ((RenderBounds(filter.gameObject).center - centre).magnitude > ColliderRange) continue;
            try
            {
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                added++;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[DesktopTest] could not collide '" + Path(filter.transform) + "': " + e.Message);
            }
        }
        return added;
    }

    /// <summary>
    /// A floor, if the plaza has nothing solid to stand on. The carousel was authored for
    /// a headset with teleport/room-scale movement, so nothing here guarantees ground --
    /// and the team's scene has the harbour ground switched off, so at the spawn there is
    /// none. The test is made where the player will stand, not at the middle of the
    /// plaza: the middle is inside an overlapping house, which reads as solid ground.
    /// </summary>
    static bool AddFloor(Vector3 centre, Vector3 spawn, float baseY, float ring)
    {
        if (Physics.Raycast(new Vector3(spawn.x, baseY + 5f, spawn.z), Vector3.down, 10f,
                ~0, QueryTriggerInteraction.Ignore))
            return false;

        var floor = new GameObject(FloorName);
        floor.transform.position = new Vector3(centre.x, baseY - 0.05f, centre.z);
        var box = floor.AddComponent<BoxCollider>();
        float size = 2f * (ring + 20f);
        box.size = new Vector3(size, 0.1f, size);
        return true;
    }

    /// <summary>
    /// The tour is the team's timed slide show: three seconds, then the next scene. Left
    /// alone it ends the review before it starts, so the hold is raised and said out loud
    /// rather than the component being disabled -- a number is easier to put back.
    /// </summary>
    static void NeutraliseAutoAdvance()
    {
        foreach (MuseumSceneTour tour in Object.FindObjectsOfType<MuseumSceneTour>(true))
        {
            if (tour.holdSeconds >= 600f) continue;
            int next = MuseumSceneTour.NextScene(tour.sceneNumber);
            Debug.Log("[DesktopTest] '" + Path(tour.transform) + "' would load scene_"
                + next.ToString("00") + " after " + tour.holdSeconds
                + "s (the team's timed tour). holdSeconds raised to 3600 so you can stay and look;"
                + " set it back to " + tour.holdSeconds + " to restore the tour.");
            tour.holdSeconds = 3600f;
        }
    }

    static GameObject MakePlayer(List<GameObject> houses, Vector3 centre, Vector3 spawn, float baseY, float ring)
    {
        var player = new GameObject(PlayerName);

        var body = player.AddComponent<CharacterController>();
        body.radius = 0.3f;
        body.height = 1.7f;
        body.center = new Vector3(0f, 0.85f, 0f);
        body.skinWidth = 0.02f;
        body.minMoveDistance = 0f;
        body.stepOffset = 0.35f;
        body.slopeLimit = 50f;

        var head = new GameObject(HeadName);
        head.transform.SetParent(player.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        head.tag = "MainCamera";

        Camera camera = head.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 1000f;
        camera.fieldOfView = 60f;
        head.AddComponent<AudioListener>();

        var driver = player.AddComponent<DesktopMuseumDriver>();
        driver.head = head.transform;
        driver.focusPoints = houses.Select(h => h.transform).ToArray();
        // These focus points are whole houses, so 1-4 stand outside the ring and look in
        // rather than walking into a wall -- a house has no "front" a metre from its middle.
        driver.focusStandoff = ring + 4f;

        // Outside the ring, just above the plaza, facing the middle. The 1-4 keys do the
        // close-up work from there.
        player.transform.position = new Vector3(spawn.x, baseY + 0.5f, spawn.z);
        player.transform.rotation = Quaternion.LookRotation(
            new Vector3(centre.x - spawn.x, 0f, centre.z - spawn.z));

        Physics.SyncTransforms();
        if (!driver.DropToFloor())
            Debug.LogWarning("[DesktopTest] nothing solid under the spawn; the walker will fall "
                + "and recover until the floor is reached. Check the floor collider.");
        return player;
    }

    /// <summary>
    /// Confirms the three fixes without starting play mode.
    ///   Unity -batchmode -nographics -quit -projectPath shared \
    ///         -executeMethod AddDesktopTestPlayer.Verify
    /// </summary>
    public static void Verify()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Physics.SyncTransforms();

        DesktopMuseumDriver driver = Object.FindObjectsOfType<DesktopMuseumDriver>(true).FirstOrDefault();
        Camera[] live = Object.FindObjectsOfType<Camera>(true)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy).ToArray();
        List<GameObject> houses = Houses();
        MuseumSceneTour tour = Object.FindObjectsOfType<MuseumSceneTour>(true).FirstOrDefault();

        Collider self = driver ? driver.GetComponent<Collider>() : null;
        bool grounded = driver && Physics.RaycastAll(driver.transform.position + Vector3.up * 0.3f,
                Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore).Any(h => h.collider != self);

        Debug.Log("[DesktopTest] VERIFY player=" + (driver ? driver.transform.position.ToString() : "(none)")
            + " liveCameras=" + live.Length + " houses=" + houses.Count
            + " focusPoints=" + (driver && driver.focusPoints != null ? driver.focusPoints.Length : 0)
            + " grounded=" + grounded
            + " holdSeconds=" + (tour ? tour.holdSeconds.ToString("F0") : "no tour"));

        bool ok = driver && live.Length == 1 && houses.Count > 0
            && driver.focusPoints != null && driver.focusPoints.Length == houses.Count
            && grounded && tour && tour.holdSeconds > 600f;
        if (ok) Debug.Log("[DesktopTest] VERIFY PASS: one rendering camera, a floor under the spawn, "
            + "all four houses wired, and the tour will not eject you.");
        else Debug.LogError("[DesktopTest] VERIFY FAIL: see the line above.");
    }

    // ---- small helpers -------------------------------------------------------

    /// <summary>
    /// Whether a scene name is in Build Settings. Not Application.CanStreamedLevelBeLoaded:
    /// that answers about a *built player* and returns false in batchmode for scenes the
    /// editor can load perfectly well, which reads as a broken tour when the tour is fine.
    /// </summary>
    static bool InBuildSettings(string sceneName)
    {
        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            if (entry.enabled
                && System.IO.Path.GetFileNameWithoutExtension(entry.path) == sceneName)
                return true;
        return false;
    }

    static Bounds RenderBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(r => !(r is LineRenderer) && !(r is TrailRenderer)).ToArray();
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    /// <summary>The caption nearest a house, which is how the team labels the four eras.</summary>
    static Text NearestCaption(GameObject house)
    {
        Vector3 at = RenderBounds(house).center;
        Text best = null;
        float bestSquared = float.MaxValue;
        foreach (Text text in Object.FindObjectsOfType<Text>(true))
        {
            float squared = (text.transform.position - at).sqrMagnitude;
            if (squared < bestSquared) { bestSquared = squared; best = text; }
        }
        return best;
    }

    static string Caption(GameObject house)
    {
        Text best = NearestCaption(house);
        return best ? Flat(best.text) : "(none)";
    }

    static string Size(Bounds bounds) =>
        bounds.size.x.ToString("F1") + " x " + bounds.size.y.ToString("F1") + " x " + bounds.size.z.ToString("F1") + " m";

    static float Horizontal(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    static string Flat(string text)
    {
        if (string.IsNullOrEmpty(text)) return "(empty)";
        string line = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return line.Length > 96 ? line.Substring(0, 96) + "..." : line;
    }

    static string Path(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    static string Asset(string path) => string.IsNullOrEmpty(path) ? "(scene object)" : System.IO.Path.GetFileName(path) + " <" + path + ">";
}
