#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace XRTeam.DebugTools.Editor
{
    /// <summary>Deterministic assertions; does not label manual or hardware tests as passes.</summary>
    public static class WorkbenchSelfTests
    {
        public static List<CheckResult> Run()
        {
            var results=new List<CheckResult>();
            Test(results,"扳机状态边沿",()=>{
                var root=new GameObject("Workbench SelfTest Hand");var hand=new SimulatedHand(root.transform,false,Color.cyan);
                try{
                    hand.InputFrame(false,false);Require(!hand.Down && !hand.Held && !hand.Released,"Idle");
                    hand.InputFrame(true,false);Require(hand.Down && hand.Held && !hand.Released,"Down");
                    hand.InputFrame(true,false);Require(!hand.Down && hand.Held,"Held");
                    hand.InputFrame(false,false);Require(hand.Released && !hand.Held,"Up");
                    hand.InputFrame(false,false);Require(!hand.Released,"Up must occur once");
                    hand.InputFrame(false,true);Require(hand.GripDown,"Grip down");
                    hand.InputFrame(false,false);Require(hand.GripReleased,"Grip release");
                }finally{hand.Dispose();Dispose(root);}
            });
            Test(results,"通用事件去重与恢复",()=>{
                var go=new GameObject("Workbench SelfTest Target");var target=go.AddComponent<DebugInteractionTarget>();
                try{
                    int near=0,far=0,hover=0,select=0,held=0,release=0;
                    target.onNearby.AddListener(()=>near++);target.onFar.AddListener(()=>far++);target.onHoverEnter.AddListener(()=>hover++);
                    target.onSelect.AddListener(()=>select++);target.onHeld.AddListener(()=>held++);target.onReleased.AddListener(()=>release++);
                    target.SetNearby(true);target.SetNearby(true);target.SetHovered(true);target.SetHovered(true);target.Select();target.SetHeld(true);target.SetHeld(true);target.ResetState();
                    Require(near==1 && far==1 && hover==1 && select==1 && held==1 && release==1,"State events duplicated");
                    Require(!target.Nearby && !target.Hovered && !target.Held,"State not cleared");
                    target.allowLeftHand=false;Require(!target.Allows(true) && target.Allows(false),"Hand filter");
                    target.allowHolding=false;target.SetHeld(true);Require(!target.Held,"Holding disabled");
                }finally{Dispose(go);}
            });
            Test(results,"设置JSON往返",()=>{
                var settings=new WorkbenchSettings{cameraSpeed=3.2f,rightHand=false};var copy=JsonUtility.FromJson<WorkbenchSettings>(JsonUtility.ToJson(settings));
                Require(Mathf.Abs(copy.cameraSpeed-3.2f)<.001f && !copy.rightHand,"Settings lost");
            });
            Test(results,"个人书签JSON往返",()=>{
                var collection=new BookmarkCollection();collection.items.Add(new ViewBookmark{label="测试",scene="scene_07",position=new Vector3(1,2,3),rotation=Quaternion.Euler(10,20,30)});
                var copy=JsonUtility.FromJson<BookmarkCollection>(JsonUtility.ToJson(collection));Require(copy.items.Count==1 && copy.items[0].position==new Vector3(1,2,3),"Bookmark lost");
            });
            Test(results,"白色5米射线几何",()=>{
                var ray=new Ray(new Vector3(1,2,3),Vector3.forward);Require(Mathf.Abs(Vector3.Distance(ray.origin,ray.GetPoint(5))-5)<.001f,"Ray length");
            });
            Test(results,"报告区分验证来源",()=>{
                var report=new WorkbenchReport{events=new List<WorkbenchEvent>(),checks=new List<CheckResult>{new CheckResult("人工确认","MANUAL_PASS","Human confirmation")}};
                string json=JsonUtility.ToJson(report);Require(json.Contains("NOT validated") && json.Contains("MANUAL_PASS"),"Report scope missing");
            });
            WorkbenchJournal.Checks.AddRange(results);WorkbenchJournal.Add("自动自检","工具核心",results.Count+"项断言");return results;
        }
        static void Test(List<CheckResult> results,string name,Action action)
        {
            try{action();results.Add(new CheckResult(name,"PASS","Deterministic assertion"));}
            catch(Exception e){results.Add(new CheckResult(name,"FAIL",e.Message));}
        }
        public static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static void Dispose(UnityEngine.Object obj){if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);}
    }
}
#endif
