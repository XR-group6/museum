using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneOneNarration : MonoBehaviour
{
    public AudioClip museumEcho, censerIntro, censerAfterGrab, censerFinal;
    public VRSubtitle narrationSubtitle, interactionSubtitle;
    public TextAsset introSubtitles;
    [Tooltip("Start times in seconds within the censer intro audio; one per paragraph.")]
    public float[] paragraphStartTimes = { 0f, 5.65f, 14.2f };
    public string nextScene = "scene_02";
    public bool WaitingForInteraction { get; private set; }
    private AudioSource source;
    private bool failed;
    private string[] paragraphs;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = false; source.spatialBlend = 0; source.pitch = 1;
    }

    IEnumerator Start()
    {
        if (!narrationSubtitle || !interactionSubtitle)
        { Fail("Missing narration or interaction subtitle component."); yield break; }
        narrationSubtitle.ShowText(""); interactionSubtitle.ShowText("");
        if (!museumEcho || !censerIntro || !censerAfterGrab || !censerFinal || !introSubtitles ||
            !Application.CanStreamedLevelBeLoaded(nextScene))
        { Fail("Missing scene-one audio, subtitle asset or next scene."); yield break; }
        paragraphs = Regex.Split(introSubtitles.text.Trim(), @"(?:<br\s*/?>\s*){2}|\r?\n\s*\r?\n", RegexOptions.IgnoreCase);
        if (paragraphs.Length != 3 || paragraphStartTimes.Length != 3 ||
            paragraphStartTimes[0] < 0 || paragraphStartTimes[1] <= paragraphStartTimes[0] ||
            paragraphStartTimes[2] <= paragraphStartTimes[1] || paragraphStartTimes[2] >= censerIntro.length)
        { Fail("Intro requires three paragraphs and three increasing audio cue times."); yield break; }
        // Preload to avoid gaps between narration clips.
        foreach (var clip in new[] { museumEcho, censerIntro, censerAfterGrab, censerFinal })
        {
            clip.LoadAudioData();
            double timeout = Time.realtimeSinceStartupAsDouble + 15;
            while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
            if (clip.loadState != AudioDataLoadState.Loaded) { Fail("Failed to load " + clip.name); yield break; }
        }
        if (museumEcho.length < 10) { Fail("Museum echo must contain at least ten seconds."); yield break; }
        source.clip = museumEcho; source.Play();
        while (AudioListener.pause || (source.isPlaying && source.timeSamples / (float)museumEcho.frequency < 10)) yield return null;
        source.Stop();
        source.clip = censerIntro; source.Play();
        int paragraph = -1;
        while (source.isPlaying || AudioListener.pause)
        {
            float audioTime = source.timeSamples / (float)censerIntro.frequency;
            int current = -1;
            for (int i = 0; i < paragraphStartTimes.Length; i++)
                if (audioTime >= paragraphStartTimes[i]) current = i;
            if (current != paragraph)
            {
                paragraph = current;
                narrationSubtitle.ShowText(current >= 0 ? paragraphs[current].Trim() : "");
            }
            yield return null;
        }
        narrationSubtitle.ShowText("");
        interactionSubtitle.ShowText("请用右手触摸并握住香炉。");
        WaitingForInteraction = true;
    }

    public void Interact()
    {
        if (!WaitingForInteraction || failed) return;
        WaitingForInteraction = false;
        interactionSubtitle.ShowText("");
        StartCoroutine(AfterInteraction());
    }

    IEnumerator AfterInteraction()
    {
        // These two assets already contain speech slowed to 0.85, so keep pitch at 1.
        var clips = new[] { censerAfterGrab, censerFinal };
        var captions = new[] { "欸，你可认得我?", "或许，你愿意听听我渡海而来的故事?" };
        for (int i = 0; i < clips.Length; i++)
        {
            narrationSubtitle.ShowText(captions[i]);
            source.clip = clips[i]; source.Play();
            while (source.isPlaying || AudioListener.pause) yield return null;
            narrationSubtitle.ShowText("");
        }
        SceneManager.LoadSceneAsync(nextScene);
    }

    void Fail(string reason)
    {
        failed = true; WaitingForInteraction = false;
        Debug.LogError("[Museum] " + reason, this);
    }

    void OnDisable()
    {
        StopAllCoroutines(); WaitingForInteraction = false;
        if (source) source.Stop();
        if (narrationSubtitle) narrationSubtitle.ShowText("");
        if (interactionSubtitle) interactionSubtitle.ShowText("");
    }
}
