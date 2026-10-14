using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

public class VRSubtitle : MonoBehaviour
{
    public TextAsset subtitleFile;
    public Camera vrCamera;
    [Tooltip("When disabled, the subtitle stays at this object's scene position and faces the headset.")]
    public bool followCamera = true;
    public Vector3 cameraOffset = new Vector3(0, -0.35f, 2);
    public float subtitleWidth = 900;
    public int fontSize = 48;
    public float canvasScale = 0.0015f;
    public Font subtitleFont;
    private RectTransform panel, textRect;
    private Text label;
    private Canvas canvas;
    private Font runtimeFont;
    private string displayed;
    private string runtimeText;
    void OnEnable() { SubtitleLanguage.Changed+=LanguageChanged; }
    void OnDisable() { SubtitleLanguage.Changed-=LanguageChanged; }
    void LanguageChanged() { if(label) Refresh(); }

    public void ShowText(string text)
    {
        runtimeText = text ?? "";
        if (label) Refresh();
    }
#if UNITY_EDITOR
    private double nextRefresh;
#endif

    void Awake()
    {
        var root = new GameObject("SubtitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        var group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false;
        panel = root.GetComponent<RectTransform>();
        var background = root.AddComponent<Image>();
        background.color = new Color(0, 0, 0, 0.65f); background.raycastTarget = false;
        var textObject = new GameObject("SubtitleText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(root.transform, false);
        textRect = textObject.GetComponent<RectTransform>();
        label = textObject.GetComponent<Text>();
        runtimeFont = subtitleFont ? null : Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial Unicode MS", "Arial" }, fontSize);
        label.font = subtitleFont ? subtitleFont : runtimeFont;
        label.color = Color.white; label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.supportRichText = false; label.raycastTarget = false;
        Refresh();
    }

    void Refresh()
    {
        string content = runtimeText ?? (subtitleFile ? subtitleFile.text : "");
#if UNITY_EDITOR
        if (runtimeText == null && subtitleFile)
        {
            string path = AssetDatabase.GetAssetPath(subtitleFile);
            try { if (File.Exists(path)) content = File.ReadAllText(path); }
            catch (IOException) { return; }
        }
#endif
        content=SubtitleLanguage.Translate(content);
        if (displayed == content && label.fontSize == fontSize && textRect.rect.width == Mathf.Max(1, subtitleWidth)) return;
        displayed = content; label.text = content; label.fontSize = Mathf.Max(1, fontSize);
        float width = Mathf.Max(1, subtitleWidth);
        textRect.sizeDelta = new Vector2(width, 0);
        float height = Mathf.Max(label.fontSize, label.preferredHeight);
        textRect.sizeDelta = new Vector2(width, height);
        panel.sizeDelta = new Vector2(width + 48, height + 32);
        canvas.gameObject.SetActive(!string.IsNullOrEmpty(content));
    }

    void LateUpdate()
    {
        if (!canvas) return;
#if UNITY_EDITOR
        if (EditorApplication.timeSinceStartup >= nextRefresh)
        {
            nextRefresh = EditorApplication.timeSinceStartup + 0.2;
            Refresh();
        }
#endif
        if (!vrCamera || !vrCamera.isActiveAndEnabled)
        {
            vrCamera = null;
            foreach (var candidate in Camera.allCameras)
            {
                if (candidate.gameObject.scene != gameObject.scene) continue;
                if (candidate.GetComponentInParent<Valve.VR.SteamVR_Camera>() ||
                    candidate.transform.root.name.Contains("CameraRig")) { vrCamera = candidate; break; }
                if (!vrCamera || candidate.CompareTag("MainCamera")) vrCamera = candidate;
            }
        }
        if (!vrCamera) return;
        canvas.worldCamera = vrCamera;
        if (followCamera)
        {
            panel.position = vrCamera.transform.TransformPoint(cameraOffset);
            panel.rotation = vrCamera.transform.rotation;
        }
        else
        {
            panel.position = transform.position;
            // UI's readable front faces opposite its forward direction.
            Vector3 facing = panel.position - vrCamera.transform.position;
            if (facing.sqrMagnitude > 0.0001f)
                panel.rotation = Quaternion.LookRotation(facing, Vector3.up);
        }
        panel.localScale = Vector3.one * Mathf.Max(0.00001f, canvasScale);
    }

    void OnDestroy()
    {
        if (canvas) Destroy(canvas.gameObject);
        if (runtimeFont) Destroy(runtimeFont);
    }
}
