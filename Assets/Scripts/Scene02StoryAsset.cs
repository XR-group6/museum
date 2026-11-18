using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
public sealed class Scene02StoryAsset:PlayableAsset,ITimelineClipAsset{
 public ClipCaps clipCaps=>ClipCaps.None;
 public override Playable CreatePlayable(PlayableGraph graph,GameObject owner)=>ScriptPlayable<Scene02StoryBehaviour>.Create(graph);
}
public sealed class Scene02StoryBehaviour:PlayableBehaviour{
 public override void ProcessFrame(Playable playable,FrameData info,object data){var story=data as Scene02Story;if(story)story.Sample(playable.GetTime());}
}
