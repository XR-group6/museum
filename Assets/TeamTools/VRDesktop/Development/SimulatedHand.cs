#if UNITY_EDITOR
using UnityEngine;
namespace XRTeam.DebugTools.Editor
{
    public sealed class SimulatedHand
    {
        public readonly Transform Transform;
        public bool Down {get;private set;}
        public bool Held {get;private set;}
        public bool Released {get;private set;}
        public bool GripDown {get;private set;}
        public bool GripHeld {get;private set;}
        public bool GripReleased {get;private set;}
        public Ray Ray=>new Ray(Transform.position,Transform.forward);
        readonly Renderer marker;
        readonly Material material;
        public SimulatedHand(Transform parent,bool left,Color color)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);obj.name=left?"Simulated Left Controller":"Simulated Right Controller";
            var collider=obj.GetComponent<Collider>();collider.enabled=false;DisposeObject(collider);Transform=obj.transform;Transform.SetParent(parent,false);Transform.localScale=Vector3.one*.035f;
            material=new Material(Shader.Find("Sprites/Default"));material.color=color;marker=obj.GetComponent<Renderer>();marker.sharedMaterial=material;
        }
        public void Place(Camera view,bool left)
        {
            Transform.SetPositionAndRotation(view.transform.TransformPoint(new Vector3(left?-.22f:.22f,-.18f,.4f)),view.transform.rotation);
        }
        public void SetActive(bool value){marker.enabled=value;}
        public void InputFrame(bool trigger,bool grip)
        {
            Down=trigger&&!Held;Released=!trigger&&Held;Held=trigger;
            GripDown=grip&&!GripHeld;GripReleased=!grip&&GripHeld;GripHeld=grip;
        }
        public void Move(Vector3 local,Vector3 angles,Camera view,float speed,float turn,float dt)
        {
            Transform.position+=view.transform.TransformDirection(Vector3.ClampMagnitude(local,1))*speed*dt;
            Transform.Rotate(Vector3.up,angles.y*turn*dt,Space.World);
            Transform.Rotate(Vector3.right,angles.x*turn*dt,Space.Self);
            Transform.Rotate(Vector3.forward,angles.z*turn*dt,Space.Self);
        }
        public void AimAt(Vector3 point){if((point-Transform.position).sqrMagnitude>.0001f)Transform.LookAt(point);}
        public void Dispose(){if(material)DisposeObject(material);if(Transform)DisposeObject(Transform.gameObject);}
        static void DisposeObject(Object obj){if(Application.isPlaying)Object.Destroy(obj);else Object.DestroyImmediate(obj);}
    }
}
#endif
