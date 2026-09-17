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

    Vector2 moveInput = Vector2.zero;

    [Header("Visuals / Animation")]
    public Transform visualModel; // child model to visually rotate (keep physics upright)
    public Animator animator; // optional Animator (Mixamo)

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
    public float groundCheckDistance = 0.55f;
    public LayerMask groundMask = ~0;

    Rigidbody rb;
    float h, v;
    bool sprint;
    bool jumpQueued;
    bool isGrounded;
    float sphereRadius = 0.5f;
    bool hasSpeedParameter;
    bool hasGroundedParameter;
    bool hasJumpParameter;

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
                hasGroundedParameter |= parameter.name == "IsGrounded";
                hasJumpParameter |= parameter.name == "Jump";
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
            sprint = sprintAction.ReadValue<float>() > 0.5f;
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

        float speed = moveSpeed * (sprint ? sprintMultiplier : 1f);
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

        if (jumpQueued && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            isGrounded = false;
            jumpQueued = false;
            if (animator != null && hasJumpParameter)
                animator.SetTrigger("Jump");
        }
        else
        {
            jumpQueued = false;
        }

        if (visualModel != null)
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
                float inputSpeed = moveInput.magnitude * (sprint ? sprintMultiplier : 1f);
                animator.SetFloat("Speed", Mathf.Clamp01(inputSpeed));
            }

            if (hasGroundedParameter)
                animator.SetBool("IsGrounded", isGrounded);
        }
    }

    void CheckGround()
    {
        Collider ownCollider = GetComponent<Collider>();
        if (rb.linearVelocity.y > 0.1f)
        {
            isGrounded = false;
            return;
        }

        Vector3 footPosition = ownCollider != null
            ? new Vector3(ownCollider.bounds.center.x, ownCollider.bounds.min.y + 0.08f, ownCollider.bounds.center.z)
            : transform.position + Vector3.down * 0.5f;
        Collider[] hits = Physics.OverlapSphere(footPosition, 0.14f, groundMask, QueryTriggerInteraction.Ignore);

        isGrounded = false;
        foreach (Collider hit in hits)
        {
            if (hit != ownCollider && !hit.transform.IsChildOf(transform))
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

        if (moveAction != null) moveAction.Enable();
        if (sprintAction != null) sprintAction.Enable();
        if (jumpAction != null)
        {
            jumpAction.Enable();
        }
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
