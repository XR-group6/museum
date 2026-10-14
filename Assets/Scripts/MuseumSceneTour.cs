using System.Collections;
using IncenseCharacters;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene-local configuration; only the tour phase survives Single scene loads.
[DefaultExecutionOrder(10001)]
public sealed class MuseumSceneTour : MonoBehaviour
{
    public int sceneNumber;
    [Min(0)] public float holdSeconds = 3;
    public Scene03Story story;
    public SceneThreeNarration narration;
    public MuseumLeftStickMove movement;
    public MuseumRightStickTurn turning;
    [Min(0), Tooltip("Move the rig forward once when entering free exploration, in metres.")]
    public float explorationSpawnForward = 0.35f;
    [TextArea] public string explorationHintText = "可以使用左摇杆移动，右摇杆可以辅助您调整视角（请不要调整过快以免出现眩晕）";
    [Min(0)] public float explorationHintSeconds = 10;
    VRSubtitle explorationHint;
    static int expectedScene;
    static bool returningToExplore;
    bool loading;
    bool finishingStory;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetTour() { expectedScene = 0; returningToExplore = false; }

    public static int NextScene(int current) => current == 7 ? 5 : current + 1;
    public static bool CanExplore(int current, bool inTour, bool returning) => current == 5 && (!inTour || returning);

    void Awake()
    {
        if (sceneNumber == 3) ResetTour();
        if (sceneNumber == 4) { expectedScene = 4; returningToExplore = false; }
        bool explore = CanExplore(sceneNumber, expectedScene != 0, returningToExplore);
        if (explore && movement && explorationSpawnForward > 0)
        {
            Transform rig = movement.transform;
            Vector3 forward = Vector3.ProjectOnPlane(rig.forward, Vector3.up).normalized;
            var body = rig.GetComponent<CharacterController>();
            bool bodyEnabled = body && body.enabled;
            if (bodyEnabled) body.enabled = false;
            rig.position += forward * explorationSpawnForward;
            if (bodyEnabled) body.enabled = true;
        }
        if (movement) movement.enabled = explore;
        if (turning) turning.enabled = explore;
        if (explore) { expectedScene = 0; returningToExplore = false; }
    }

    IEnumerator Start()
    {
        if (sceneNumber == 5 && movement && movement.enabled)
        {
            explorationHint = new GameObject("Exploration VR Hint").AddComponent<VRSubtitle>();
            explorationHint.transform.SetParent(transform, false);
            explorationHint.followCamera = true;
            explorationHint.cameraOffset = new Vector3(0, 0.35f, 2);
            explorationHint.ShowText(explorationHintText);
            yield return new WaitForSecondsRealtime(explorationHintSeconds);
            if (explorationHint) Destroy(explorationHint.gameObject);
            yield break;
        }
        if (sceneNumber == 3) yield break;
        // Count residence after the new scene has produced its first frame.
        yield return null;
        yield return new WaitForSecondsRealtime(holdSeconds);
        LoadNext();
    }

    void Update()
    {
        // Hold mode can stop at the last evaluated frame, just short of duration.
        // Accept that final frame, then explicitly render the completed cover before loading.
        if (sceneNumber == 3 && !finishingStory && story && story.director &&
            (!narration || narration.ReadyForTransition) &&
            story.director.time >= story.Duration - .05)
        {
            finishingStory = true;
            StartCoroutine(FinishStory());
        }
    }

    void OnDisable()
    {
        if (explorationHint) Destroy(explorationHint.gameObject);
    }

    IEnumerator FinishStory()
    {
        story.director.Pause();
        story.director.time = story.Duration;
        story.director.Evaluate();
        story.Sample(story.Duration);
        yield return null;
        LoadNext();
    }

    void LoadNext()
    {
        if (loading) return;
        int next = NextScene(sceneNumber);
        string name = "scene_" + next.ToString("00");
        if (!Application.CanStreamedLevelBeLoaded(name))
        { Debug.LogError("[Museum tour] Scene absent from Build Settings: " + name, this); enabled = false; return; }
        loading = true; expectedScene = next; returningToExplore = sceneNumber == 7;
        Debug.Log("[Museum tour] " + sceneNumber + " -> " + next + "; exploration=" + returningToExplore);
        SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);
    }
}
