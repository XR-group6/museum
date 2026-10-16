using System.Collections;
using IncenseCharacters;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene 3 enters the four-era room. Legacy views remain available for previews;
// scene 7 unlocks the existing free-exploration controls after its timed view.
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
    [TextArea] public string explorationHintText = "可以使用左摇杆移动，右摇杆可以辅助您调整视角（请不要调整过快以免出现眩晕）。试试用右摇杆的激光指向桌上的台胞证并按下扳机。";
    [Min(0)] public float explorationHintSeconds = 10;
    VRSubtitle explorationHint;
    public bool ExplorationStarted { get; private set; }
    bool loading;
    bool finishingStory;

    public static int NextScene(int current) => current >= 7 ? 0 : current + 1;
    public static string NextSceneName(int current) => current == 3 ? "scene_04_01" :
        NextScene(current) == 0 ? null : "scene_" + NextScene(current).ToString("00");
    public static bool CanExplore(int current) => current == 7;

    void Awake()
    {
        if (movement) movement.enabled = false;
        if (turning) turning.enabled = false;
    }

    void BeginExploration()
    {
        if (ExplorationStarted || !CanExplore(sceneNumber)) return;
        if (!movement || !turning)
        { Debug.LogError("[Museum tour] Scene seven exploration references are missing.", this); return; }
        if (explorationSpawnForward > 0)
        {
            Transform rig = movement.transform;
            Vector3 forward = Vector3.ProjectOnPlane(rig.forward, Vector3.up).normalized;
            var body = rig.GetComponent<CharacterController>();
            bool bodyEnabled = body && body.enabled;
            if (bodyEnabled) body.enabled = false;
            rig.position += forward * explorationSpawnForward;
            if (bodyEnabled) body.enabled = true;
        }
        movement.enabled = turning.enabled = true;
        ExplorationStarted = true;
        Debug.Log("[Museum tour] Scene 7 exploration enabled after " + holdSeconds + " seconds.", this);
    }

    IEnumerator Start()
    {
        if (sceneNumber == 3) yield break;
        // Scene seven is immediately interactive; legacy preview scenes keep their hold.
        if (!CanExplore(sceneNumber))
        {
            yield return null;
            yield return new WaitForSecondsRealtime(holdSeconds);
        }
        if (CanExplore(sceneNumber))
        {
            BeginExploration();
            if (!ExplorationStarted) yield break;
            explorationHint = new GameObject("Exploration VR Hint").AddComponent<VRSubtitle>();
            explorationHint.transform.SetParent(transform, false);
            explorationHint.followCamera = true;
            explorationHint.cameraOffset = new Vector3(0, 0.35f, 2);
            explorationHint.ShowText(explorationHintText);
            yield return new WaitForSecondsRealtime(explorationHintSeconds);
            if (explorationHint) Destroy(explorationHint.gameObject);
            yield break;
        }
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
        string name = NextSceneName(sceneNumber);
        if (string.IsNullOrEmpty(name)) return;
        if (!Application.CanStreamedLevelBeLoaded(name))
        { Debug.LogError("[Museum tour] Scene absent from Build Settings: " + name, this); enabled = false; return; }
        loading = true;
        Debug.Log("[Museum tour] " + sceneNumber + " -> " + name);
        SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);
    }
}
