using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Playables;

// Added to the exported ocean scene by the build processor.
public sealed class MuseumOceanFrameReport : MonoBehaviour
{
#if !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Register()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name != "scene_06_01") return;
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<MuseumOceanFrameReport>(true)) return;
        var report = new GameObject("Ocean performance report");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(report, scene);
        report.AddComponent<MuseumOceanFrameReport>();
        Debug.Log("[Ocean performance] recording started");
    }
#endif
    readonly FrameTiming[] timings = new FrameTiming[1];
    MuseumJourney journey;
    PlayableDirector director;
    float elapsed, total, worst;
    int frames;
    void Start()
    {
        journey = FindObjectOfType<MuseumJourney>();
        director = journey ? journey.oceanTimeline : null;
    }
    void Update()
    {
        FrameTimingManager.CaptureFrameTimings();
        float milliseconds = Time.unscaledDeltaTime * 1000;
        total += milliseconds;
        worst = Mathf.Max(worst, milliseconds);
        frames++;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed < 3) return;
        uint count = FrameTimingManager.GetLatestTimings(1, timings);
        Debug.Log("[Ocean performance] timeline=" + (director ? director.time.ToString("F1") : "none") +
            " waiting=" + (journey && journey.WaitingForCenser) +
            " frameMs=" + (total / frames).ToString("F1") + " worstMs=" + worst.ToString("F1") +
            " cpuMs=" + (count > 0 ? timings[0].cpuFrameTime.ToString("F1") : "unavailable") +
            " gpuMs=" + (count > 0 ? timings[0].gpuFrameTime.ToString("F1") : "unavailable") +
            " memoryMB=" + (Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)));
        elapsed = total = worst = 0;
        frames = 0;
    }
}
