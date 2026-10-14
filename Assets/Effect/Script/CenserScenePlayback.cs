using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// A persistent visual replacement. The component stays on the original tagged
// interaction target so its colliders and all existing references remain valid.
public sealed class CenserScenePlayback : MonoBehaviour
{
    public GameObject visualRoot;
    public PlayableDirector director;
    public Renderer originalRenderer;
    public bool playOnStart;
    public bool HasDisappeared { get; private set; }

    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<Material[]> originals = new List<Material[]>();
    readonly List<Material> materials = new List<Material>();
    bool stopping;

    void Awake()
    {
        if (!visualRoot || !director) { enabled = false; return; }
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;
        foreach (var renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
        {
            var source = renderer.sharedMaterials;
            renderers.Add(renderer);
            originals.Add(source);
            var copies = (Material[])source.Clone();
            for (int i = 0; i < copies.Length; i++)
            {
                if (!copies[i]) continue;
                copies[i] = new Material(copies[i]);
                materials.Add(copies[i]);
            }
            renderer.sharedMaterials = copies;
        }
        director.stopped += OnStopped;
        director.time = 0;
        director.Evaluate();
    }

    void OnEnable()
    {
        if (!visualRoot || !director) return;
        visualRoot.SetActive(!HasDisappeared);
        if (originalRenderer) originalRenderer.enabled = false;
    }

    void Start() { if (playOnStart) Play(); }

    [ContextMenu("Play Censer Effect")]
    public void Play()
    {
        if (!Application.isPlaying || !director || !visualRoot || HasDisappeared) return;
        if (!enabled) enabled = true;
        if (director.state == PlayState.Playing) return;
        director.time = 0;
        director.Evaluate();
        director.Play();
    }

    void OnStopped(PlayableDirector stopped)
    {
        if (stopping) return;
        HidePermanently();
    }

    public void HidePermanently()
    {
        if (HasDisappeared) return;
        HasDisappeared = true;
        if (visualRoot) visualRoot.SetActive(false);
        if (originalRenderer) originalRenderer.enabled = false;
        foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
    }

    void OnDisable()
    {
        stopping = true;
        if (director) director.Stop();
        if (visualRoot) visualRoot.SetActive(false);
        if (originalRenderer) originalRenderer.enabled = !HasDisappeared;
        stopping = false;
    }

    void OnDestroy()
    {
        if (director) director.stopped -= OnStopped;
        for (int i = 0; i < renderers.Count; i++)
            if (renderers[i]) renderers[i].sharedMaterials = originals[i];
        foreach (var material in materials) if (material) Destroy(material);
    }
}
