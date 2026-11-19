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
        caption.text = t >= 4 && t < 4 + Length(daughterVoice, 3.082) ? "女儿：爸，怎么把它也带来了？" :
            t >= 8 && t < 8 + Length(fatherVoice, 6.818) ? "父亲：等回去那天，还要求妈祖保佑航海顺利呢。\n快去上学吧。" :
            t >= 18.5 && t < 18.5 + Length(burnerVoice, 5.721) ? "香炉：等他们再求航海顺利，我就能回家。那我就等。" : "";
        if (Application.isPlaying && director.state == PlayState.Playing)
        {
            if (!source) { source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; }
            Cue(4, daughterVoice, t); Cue(8, fatherVoice, t); Cue(18.5, burnerVoice, t); Cue(12, chime, t);
        }
        previous = t;
    }
    static double Length(AudioClip clip, double fallback) => clip ? clip.length : fallback;
    void Cue(double at, AudioClip clip, double t) { if (clip && previous < at && t >= at && t-at < .5) source.PlayOneShot(clip); }
}
