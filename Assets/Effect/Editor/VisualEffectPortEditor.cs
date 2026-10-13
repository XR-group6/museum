using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

public static class VisualEffectPortEditor
{
    const string ResourcesPath = "Assets/Effect/Resources/";
    static readonly List<string> report = new List<string>();
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        report.Add("PASS " + message);
    }
    public static void Build()
    {
        try
        {
            BuildCenser();
            BuildOcean();
            AssetDatabase.SaveAssets();
            VerifyScenes();
            Directory.CreateDirectory("Library/VisualEffectPort");
            File.WriteAllLines("Library/VisualEffectPort/unity-report.txt", report);
            Debug.Log("VISUAL_EFFECT_PORT_PASS\n" + string.Join("\n", report));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Directory.CreateDirectory("Library/VisualEffectPort");
            File.WriteAllText("Library/VisualEffectPort/unity-failure.txt", e.ToString());
            EditorApplication.Exit(1);
        }
    }

    static void BuildCenser()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesPath + "CenserVisualEffectRig.prefab");
        Check(source, "Source censer rig loads");
        var rig = Object.Instantiate(source);
        rig.name = "CenserInteractionEffect";
        var director = rig.GetComponentInChildren<PlayableDirector>(true);
        var censer = rig.GetComponentsInChildren<Transform>(true).Single(x => x.name == "censer");
        var original = (TimelineAsset)director.playableAsset;
        var bindings = new Dictionary<long, Object>();
        foreach (var track in original.GetOutputTracks())
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(track, out string guid, out long id);
            bindings[id] = director.GetGenericBinding(track);
            director.ClearGenericBinding(track);
        }
        string timelinePath = "Assets/Effect/Timeline/TL_censer_interaction.playable";
        if (!AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath))
            Check(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original), timelinePath), "Copy original effect Timeline with its animation clips");
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
        director.playableAsset = timeline;
        foreach (var track in timeline.GetOutputTracks().ToArray())
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(track, out string guid, out long id);
            bindings.TryGetValue(id, out Object target);
            var go = target as GameObject;
            if (target is Component component) go = component.gameObject;
            if (go && (go.transform == censer || go.transform.IsChildOf(censer)))
                director.SetGenericBinding(track, target);
            else timeline.DeleteTrack(track);
        }
        // Drop museum lighting and their Animators; all remaining bindings belong
        // exclusively to the imported censer and its particle hierarchy.
        foreach (Transform child in rig.transform.Cast<Transform>().ToArray())
            if (child != censer && child != director.transform) Object.DestroyImmediate(child.gameObject);
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;
        rig.SetActive(false);
        Check(rig.GetComponentsInChildren<ParticleSystem>(true).Length == 6, "Preserve all six original censer particle systems");
        Check(rig.GetComponentsInChildren<Light>(true).Length == 0, "Effect cannot override scene lighting");
        Check(timeline.GetOutputTracks().Count() == 3, "Only censer animation, particles and activation tracks remain");
        Check(censer.GetComponent<MeshFilter>().sharedMesh, "Original censer mesh resolves");
        foreach (var renderer in rig.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
                Check(material && material.shader && !ShaderUtil.ShaderHasError(material.shader), "Censer material resolves: " + (material ? material.name : "MISSING"));
        EditorUtility.SetDirty(timeline);
        PrefabUtility.SaveAsPrefabAsset(rig, ResourcesPath + "CenserInteractionEffect.prefab");
        Object.DestroyImmediate(rig);
    }

    static void BuildOcean()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Effect/Prefab/water.prefab");
        Check(source, "Original wave prefab loads");
        var ocean = Object.Instantiate(source);
        ocean.name = "VisualEffectOcean";
        ocean.transform.position = Vector3.zero;
        foreach (Transform child in ocean.transform) child.localScale = Vector3.one * 3.3344736f;
        foreach (var renderer in ocean.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            renderer.enabled = renderer.gameObject != ocean;
            renderer.maxParticleSize = 500f;
        }
        foreach (var system in ocean.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = system.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        var surface = ocean.transform.Find("water_2").GetComponent<ParticleSystem>();
        surface.Simulate(1.25f, false, true);
        var particles = new ParticleSystem.Particle[surface.main.maxParticles];
        int count = surface.GetParticles(particles);
        var mesh = surface.GetComponent<ParticleSystemRenderer>().mesh;
        Debug.Log("OCEAN_DIAGNOSTIC count=" + count + " mesh=" + mesh + " mode=" + surface.GetComponent<ParticleSystemRenderer>().renderMode + " active=" + ocean.activeSelf + " size=" + surface.main.startSize.constant + " lifetime=" + surface.main.startLifetime.constant + " emission=" + surface.emission.enabled);
        Check(mesh && count > 0, "Imported ocean base emits mesh particles");
        Bounds bounds = new Bounds();
        bool first = true;
        for (int n = 0; n < count; n++)
        {
            var p = particles[n];
            var matrix = Matrix4x4.TRS(p.position, Quaternion.Euler(p.rotation3D), p.GetCurrentSize3D(surface));
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mesh.bounds.center + Vector3.Scale(mesh.bounds.extents,
                    new Vector3((i&1)==0?-1:1, (i&2)==0?-1:1, (i&4)==0?-1:1));
                corner = matrix.MultiplyPoint3x4(corner);
                if (surface.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                    corner = surface.transform.TransformPoint(corner);
                else if (surface.GetComponent<ParticleSystemRenderer>().alignment == ParticleSystemRenderSpace.Local)
                    corner = surface.transform.rotation * Vector3.Scale(corner, surface.transform.lossyScale);
                if (first) { bounds = new Bounds(corner, Vector3.zero); first = false; }
                else bounds.Encapsulate(corner);
            }
        }
        Check(bounds.size.x > 0.1f && bounds.size.z > 0.1f, "Ocean footprint has horizontal coverage");
        var footprint = ocean.AddComponent<VisualEffectOceanFootprint>();
        footprint.width = bounds.size.x;
        footprint.depth = bounds.size.z;
        footprint.center = bounds.center;
        report.Add("Ocean base bounds: " + bounds);
        foreach (var system in ocean.GetComponentsInChildren<ParticleSystem>(true))
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        PrefabUtility.SaveAsPrefabAsset(ocean, ResourcesPath + "VisualEffectOcean.prefab");
        Object.DestroyImmediate(ocean);
    }

    static void VerifyScenes()
    {
        foreach (string name in new[] { "scene_01", "scene_02", "scene_03" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Project3D/Scenes/" + name + ".unity");
            var roots = scene.GetRootGameObjects();
            var directors = roots.SelectMany(x => x.GetComponentsInChildren<PlayableDirector>(true)).ToArray();
            var animation = directors.Select(x => x.playableAsset).ToArray();
            var colliderIds = roots.SelectMany(x => x.GetComponentsInChildren<Collider>(true)).Select(x => x.GetInstanceID()).ToArray();
            Check(roots.SelectMany(x => x.GetComponentsInChildren<Valve.VR.SteamVR_Behaviour_Pose>(true)).Any(), name + " retains VR controller poses");
            if (name != "scene_01")
            {
                foreach (var renderer in roots.SelectMany(x => x.GetComponentsInChildren<Renderer>(true)).Where(x => x.name.ToLower().Contains("water") || x.name.ToLower().Contains("sea") || x.name.ToLower().Contains("ocean")))
                    report.Add(name + " surface candidate: " + renderer.name + " / " + renderer.GetType().Name + " active=" + renderer.gameObject.activeInHierarchy + " enabled=" + renderer.enabled);
                SceneWaterVisualEffect.Apply(scene);
                SceneWaterVisualEffect.Apply(scene);
                var updated = scene.GetRootGameObjects();
                Check(updated.Count(x => x.GetComponent<SceneWaterVisualEffect>()) == 1, name + " installs ocean only once");
                var waves = updated.Single(x => x.name == "VisualEffect_Waves");
                Check(waves.GetComponentsInChildren<ParticleSystem>(true).Length == 4, name + " has all four wave particle layers");
                foreach (var renderer in waves.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        Check(material && material.shader && !ShaderUtil.ShaderHasError(material.shader), name + " wave shader " + (material ? material.name : "MISSING"));
                foreach (var old in updated.SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Where(x => new[]{"bay_water","strait_water","harbour_water"}.Contains(x.name)))
                    foreach (var renderer in old.GetComponentsInChildren<Renderer>(true))
                        if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) Check(renderer.forceRenderingOff, name + " old sea hidden (" + renderer.GetType().Name + ")");
            }
            Check(directors.Select(x => x.playableAsset).SequenceEqual(animation), name + " story Timeline assets unchanged");
            Check(scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Collider>(true)).Select(x => x.GetInstanceID()).SequenceEqual(colliderIds), name + " all collider instances unchanged");
        }
        // Do not save opened scenes: all user-authored serialization stays intact.
    }
}
