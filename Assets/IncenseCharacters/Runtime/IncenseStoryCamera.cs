using UnityEngine;
using UnityEngine.Playables;

namespace IncenseCharacters
{
    /// <summary>
    /// Compatibility component for scenes saved with the retired story camera.
    /// Camera pose and projection are now owned by the player/XR camera rig.
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

        void OnEnable()
        {
            // Also handles old components still loaded in the editor when scripts reload.
            var cam = GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = 60f;
            enabled = false;
        }
    }
}
