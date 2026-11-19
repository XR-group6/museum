using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// Sound beds and action cues share the story clock, including pause and seeking.
public sealed class Scene04RoomSoundscape : MonoBehaviour
{
    public PlayableDirector director;
    public AudioClip roomTone, harbour, gulls, clockTick, clothes, luggageRustle, footsteps, tableware, burnerSetDown;
    Scene01CenserView view;
    Scene01Dialogue morning;
    readonly List<Cue> cues = new List<Cue>();
    sealed class Cue
    {
        public AudioSource source;
        public double start, end;
        public float volume;
        public bool duckUnderDialogue;
        public Transform anchor;
    }
    void Start()
    {
        if(!director)director=GetComponent<PlayableDirector>();
        view=GetComponent<Scene01CenserView>();
        morning=GetComponent<Scene01Dialogue>();
        if(!director || !view || !view.scene02 || !view.scene03 || !view.scene04){enabled=false;return;}
        double second=view.scene02.startTime, third=view.scene03.startTime, fourth=view.scene04.startTime;
        double end=view.scene04.EndTime;
        Add("Room tone",roomTone,0,end,.10f,null,true);
        Add("Distant harbour",harbour,0,end,.055f,null,true);
        Add("Clock ticking",clockTick,0,end,.045f,null,true);
        Add("Morning clothes",clothes,.5,3.5,.10f,view.storyBurner,true);
        Add("Unpacking",luggageRustle,12,16,.07f,view.storyBurner,true);
        // A distinct contact cue at the end of the placement animation.
        // Play the complete recording and keep it audible under Father's line.
        Add("Burner on table",burnerSetDown,11,double.MaxValue,.65f,view.storyBurner,false,false);
        Add("School departure",footsteps,18,24,.13f,view.daughterHead,true);
        Add("Work departure",footsteps,second+10.8,second+16,.45f,view.scene02.daughter,true);
        Add("Fish plate",tableware,third+.6,third+2,.4f,view.scene03.fishPlate);
        Add("Dinner tableware",tableware,third+8,third+9.5,.32f,view.scene03.fishPlate);
        Add("Dusk gull",gulls,fourth+.2,fourth+8,.28f,null);
    }
    void Add(string name, AudioClip clip,double start,double end,float volume,Transform anchor,bool loop=false,bool duckUnderDialogue=true)
    {
        if(!clip)return;
        var go=new GameObject("Scene04 sound - "+name);go.transform.SetParent(transform,false);
        var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.clip=clip;source.loop=loop;
        source.spatialBlend=anchor?(duckUnderDialogue?1:.35f):0;source.minDistance=2;source.maxDistance=15;
        if(name=="Work departure" || name=="Fish plate" || name=="Dinner tableware")
        { source.spatialBlend=.5f; source.minDistance=4; }
        cues.Add(new Cue{source=source,start=start,end=loop?end:System.Math.Min(end,start+clip.length),volume=volume,anchor=anchor,duckUnderDialogue=duckUnderDialogue});
    }
    static bool Speaking(double time,double at,AudioClip clip) => clip && time>=at && time<at+clip.length;
    bool DialogueActive(double time)
    {
        if(morning && (Speaking(time,4,morning.daughterVoice) || Speaking(time,8,morning.fatherVoice) || Speaking(time,18.5,morning.burnerVoice)))return true;
        var second=view.scene02;var third=view.scene03;var fourth=view.scene04;
        return Speaking(time,second.startTime+2,second.daughterVoice) || Speaking(time,second.startTime+6.4,second.fatherVoice) || Speaking(time,second.startTime+9,second.burnerVoice) ||
            Speaking(time,third.startTime+2,third.daughterVoice) || Speaking(time,third.startTime+7,third.fatherVoice) || Speaking(time,third.startTime+11,third.burnerVoice) ||
            Speaking(time,fourth.startTime+2,fourth.fatherVoice) || Speaking(time,fourth.startTime+14,fourth.burnerVoice);
    }
    void Update()
    {
        if(!director)return;
        double time=director.time;
        var fourth=view.scene04;
        // Leave space around the two clock strikes and the burner's quiet reply.
        bool laterEra=time>=view.scene02.startTime-2.4;
        float quiet=fourth && time>=fourth.startTime+8 && time<fourth.startTime+18 ? .65f : DialogueActive(time) ? (laterEra?.75f:.5f) : 1;
        foreach(var cue in cues)
        {
            var source=cue.source;
            bool active=director.state==PlayState.Playing && time>=cue.start && time<cue.end;
            if(!active){if(source.isPlaying)source.Stop();continue;}
            if(cue.anchor)source.transform.position=cue.anchor.position;
            // Lift the shared room/harbour/clock beds only in rotations 2–4.
            bool ambience=cue.anchor==null && source.loop;
            float gain=laterEra && ambience?3f:1f;
            source.volume=Mathf.Clamp01(cue.volume*gain*(cue.duckUnderDialogue?quiet:1));
            float local=(float)(time-cue.start);
            float expected=source.loop?local%source.clip.length:local;
            if(!source.isPlaying || Mathf.Abs(source.time-expected)>.3f)
            {source.time=Mathf.Min(expected,Mathf.Max(0,source.clip.length-.01f));if(!source.isPlaying)source.Play();}
        }
    }
    void OnDisable(){foreach(var cue in cues)if(cue.source)cue.source.Stop();}
}
