#if UNITY_EDITOR
using UnityEngine;
namespace XRTeam.DebugTools.Editor
{
    public sealed class WorkbenchVisualOverride
    {
        public SoftInteractionHighlight Target {get;private set;}
        Color color;float width,idle,near,multiplier;bool nearby,held;
        public int Mode;
        public void Select(SoftInteractionHighlight target)
        {
            if(target==Target)return;Restore();Target=target;
            if(!Target)return;color=Target.color;width=Target.width;idle=Target.idleStrength;near=Target.nearbyStrength;multiplier=Target.nearbyWidthMultiplier;nearby=Target.IsNearby;held=Target.IsHeld;
        }
        public void Apply()
        {
            if(!Target)return;
            Target.SetNearby(Mode==1);Target.SetHeld(Mode==2);
        }
        public void Restore()
        {
            if(Target){Target.color=color;Target.width=width;Target.idleStrength=idle;Target.nearbyStrength=near;Target.nearbyWidthMultiplier=multiplier;Target.SetNearby(nearby);Target.SetHeld(held);}
            Target=null;
        }
    }
}
#endif
