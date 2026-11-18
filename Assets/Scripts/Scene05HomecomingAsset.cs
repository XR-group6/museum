using UnityEngine;using UnityEngine.Playables;using UnityEngine.Timeline;
public sealed class Scene05HomecomingAsset:PlayableAsset,ITimelineClipAsset{
 public ClipCaps clipCaps=>ClipCaps.None;
 public override Playable CreatePlayable(PlayableGraph graph,GameObject owner)=>ScriptPlayable<Scene05HomecomingBehaviour>.Create(graph);
}
public sealed class Scene05HomecomingBehaviour:PlayableBehaviour{
 public override void ProcessFrame(Playable playable,FrameData info,object data){var story=data as Scene05HomecomingStory;if(story)story.Sample(playable.GetTime());}
}
