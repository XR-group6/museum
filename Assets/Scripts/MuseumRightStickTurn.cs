using UnityEngine;
using Valve.VR;

// Turn before walking so simultaneous movement uses the new heading.
[DefaultExecutionOrder(-10)]
public class MuseumRightStickTurn : MonoBehaviour
{
    public Transform headset;
    public SteamVR_Behaviour_Pose rightHand;
    [Min(0)] public float degreesPerSecond = 60f;
    [Range(0, 0.9f)] public float deadzone = 0.2f;
    public Vector2 lastAxis;
    public bool inputBound;
    private SteamVR_Action_Vector2 stick;

    void OnEnable()
    {
        stick = SteamVR_Input.GetAction<SteamVR_Action_Vector2>("platformer", "Move");
        if (!headset || !rightHand || stick == null)
        {
            Debug.LogError("[Museum turn] Missing headset, right-hand pose or platformer/Move action.", this);
            enabled = false;
            return;
        }
        stick.actionSet.Activate(SteamVR_Input_Sources.RightHand);
    }

    void Update()
    {
        inputBound = SteamVR_Input.initialized && stick.GetActive(SteamVR_Input_Sources.RightHand);
        lastAxis = inputBound && rightHand.isActiveAndEnabled && rightHand.isValid ? stick.GetAxis(SteamVR_Input_Sources.RightHand) : Vector2.zero;
        TurnAxis(lastAxis, Time.deltaTime);
    }

    public void TurnAxis(Vector2 axis, float deltaTime)
    {
        float amount = Mathf.Clamp(axis.x, -1f, 1f);
        if (!headset || Mathf.Abs(amount) <= deadzone || deltaTime <= 0) return;
        float rate = Mathf.Sign(amount) * (Mathf.Abs(amount) - deadzone) / (1f - deadzone);
        // Pivot at the tracked head so room-scale offsets do not orbit around the rig origin.
        var body = GetComponent<CharacterController>();
        bool restoreBody = body && body.enabled;
        if (restoreBody) body.enabled = false;
        try { transform.RotateAround(headset.position, Vector3.up, rate * degreesPerSecond * deltaTime); }
        finally { if (restoreBody) body.enabled = true; }
    }

    void OnDisable()
    {
        if (stick != null) stick.actionSet.Deactivate(SteamVR_Input_Sources.RightHand);
        lastAxis = Vector2.zero; inputBound = false;
    }
}
