using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a quarter's own lamps follow the existing era stage light.
///
/// `MuseumJourney` already animates four stage lights through TL_scene04_01. The lamps
/// imported from scene 9 are separate objects, saved disabled so the scene does not open
/// with all four quarters lit. This keeps their authored intensities and enables/scales
/// them from the stage light's current intensity, so the old one-quarter-at-a-time
/// sequence remains the single source of truth.
/// </summary>
public sealed class EraLightFollower : MonoBehaviour
{
    public Light eraLight;
    public Light[] lights = new Light[0];

    // The four broad sector fills are what light each quarter's land. They are kept
    // separate from the shore lamps so the land can follow the era's own hour without
    // dimming the local accents that make a boat or a beacon readable.
    [Tooltip("Broad outdoor sector fills scaled by exteriorIntensityScale.")]
    public Light[] sectorLights = new Light[0];

    // Shore lamps, boat spills and beacons are the local accents of the exterior. They
    // are part of each era group because the island is the view through that era's door.
    [Tooltip("Outdoor accents (shore lamps, boats, beacons) scaled by accentIntensityScale.")]
    public Light[] exteriorLights = new Light[0];

    // The quadrant lights are authored against scene 9's lighting, so 1.0 reproduces
    // that room when its stage-light track is at full. Each era has its own hour and its
    // own authored brightness, so the scale is a per-quadrant tuning value rather than a
    // global dimmer.
    [Range(0f, 8f)] public float intensityScale = 1f;

    [Range(0f, 4f)]
    [Tooltip("Scale for the broad sector fill only. The four fills are the 1950s dusk " +
             "(900 W), 1960s daylight (2200 W), 1980s night (180 W) and 1990s dawn " +
             "(1100 W); keeping their relative levels is what makes the land change " +
             "hour with the quadrant rather than change brightness alone.")]
    public float exteriorIntensityScale = 0.1f;

    [Range(0f, 4f)]
    [Tooltip("Scale for the local outdoor accents. These are small sources with their own " +
             "range, so they stay at their authored intensity when their quadrant is lit.")]
    public float accentIntensityScale = 1f;

    [Tooltip("Tint multiplied into the quadrant's interior lamps. White leaves the " +
             "authored colour untouched; the exterior keeps its own authored colour.")]
    public Color colorTint = Color.white;

    [Tooltip("Sky material whose sector brightness follows this quadrant.")]
    public Material skyMaterial;

    [Range(0, 4)] public int skySector;
    [Range(0f, 2f)] public float skyBrightness = 1f;

    [Range(0f, 1f)]
    [Tooltip("Fraction of the sector's sky brightness left when the quadrant is unlit.")]
    public float unlitSkyScale = 0.05f;

    readonly Dictionary<Light, float> originals = new Dictionary<Light, float>();
    readonly Dictionary<Light, Color> originalColors = new Dictionary<Light, Color>();

    void Awake() => CacheOriginals();
    void OnEnable() => CacheOriginals();

    void CacheOriginals()
    {
        // Do not clear here. OnEnable can run after LateUpdate has already driven the
        // follower's lights to zero; recaching then would make the authored intensity
        // unrecoverable. The scene saves these lights disabled, with their authored
        // intensity still present for this cache to read.
        foreach (var light in lights)
            if (light != null && !originals.ContainsKey(light))
            {
                originals[light] = light.intensity;
                originalColors[light] = light.color;
            }
        foreach (var light in sectorLights)
            if (light != null && !originals.ContainsKey(light))
            {
                originals[light] = light.intensity;
                originalColors[light] = light.color;
            }
        foreach (var light in exteriorLights)
            if (light != null && !originals.ContainsKey(light))
            {
                originals[light] = light.intensity;
                originalColors[light] = light.color;
            }
    }

    void LateUpdate()
    {
        if (eraLight == null) return;
        if (originals.Count == 0) CacheOriginals();

        float factor = eraLight.enabled ? Mathf.Clamp01(eraLight.intensity) : 0f;
        bool lit = factor > 0.001f;
        foreach (var light in lights)
        {
            if (light == null) continue;
            if (!originals.TryGetValue(light, out var original))
            {
                originals[light] = original = light.intensity;
                originalColors[light] = light.color;
            }
            float scale = factor * Mathf.Max(0f, intensityScale);
            if (IsSector(light)) scale = factor * Mathf.Max(0f, exteriorIntensityScale);
            else if (IsExterior(light)) scale = factor * Mathf.Max(0f, accentIntensityScale);
            light.intensity = original * scale;
            if (!originalColors.TryGetValue(light, out var originalColor))
                originalColors[light] = originalColor = light.color;
            light.color = IsExterior(light) ? originalColor : originalColor * colorTint;
            light.enabled = lit && light.intensity > 0.0001f;
        }

        ApplySky(factor);
    }

    void ApplySky(float factor)
    {
        if (skyMaterial == null || skySector < 1 || skySector > 4) return;
        string property = "_Sector" + skySector + "Shape";
        if (!skyMaterial.HasProperty(property)) return;

        Vector4 shape = skyMaterial.GetVector(property);
        float unlit = skyBrightness * Mathf.Clamp01(unlitSkyScale);
        shape.x = Mathf.Lerp(unlit, skyBrightness, Mathf.Clamp01(factor));
        skyMaterial.SetVector(property, shape);
    }

    bool IsSector(Light light)
    {
        return sectorLights != null && System.Array.IndexOf(sectorLights, light) >= 0;
    }

    bool IsExterior(Light light)
    {
        if (IsSector(light)) return true;
        if (exteriorLights != null && exteriorLights.Length > 0)
            return System.Array.IndexOf(exteriorLights, light) >= 0;

        // Fallback for an older scene that has not filled exteriorLights yet. The
        // pipeline names these lights by role, so the outdoor ones are recognisable.
        string name = light.name;
        return name.StartsWith("glow_shore_") || name.StartsWith("glow_beacon_") ||
               name == "scene_fill_4" || name == "scene_fill_5" ||
               name == "scene_fill_6" || name == "scene_fill_7";
    }
}
