#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace XRTeam.DebugTools.Editor
{
    public sealed class WorkbenchTargetInfo
    {
        public GameObject gameObject;
        public string kind,detail;
        public bool warning;
    }
    public static class WorkbenchValidation
    {
        public static Bounds BoundsOf(GameObject obj)
        {
            var bounds=new Bounds(obj.transform.position,Vector3.zero);bool first=true;
            foreach(var r in obj.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            if(first)foreach(var c in obj.GetComponentsInChildren<Collider>()){if(first){bounds=c.bounds;first=false;}else bounds.Encapsulate(c.bounds);}
            return bounds;
        }
        public static List<WorkbenchTargetInfo> ScanTargets()
        {
            var result=new List<WorkbenchTargetInfo>();
            foreach(var target in Object.FindObjectsOfType<DebugInteractionTarget>())
            {
                bool missing=!target.hitCollider || !target.hitCollider.enabled;
                int events=target.onSelect.GetPersistentEventCount();
                result.Add(new WorkbenchTargetInfo{gameObject=target.gameObject,kind="通用事件目标",warning=missing || events==0,
                    detail=missing?"缺少可用碰撞体":events==0?"选择事件没有持久监听；运行时监听需另行确认":$"选择事件 {events} 项 · 接近 {target.proximity:F2}m"});
            }
            foreach(var permit in Object.FindObjectsOfType<PermitInspection>())
                result.Add(new WorkbenchTargetInfo{gameObject=permit.gameObject,kind="入台证",warning=!permit.tableCollider || !permit.closedPrefab || !permit.openPrefab || !permit.tour,
                    detail=$"自由探索 {(permit.Ready?"已开放":"未开放")} · 预览 {(permit.IsOpen?"开启":"关闭")}"});
            foreach(var choice in Object.FindObjectsOfType<InitialLanguageSelector>())
                result.Add(new WorkbenchTargetInfo{gameObject=choice.gameObject,kind="语言选择",detail=choice.Built?"右手射线选择":"等待相机创建面板"});
            foreach(var button in Object.FindObjectsOfType<Button>())
                result.Add(new WorkbenchTargetInfo{gameObject=button.gameObject,kind="uGUI按钮",warning=!button.interactable,detail=button.interactable?"可交互；需世界空间Canvas与GraphicRaycaster":"按钮当前不可交互"});
            foreach(var glow in Object.FindObjectsOfType<SoftInteractionHighlight>())
                result.Add(new WorkbenchTargetInfo{gameObject=glow.gameObject,kind="柔光目标",detail=$"宽度 {glow.width:F3}m · 默认 {glow.idleStrength:F2} · 靠近 {glow.nearbyStrength:F2}"});
            foreach(var narration in Object.FindObjectsOfType<SceneOneNarration>())
                result.Add(new WorkbenchTargetInfo{gameObject=narration.gameObject,kind="博物馆真实剧情入口",detail=narration.WaitingForInteraction?"正在等待香炉接近＋扳机":"剧情条件尚未允许香炉选择"});
            return result;
        }
        public static List<CheckResult> RunSceneChecks()
        {
            var checks=new List<CheckResult>();
            foreach(var target in Object.FindObjectsOfType<DebugInteractionTarget>())
            {
                checks.Add(new CheckResult(target.name+" / Collider",target.hitCollider && target.hitCollider.enabled?"PASS":"FAIL","显式碰撞体引用"));
                if(target.hitCollider && target.hitCollider.GetComponentInParent<DebugInteractionTarget>()!=target)
                    checks.Add(new CheckResult(target.name+" / 归属","FAIL","碰撞体必须位于适配器本体或子物体"));
                if(target.onSelect.GetPersistentEventCount()==0)checks.Add(new CheckResult(target.name+" / 选择事件","WARN","没有持久监听；工具不会猜测业务行为"));
            }
            foreach(var permit in Object.FindObjectsOfType<PermitInspection>())
            {
                bool refs=permit.closedPrefab && permit.openPrefab && permit.tableCollider && permit.tour && permit.tableModel;
                checks.Add(new CheckResult("入台证资源引用",refs?"PASS":"FAIL","模型、碰撞体、场景流程、桌面引用"));
                checks.Add(new CheckResult("ObjectPreview层",LayerMask.NameToLayer("ObjectPreview")>=0?"PASS":"FAIL","Render Texture隔离层"));
            }
            foreach(var text in Object.FindObjectsOfType<Text>())
            {
                if(!text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text) || !text.font)continue;
                var rect=text.rectTransform.rect;
                if(rect.width<=0 || rect.height<=0){checks.Add(new CheckResult(text.name+" / 字幕区域","WARN","布局尚未得到有效尺寸"));continue;}
                if(text.verticalOverflow==VerticalWrapMode.Truncate && text.preferredHeight>rect.height+2)
                    checks.Add(new CheckResult(text.name+" / 高度","WARN","文字可能被裁切，需要观察截图；不是自动判定失败"));
                if(text.horizontalOverflow==HorizontalWrapMode.Overflow && text.preferredWidth>rect.width+2)
                    checks.Add(new CheckResult(text.name+" / 宽度","WARN","文字超出RectTransform宽度"));
            }
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)
                        checks.Add(new CheckResult(t.name+" / 脚本","FAIL","发现Missing Script"));
            if(checks.Count==0)checks.Add(new CheckResult("场景扫描","INFO","没有发现已适配对象；不代表整个场景已验证"));
            checks.Add(new CheckResult("Quest实机","NOT_TESTED","桌面工具不能验证追踪、真实绑定、双眼渲染和设备性能"));
            WorkbenchJournal.Checks.AddRange(checks);WorkbenchJournal.Add("场景检查","当前场景",checks.Count+"项结果");return checks;
        }
    }
}
#endif
