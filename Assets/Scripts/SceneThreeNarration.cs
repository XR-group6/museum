using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using IncenseCharacters;
using UnityEngine;
using UnityEngine.Playables;

[DefaultExecutionOrder(9999)]
public class SceneThreeNarration : MonoBehaviour
{
    public Scene03Story story;
    public VRSubtitle subtitles;
    public TextAsset subtitleText;
    public AudioClip engine, waves, wind, fabric, daughterVoice, fatherVoice, censerVoice;
    public float daughterSecondLineAt = 4.15f;
    public float fatherSecondLineAt = 5f;
    private AudioSource voice;
    private readonly List<AudioSource> ambience = new List<AudioSource>();
    private readonly float[] samples = new float[256];
    private float storyTime, motionFrom, motionTo, motionElapsed, motionDuration;
    private bool runningMotion, complete;
    private int speaker;
    public bool ReadyForTransition => complete && story && story.ClothOpening <= 0.001f;

    void Awake()
    {
        if (story && story.director)
        {
            story.director.playOnAwake = false;
            story.director.timeUpdateMode = DirectorUpdateMode.Manual;
        }
    }

    AudioSource Source(string name, AudioClip clip, float volume, bool loop)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        var source = obj.AddComponent<AudioSource>();
        source.playOnAwake = false; source.clip = clip; source.volume = volume;
        source.loop = loop; source.spatialBlend = 0; source.pitch = 1;
        return source;
    }

    IEnumerator Start()
    {
        if (!story || !story.director || !subtitles || !subtitleText)
        { Debug.LogError("[Museum] Scene-three narration references are missing.", this); yield break; }
        subtitles.ShowText("");
        string[] lines = Regex.Split(subtitleText.text.Trim(), @"(?:<br\s*/?>\s*){2}|\r?\n\s*\r?\n", RegexOptions.IgnoreCase);
        if (lines.Length != 5)
        { Debug.LogError("[Museum] Scene-three subtitles must contain five paragraphs.", this); yield break; }
        foreach (var clip in new[] { engine, waves, wind, fabric, daughterVoice, fatherVoice, censerVoice })
        {
            if (!clip) { Debug.LogError("[Museum] Missing scene-three audio.", this); yield break; }
            clip.LoadAudioData();
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (clip.loadState != AudioDataLoadState.Loaded)
            { Debug.LogError("[Museum] Failed to load " + clip.name, this); yield break; }
        }
        if (daughterSecondLineAt <= 0 || daughterSecondLineAt >= daughterVoice.length ||
            fatherSecondLineAt <= 0 || fatherSecondLineAt >= fatherVoice.length)
        { Debug.LogError("[Museum] Subtitle cue exceeds voice duration.", this); yield break; }
        voice = Source("SceneThreeDialogue", null, 1, false);
        ambience.Add(Source("ShipEngineLowHum", engine, 0.22f, true));
        ambience.Add(Source("GentleWaves", waves, 0.16f, true));
        ambience.Add(Source("SeaWind", wind, 0.12f, true));
        ambience.Add(Source("ClothesInsideBag", fabric, 0.06f, true));
        foreach (var source in ambience) source.Play();
        story.daughterSpeechLevel = story.fatherSpeechLevel = 0;
        story.director.time = 0; story.director.Evaluate();
        yield return MoveStory(0, 5, 5);
        foreach (var source in ambience) source.volume *= 0.35f;
        yield return Speak(daughterVoice, lines[0], lines[1], daughterSecondLineAt, 1, 5, 10);
        yield return MoveStory(10, 11, 0.5f);
        yield return Speak(fatherVoice, lines[2], lines[3], fatherSecondLineAt, 2, 11, 20);
        yield return MoveStory(20, 21, 0.5f);
        yield return Speak(censerVoice, lines[4], null, float.PositiveInfinity, 0, 21, 23);
        // Run the existing woven-cover action only after every spoken clip has finished.
        yield return MoveStory(23, 30, 7);
        complete = true;
    }

    IEnumerator Speak(AudioClip clip, string first, string second, float cue, int who, float from, float to)
    {
        speaker = who; voice.clip = clip; voice.Play();
        bool secondShown = false; subtitles.ShowText(first.Trim());
        while (voice.isPlaying || AudioListener.pause)
        {
            float time = voice.timeSamples / (float)clip.frequency;
            storyTime = Mathf.Lerp(from, to, Mathf.Clamp01(time / clip.length));
            if (!secondShown && time >= cue)
            { secondShown = true; subtitles.ShowText(second.Trim()); }
            yield return null;
        }
        speaker = 0; storyTime = to; subtitles.ShowText("");
    }

    IEnumerator MoveStory(float from, float to, float seconds)
    {
        motionFrom = from; motionTo = to; motionDuration = seconds; motionElapsed = 0;
        storyTime = from; runningMotion = true;
        while (motionElapsed < motionDuration) yield return null;
        runningMotion = false; storyTime = to;
    }

    void LateUpdate()
    {
        if (!story || !story.director) return;
        if (runningMotion && !AudioListener.pause)
        {
            motionElapsed += Time.deltaTime;
            storyTime = Mathf.Lerp(motionFrom, motionTo, Mathf.Clamp01(motionElapsed / motionDuration));
        }
        float level = 0;
        if (voice && voice.isPlaying && speaker != 0 && !AudioListener.pause)
        {
            // Measure decoded PCM around the current sample so silent pauses close the mouth.
            int frame = Mathf.Min(voice.timeSamples, Mathf.Max(0, voice.clip.samples - samples.Length));
            if (voice.clip.GetData(samples, frame))
            {
                float energy = 0; foreach (float sample in samples) energy += sample * sample;
                level = Mathf.Clamp01(Mathf.Sqrt(energy / samples.Length) * 8);
            }
        }
        story.daughterSpeechLevel = speaker == 1 ? level : 0;
        story.fatherSpeechLevel = speaker == 2 ? level : 0;
        story.director.time = storyTime;
        story.director.Evaluate();
    }

    void OnDisable()
    {
        StopAllCoroutines(); foreach (var source in ambience) if (source) source.Stop();
        if (voice) voice.Stop(); if (subtitles) subtitles.ShowText("");
        if (story) story.daughterSpeechLevel = story.fatherSpeechLevel = -1;
    }
}
