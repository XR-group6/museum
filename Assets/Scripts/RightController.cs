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
        if (choicePointer) choicePointer.enabled = false;
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

    void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, interactionDistance); }
    void OnDisable() { if (choicePointer) choicePointer.enabled = false; }
    void OnDestroy() { if (pointerMaterial) Destroy(pointerMaterial); }
}
