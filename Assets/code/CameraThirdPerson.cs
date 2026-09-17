using UnityEngine;
using UnityEngine.InputSystem;

public class CameraThirdPerson : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 1.8f, -5.5f);
    public float followSpeed = 10f;
    public float mouseSensitivity = 4f;
    public float pitchMin = -30f;
    public float pitchMax = 60f;

    public bool spooderStyle = false;
    public Vector3 spooderOffset = new Vector3(0f, 1.5f, -4.5f);

    public InputActionAsset inputActionsAsset; // optional, assign InputSystem_Actions
    InputAction lookAction;

    float yaw = 0f;
    float pitch = 10f;

    void Start()
    {
        LockCursor();

        if (target == null)
            target = GameObject.FindWithTag("Player") != null ? GameObject.FindWithTag("Player").transform : null;

        var e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x;

        if (inputActionsAsset != null)
        {
            var map = inputActionsAsset.FindActionMap("Player", true);
            if (map != null)
            {
                lookAction = map.FindAction("Look", false);
                if (lookAction != null) lookAction.Enable();
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            UnlockCursor();
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            LockCursor();

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 lookDelta = Vector2.zero;
        if (lookAction != null)
        {
            lookDelta = lookAction.ReadValue<Vector2>();
        }
        else
        {
            lookDelta.x = Input.GetAxis("Mouse X");
            lookDelta.y = Input.GetAxis("Mouse Y");
        }

        yaw += lookDelta.x * mouseSensitivity;
        pitch -= lookDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 useOffset = spooderStyle ? spooderOffset : offset;
        Vector3 desiredPos = target.position + rot * useOffset;

        transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * followSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * followSpeed);
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDisable()
    {
        UnlockCursor();
    }
}
