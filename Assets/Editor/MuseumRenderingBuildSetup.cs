using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

public sealed class MuseumRenderingBuildSetup : IPreprocessBuildWithReport, IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report)
    {
        const string destination = "Assets/Resources/MuseumPostProcessResources.asset";
        var source = AssetDatabase.GUIDToAssetPath("d82512f9c8e5d4a4d938b575d47f88d4");
        if (!AssetDatabase.LoadAssetAtPath<PostProcessResources>(destination))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.CopyAsset(source, destination)) throw new BuildFailedException("Could not include runtime post-processing resources");
        }
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var shaders = settings.FindProperty("m_AlwaysIncludedShaders");
        foreach (string name in new[] { "Unlit/Texture", "Unlit/Transparent" })
        {
            var shader = Shader.Find(name);
            if (!shader) throw new BuildFailedException("Missing required video shader: " + name);
            bool included = false;
            for (int i = 0; i < shaders.arraySize; i++)
                included |= shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader;
            if (included) continue;
            int index = shaders.arraySize++;
            shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null) return;
        if (scene.name == "scene_06_01")
        {
            var diagnostics = new GameObject("Ocean performance report");
            SceneManager.MoveGameObjectToScene(diagnostics, scene);
            diagnostics.AddComponent<MuseumOceanFrameReport>();
            foreach (var particle in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ParticleSystem>(true)))
            {
                var main = particle.main;
                if (main.maxParticles < 100000000) continue;
                main.maxParticles = 64;
                var renderer = particle.GetComponent<ParticleSystemRenderer>();
                if (renderer) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        var resources = AssetDatabase.LoadAssetAtPath<PostProcessResources>(
            AssetDatabase.GUIDToAssetPath("d82512f9c8e5d4a4d938b575d47f88d4"));
        if (!resources) throw new BuildFailedException("Post-processing resources missing");
        foreach (var layer in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PostProcessLayer>(true)))
        {
            var serialized = new SerializedObject(layer);
            var property = serialized.FindProperty("m_Resources");
            if (property.objectReferenceValue) continue;
            property.objectReferenceValue = resources;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
