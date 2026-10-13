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
        if (narration && !narration.WaitingForInteraction) return;
        if (triggered || !rightHand.isValid || !trigger.GetState(SteamVR_Input_Sources.RightHand)) return;
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
                CenserVisualEffect.Play(ancestor);
                break;
            }
        }
    }

    void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, interactionDistance); }
}
