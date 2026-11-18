using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
public sealed class Scene01MotionAsset : PlayableAsset, ITimelineClipAsset {
 public ClipCaps clipCaps => ClipCaps.None;
 public override Playable CreatePlayable(PlayableGraph graph,GameObject owner) { return ScriptPlayable<Scene01MotionBehaviour>.Create(graph); }
}
public sealed class Scene01MotionBehaviour : PlayableBehaviour {
 public override void ProcessFrame(Playable playable,FrameData info,object playerData) {
  var motion=playerData as Scene01Motion; if(motion) motion.Sample(playable.GetTime());
 }
}
