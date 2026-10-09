using UnityEngine;
using UnityEngine.Playables;

namespace IncenseCharacters
{
    /// <summary>
    /// 香炉第一视角相机的动画：
    /// 敬香时贴近看父亲的手肘/鞠躬（看不见香），敬完香（手臂垂下）后镜头拉开、看向父亲的脸。
    /// 用 director.time 驱动，拖时间轴也能正确表现。
    /// </summary>
    public class IncenseStoryCamera : MonoBehaviour
    {
        public PlayableDirector director;
        public Vector3 lookAnchor = new Vector3(0f, 0f, 1.1f); // 父亲脚底的世界坐标

        [Header("敬香时（近景，只看手/动作，不看头）")]
        public float closeFOV = 40f;
        public float lookHeightClose = 1.2f; // 平视父亲胸部（≈女儿头部）

        [Header("拉开后（看到父亲全部上半身）")]
        public float wideFOV = 65f;
        public float lookHeightWide = 1.2f; // 仍平视胸部，只拉大视野

        [Header("拉开时机")]
        public float pullBackAt = 5f;       // 敬香结束
        public float pullBackDuration = 1.5f;

        Camera cam;

        void Awake() { cam = GetComponent<Camera>(); }

        void LateUpdate()
        {
            if (cam == null) return;
            float t = director != null ? (float)director.time : Time.timeSinceLevelLoad;
            float k = Mathf.Clamp01((t - pullBackAt) / pullBackDuration);
            cam.fieldOfView = Mathf.Lerp(closeFOV, wideFOV, k);
            cam.transform.LookAt(lookAnchor + Vector3.up * Mathf.Lerp(lookHeightClose, lookHeightWide, k));
        }
    }
}
