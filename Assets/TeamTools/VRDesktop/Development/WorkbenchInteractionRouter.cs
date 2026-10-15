#if UNITY_EDITOR
using UnityEngine;
namespace XRTeam.DebugTools.Editor
{
    public sealed class WorkbenchInteractionRouter
    {
        public string TargetName {get;private set;}="未命中";
        public string Route {get;private set;}="等待输入";
        public bool Hit {get;private set;}
        public Vector3 End {get;private set;}
        DebugInteractionTarget previous,heldTarget;
        readonly WorkbenchUIRaycaster ui=new WorkbenchUIRaycaster();

        public void Process(SimulatedHand hand,Camera camera,bool left)
        {
            Ray ray=hand.Ray;End=ray.GetPoint(5);Hit=false;TargetName="未命中";Route="自由射线";
            var language=Object.FindObjectOfType<InitialLanguageSelector>();
            if(language)
            {
                ReleaseGeneric();ui.Reset();Route="语言选择（仅右手）";
                if(!language.Built)language.Build(camera);
                if(!left){Hit=language.ProcessPointer(ray,hand.Down);TargetName=Hit?"语言按钮":"语言面板";FindColliderEnd(language.gameObject,ray);}
                Record(hand);return;
            }
            var permit=Object.FindObjectOfType<PermitInspection>();
            if(permit && permit.IsOpen)
            {
                ReleaseGeneric();ui.Reset();Route="入台证悬浮查看（仅右手）";
                if(!left){Hit=permit.ProcessDesktopPointer(ray,hand.Down,hand.Held,hand.Transform.rotation,camera);TargetName=Hit?"预览交互区域":"预览面板";var line=permit.GetComponentInChildren<LineRenderer>();if(line)End=line.GetPosition(1);}
                Record(hand);return;
            }
            var narration=Object.FindObjectOfType<SceneOneNarration>();
            if(narration && narration.ShowingMemoryChoice)
            {
                ReleaseGeneric();ui.Reset();Route="进入记忆确认（仅右手）";
                if(!left){Hit=narration.PointAtMemoryChoice(ray,hand.Down,out var point);End=point;TargetName=Hit?"进入记忆按钮":"确认面板";}
                Record(hand);return;
            }
            if(permit && permit.Ready && !left)
            {
                Hit=permit.ProcessDesktopPointer(ray,hand.Down,hand.Held,hand.Transform.rotation,camera);
                if(Hit){ReleaseGeneric();ui.Reset();Route="入台证";TargetName="入台证";FindColliderEnd(permit.gameObject,ray);Record(hand);return;}
            }
            if(ui.Process(ray,camera,hand,out var uiEnd))
            {
                ReleaseGeneric();Route="通用世界空间UI";TargetName="uGUI交互区域";Hit=true;End=uiEnd;Record(hand);return;
            }
            var target=default(DebugInteractionTarget);
            if(Physics.Raycast(ray,out var hit,5,~0,QueryTriggerInteraction.Ignore))
            {
                End=hit.point;TargetName=hit.collider.name;
                target=hit.collider.GetComponentInParent<DebugInteractionTarget>();
                if(target && !target.Allows(left))target=null;
            }
            foreach(var item in Object.FindObjectsOfType<DebugInteractionTarget>())
            {
                bool near=item.Allows(left) && item.hitCollider && item.hitCollider.enabled && Vector3.Distance(ray.origin,item.hitCollider.ClosestPoint(ray.origin))<=item.proximity;
                item.SetNearby(near);
            }
            if(previous!=target){if(previous)previous.SetHovered(false);previous=target;if(target)target.SetHovered(true);}
            if(target)
            {
                Hit=true;Route="通用事件适配器";TargetName=target.displayName;
                if(hand.Down)target.Select();
                if(hand.GripDown && target.allowHolding){heldTarget=target;target.SetHeld(true);}
            }
            if(hand.GripReleased && heldTarget){heldTarget.SetHeld(false);heldTarget=null;}
            if(narration && narration.WaitingForInteraction && !left)
            {
                foreach(var col in Object.FindObjectsOfType<Collider>())
                {
                    var ancestor=col.transform;while(ancestor && !ancestor.CompareTag("Censer"))ancestor=ancestor.parent;
                    if(!ancestor || !col.enabled || Vector3.Distance(ray.origin,col.ClosestPoint(ray.origin))>.15f)continue;
                    Route="香炉接近交互";TargetName="香炉";Hit=true;
                    if(hand.Down)narration.Interact();break;
                }
            }
            Record(hand);
        }
        void FindColliderEnd(GameObject root,Ray ray)
        {
            float closest=5;foreach(var col in root.GetComponentsInChildren<Collider>())if(col.Raycast(ray,out var hit,closest)){closest=hit.distance;End=hit.point;}
        }
        void Record(SimulatedHand hand)
        {
            if(hand.Down)WorkbenchJournal.Add("扳机按下",TargetName,Route+(Hit?"；命中":"；未命中，无选择"));
            if(hand.Released)WorkbenchJournal.Add("扳机释放",TargetName,Route);
            if(hand.GripDown || hand.GripReleased)WorkbenchJournal.Add("握持",TargetName,hand.GripDown?"按下":"释放");
        }
        void ReleaseGeneric()
        {
            if(previous)previous.SetHovered(false);previous=null;
            if(heldTarget)heldTarget.SetHeld(false);heldTarget=null;
            foreach(var target in Object.FindObjectsOfType<DebugInteractionTarget>())target.SetNearby(false);
        }
        public void Reset(){ReleaseGeneric();ui.Reset();Hit=false;TargetName="未命中";}
        public void Dispose(){Reset();ui.Dispose();}
    }
}
#endif
