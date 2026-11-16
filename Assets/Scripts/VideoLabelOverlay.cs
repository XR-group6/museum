using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Sits on a child quad in front of a video screen and shows a title card
/// while the clip is waiting to be played. Hides itself once playback starts,
/// and comes back if the clip is paused before it ends.
/// Deliberately self-contained: it only reads the parent VideoPlayer's state.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VideoLabelOverlay : MonoBehaviour
{
    [Tooltip("Title card texture shown over the video screen while it waits for a click.")]
    public Texture texture;
    [Tooltip("Seconds of slack when deciding whether the clip already finished.")]
    public float finishedTolerance = 0.15f;

    VideoPlayer player;
    MeshRenderer meshRenderer;

    void Awake()
    {
        var filter = GetComponent<MeshFilter>();
        if (!filter) filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = BuildQuad();
        meshRenderer = GetComponent<MeshRenderer>();
        if (!meshRenderer) meshRenderer = gameObject.AddComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Unlit/Transparent"));
        material.mainTexture = texture;
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.enabled = true;
    }

    void OnDestroy()
    {
        if (meshRenderer && meshRenderer.sharedMaterial) Destroy(meshRenderer.sharedMaterial);
    }

    void Update()
    {
        if (!player) player = GetComponentInParent<VideoPlayer>();
        if (!player || !meshRenderer) return;
        double length = player.clip ? player.clip.length : 0;
        bool finished = length > 0 && player.time >= length - Mathf.Max(0, finishedTolerance);
        bool waiting = !player.isPlaying && !finished;
        meshRenderer.enabled = waiting;
    }

    // A one-unit quad whose visible face points along local -Z, matching how
    // the project's video screens are oriented. Both windings are emitted so
    // the card cannot be back-face culled.
    Mesh BuildQuad()
    {
        var mesh = new Mesh { name = "Video label quad" };
        mesh.vertices = new[]
        {
            new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0),
            new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0)
        };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        mesh.triangles = new[] { 0, 1, 2, 2, 1, 3, 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }
}
