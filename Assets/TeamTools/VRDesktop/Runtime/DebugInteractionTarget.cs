using UnityEngine;
using UnityEngine.Events;
namespace XRTeam.DebugTools
{
    /// <summary>Explicit adapter. Wire events to real gameplay methods; never guesses a scene transition.</summary>
    [DisallowMultipleComponent]
    public sealed class DebugInteractionTarget : MonoBehaviour
    {
        public string displayName="交互物体";
        [Min(.01f)] public float proximity=.25f;
        public Collider hitCollider;
        public bool allowLeftHand=true, allowRightHand=true, allowHolding=true;
        public UnityEvent onNearby=new UnityEvent(), onFar=new UnityEvent();
        public UnityEvent onHoverEnter=new UnityEvent(), onHoverExit=new UnityEvent();
        public UnityEvent onSelect=new UnityEvent(), onHeld=new UnityEvent(), onReleased=new UnityEvent();
        public bool Nearby {get;private set;}
        public bool Hovered {get;private set;}
        public bool Held {get;private set;}
        void Reset(){hitCollider=GetComponentInChildren<Collider>();}
        public bool Allows(bool left)=>left?allowLeftHand:allowRightHand;
        public void SetNearby(bool value){if(value==Nearby)return;Nearby=value;if(value)onNearby.Invoke();else onFar.Invoke();}
        public void SetHovered(bool value){if(value==Hovered)return;Hovered=value;if(value)onHoverEnter.Invoke();else onHoverExit.Invoke();}
        public void Select(){onSelect.Invoke();}
        public void SetHeld(bool value){if(value && !allowHolding)return;if(value==Held)return;Held=value;if(value)onHeld.Invoke();else onReleased.Invoke();}
        public void ResetState(){SetHeld(false);SetHovered(false);SetNearby(false);}
        void OnDisable(){ResetState();}
    }
}
