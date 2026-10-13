using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VisualEffectPlayCheckEditor
{
    const string Key = "Museum.VisualEffect.PlayCheck";
    static GameObject target;
    static Renderer original;
    static Material material;
    static Collider collider;
    static Animator animator;
    static double started;
    static int phase;
    static bool sampled;

    static VisualEffectPlayCheckEditor()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (phase == 0)
            {
                target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.name = "Independent interaction target";
                original = target.GetComponent<Renderer>();
                material = original.sharedMaterial;
                collider = target.GetComponent<Collider>();
                animator = target.AddComponent<Animator>();
                CenserVisualEffect.Play(target.transform);
                CenserVisualEffect.Play(target.transform);
                Require(Object.FindObjectsOfType<PlayableDirector>().Length == 1, "Duplicate cue spawned");
                Require(original.forceRenderingOff && collider.enabled && animator.enabled, "Original interaction objects were changed");
                started = EditorApplication.timeSinceStartup;
                phase = 1;
            }
            double elapsed = EditorApplication.timeSinceStartup - started;
            if (phase == 1 && elapsed > 4 && !sampled)
            {
                var rig = Object.FindObjectOfType<PlayableDirector>();
                Require(rig && rig.state == PlayState.Playing, "Original effect Timeline stopped early");
                var proxy = rig.transform.parent.GetComponentInChildren<MeshRenderer>();
                var properties = new MaterialPropertyBlock();
                if (proxy) proxy.GetPropertyBlock(properties);
                Require(proxy && (proxy.sharedMaterial.GetFloat("_Fresnel_Intensity") > 0 || properties.GetFloat("_Fresnel_Intensity") > 0), "Original glow animation did not evaluate, director time=" + rig.time + " material=" + (proxy ? proxy.sharedMaterial.GetFloat("_Fresnel_Intensity") : -1) + " block=" + properties.GetFloat("_Fresnel_Intensity"));
                Require(rig.transform.parent.GetComponentsInChildren<ParticleSystem>().Length == 6, "Original particle activation did not evaluate");
                sampled = true;
            }
            if (phase == 1 && elapsed > 11)
            {
                Require(sampled, "Cue was not sampled");
                Require(!original.forceRenderingOff && original.sharedMaterial == material, "Cue did not restore original renderer/material");
                Require(collider.enabled && animator.enabled, "Cue affected collider/Animator");
                Require(Object.FindObjectsOfType<PlayableDirector>().Length == 0, "Completed rig leaked");
                CenserVisualEffect.Play(target.transform);
                Require(original.forceRenderingOff, "Replay did not start");
                target.GetComponent<CenserVisualEffect>().enabled = false;
                Require(!original.forceRenderingOff, "Interrupted cue did not restore renderer");
                phase = 2;
            }
            else if (phase == 2)
            {
                Require(Object.FindObjectsOfType<PlayableDirector>().Length == 0, "Interrupted rig leaked");
                File.WriteAllText("Library/VisualEffectPort/play-report.txt", "PASS actual Play Mode: original Timeline glow and all six particle systems; duplicate trigger guard; natural completion; material/renderer restoration; collider and Animator preservation; replay; interruption cleanup.");
                SessionState.SetBool(Key, false);
                Debug.Log("VISUAL_EFFECT_PLAY_PASS");
                EditorApplication.Exit(0);
            }
            if (elapsed > 25) throw new Exception("Play check timed out");
        }
        catch (Exception e)
        {
            SessionState.SetBool(Key, false);
            File.WriteAllText("Library/VisualEffectPort/play-failure.txt", e.ToString());
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
