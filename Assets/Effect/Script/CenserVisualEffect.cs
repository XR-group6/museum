using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

// Animate a separate visual copy; leave VR colliders and story Animators intact.
public sealed class CenserVisualEffect : MonoBehaviour
{
    readonly List<Renderer> originals = new List<Renderer>();
    readonly List<bool> visibility = new List<bool>();
    readonly List<Material> materials = new List<Material>();
    GameObject rig;
    PlayableDirector director;
    Coroutine cue;

    public static void Play(Transform censer)
    {
        if (!censer || !censer.gameObject.activeInHierarchy) return;
        var effect = censer.GetComponent<CenserVisualEffect>();
        if (!effect) effect = censer.gameObject.AddComponent<CenserVisualEffect>();
        effect.enabled = true;
        effect.Begin();
    }

    void Begin()
    {
        if (rig) return;
        var prefab = Resources.Load<GameObject>("CenserInteractionEffect");
        if (!prefab)
        {
            Debug.LogWarning("[Museum] CenserInteractionEffect is missing.", this);
            return;
        }
        var targets = GetComponentsInChildren<MeshRenderer>(true);
        if (targets.Length == 0) return;
        var bounds = targets[0].bounds;
        foreach (var target in targets) bounds.Encapsulate(target.bounds);
        rig = Instantiate(prefab);
        SceneManager.MoveGameObjectToScene(rig, gameObject.scene);
        director = rig.GetComponentInChildren<PlayableDirector>(true);
        var proxy = rig.GetComponentInChildren<MeshRenderer>(true);
        if (!director || !proxy) { Restore(); return; }
        foreach (var renderer in rig.GetComponentsInChildren<Renderer>(true))
        {
            var copies = renderer.sharedMaterials;
            for (int i = 0; i < copies.Length; i++)
            {
                if (!copies[i]) continue;
                copies[i] = new Material(copies[i]);
                materials.Add(copies[i]);
            }
            renderer.sharedMaterials = copies;
        }
        rig.SetActive(true);
        director.time = 0;
        director.Evaluate();
        float scale = bounds.size.magnitude / Mathf.Max(proxy.bounds.size.magnitude, 0.0001f);
        rig.transform.localScale *= scale;
        rig.transform.position += bounds.center - proxy.bounds.center;
        foreach (var target in targets)
        {
            originals.Add(target);
            visibility.Add(target.forceRenderingOff);
            target.forceRenderingOff = true;
        }
        director.extrapolationMode = DirectorWrapMode.None;
        director.Play();
        cue = StartCoroutine(FinishCue());
    }

    IEnumerator FinishCue()
    {
        yield return null;
        while (director && director.state == PlayState.Playing) yield return null;
        cue = null;
        Restore();
    }

    void Restore()
    {
        if (director) director.Stop();
        for (int i = 0; i < originals.Count; i++)
            if (originals[i]) originals[i].forceRenderingOff = visibility[i];
        if (rig) Destroy(rig);
        foreach (var material in materials) if (material) Destroy(material);
        rig = null;
        director = null;
        originals.Clear();
        visibility.Clear();
        materials.Clear();
    }

    void OnDisable()
    {
        if (cue != null) StopCoroutine(cue);
        cue = null;
        Restore();
    }
}
