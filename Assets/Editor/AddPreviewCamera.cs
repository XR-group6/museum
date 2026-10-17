using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds a plain editor camera to the scene so the carousel can be previewed
/// without a VR headset. The scene's only camera is inside the SteamVR rig,
/// which does not render to the Game view when SteamVR is not running, so the
/// editor reports "No cameras rendering".
///
/// Run from the menu (Project3D > Add Preview Camera), or headless:
///   Unity -batchmode -nographics -quit -projectPath shared \
///         -executeMethod AddPreviewCamera.Add
/// </summary>
public static class AddPreviewCamera
{
    const string ScenePath = "Assets/Project3D/Scenes/scene_04_01.unity";
    const string CamName = "Preview Camera";

    [MenuItem("Project3D/Add Preview Camera")]
    public static void Run() => Add();

    public static void Add()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Idempotent: leave an existing preview camera alone.
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == CamName)
            {
                Debug.Log("[AddPreviewCamera] '" + CamName + "' already present, skipping.");
                return;
            }

        GameObject cam = new GameObject(CamName);
        cam.tag = "MainCamera";
        cam.AddComponent<Camera>();
        cam.AddComponent<AudioListener>();

        // Elevated 3/4 view from the south, framing all four houses around (0, 3).
        cam.transform.position = new Vector3(0f, 7f, -4f);
        cam.transform.rotation = Quaternion.LookRotation(
            new Vector3(0f, 2f, 4f) - cam.transform.position);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AddPreviewCamera] added '" + CamName + "' at " + cam.transform.position
            + " looking at (0, 2, 4).");
    }
}
