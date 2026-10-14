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
    public MuseumLeftStickMove movement;
    public MuseumRightStickTurn turning;
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
        if (movement) movement.enabled = explore;
        if (turning) turning.enabled = explore;
        if (explore) { expectedScene = 0; returningToExplore = false; }
    }

    IEnumerator Start()
    {
        if (sceneNumber == 3 || (sceneNumber == 5 && movement && movement.enabled)) yield break;
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
            story.director.time >= story.Duration - .05)
        {
            finishingStory = true;
            StartCoroutine(FinishStory());
        }
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
