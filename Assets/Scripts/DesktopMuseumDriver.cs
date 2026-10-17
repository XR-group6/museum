using UnityEngine;

/// <summary>
/// Desktop (keyboard + mouse) substitute for the SteamVR interaction rig, so the
/// carousel scene can be exercised without a headset.
///
/// It does not invent a second interaction: the trigger runs the same check
/// <see cref="RightController"/> runs -- a collider whose ancestor carries the
/// "Censer" tag, within reach -- and plays the same <see cref="CenserVisualEffect"/>.
/// Movement is a plain CharacterController walk, which is the non-VR equivalent of
/// <see cref="MuseumLeftStickMove"/>.
///
/// Wired up by Assets/Editor/AddDesktopTestPlayer.cs. This file is yours, not the
/// pipeline's: Assets/Project3D/ is generated and would overwrite anything put there.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class DesktopMuseumDriver : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The child camera; pitch is applied here, yaw to the root.")]
    public Transform head;
    [Tooltip("What the 1-4 keys jump to, in order. In the carousel that is one house "
           + "per era; in a furnished room it is one censer per era.")]
    public Transform[] focusPoints;
    [Tooltip("Metres to stand back from a focus point, measured outward from the middle of "
           + "the group. 0 walks in on the target's own house direction instead, which suits "
           + "a small prop in a room and not a building.")]
    [Min(0)] public float focusStandoff = 0f;

    [Header("Movement")]
    [Min(0)] public float walkSpeed = 2.2f;
    [Min(0)] public float runSpeed = 4.5f;
    [Min(0)] public float gravity = 9.81f;
    [Min(1)] public float fallResetDistance = 5f;

    [Header("Look")]
    [Min(0)] public float mouseSensitivity = 2.2f;
    [Min(0)] public float turnSpeed = 90f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("Interaction")]
    [Min(0)] public float interactDistance = 2f;
    public KeyCode interactKey = KeyCode.E;
    public bool showHud = true;

    CharacterController body;
    float pitch;
    float verticalSpeed;
    Vector3 safePosition;
    Transform censerInReach;
    Texture2D dot;
    GUIStyle hud;

    void OnEnable()
    {
        body = GetComponent<CharacterController>();
        safePosition = transform.position;
        if (!head)
        {
            var camera = GetComponentInChildren<Camera>();
            if (camera) head = camera.transform;
        }
        if (!head) Debug.LogError("[Desktop test] No head camera; look and interaction are disabled.", this);
    }

    void OnDisable()
    {
        verticalSpeed = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (!head) return;
        Look();
        Walk();
        Teleports();
        Interact();
    }

    /// <summary>Mouse-look while the right button is held, arrows as the no-mouse fallback.</summary>
    void Look()
    {
        bool held = Input.GetKey(KeyCode.Mouse1);
        if (held)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            transform.Rotate(0f, Input.GetAxisRaw("Mouse X") * mouseSensitivity, 0f, Space.Self);
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * mouseSensitivity, minPitch, maxPitch);
        }
        else if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Input.GetKey(KeyCode.LeftArrow)) transform.Rotate(0f, -turnSpeed * Time.deltaTime, 0f, Space.Self);
        if (Input.GetKey(KeyCode.RightArrow)) transform.Rotate(0f, turnSpeed * Time.deltaTime, 0f, Space.Self);
        head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Walk()
    {
        float x = 0f, z = 0f;
        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.W)) z += 1f;
        if (Input.GetKey(KeyCode.S)) z -= 1f;

        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 wish = forward * z + right * x;
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        if (body.isGrounded && verticalSpeed < 0f) verticalSpeed = -0.5f;
        verticalSpeed = Mathf.Max(verticalSpeed - gravity * Time.deltaTime, -20f);
        var flags = body.Move((wish * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
        if ((flags & CollisionFlags.Below) != 0) verticalSpeed = -0.5f;

        if (body.isGrounded) safePosition = transform.position;
        else if (transform.position.y < safePosition.y - fallResetDistance)
        {
            var failed = transform.position;
            SetPosition(safePosition);
            verticalSpeed = 0f;
            Debug.Log("[Desktop test] Fell from " + failed + " to " + safePosition + "; recovered.", this);
        }
    }

    void Teleports()
    {
        // 1-4 land in front of each era's censer; R re-snaps to the floor.
        for (int i = 0; i < 4; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SnapToFocus(i);
        if (Input.GetKeyDown(KeyCode.R)) DropToFloor();
    }

    void Interact()
    {
        censerInReach = NearestCenser();
        if (!censerInReach || !Input.GetKeyDown(interactKey)) return;
        Debug.Log("[Desktop test] Trigger on censer '" + censerInReach.name + "' under '"
            + (censerInReach.parent ? censerInReach.parent.name : "?") + "'.", this);
        CenserVisualEffect.Play(censerInReach);
    }

    /// <summary>
    /// What the crosshair is on, if that is a censer; otherwise the nearest censer in
    /// reach. Both go through the same tag-and-ancestor rule <see cref="RightController"/>
    /// uses, so a censer that works under VR works here.
    /// </summary>
    Transform NearestCenser()
    {
        Vector3 eye = head.position;
        if (Physics.Raycast(eye, head.forward, out RaycastHit look, interactDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform aimed = TaggedAncestor(look.collider.transform);
            if (aimed) return aimed;
        }

        Transform best = null;
        float bestSquared = float.MaxValue;
        foreach (var collider in Physics.OverlapSphere(eye, interactDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform ancestor = TaggedAncestor(collider.transform);
            if (!ancestor) continue;
            float squared = (collider.ClosestPoint(eye) - eye).sqrMagnitude;
            if (squared < bestSquared) { bestSquared = squared; best = ancestor; }
        }
        return best;
    }

    static Transform TaggedAncestor(Transform from)
    {
        Transform ancestor = from;
        while (ancestor && !ancestor.CompareTag("Censer")) ancestor = ancestor.parent;
        return ancestor;
    }

    public void SnapToFocus(int index)
    {
        if (focusPoints == null || index < 0 || index >= focusPoints.Length || !focusPoints[index])
        {
            Debug.LogWarning("[Desktop test] Nothing wired for focus point " + index + ".", this);
            return;
        }

        Transform target = focusPoints[index];
        if (focusStandoff > 0f)
        {
            // A building is too big to walk up to from a metre away -- that pose is inside
            // its own wall. Stand off from the middle of the group instead, which lands you
            // outside the ring looking in, which is how a house is actually reviewed.
            Vector3 outward = Vector3.ProjectOnPlane(AimPoint(target) - GroupCentre(), Vector3.up);
            if (outward.sqrMagnitude > 0.0001f)
            {
                SnapTo(target, focusStandoff, outward.normalized);
                return;
            }
        }
        SnapTo(target);
    }

    /// <summary>Middle of everything wired to focusPoints, in world space.</summary>
    Vector3 GroupCentre()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (Transform point in focusPoints)
            if (point) { sum += AimPoint(point); count++; }
        return count > 0 ? sum / count : transform.position;
    }

    /// <summary>
    /// Stand a step in front of a censer, on the floor, looking at it -- the pose the
    /// trigger test needs, since the effect plays on the censer itself.
    /// </summary>
    public void SnapTo(Transform target, float distance = 1.2f, Vector3 approach = default)
    {
        if (!target) return;
        if (!body) body = GetComponent<CharacterController>();

        // Aim at the middle of the object, not its pivot. A prop's pivot is its base on
        // the floor of whatever it stands on, so aiming there puts the crosshair on the
        // shelf edge and the ray hits that instead of the object.
        Vector3 aim = AimPoint(target);
        Vector3 footprint = new Vector3(aim.x, target.position.y, aim.z);

        // Which side to stand on is geometry, not something to guess: a censer against a
        // wall is approached from inside its room, a house on a plaza from the middle of
        // the plaza. The caller knows which one it wired, so it can say; with nothing
        // said, fall back to the direction towards the object's house.
        Vector3 towards = approach.sqrMagnitude > 0.0001f
            ? Vector3.ProjectOnPlane(approach, Vector3.up).normalized
            : Inward(target);
        SetPosition(footprint + towards * distance);
        if (!DropToFloor())
            Debug.LogWarning("[Desktop test] Nothing solid under '" + target.name + "'; used its own height.", this);
        safePosition = transform.position;

        transform.rotation = Quaternion.Euler(0f, YawTo(transform.position, aim), 0f);
        if (head)
        {
            Vector3 eye = head.position;
            Vector3 toTarget = aim - eye;
            pitch = Mathf.Clamp(-Mathf.Atan2(toTarget.y, new Vector2(toTarget.x, toTarget.z).magnitude)
                * Mathf.Rad2Deg, minPitch, maxPitch);
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        Physics.SyncTransforms();
    }

    /// <summary>Centre of a censer's visible geometry, in world space.</summary>
    static Vector3 AimPoint(Transform censer)
    {
        bool any = false;
        Bounds bounds = new Bounds(censer.position, Vector3.zero);
        foreach (Renderer renderer in censer.GetComponentsInChildren<Renderer>())
        {
            if (renderer is LineRenderer || renderer is TrailRenderer) continue;
            if (!any) { bounds = renderer.bounds; any = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return any ? bounds.center : censer.position;
    }

    /// <summary>
    /// Drops the player onto the first solid thing below, ignoring its own capsule.
    /// Public so the editor wiring tool can land the spawn pose without starting play mode.
    /// </summary>
    public bool DropToFloor()
    {
        if (!body) body = GetComponent<CharacterController>();
        bool wasEnabled = body.enabled;
        body.enabled = false;
        Physics.SyncTransforms();
        Vector3 from = transform.position + Vector3.up * 1.5f;
        bool dropped = false;
        foreach (var hit in Physics.RaycastAll(from, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider is CharacterController) continue;   // never stand on ourselves
            transform.position = new Vector3(transform.position.x, hit.point.y + 0.02f, transform.position.z);
            dropped = true;
            break;
        }
        body.enabled = wasEnabled;
        Physics.SyncTransforms();
        return dropped;
    }

    void SetPosition(Vector3 position)
    {
        if (!body) body = GetComponent<CharacterController>();
        bool wasEnabled = body.enabled;
        body.enabled = false;
        transform.position = position;
        body.enabled = wasEnabled;
    }

    /// <summary>Horizontal direction from a censer towards the middle of its house.</summary>
    static Vector3 Inward(Transform censer)
    {
        Transform root = censer;
        while (root.parent && root.parent.parent) root = root.parent;
        Vector3 direction = Vector3.ProjectOnPlane(root.position - censer.position, Vector3.up);
        if (direction.sqrMagnitude < 0.04f) direction = Vector3.ProjectOnPlane(-censer.forward, Vector3.up);
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
        return direction.normalized;
    }

    static float YawTo(Vector3 from, Vector3 to)
    {
        Vector3 direction = Vector3.ProjectOnPlane(to - from, Vector3.up);
        return direction.sqrMagnitude < 0.000001f ? 0f : Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }

    void OnGUI()
    {
        if (!showHud) return;
        if (hud == null)
        {
            hud = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = false };
            hud.normal.textColor = Color.white;
            dot = new Texture2D(1, 1);
            dot.SetPixel(0, 0, Color.white);
            dot.Apply();
        }

        string target = censerInReach ? censerInReach.parent.name : "-";
        GUI.Box(new Rect(8, 8, 372, 108), GUIContent.none);
        GUI.Label(new Rect(16, 14, 360, 96),
            "Desktop test (no headset)\n"
            + "WASD walk   Shift run   hold RMB look   arrows turn\n"
            + "1-4 jump to each focus point   R re-snap to floor\n"
            + "E trigger the censer you are on (nothing is tagged here)\n"
            + "Censer under the crosshair: " + target, hud);

        // Crosshair.
        GUI.color = censerInReach ? Color.green : Color.white;
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 3f, Screen.height * 0.5f - 1f, 6f, 2f), dot);
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 1f, Screen.height * 0.5f - 3f, 2f, 6f), dot);
        GUI.color = Color.white;
    }
}
