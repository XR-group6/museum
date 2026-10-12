using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class SceneTwoNarration : MonoBehaviour
{
    public AudioClip footsteps, luggage, harbour, seagulls, fabric, shipHorn;
    public AudioClip fatherVoice, daughterVoice, censerVoice;
    public VRSubtitle subtitles;
    public TextAsset dialogueSubtitles;
    public PlayableDirector storyDirector;
    public float daughterArrivalTime = 15f;
    public float[] historyStartTimes = { 0f, 7.7f, 14.85f };
    public string nextScene = "";
    public float delayAfterCompletion = 3;
    [Range(0, 1)] public float speechBackgroundMultiplier = 0.25f;
    private AudioSource voice, clothing, horn;
    private readonly List<AudioSource> background = new List<AudioSource>();
    private readonly List<float> backgroundVolumes = new List<float>();

    void Awake()
    {
        if (storyDirector) storyDirector.playOnAwake = false;
    }

    AudioSource MakeSource(string name, AudioClip clip, Vector3 position, float volume, bool spatial, bool loop)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        obj.transform.position = position;
        var audio = obj.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.clip = clip; audio.volume = volume;
        audio.loop = loop; audio.pitch = 1; audio.spatialBlend = spatial ? 1 : 0;
        audio.dopplerLevel = 0; audio.rolloffMode = AudioRolloffMode.Linear;
        audio.minDistance = spatial ? 3 : 1; audio.maxDistance = 45;
        return audio;
    }

    void AddBackground(string name, AudioClip clip, Vector3 position, float volume)
    {
        var audio = MakeSource(name, clip, position, volume, true, true);
        background.Add(audio); backgroundVolumes.Add(volume); audio.Play();
    }

    IEnumerator Start()
    {
        if (!subtitles || !dialogueSubtitles || !storyDirector || !storyDirector.playableAsset)
        { Debug.LogError("[Museum] Scene two needs subtitles and an animation director.", this); yield break; }
        subtitles.ShowText("");
        string[] paragraphs = Regex.Split(dialogueSubtitles.text.Trim(), @"(?:<br\s*/?>\s*){2}|\r?\n\s*\r?\n", RegexOptions.IgnoreCase);
        if (paragraphs.Length != 5 || historyStartTimes.Length != 3 || historyStartTimes[0] < 0 ||
            historyStartTimes[1] <= historyStartTimes[0] || historyStartTimes[2] <= historyStartTimes[1])
        { Debug.LogError("[Museum] Scene two needs two dialogue paragraphs and three timed history paragraphs.", this); yield break; }
        var clips = new[] { footsteps, luggage, harbour, seagulls, fabric, shipHorn, fatherVoice, daughterVoice, censerVoice };
        foreach (var clip in clips)
        {
            if (!clip) { Debug.LogError("[Museum] Scene two audio clip is missing.", this); yield break; }
            clip.LoadAudioData();
            double timeout = Time.realtimeSinceStartupAsDouble + 15;
            while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
            if (clip.loadState != AudioDataLoadState.Loaded)
            { Debug.LogError("[Museum] Failed to load " + clip.name, this); yield break; }
        }
        if (historyStartTimes[2] >= censerVoice.length || daughterArrivalTime >= storyDirector.playableAsset.duration)
        { Debug.LogError("[Museum] Dialogue timing exceeds its audio or animation duration.", this); yield break; }

        voice = MakeSource("SceneTwoVoice", null, transform.position, 1, false, false);
        clothing = MakeSource("IncenseFabric", fabric, new Vector3(0, 1.2f, 1.1f), 0.07f, true, true);
        horn = MakeSource("DistantShipHorn", shipHorn, new Vector3(0, 2, -12), 0, true, false);
        AddBackground("IndoorFootsteps", footsteps, new Vector3(-1, 0.1f, 0), 0.25f);
        AddBackground("PackingLuggage", luggage, new Vector3(1.5f, 0.5f, 0), 0.2f);
        // The supplied harbour bed includes the distant waterfront/crowd environment.
        AddBackground("OutsideHarbour", harbour, new Vector3(0, 1.5f, -12), 0.32f);
        AddBackground("OutsideSeagulls", seagulls, new Vector3(3, 4, -14), 0.16f);
        // Keep the original animation playing from scene entry, independently of audio staging.
        storyDirector.timeUpdateMode = DirectorUpdateMode.DSPClock;
        storyDirector.extrapolationMode = DirectorWrapMode.Hold;
        storyDirector.time = 0; storyDirector.Play();
        yield return WaitSeconds(5);

        StartCoroutine(FadeBackground(speechBackgroundMultiplier, 0.6f));
        clothing.Play();
        yield return PlayDialogue(fatherVoice, paragraphs[0]);
        clothing.Stop();

        // Arrival is driven by the animation Timeline rather than a separate wall clock.
        while (storyDirector.time < daughterArrivalTime) yield return null;
        yield return PlayDialogue(daughterVoice, paragraphs[1]);
        horn.volume = 0.8f; horn.Play();
        yield return WaitSeconds(2);
        StartCoroutine(FadeHorn(0.04f, 0.35f));
        StartCoroutine(FadeBackground(0.12f, 0.35f));

        voice.clip = censerVoice; voice.Play();
        int previous = -1;
        while (voice.isPlaying || AudioListener.pause)
        {
            float position = voice.timeSamples / (float)censerVoice.frequency;
            int current = -1;
            for (int i = 0; i < historyStartTimes.Length; i++) if (position >= historyStartTimes[i]) current = i;
            if (current != previous)
            {
                previous = current;
                subtitles.ShowText(current < 0 ? "" : paragraphs[current + 2].Trim());
            }
            yield return null;
        }
        subtitles.ShowText("");
        if (!string.IsNullOrEmpty(nextScene))
        {
            while (storyDirector.time < storyDirector.playableAsset.duration - 0.001) yield return null;
            yield return WaitSeconds(Mathf.Max(0, delayAfterCompletion));
            if (Application.CanStreamedLevelBeLoaded(nextScene)) SceneManager.LoadSceneAsync(nextScene);
            else Debug.LogError("[Museum] Next scene is absent from Build Settings: " + nextScene, this);
        }
    }

    IEnumerator PlayDialogue(AudioClip clip, string text)
    {
        voice.clip = clip; subtitles.ShowText(text.Trim()); voice.Play();
        while (voice.isPlaying || AudioListener.pause) yield return null;
        subtitles.ShowText("");
    }

    IEnumerator WaitSeconds(float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            if (!AudioListener.pause) elapsed += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator FadeBackground(float multiplier, float seconds)
    {
        var starts = background.ConvertAll(audio => audio.volume);
        float elapsed = 0;
        do
        {
            if (!AudioListener.pause) elapsed += Time.deltaTime;
            for (int i = 0; i < background.Count; i++)
                background[i].volume = Mathf.Lerp(starts[i], backgroundVolumes[i] * multiplier, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        } while (elapsed < seconds);
    }

    IEnumerator FadeHorn(float target, float seconds)
    {
        float initial = horn.volume, elapsed = 0;
        do
        {
            if (!AudioListener.pause) elapsed += Time.deltaTime;
            horn.volume = Mathf.Lerp(initial, target, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        } while (elapsed < seconds);
    }

    void OnDisable()
    {
        StopAllCoroutines();
        foreach (var audio in background) if (audio) audio.Stop();
        if (voice) voice.Stop(); if (clothing) clothing.Stop(); if (horn) horn.Stop();
        if (storyDirector) storyDirector.Stop();
        if (subtitles) subtitles.ShowText("");
    }
}
