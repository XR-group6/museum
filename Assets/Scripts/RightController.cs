using UnityEngine;
using Valve.VR;

public class RightController : MonoBehaviour
{
    public SteamVR_Behaviour_Pose rightHand;
    public Collider[] censerColliders;
    public SceneAudioTimeline sequence;
    public SceneOneNarration narration;
    public float interactionDistance = 0.15f;
    private SteamVR_Action_Boolean trigger;
    private bool triggered;
    private LineRenderer choicePointer;
    private Material pointerMaterial;

    bool rotating, inspectionSaved;
    Quaternion handStart, visualStart, initialRotation;
    Vector3 initialPosition, rotationCenter, startOffset;
    Transform inspectionVisual;

    void Start()
    {
        if (!rightHand)
            foreach (var pose in FindObjectsOfType<SteamVR_Behaviour_Pose>())
                if (pose.inputSource == SteamVR_Input_Sources.RightHand) { rightHand = pose; break; }
        trigger = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("GrabPinch");
        if (!rightHand || (!sequence && !narration) || censerColliders == null || censerColliders.Length == 0 || trigger == null)
        {
            Debug.LogError("[Museum] RightController requires a right-hand pose, GrabPinch, censer colliders and sequence.", this);
            enabled = false;
        }
        else trigger.actionSet.Activate(SteamVR_Input_Sources.RightHand);
    }

    void Update()
    {
        transform.SetPositionAndRotation(rightHand.transform.position, rightHand.transform.rotation);
        if (narration && narration.CanInspect)
            InspectCenser();
        else if (rotating) rotating = false;
        if (narration && narration.ShowingMemoryChoice && rightHand.isActiveAndEnabled && rightHand.isValid)
        {
            if (!choicePointer)
            {
                var pointer = new GameObject("MemoryChoicePointer");
                pointer.transform.SetParent(transform, false);
                choicePointer = pointer.AddComponent<LineRenderer>();
                choicePointer.positionCount = 2; choicePointer.useWorldSpace = true;
                choicePointer.startWidth = 0.004f; choicePointer.endWidth = 0.002f;
                pointerMaterial = new Material(Shader.Find("Sprites/Default"));
                choicePointer.sharedMaterial = pointerMaterial;
            }
            Vector3 hit;
            bool hovered = narration.PointAtMemoryChoice(new Ray(transform.position, transform.forward),
                trigger.GetStateDown(SteamVR_Input_Sources.RightHand), out hit);
            choicePointer.enabled = narration.ShowingMemoryChoice;
            choicePointer.SetPosition(0, transform.position); choicePointer.SetPosition(1, hit);
            choicePointer.startColor = choicePointer.endColor = hovered ? Color.green : Color.white;
            return;
        }
        if (choicePointer && (!narration || !narration.CanInspect)) choicePointer.enabled = false;
        if (narration && !narration.WaitingForInteraction) return;
        if (triggered || !rightHand.isValid) return;
        bool pressed = narration ? trigger.GetStateDown(SteamVR_Input_Sources.RightHand) :
            trigger.GetState(SteamVR_Input_Sources.RightHand);
        if (!pressed) return;
        foreach (var collider in censerColliders)
        {
            if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            Transform ancestor = collider.transform;
            while (ancestor && !ancestor.CompareTag("Censer")) ancestor = ancestor.parent;
            if (ancestor && Vector3.Distance(transform.position, collider.ClosestPoint(transform.position)) <= interactionDistance)
            {
                triggered = true;
                Debug.Log("[Museum] Right trigger activated censer.", this);
                if (narration) narration.Interact();
                else sequence.Begin();
                if (!narration) CenserVisualEffect.Play(ancestor);
                break;
            }
        }
    }

    void InspectCenser()
    {
        if (!rightHand.isValid || !rightHand.isActiveAndEnabled) { rotating = false; if (choicePointer) choicePointer.enabled = false; return; }
        var playback = narration.censerPlayback;
        if (!playback || !playback.visualRoot) return;
        inspectionVisual = playback.visualRoot.transform;
        if (!inspectionSaved)
        {
            inspectionSaved = true;
            initialPosition = inspectionVisual.position; initialRotation = inspectionVisual.rotation;
            Bounds bounds = new Bounds(inspectionVisual.position, Vector3.zero);
            bool first = true;
            foreach (var renderer in inspectionVisual.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
            }
            rotationCenter = bounds.center;
        }
        if (!choicePointer)
        {
            var go = new GameObject("Censer inspection pointer"); go.transform.SetParent(transform, false);
            choicePointer = go.AddComponent<LineRenderer>(); choicePointer.positionCount = 2;
            choicePointer.startWidth = .004f; choicePointer.endWidth = .002f;
            pointerMaterial = new Material(Shader.Find("Sprites/Default")); choicePointer.sharedMaterial = pointerMaterial;
        }
        var ray = new Ray(transform.position, transform.forward);
        // The authored censer collider is a trigger. Query it directly, as the
        // permit UI does, so global trigger filtering and the vitrine cannot
        // discard the intended inspection target.
        RaycastHit hit = new RaycastHit();
        bool hovered = false;
        float nearest = 5f;
        foreach (var target in censerColliders)
        {
            if (!target || !target.enabled || !target.gameObject.activeInHierarchy) continue;
            RaycastHit candidate;
            if (target.Raycast(ray, out candidate, nearest))
            { hovered = true; nearest = candidate.distance; hit = candidate; }
        }
        bool held = trigger.GetState(SteamVR_Input_Sources.RightHand);
        if (hovered && trigger.GetStateDown(SteamVR_Input_Sources.RightHand))
        {
            rotating = true; handStart = rightHand.transform.rotation;
            visualStart = inspectionVisual.rotation; startOffset = inspectionVisual.position - rotationCenter;
        }
        if (!held) rotating = false;
        if (rotating)
        {
            // Same controller quaternion delta used by the permit inspection.
            Quaternion delta = rightHand.transform.rotation * Quaternion.Inverse(handStart);
            inspectionVisual.rotation = delta * visualStart;
            inspectionVisual.position = rotationCenter + delta * startOffset;
        }
        choicePointer.enabled = true;
        choicePointer.SetPosition(0, ray.origin); choicePointer.SetPosition(1, hovered ? hit.point : ray.GetPoint(5));
        choicePointer.startColor = choicePointer.endColor = hovered ? Color.green : Color.white;
    }

    public void EndInspection()
    {
        rotating = false;
        // Restore the authored pose before playing the unchanged animation.
        if (inspectionSaved && inspectionVisual) inspectionVisual.SetPositionAndRotation(initialPosition, initialRotation);
        inspectionSaved = false;
        if (choicePointer) choicePointer.enabled = false;
    }

    void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, interactionDistance); }
    void OnDisable() { if (choicePointer) choicePointer.enabled = false; }
    void OnDestroy() { if (pointerMaterial) Destroy(pointerMaterial); }
}
