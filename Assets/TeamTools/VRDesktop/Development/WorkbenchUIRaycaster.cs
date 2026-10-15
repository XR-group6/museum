#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace XRTeam.DebugTools.Editor
{
    /// <summary>World-space uGUI adapter. Explicit gameplay adapters take precedence.</summary>
    public sealed class WorkbenchUIRaycaster
    {
        GameObject hovered,pressed;
        EventSystem eventSystem;
        bool ownsEventSystem;
        PointerEventData data;
        public bool Process(Ray ray,Camera view,SimulatedHand hand,out Vector3 end)
        {
            end=ray.GetPoint(5);Graphic best=null;float distance=5;
            foreach(var canvas in Object.FindObjectsOfType<Canvas>())
            {
                if(!canvas.isActiveAndEnabled || canvas.renderMode!=RenderMode.WorldSpace || !canvas.GetComponent<GraphicRaycaster>())continue;
                foreach(var graphic in canvas.GetComponentsInChildren<Graphic>())
                {
                    if(!graphic.isActiveAndEnabled || !graphic.raycastTarget || graphic.canvasRenderer.cull)continue;
                    var plane=new Plane(graphic.transform.forward,graphic.transform.position);
                    if(!plane.Raycast(ray,out float d) || d<0 || d>distance)continue;
                    Vector3 hit=ray.GetPoint(d);
                    if(!graphic.rectTransform.rect.Contains(graphic.rectTransform.InverseTransformPoint(hit)))continue;
                    if(!graphic.Raycast(view.WorldToScreenPoint(hit),view))continue;
                    if(Physics.Raycast(ray,out var obstruction,d-.005f,~0,QueryTriggerInteraction.Ignore) && !obstruction.transform.IsChildOf(canvas.transform))continue;
                    best=graphic;distance=d;end=hit;
                }
            }
            EnsureEventSystem();data.position=view.WorldToScreenPoint(end);data.worldPosition=end;
            GameObject current=best?best.gameObject:null;
            if(current!=hovered){if(hovered)ExecuteEvents.ExecuteHierarchy(hovered,data,ExecuteEvents.pointerExitHandler);hovered=current;if(hovered)ExecuteEvents.ExecuteHierarchy(hovered,data,ExecuteEvents.pointerEnterHandler);}
            if(hand.Down && current){pressed=ExecuteEvents.ExecuteHierarchy(current,data,ExecuteEvents.pointerDownHandler);if(!pressed)pressed=ExecuteEvents.GetEventHandler<IPointerClickHandler>(current);data.pressPosition=data.position;ExecuteEvents.ExecuteHierarchy(current,data,ExecuteEvents.initializePotentialDrag);}
            if(hand.Held && pressed){data.delta=data.position-data.pressPosition;ExecuteEvents.ExecuteHierarchy(pressed,data,ExecuteEvents.dragHandler);}
            if(hand.Released){if(pressed){ExecuteEvents.ExecuteHierarchy(pressed,data,ExecuteEvents.pointerUpHandler);if(current && ExecuteEvents.GetEventHandler<IPointerClickHandler>(current)==ExecuteEvents.GetEventHandler<IPointerClickHandler>(pressed))ExecuteEvents.ExecuteHierarchy(pressed,data,ExecuteEvents.pointerClickHandler);}pressed=null;}
            return current;
        }
        void EnsureEventSystem()
        {
            if(!eventSystem){eventSystem=EventSystem.current;if(!eventSystem){eventSystem=new GameObject("Desktop Workbench EventSystem").AddComponent<EventSystem>();ownsEventSystem=true;}data=new PointerEventData(eventSystem){button=PointerEventData.InputButton.Left};}
        }
        public void Reset()
        {
            if(data!=null){if(pressed)ExecuteEvents.ExecuteHierarchy(pressed,data,ExecuteEvents.pointerUpHandler);if(hovered)ExecuteEvents.ExecuteHierarchy(hovered,data,ExecuteEvents.pointerExitHandler);}
            hovered=pressed=null;
        }
        public void Dispose(){Reset();if(ownsEventSystem && eventSystem)Object.Destroy(eventSystem.gameObject);eventSystem=null;}
    }
}
#endif
