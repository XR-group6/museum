// Run in the disposable verification project; write only scene-level overrides.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SceneSevenLightingMatch
{
    struct Settings
    {
        public Color color; public float intensity, temperature; public bool useTemperature;
        public Quaternion rotation;
        public Settings(Light light) { color = light.color; intensity = light.intensity; temperature = light.colorTemperature; useTemperature = light.useColorTemperature; rotation = light.transform.rotation; }
    }
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Project3D/Scenes/scene_05.unity");
            var reference = UnityEngine.Object.FindObjectsOfType<Light>(true).ToDictionary(l => l.name, l => new Settings(l));
            Color sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor;
            float intensity = RenderSettings.ambientIntensity;
            var mode = RenderSettings.ambientMode;
            string skyProperties = EditorJsonUtility.ToJson(RenderSettings.skybox);
            string skyShader = RenderSettings.skybox.shader.name;
            Capture("Scene05Reference.png");
            var scene = EditorSceneManager.OpenScene("Assets/Project3D/Scenes/scene_07.unity");
            var tour = UnityEngine.Object.FindObjectOfType<MuseumSceneTour>();
            if (!tour || !tour.movement || !tour.turning) throw new Exception("Exploration configuration missing before lighting change");
            string tourBefore = EditorJsonUtility.ToJson(tour);
            var report = new List<string>();
            foreach (var light in UnityEngine.Object.FindObjectsOfType<Light>(true))
            {
                string key = light.name;
                if (key == "sun_dawn") key = "sun_sea";
                if (key == "glow_candle_e" || key == "glow_censer") key = "candle_flame_e";
                if (key == "glow_candle_w") key = "candle_flame_w";
                if (key.StartsWith("glow_vessel")) key = "scene_fill";
                if (!reference.TryGetValue(key, out var settings)) throw new Exception("Unmapped scene-seven light: " + light.name);
                light.color = settings.color; light.intensity = settings.intensity;
                light.colorTemperature = settings.temperature; light.useColorTemperature = settings.useTemperature;
                if (light.type == LightType.Directional) { light.transform.rotation = settings.rotation; PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform); }
                PrefabUtility.RecordPrefabInstancePropertyModifications(light); EditorUtility.SetDirty(light);
                report.Add(light.name + " <- " + key + ": color=" + light.color + ", intensity=" + light.intensity);
            }
            RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground; RenderSettings.ambientIntensity = intensity; RenderSettings.ambientMode = mode;
            if (!RenderSettings.skybox || RenderSettings.skybox.shader.name != skyShader) throw new Exception("Incompatible scene sky shaders");
            EditorJsonUtility.FromJsonOverwrite(skyProperties, RenderSettings.skybox); EditorUtility.SetDirty(RenderSettings.skybox);
            if (tourBefore != EditorJsonUtility.ToJson(tour)) throw new Exception("Exploration settings changed");
            if (scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))) != 0)
                throw new Exception("Missing scripts");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Capture("Scene07Matched.png");
            File.WriteAllLines("SceneSevenLightingValidation.txt", report);
            Debug.Log("SCENE_SEVEN_LIGHTING_PASS: lights, sky and ambient matched; exploration settings preserved; no missing scripts");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Capture(string path)
    {
        var source = UnityEngine.Object.FindObjectsOfType<Camera>().First(c => c.transform.root.name.Contains("CameraRig"));
        var camera = new GameObject("Temporary lighting preview").AddComponent<Camera>();
        camera.CopyFrom(source); camera.stereoTargetEye = StereoTargetEyeMask.None;
        camera.transform.SetPositionAndRotation(source.transform.position + Vector3.up * 1.6f, source.transform.rotation);
        var target = new RenderTexture(960, 540, 24); camera.targetTexture = target;
        var previous = RenderTexture.active;
        camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(camera.gameObject);
    }
}
