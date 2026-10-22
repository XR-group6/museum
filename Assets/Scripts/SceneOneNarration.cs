using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneOneNarration : MonoBehaviour
{
    public AudioClip museumEcho, censerIntro, censerAfterGrab, censerFinal;
    public VRSubtitle narrationSubtitle, interactionSubtitle;
    public TextAsset introSubtitles;
    [Tooltip("Start times in seconds within the censer intro audio; one per paragraph.")]
    public float[] paragraphStartTimes = { 0f, 5.65f, 14.2f };
    public string nextScene = "scene_02";
    public CenserScenePlayback censerPlayback;
    [Tooltip("Time in the interaction Timeline when the dissolve reaches 1.")]
    public float dissolveEndTime = 8.133333f;
    public bool WaitingForInteraction { get; private set; }
    public bool ShowingMemoryChoice => choiceRoot && choiceRoot.activeSelf && !loadingScene;
    private AudioSource source;
    private bool failed;
    private string[] paragraphs;
    private GameObject choiceRoot;
    private BoxCollider yesCollider;
    private Button yesButton;
    private Font choiceFont;
    private bool followupFinished, dissolveFinished, loadingScene, hovered;
    public bool CanInspect { get; private set; }
    VRSubtitle rotationHint;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = false; source.spatialBlend = 0; source.pitch = 1;
        if (!censerPlayback)
            foreach (var candidate in FindObjectsOfType<CenserScenePlayback>())
                if (candidate.gameObject.scene == gameObject.scene) { censerPlayback = candidate; break; }
        if (censerPlayback) censerPlayback.playOnStart = false;
    }

    IEnumerator Start()
    {
        // Hold the existing story until the initial headset language choice is made.
        while(!SubtitleLanguage.HasSelected) yield return null;
        if (!narrationSubtitle || !interactionSubtitle)
        { Fail("Missing narration or interaction subtitle component."); yield break; }
        narrationSubtitle.ShowText(""); interactionSubtitle.ShowText("");
        if (!museumEcho || museumEcho.length < 5f || !censerIntro || !censerAfterGrab || !censerFinal || !introSubtitles ||
            !censerPlayback || !censerPlayback.director || !censerPlayback.director.playableAsset ||
            !Application.CanStreamedLevelBeLoaded(nextScene))
        { Fail("Missing scene-one audio, subtitle asset or next scene."); yield break; }
        paragraphs = Regex.Split(introSubtitles.text.Trim(), @"(?:<br\s*/?>\s*){2}|\r?\n\s*\r?\n", RegexOptions.IgnoreCase);
        if (paragraphs.Length != 3 || paragraphStartTimes.Length != 3 ||
            paragraphStartTimes[0] < 0 || paragraphStartTimes[1] <= paragraphStartTimes[0] ||
            paragraphStartTimes[2] <= paragraphStartTimes[1] || paragraphStartTimes[2] >= censerIntro.length)
        { Fail("Intro requires three paragraphs and three increasing audio cue times."); yield break; }
        // Preload to avoid gaps between narration clips.
        foreach (var clip in new[] { museumEcho, censerIntro, censerAfterGrab, censerFinal })
        {
            clip.LoadAudioData();
            double timeout = Time.realtimeSinceStartupAsDouble + 15;
            while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
            if (clip.loadState != AudioDataLoadState.Loaded) { Fail("Failed to load " + clip.name); yield break; }
        }
        source.clip = museumEcho; source.Play();
        while (AudioListener.pause || (source.isPlaying && source.timeSamples / (float)museumEcho.frequency < 5f))
            yield return null;
        source.Stop();
        interactionSubtitle.ShowText("请用右手触摸并握住香炉。");
        WaitingForInteraction = true;
    }

    IEnumerator PlayIntroduction()
    {
        CanInspect = true;
        ShowRotationHint();
        source.clip = censerIntro; source.Play();
        int paragraph = -1;
        while (source.isPlaying || AudioListener.pause)
        {
            float audioTime = source.timeSamples / (float)censerIntro.frequency;
            int current = -1;
            for (int i = 0; i < paragraphStartTimes.Length; i++)
                if (audioTime >= paragraphStartTimes[i]) current = i;
            if (current != paragraph)
            {
                paragraph = current;
                narrationSubtitle.ShowText(current >= 0 ? paragraphs[current].Trim() : "");
            }
            yield return null;
        }
        narrationSubtitle.ShowText("");
        if (rotationHint) rotationHint.ShowText("");
        while (!FindSceneCamera()) yield return null;
        ShowMemoryChoice();
    }

    void ShowRotationHint()
    {
        var camera = FindSceneCamera();
        if (!camera) return;
        var go = new GameObject("Censer rotation wall hint");
        go.transform.SetParent(transform, false);
        Vector3 direction = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        if (direction.sqrMagnitude < .01f) direction = Vector3.forward;
        Vector3 position = camera.transform.position + direction * 4f;
        float nearestWall = float.PositiveInfinity;
        // Find the room surface ahead, excluding the censer and small props.
        foreach (var hit in Physics.RaycastAll(camera.transform.position, direction, 30f))
        {
            if (hit.distance < 1.5f || Mathf.Abs(hit.normal.y) > .35f ||
                Mathf.Abs(Vector3.Dot(hit.normal, direction)) < .7f ||
                hit.collider.GetComponentInParent<CenserScenePlayback>()) continue;
            if (hit.distance < nearestWall)
            {
                nearestWall = hit.distance;
                position = hit.point - direction * .04f;
            }
        }
        // Published room meshes may have no physics colliders. Use a temporary
        // mesh query to locate the actual wall without changing the scene asset.
        foreach (var mesh in FindObjectsOfType<MeshFilter>())
        {
            if (!mesh.sharedMesh || mesh.gameObject.scene != gameObject.scene) continue;
            Transform room = mesh.transform;
            while (room && room.name != "museum_hall") room = room.parent;
            if (!room) continue;
            var query = new GameObject("Temporary room wall query");
            query.transform.SetPositionAndRotation(mesh.transform.position, mesh.transform.rotation);
            query.transform.localScale = mesh.transform.lossyScale;
            var collider = query.AddComponent<MeshCollider>(); collider.sharedMesh = mesh.sharedMesh;
            RaycastHit wall;
            if (collider.Raycast(new Ray(camera.transform.position, direction), out wall, 30f) &&
                wall.distance > 1.5f && Mathf.Abs(wall.normal.y) < .35f && wall.distance < nearestWall)
            {
                nearestWall = wall.distance;
                position = wall.point - direction * .04f;
            }
            collider.enabled = false;
            Destroy(query);
        }
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        rotationHint = go.AddComponent<VRSubtitle>();
        rotationHint.followCamera = false; rotationHint.faceCamera = false;
        rotationHint.vrCamera = camera; rotationHint.subtitleFont = narrationSubtitle.subtitleFont;
        rotationHint.subtitleWidth = 1500; rotationHint.fontSize = 42;
        rotationHint.ShowBilingualText("试试用右摇杆的激光长按扳机转动香炉。",
            "Aim the right controller laser at the censer; hold the trigger and rotate your controller.");
    }

    public void Interact()
    {
        if (!WaitingForInteraction || failed) return;
        WaitingForInteraction = false;
        interactionSubtitle.ShowText("");
        StartCoroutine(PlayIntroduction());
    }

    IEnumerator AfterInteraction()
    {
        // These two assets already contain speech slowed to 0.85, so keep pitch at 1.
        var clips = new[] { censerAfterGrab, censerFinal };
        var captions = new[] { "欸，你可认得我?", "或许，你愿意听听我渡海而来的故事?" };
        for (int i = 0; i < clips.Length; i++)
        {
            narrationSubtitle.ShowText(captions[i]);
            source.clip = clips[i]; source.Play();
            while (source.isPlaying || AudioListener.pause) yield return null;
            narrationSubtitle.ShowText("");
        }
        followupFinished = true;
    }

    IEnumerator WaitForDissolve()
    {
        double end = System.Math.Min(dissolveEndTime, censerPlayback.director.playableAsset.duration);
        while (!censerPlayback.HasDisappeared && censerPlayback.director.time < end) yield return null;
        censerPlayback.HidePermanently();
        // Stop without resetting the model or restoring its renderer.
        censerPlayback.director.Stop();
        dissolveFinished = true;
    }

    Camera FindSceneCamera()
    {
        if (narrationSubtitle.vrCamera && narrationSubtitle.vrCamera.isActiveAndEnabled)
            return narrationSubtitle.vrCamera;
        foreach (var camera in Camera.allCameras)
            if (camera.gameObject.scene == gameObject.scene && camera.isActiveAndEnabled) return camera;
        return null;
    }

    void ShowMemoryChoice()
    {
        var camera = FindSceneCamera();
        choiceRoot = new GameObject("EnterCenserMemory", typeof(RectTransform), typeof(Canvas));
        choiceRoot.transform.SetParent(camera.transform, false);
        var canvas = choiceRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera; canvas.sortingOrder = 110;
        choiceRoot.transform.localPosition = new Vector3(0, 0.12f, 1.6f);
        choiceRoot.transform.localRotation = Quaternion.identity;
        choiceRoot.transform.localScale = Vector3.one * 0.0015f;
        choiceRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 230);
        choiceFont = narrationSubtitle.subtitleFont ? narrationSubtitle.subtitleFont :
            Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" }, 48);
        AddChoiceText(choiceRoot.transform, "是否愿意进入香炉的记忆", new Vector2(0, 65), new Vector2(900, 80));
        var button = new GameObject("Yes", typeof(RectTransform), typeof(Image), typeof(Button), typeof(BoxCollider));
        button.transform.SetParent(choiceRoot.transform, false);
        var rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(240, 90); rect.anchoredPosition = new Vector2(0, -35);
        yesButton = button.GetComponent<Button>();
        yesButton.targetGraphic = button.GetComponent<Image>();
        yesButton.onClick.AddListener(EnterMemory);
        yesCollider = button.GetComponent<BoxCollider>();
        yesCollider.size = new Vector3(240, 90, 16);
        AddChoiceText(button.transform, "是", Vector2.zero, rect.sizeDelta);
    }

    void AddChoiceText(Transform parent, string content, Vector2 position, Vector2 size)
    {
        var text = new GameObject("Label", typeof(RectTransform), typeof(Text));
        text.transform.SetParent(parent, false);
        var rect = text.GetComponent<RectTransform>(); rect.sizeDelta = size; rect.anchoredPosition = position;
        var label = text.GetComponent<Text>();
        label.font = choiceFont; label.fontSize = 48; label.text = content;
        label.alignment = TextAnchor.MiddleCenter; label.color = Color.white; label.raycastTarget = false;
        text.AddComponent<LocalizedUIText>().Bind(content);
    }

    public bool PointAtMemoryChoice(Ray ray, bool pressed, out Vector3 hitPoint)
    {
        hitPoint = ray.GetPoint(5);
        if (!ShowingMemoryChoice || !yesCollider) return false;
        RaycastHit hit;
        bool rayHit = yesCollider.Raycast(ray, out hit, 5);
        hovered = rayHit;
        if (rayHit) hitPoint = hit.point;
        if (hovered && pressed) yesButton.onClick.Invoke();
        return hovered;
    }

    void Update()
    {
        if (!ShowingMemoryChoice) return;
        var camera = FindSceneCamera();
        if (camera)
        {
            if (choiceRoot.transform.parent != camera.transform)
                choiceRoot.transform.SetParent(camera.transform, false);
            choiceRoot.transform.localPosition = new Vector3(0, .12f, 1.6f);
            choiceRoot.transform.localRotation = Quaternion.identity;
            if (Input.GetMouseButtonDown(0))
            {
                RaycastHit hit;
                if (yesCollider.Raycast(camera.ScreenPointToRay(Input.mousePosition), out hit, 5)) yesButton.onClick.Invoke();
            }
        }
        yesButton.targetGraphic.color = hovered ? new Color(0.2f, 0.65f, 0.55f) : new Color(0.12f, 0.36f, 0.3f);
        hovered = false;
    }

    public void EnterMemory()
    {
        if (!ShowingMemoryChoice || failed) return;
        loadingScene = true;
        CanInspect = false;
        choiceRoot.SetActive(false);
        var controller = FindObjectOfType<RightController>();
        if (controller) controller.EndInspection();
        censerPlayback.Play();
        StartCoroutine(WaitForDissolve());
        StartCoroutine(AfterInteraction());
        StartCoroutine(LoadMemoryAfterNarration());
    }

    IEnumerator LoadMemoryAfterNarration()
    {
        // A quick click must not cut off censer 2 or censer 3.
        while (!followupFinished || !dissolveFinished) yield return null;
        SceneManager.LoadSceneAsync(nextScene);
    }

    void Fail(string reason)
    {
        failed = true; WaitingForInteraction = false;
        Debug.LogError("[Museum] " + reason, this);
    }

    void OnDisable()
    {
        StopAllCoroutines(); WaitingForInteraction = false; CanInspect = false;
        if (rotationHint) rotationHint.ShowText("");
        if (source) source.Stop();
        if (narrationSubtitle) narrationSubtitle.ShowText("");
        if (interactionSubtitle) interactionSubtitle.ShowText("");
        if (choiceRoot) choiceRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (choiceRoot) Destroy(choiceRoot);
        if (choiceFont && (!narrationSubtitle || choiceFont != narrationSubtitle.subtitleFont)) Destroy(choiceFont);
    }
}
