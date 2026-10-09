using UnityEngine;
using UnityEngine.Playables;

namespace IncenseCharacters
{
    /// <summary>
    /// 精确视线跟随：在 LateUpdate 里把 Head 骨骼持续转向目标（例如让父亲一直看女儿）。
    /// 用 director.time 控制生效时机，这样拖动 Timeline 时间轴也能正确表现。
    /// </summary>
    public class IncenseLookAt : MonoBehaviour
    {
        public Transform target;
        public PlayableDirector director;   // 读取时间轴时间；为空则用场景运行时间
        public float lookFrom = 0f;         // 时间轴到该秒后才开始看
        public string headBoneName = "Head";
        public string neckBoneName = "Neck";
        [Range(0f, 1f)] public float neckWeight = 0f; // 颈部也分担一部分（0=只用头）

        Transform head, neck;
        Quaternion neckBase;

        void Awake()
        {
            head = FindBone(headBoneName);
            neck = FindBone(neckBoneName);
            if (neck) neckBase = neck.localRotation;
        }

        Transform FindBone(string n)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
            return null;
        }

        void LateUpdate()
        {
            if (target == null || head == null) return;
            if (director != null)
            {
                if (director.time < lookFrom) return;
            }
            else if (lookFrom > 0f && Time.timeSinceLevelLoad < lookFrom)
            {
                return;
            }

            Vector3 dir = target.position - head.position;
            if (dir.sqrMagnitude < 1e-6f) return;

            Quaternion worldLook = Quaternion.LookRotation(dir, Vector3.up);
            Quaternion parentInv = head.parent != null ? Quaternion.Inverse(head.parent.rotation) : Quaternion.identity;
            Quaternion localLook = parentInv * worldLook;
            head.localRotation = localLook;

            if (neck != null && neckWeight > 0f)
            {
                Quaternion neckParentInv = neck.parent != null ? Quaternion.Inverse(neck.parent.rotation) : Quaternion.identity;
                Quaternion neckLocalLook = neckParentInv * worldLook;
                neck.localRotation = Quaternion.Slerp(neckBase, neckLocalLook, neckWeight);
            }
        }
    }
}
