using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
public sealed class Scene04HarborAsset : PlayableAsset, ITimelineClipAsset
{
 public ClipCaps clipCaps => ClipCaps.None;
 public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => ScriptPlayable<Scene04HarborBehaviour>.Create(graph);
}
public sealed class Scene04HarborBehaviour : PlayableBehaviour
{
 public override void ProcessFrame(Playable playable, FrameData info, object data) { var story = data as Scene04HarborStory; if(story) story.Sample(playable.GetTime()); }
}
