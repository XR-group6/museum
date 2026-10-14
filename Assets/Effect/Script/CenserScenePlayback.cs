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
    public bool playOnStart = true;

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
    }

    void OnEnable()
    {
        if (!visualRoot || !director) return;
        visualRoot.SetActive(true);
        if (originalRenderer) originalRenderer.enabled = false;
    }

    void Start() { if (playOnStart) Play(); }

    [ContextMenu("Play Censer Effect")]
    public void Play()
    {
        if (!Application.isPlaying || !director || !visualRoot) return;
        if (!enabled) enabled = true;
        if (director.state == PlayState.Playing) return;
        director.time = 0;
        director.Evaluate();
        director.Play();
    }

    void OnStopped(PlayableDirector stopped)
    {
        if (stopping) return;
        // Return the persistent replacement to its visible resting pose.
        stopped.time = 0;
        stopped.Evaluate();
    }

    void OnDisable()
    {
        stopping = true;
        if (director) director.Stop();
        if (visualRoot) visualRoot.SetActive(false);
        if (originalRenderer) originalRenderer.enabled = true;
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
