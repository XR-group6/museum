using UnityEngine;
using Valve.VR;

[RequireComponent(typeof(CharacterController))]
public class MuseumLeftStickMove : MonoBehaviour
{
    public Transform headset;
    public SteamVR_Behaviour_Pose leftHand;
    [Min(0)] public float speed = 1.2f;
    [Range(0, 0.9f)] public float deadzone = 0.2f;
    public Vector2 lastAxis;
    public bool inputBound;
    [Tooltip("Only allow forward input, used when approaching the censer in the final scene.")]
    public bool forwardOnly;
    public bool forwardBackwardOnly;
    private CharacterController body;
    private SteamVR_Action_Vector2 move;
    [Min(0)] public float gravity = 9.81f;
    private float verticalSpeed;
    [Min(1)] public float fallResetDistance = 5f;
    private Vector3 safePosition;

    void OnEnable()
    {
        body = GetComponent<CharacterController>();
        safePosition = transform.position;
        move = SteamVR_Input.GetAction<SteamVR_Action_Vector2>("platformer", "Move");
        if (!headset || !leftHand || move == null)
        {
            Debug.LogError("[Museum movement] Missing headset, left hand or platformer/Move action.", this);
            enabled = false;
            return;
        }
        move.actionSet.Activate(SteamVR_Input_Sources.LeftHand);
        Debug.Log("[Museum movement] Left stick enabled: head-relative, speed=" + speed + ", deadzone=" + deadzone, this);
    }

    void Update()
    {
        inputBound = SteamVR_Input.initialized && move.GetActive(SteamVR_Input_Sources.LeftHand);
        lastAxis = inputBound && leftHand.isActiveAndEnabled && leftHand.isValid ? move.GetAxis(SteamVR_Input_Sources.LeftHand) : Vector2.zero;
        MoveAxis(lastAxis, Time.deltaTime);
    }

    public void MoveAxis(Vector2 axis, float deltaTime)
    {
        if (forwardOnly) axis = new Vector2(0, Mathf.Max(0, axis.y));
        else if (forwardBackwardOnly) axis = new Vector2(0, axis.y);
        if (!headset || deltaTime <= 0) return;
        if (!body) body = GetComponent<CharacterController>();
        if (!body.enabled) return;
        if (Application.isPlaying && transform.position.y < safePosition.y - fallResetDistance)
        {
            body.enabled = false; transform.position = safePosition; body.enabled = true;
            verticalSpeed = 0;
        }
        // Follow the physical headset offset without moving the tracked camera itself.
        Vector3 localHead = transform.InverseTransformPoint(headset.position);
        body.height = Mathf.Clamp(localHead.y, 0.6f, 2.2f);
        body.center = new Vector3(localHead.x, body.height * 0.5f, localHead.z);
        if (body.isGrounded && verticalSpeed < 0) verticalSpeed = -0.5f;
        verticalSpeed = Mathf.Max(verticalSpeed - gravity * deltaTime, -20f);
        var flags = body.Move((CalculateVelocity(axis, headset.forward, speed, deadzone) + Vector3.up * verticalSpeed) * deltaTime);
        if ((flags & CollisionFlags.Below) != 0) verticalSpeed = -0.5f;
    }

    void OnDisable()
    {
        if (move != null) move.actionSet.Deactivate(SteamVR_Input_Sources.LeftHand);
        lastAxis = Vector2.zero; inputBound = false; verticalSpeed = 0;
    }

    public static Vector3 CalculateVelocity(Vector2 axis, Vector3 look, float speed, float deadzone)
    {
        float magnitude = Mathf.Min(axis.magnitude, 1f);
        if (magnitude <= deadzone) return Vector3.zero;
        Vector3 forward = Vector3.ProjectOnPlane(look, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector2 input = axis.normalized * ((magnitude - deadzone) / (1f - deadzone));
        return (forward * input.y + right * input.x) * speed;
    }
}
