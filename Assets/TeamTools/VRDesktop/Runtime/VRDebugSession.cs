using System;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace XRTeam.DebugTools
{
    /// <summary>Player builds always keep the native input and scene-loading path.</summary>
    public static class VRDebugSession
    {
#if UNITY_EDITOR
        public static bool Active;
        public static Func<string, LoadSceneMode, AsyncOperation> SceneLoader;
        public static Camera View;
#else
        public static bool Active => false;
#endif
        public static AsyncOperation LoadSceneAsync(string name, LoadSceneMode mode=LoadSceneMode.Single)
        {
#if UNITY_EDITOR
            if(Active && SceneLoader!=null)return SceneLoader(name,mode);
#endif
            return SceneManager.LoadSceneAsync(name,mode);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
#if UNITY_EDITOR
            Active=false;SceneLoader=null;View=null;
#endif
        }
    }
}
