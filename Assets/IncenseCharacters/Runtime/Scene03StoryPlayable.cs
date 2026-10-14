using UnityEngine;
using UnityEngine.Playables;

namespace IncenseCharacters
{
    public sealed class Scene03StoryPlayable : PlayableAsset
    {
        public override double duration => 30;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<Scene03StoryBehaviour>.Create(graph);
            playable.GetBehaviour().story = owner.GetComponent<Scene03Story>();
            var output = ScriptPlayableOutput.Create(graph, "Scene03 story poses");
            output.SetSourcePlayable(playable);
            return playable;
        }
    }
    public sealed class Scene03StoryBehaviour : PlayableBehaviour
    {
        public Scene03Story story;
        public override void PrepareFrame(Playable playable, FrameData info)
        {
            if (story) story.Sample((float)playable.GetTime());
        }
    }
}
