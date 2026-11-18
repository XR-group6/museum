using UnityEngine;
using UnityEngine.Playables;

[ExecuteAlways]
public sealed class Scene01Dialogue : MonoBehaviour
{
    public PlayableDirector director;
    public TextMesh caption;
    public AudioClip daughterVoice, fatherVoice, burnerVoice, chime;
    AudioSource source;
    double previous = -1;
    void Update()
    {
        if (!director || !caption) return;
        double t = director.time;
        caption.text = t < 4 || t >= 26 ? "" : t < 8 ? "女儿：爸，怎么把它也带来了？" : t < 16 ? "父亲：等回去那天，还要求妈祖保佑航海顺利呢。" : t < 18.5 ? "父亲：快去上学吧。" : "香炉：等他们再求航海顺利，我就能回家。那我就等。";
        if (Application.isPlaying && director.state == PlayState.Playing)
        {
            if (!source) { source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; }
            Cue(4, daughterVoice, t); Cue(8, fatherVoice, t); Cue(18.5, burnerVoice, t); Cue(14, chime, t);
        }
        previous = t;
    }
    void Cue(double at, AudioClip clip, double t) { if (clip && previous < at && t >= at && t-at < .5) source.PlayOneShot(clip); }
}
