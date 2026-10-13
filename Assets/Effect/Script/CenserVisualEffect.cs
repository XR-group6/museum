using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Uses the VisualEffect dissolve shader on the existing interactive censer mesh.
// The original renderers, mesh, colliders and materials are restored after the cue.
public sealed class CenserVisualEffect : MonoBehaviour
{
    static readonly int DissolveScale = Shader.PropertyToID("_Dissolve_Scale");
    static readonly int FresnelIntensity = Shader.PropertyToID("_Fresnel_Intensity");
    static readonly int BaseTexture = Shader.PropertyToID("_Base");

    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<Material[]> originals = new List<Material[]>();
    readonly List<Material> instances = new List<Material>();
    readonly List<GameObject> particles = new List<GameObject>();
    Coroutine cue;

    public static void Play(Transform censer)
    {
        if (!censer) return;
        var effect = censer.GetComponent<CenserVisualEffect>();
        if (!effect) effect = censer.gameObject.AddComponent<CenserVisualEffect>();
        effect.Begin();
    }

    void Begin()
    {
        if (cue != null) return;
        var template = Resources.Load<Material>("VisualEffectCenser");
        if (!template || !template.shader || !template.shader.isSupported)
        {
            Debug.LogWarning("[Museum] VisualEffect censer shader is unavailable.", this);
            return;
        }

        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            var source = renderer.sharedMaterials;
            var replacement = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var material = new Material(template);
                if (source[i] && source[i].mainTexture && material.HasProperty(BaseTexture))
                    material.SetTexture(BaseTexture, source[i].mainTexture);
                material.SetFloat(DissolveScale, 0f);
                material.SetFloat(FresnelIntensity, 0f);
                replacement[i] = material;
                instances.Add(material);
            }
            renderers.Add(renderer);
            originals.Add(source);
            renderer.sharedMaterials = replacement;
        }
        if (renderers.Count == 0) return;
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
        SpawnParticles("VisualEffectGlow", bounds, 26f, 0.8f);
        SpawnParticles("VisualEffectSpark", bounds, 8f, 0.45f);
        cue = StartCoroutine(Animate());
    }

    void SpawnParticles(string resource, Bounds bounds, float rate, float lifetime)
    {
        var material = Resources.Load<Material>(resource);
        if (!material || !material.shader || !material.shader.isSupported) return;
        var obj = new GameObject(resource);
        obj.transform.position = bounds.center;
        var system = obj.AddComponent<ParticleSystem>();
        var main = system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = lifetime;
        main.startSpeed = Mathf.Max(0.05f, bounds.extents.y * 0.45f);
        main.startSize = Mathf.Max(0.025f, bounds.extents.magnitude * 0.18f);
        main.maxParticles = 96;
        var emission = system.emission;
        emission.rateOverTime = rate;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.03f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.7f);
        obj.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        particles.Add(obj);
        system.Play();
    }

    IEnumerator Animate()
    {
        float elapsed = 0f;
        // Timing follows the imported TL_museum_dissolve curve without binding
        // that older Timeline to this scene's unrelated animation objects.
        while (elapsed < 8.15f)
        {
            float fresnel = Mathf.Lerp(0f, 8f, Mathf.Clamp01((elapsed - 1f) / 1.5f));
            float dissolve = Mathf.Clamp01((elapsed - 3.93f) / 4.2f);
            foreach (var material in instances)
            {
                material.SetFloat(FresnelIntensity, fresnel);
                material.SetFloat(DissolveScale, dissolve);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        Restore();
        cue = null;
    }

    void Restore()
    {
        for (int i = 0; i < renderers.Count; i++)
            if (renderers[i]) renderers[i].sharedMaterials = originals[i];
        foreach (var material in instances)
            if (material) Destroy(material);
        foreach (var particle in particles)
            if (particle) Destroy(particle);
        renderers.Clear();
        originals.Clear();
        instances.Clear();
        particles.Clear();
    }

    void OnDestroy() { Restore(); }
}
