using UnityEngine;
namespace IncenseCharacters
{
    [DisallowMultipleComponent]
    public class IncenseCharacterPlayer:MonoBehaviour
    {
        public Animator animator;
        public string[] actionNames;
        [Min(0)] public float blendSeconds=.15f;
        public string currentAction="Idle";
        void Awake(){if(!animator)animator=GetComponent<Animator>();}
        public void PlayAction(string actionName)
        {
            if(!animator)return;
            int hash=Animator.StringToHash("Base Layer."+actionName);
            if(!animator.HasState(0,hash)){Debug.LogWarning("Unknown action: "+actionName,this);return;}
            currentAction=actionName;animator.CrossFadeInFixedTime(hash,blendSeconds,0,0);
        }
        public Transform FindBone(string boneName)
        {
            foreach(var t in GetComponentsInChildren<Transform>(true))if(t.name==boneName)return t;
            return null;
        }
        public void AttachProp(Transform prop,string boneName)
        {
            var bone=FindBone(boneName);if(prop&&bone)prop.SetParent(bone,true);
        }
    }
}
