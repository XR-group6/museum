using System;
using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

[InitializeOnLoad]
public static class MuseumOpenVRSetup
{
    static MuseumOpenVRSetup() { EditorApplication.delayCall += Configure; }

    [MenuItem("Museum/Configure OpenVR startup")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            XRGeneralSettingsPerBuildTarget targets;
            bool changed = false;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out targets) || !targets)
            {
                const string path = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
                targets = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
                if (!targets)
                {
                    targets = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(targets, path);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, targets, true);
                changed = true;
            }
            if (!targets.HasSettingsForBuildTarget(BuildTargetGroup.Standalone))
            { targets.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Standalone); changed = true; }
            if (!targets.HasManagerSettingsForBuildTarget(BuildTargetGroup.Standalone))
            { targets.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Standalone); changed = true; }
            var general = targets.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            var manager = general.Manager;
            var loader = AssetDatabase.LoadAssetAtPath<XRLoader>("Assets/XR/Loaders/OpenVRLoader.asset");
            if (!loader) throw new InvalidOperationException("[Museum XR] OpenVRLoader asset missing.");
            if (!manager.activeLoaders.Contains(loader))
            {
                if (!manager.TryAddLoader(loader, 0)) throw new InvalidOperationException("[Museum XR] Unable to assign OpenVR loader.");
                changed = true;
            }
            if (!general.InitManagerOnStart || !manager.automaticLoading || !manager.automaticRunning) changed = true;
            general.InitManagerOnStart = true;
            manager.automaticLoading = true; manager.automaticRunning = true;
            if (changed)
            {
                EditorUtility.SetDirty(targets); EditorUtility.SetDirty(general); EditorUtility.SetDirty(manager);
                AssetDatabase.SaveAssets();
                Debug.Log("[Museum XR] Standalone OpenVR loader registered; XR initialization and subsystem startup enabled. Exit Play and enter Play again to test.");
            }
        }
        catch (Exception exception) { Debug.LogException(exception); }
    }
}
