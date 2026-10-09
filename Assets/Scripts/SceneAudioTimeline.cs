using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

[RequireComponent(typeof(PlayableDirector))]
public class SceneAudioTimeline : MonoBehaviour
{
    public TimelineAsset timeline;
    public AudioClip ambience;
    public bool playOnStart;
    public string nextScene;
    [Tooltip("Optional animation director that determines when this scene is finished.")]
    public PlayableDirector completionDirector;
    [Min(0)] public float delayAfterAnimation = 3f;
    private AudioSource source;
    private PlayableDirector director;
    private bool requested, playing, loading;
    private double duration;
    private double animationDuration;
    private bool audioFinished, animationFinished;
    private float remainingDelay;

    void Awake()
    {
        if (completionDirector) completionDirector.playOnAwake = false;
    }

    IEnumerator Start()
    {
        director = GetComponent<PlayableDirector>();
        director.timeUpdateMode = DirectorUpdateMode.DSPClock;
        director.extrapolationMode = DirectorWrapMode.Hold;
        director.playOnAwake = false;
        source = new GameObject("SequenceAudio").AddComponent<AudioSource>();
        source.transform.SetParent(transform, false);
        source.playOnAwake = false; source.spatialBlend = 0; source.volume = 1; source.mute = false;
        if (AudioListener.pause || AudioListener.volume == 0)
            Debug.LogWarning("[Museum] Global listener paused or muted; check audio settings.", this);
        if (ambience)
        {
            yield return Load(ambience);
            if (ambience.loadState != AudioDataLoadState.Loaded) yield break;
            if (!requested)
            {
                source.clip = ambience; source.loop = true; source.Play();
                Debug.Log("[Museum] Museum ambience looping.", this);
            }
        }
        if (playOnStart || requested) Begin();
    }

    public void Begin()
    {
        if (playing || loading) return;
        requested = true;
        if (!source) return;
        loading = true;
        source.Stop();
        StartCoroutine(PlaySequence());
    }

    IEnumerator Load(AudioClip clip)
    {
        clip.LoadAudioData();
        double timeout = Time.realtimeSinceStartupAsDouble + 10;
        while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
        if (clip.loadState != AudioDataLoadState.Loaded)
            Debug.LogError("[Museum] Audio data failed to load: " + clip.name, this);
    }

    IEnumerator PlaySequence()
    {
        var tracks = timeline ? timeline.GetOutputTracks().ToArray() : new TrackAsset[0];
        if (tracks.Length != 1 || !(tracks[0] is AudioTrack)) { Fail("Timeline must contain exactly one AudioTrack."); yield break; }
        var clips = tracks[0].GetClips().ToArray();
        if (clips.Length != 1 || clips[0].start != 0 || !(clips[0].asset is AudioPlayableAsset)) { Fail("Timeline must contain one audio clip starting at zero."); yield break; }
        var segment = clips[0];
        var asset = (AudioPlayableAsset)segment.asset;
        if (!asset.clip || asset.loop || segment.timeScale <= 0 || segment.clipIn + segment.duration * segment.timeScale > asset.clip.length + 0.05)
        { Fail("Missing, looping or too-short audio for sequence."); yield break; }
        if (!Application.CanStreamedLevelBeLoaded(nextScene)) { Fail("Next scene is absent from Build Settings: " + nextScene); yield break; }
        if (completionDirector)
        {
            if (completionDirector == director || !completionDirector.playableAsset || !completionDirector.isActiveAndEnabled)
            { Fail("Animation completion director must be a separate active director with a Timeline."); yield break; }
            animationDuration = completionDirector.playableAsset.duration;
            if (double.IsNaN(animationDuration) || double.IsInfinity(animationDuration) || animationDuration <= 0)
            { Fail("Animation Timeline must have a finite positive duration."); yield break; }
            if (segment.duration < animationDuration + Mathf.Max(0, delayAfterAnimation))
            { Fail("Audio Timeline is too short to cover the animation and the scene transition delay. Extend its audio clip duration."); yield break; }
        }
        yield return Load(asset.clip);
        if (asset.clip.loadState != AudioDataLoadState.Loaded) { loading = false; yield break; }
        source.Stop(); source.clip = asset.clip; source.loop = false;
        source.pitch = (float)segment.timeScale; source.time = (float)segment.clipIn;
        director.playableAsset = timeline; director.time = 0; director.Play();
        for (int i = 0; i < director.playableGraph.GetOutputCount(); i++)
        {
            var output = director.playableGraph.GetOutput(i);
            if (output.GetPlayableOutputType() == typeof(AudioPlayableOutput))
            { var audio = (AudioPlayableOutput)output; audio.SetTarget(null); audio.SetWeight(0); }
        }
        if (completionDirector)
        {
            completionDirector.timeUpdateMode = DirectorUpdateMode.DSPClock;
            completionDirector.extrapolationMode = DirectorWrapMode.Hold;
            completionDirector.time = 0;
            completionDirector.Play();
            Debug.Log("[Museum] Animation started with audio; duration: " + animationDuration + ", then wait " + delayAfterAnimation + " seconds.", this);
        }
        source.Play(); duration = segment.duration; playing = true; loading = false;
        audioFinished = false; animationFinished = false;
        Debug.Log("[Museum] Playing " + asset.clip.name + " for " + duration + " seconds, next: " + nextScene, this);
    }

    void Fail(string message) { loading = false; Debug.LogError("[Museum] " + message, this); }
    void Update()
    {
        if (!playing) return;
        // Audio keeps its own clip duration even when animation controls scene completion.
        if (!audioFinished && director.time >= duration - 0.001)
        {
            source.Stop(); audioFinished = true;
        }
        if (completionDirector)
        {
            if (!animationFinished)
            {
                if (completionDirector.time < animationDuration - 0.001) return;
                animationFinished = true;
                remainingDelay = Mathf.Max(0, delayAfterAnimation);
                Debug.Log("[Museum] Animation finished; waiting " + remainingDelay + " seconds before " + nextScene, this);
                if (remainingDelay > 0) return;
            }
            else remainingDelay -= Time.deltaTime;
            if (remainingDelay > 0) return;
        }
        else if (!audioFinished) return;
        playing = false; source.Stop(); director.Stop();
        Debug.Log("[Museum] Loading " + nextScene, this);
        SceneManager.LoadSceneAsync(nextScene);
    }
    void OnDisable()
    {
        if (source) source.Stop();
        if (director) director.Stop();
        if (completionDirector) completionDirector.Stop();
    }
}
