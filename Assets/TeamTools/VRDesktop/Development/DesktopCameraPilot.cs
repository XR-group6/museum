#if UNITY_EDITOR
using UnityEngine;
namespace XRTeam.DebugTools.Editor
{
    public sealed class DesktopCameraPilot
    {
        public Camera View {get;private set;}
        public Vector3 HomePosition {get;private set;}
        public Quaternion HomeRotation {get;private set;}
        float pitch;
        public DesktopCameraPilot(Transform parent)
        {
            var go=new GameObject("Desktop CameraRig Preview");go.tag="MainCamera";go.transform.SetParent(parent,false);
            View=go.AddComponent<Camera>();View.nearClipPlane=.02f;View.farClipPlane=500;View.fieldOfView=65;View.stereoTargetEye=StereoTargetEyeMask.None;
            go.AddComponent<AudioListener>();
        }
        public void SetHome(Vector3 pos,Quaternion rotation){HomePosition=pos;HomeRotation=rotation;Reset();}
        public void Reset(){View.transform.SetPositionAndRotation(HomePosition,HomeRotation);pitch=SignedPitch(View.transform.eulerAngles.x);}
        public void SetPose(Vector3 pos,Quaternion rotation){View.transform.SetPositionAndRotation(pos,rotation);pitch=SignedPitch(rotation.eulerAngles.x);}
        static float SignedPitch(float p)=>p>180?p-360:p;
        public void Step(Vector3 axes,Vector2 look,float dt,WorkbenchSettings settings)
        {
            Vector3 forward=Vector3.ProjectOnPlane(View.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            Vector3 delta=(right*axes.x+Vector3.up*axes.y+forward*axes.z)*settings.cameraSpeed*dt;
            if(settings.collisionMovement && delta.sqrMagnitude>0 && Physics.SphereCast(View.transform.position,settings.collisionRadius,delta.normalized,out var hit,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                delta=delta.normalized*Mathf.Max(0,hit.distance-.01f);
            View.transform.position+=delta;
            pitch=Mathf.Clamp(pitch+look.y*settings.lookSpeed*dt,-85,85);
            float yaw=View.transform.eulerAngles.y+look.x*settings.lookSpeed*dt;
            View.transform.rotation=Quaternion.Euler(pitch,yaw,0);
        }
    }
}
#endif
