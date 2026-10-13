using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一控制一组灯光：用 dimFactor(0~1) 成比例缩放列表里所有灯的光强。
/// 把该物体挂进 Timeline 的 Animation Track 后，只需动画 dimFactor 一条曲线，
/// 所有灯就会一起从各自原始亮度缓慢变暗/变亮，无需逐个拖灯。
/// </summary>
public class LightGroupFader : MonoBehaviour
{
    [Tooltip("要统一控制的灯。右键组件菜单可自动收集场景内所有 Spot 灯")]
    public List<Light> lights = new List<Light>();

    [Range(0f, 1f)]
    [Tooltip("0 = 全灭，1 = 原始亮度。Timeline 动画这个值即可")]
    public float dimFactor = 1f;

    // 记录每盏灯的原始强度，dimFactor 按比例缩放，保持各灯相对亮度
    private readonly Dictionary<Light, float> originals = new Dictionary<Light, float>();

    private void Awake()
    {
        CacheOriginals();
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    private void CacheOriginals()
    {
        originals.Clear();
        foreach (var l in lights)
        {
            if (l != null) originals[l] = l.intensity;
        }
    }

    private void Apply()
    {
        if (originals.Count == 0) CacheOriginals();
        foreach (var l in lights)
        {
            if (l == null) continue;
            if (!originals.ContainsKey(l)) originals[l] = l.intensity;
            l.intensity = originals[l] * dimFactor;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 编辑器里改 dimFactor 立即预览
        Apply();
    }

    [ContextMenu("收集场景内所有 Spot 灯")]
    private void CollectAllSpots()
    {
        lights.Clear();
        foreach (var l in FindObjectsOfType<Light>(true))
        {
            if (l.type == LightType.Spot) lights.Add(l);
        }
        CacheOriginals();
        Apply();
    }

    [ContextMenu("重新缓存原始强度")]
    private void ResetOriginals()
    {
        CacheOriginals();
        Apply();
    }
#endif
}
