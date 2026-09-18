using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class player : MonoBehaviour
{
    [Header("References")]
    public Transform cam; // assign the actual camera rig here
    public InputActionAsset inputActionsAsset; // assign InputSystem_Actions here (optional)

    InputAction moveAction;
    InputAction sprintAction;
    InputAction jumpAction;
    InputAction slideAction;

    Vector2 moveInput = Vector2.zero;

    [Header("Visuals / Animation")]
    public Transform visualModel; // child model to visually rotate (keep physics upright)
    public Animator animator; // optional Animator (Mixamo)
    public string slideAnimationState = "Running Slide";

    [Header("Slide")]
    public float slideColliderY = 0.5f;
    public float slideDuration = 0.7f;
    public float slideEndBlend = 0.2f;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.8f;
    public float torqueAmount = 5f;
    public bool lockUpright = true;
    public float turnSpeed = 10f;

    [Header("Inventory")]
    public List<string> items = new List<string>();

    [Header("Jump")]
    public float jumpForce = 5f;
    public float groundCheckDistance = 0.02f;
    public LayerMask groundMask = ~0;

    Rigidbody rb;
    float h, v;
    bool sprint;
    bool jumpQueued;
    bool slidePressedThisFrame;
    bool slideRequested;
    bool isSliding;
    float slideTimer;
    bool isGrounded;
    CapsuleCollider capsuleCollider;
    BoxCollider boxCollider;
    Vector3 normalCapsuleCenter;
    Vector3 normalBoxCenter;
    float sphereRadius = 0.5f;
    bool hasSpeedParameter;
    bool hasGroundedParameter;
    bool hasJumpParameter;
    string groundedParameterName;

    void Start()
    {
        if (cam == null)
            cam = Camera.main != null ? Camera.main.transform : null;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            animator.applyRootMotion = false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                hasSpeedParameter |= parameter.name == "Speed";
                if (parameter.name == "IsGrounded" || parameter.name == "isGrounded")
                {
                    hasGroundedParameter = true;
                    groundedParameterName = parameter.name;
                }
                hasJumpParameter |= parameter.name == "Jump" && parameter.type == AnimatorControllerParameterType.Float;
            }
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.useGravity = true;

        var sc = GetComponent<SphereCollider>();
        if (sc != null)
            sphereRadius = Mathf.Max(0.01f, sc.radius * Mathf.Max(transform.localScale.x, transform.localScale.y));

        capsuleCollider = GetComponent<CapsuleCollider>();
        if (capsuleCollider == null)
            capsuleCollider = GetComponentInChildren<CapsuleCollider>();
        if (capsuleCollider != null)
            normalCapsuleCenter = capsuleCollider.center;

        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
            boxCollider = GetComponentInChildren<BoxCollider>();
        if (boxCollider != null && capsuleCollider == null)
            normalBoxCenter = boxCollider.center;

        SetupInputActions();

        if (lockUpright)
        {
            rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    void Update()
    {
        if (moveAction != null)
        {
            moveInput = moveAction.ReadValue<Vector2>();
            h = moveInput.x;
            v = moveInput.y;
        }
        else
        {
            h = Input.GetAxis("Horizontal");
            v = Input.GetAxis("Vertical");
        }

        if (sprintAction != null)
            sprint = sprintAction.ReadValue<float>() > 0.5f ||
                (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed);
        else
            sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (jumpAction != null)
        {
            if (jumpAction.WasPressedThisFrame())
                jumpQueued = true;
        }
        else if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpQueued = true;
        }

        if (slideAction != null)
        {
            bool keyboardSlidePressed = Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
            slidePressedThisFrame = slideAction.WasPressedThisFrame() || keyboardSlidePressed;
        }
        else
        {
            slidePressedThisFrame = Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
        }

        if (slidePressedThisFrame)
            slideRequested = isGrounded;
    }

    void FixedUpdate()
    {
        CheckGround();

        Vector3 move = Vector3.zero;
        if (cam != null)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            move = camForward * v + camRight * h;
        }
        else
        {
            move = (Vector3.forward * v + Vector3.right * h);
        }

        if (move.sqrMagnitude > 1f)
            move.Normalize();

        bool canStartSlide = isGrounded;
        if (!isSliding && slideRequested && canStartSlide)
        {
            isSliding = true;
            slideRequested = false;
            slideTimer = Mathf.Max(0.05f, slideDuration);
            SetCapsuleSliding(true);
            if (animator != null)
            {
                string stateName = slideAnimationState;
                if (!animator.HasState(0, Animator.StringToHash(stateName)))
                    stateName = "Running Slide";

                if (animator.HasState(0, Animator.StringToHash(stateName)))
                    animator.Play(stateName, 0, 0f);
            }
        }
        else if (isSliding)
        {
            slideTimer -= Time.fixedDeltaTime;
            if (slideTimer <= 0f)
            {
                isSliding = false;
                SetCapsuleSliding(false);
                StopSlideAnimation(move);
            }
        }

        bool sprinting = sprint && !isSliding && move.sqrMagnitude > 0.01f;
        float speed = moveSpeed * (sprinting ? sprintMultiplier : 1f);
        if (isSliding)
            speed *= 1.35f;
        Vector3 desiredVelocity = new Vector3(move.x, 0f, move.z) * speed;
        desiredVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, desiredVelocity, 0.18f);

        if (!lockUpright)
        {
            if (move.sqrMagnitude > 0.001f)
            {
                Vector3 torque = new Vector3(-move.z, 0f, move.x) * torqueAmount;
                rb.AddTorque(torque, ForceMode.Acceleration);
            }
        }
        else
        {
            rb.angularVelocity = new Vector3(0f, rb.angularVelocity.y, 0f);

            if (move.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(move, Vector3.up);
                Quaternion flatTarget = Quaternion.Euler(0f, targetRot.eulerAngles.y, 0f);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, flatTarget, turnSpeed * Time.fixedDeltaTime));
            }
        }

        if (jumpQueued && isGrounded && !isSliding)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            isGrounded = false;
            jumpQueued = false;

            if (animator != null && hasJumpParameter)
            {
                animator.SetFloat("Jump", 0.1f);
                animator.Play("Jumping", 0, 0f);
            }
        }
        else
        {
            jumpQueued = false;
        }

        if (visualModel != null && animator == null && isGrounded)
        {
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            float vMag = flatVel.magnitude;
            if (vMag > 0.001f && sphereRadius > 0.0001f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, flatVel.normalized);
                float angleDeg = (vMag / sphereRadius) * Mathf.Rad2Deg * Time.fixedDeltaTime;
                visualModel.Rotate(axis, angleDeg, Space.World);
            }
        }

        if (animator != null)
        {
            if (hasSpeedParameter)
            {
                float inputSpeed = moveInput.magnitude * (sprinting ? sprintMultiplier : 1f);
                animator.SetFloat("Speed", Mathf.Clamp01(inputSpeed));
            }

            if (hasGroundedParameter)
                animator.SetBool(groundedParameterName, isGrounded);

            if (hasJumpParameter)
                animator.SetFloat("Jump", isGrounded ? 0f : 0.1f);
        }
    }

    void SetCapsuleSliding(bool sliding)
    {
        if (capsuleCollider != null)
        {
            if (sliding)
                capsuleCollider.center = new Vector3(normalCapsuleCenter.x, slideColliderY, normalCapsuleCenter.z);
            else
                capsuleCollider.center = normalCapsuleCenter;
        }
        else if (boxCollider != null)
        {
            if (sliding)
                boxCollider.center = new Vector3(normalBoxCenter.x, slideColliderY, normalBoxCenter.z);
            else
                boxCollider.center = normalBoxCenter;
        }

        Physics.SyncTransforms();
    }

    void StopSlideAnimation(Vector3 move)
    {
        if (animator == null)
            return;

        string stateName = move.sqrMagnitude > 0.01f ? "Walking" : "Standing Idle";
        if (animator.HasState(0, Animator.StringToHash(stateName)))
            animator.CrossFadeInFixedTime(stateName, Mathf.Max(0.05f, slideEndBlend), 0, 0f);
    }

    void CheckGround()
    {
        Collider ownCollider = capsuleCollider != null ? capsuleCollider : boxCollider;
        if (ownCollider == null)
            ownCollider = GetComponent<Collider>();
        if (ownCollider == null)
            ownCollider = GetComponentInChildren<Collider>();

        if (ownCollider == null || rb.linearVelocity.y > 0.1f)
        {
            isGrounded = false;
            return;
        }

        Bounds bounds = ownCollider.bounds;
        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + 0.01f, bounds.center.z);
        float castDistance = 0.01f + Mathf.Min(groundCheckDistance, 0.03f);
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        isGrounded = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.rigidbody != rb && !hit.transform.IsChildOf(transform))
            {
                isGrounded = true;
                break;
            }
        }
    }

    void SetupInputActions()
    {
        if (inputActionsAsset == null) return;

        var map = inputActionsAsset.FindActionMap("Player", true);
        if (map == null) return;

        moveAction = map.FindAction("Move", true);
        sprintAction = map.FindAction("Sprint", false);
        jumpAction = map.FindAction("Jump", false);
        slideAction = map.FindAction("Crouch", false);

        if (moveAction != null) moveAction.Enable();
        if (sprintAction != null) sprintAction.Enable();
        if (jumpAction != null)
        {
            jumpAction.Enable();
        }
        if (slideAction != null)
            slideAction.Enable();
    }

    public bool AddItem(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return false;

        if (items.Contains(itemName))
            return false;

        items.Add(itemName);
        return true;
    }

    public bool HasItem(string itemName)
    {
        return items.Contains(itemName);
    }

    public bool RemoveItem(string itemName)
    {
        return items.Remove(itemName);
    }

    public void ClearInventory()
    {
        items.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + Vector3.down * (groundCheckDistance * 0.5f), sphereRadius * 0.9f);
    }
}
