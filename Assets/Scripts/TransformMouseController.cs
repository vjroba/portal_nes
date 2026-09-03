using PortalNes.UnityBridge;
using UnityEngine;
using UnityEngine.InputSystem;

// Kept in the global namespace because Assets/Scenes/3D.unity references this
// original component. The PortalNES scenes use the namespaced equivalent.
public sealed class TransformMouseController : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Transform targetZ;
    [SerializeField] private Transform coordinateSystem;
    [SerializeField] private Transform scaleTarget;
    [SerializeField] private float moveSpeed = 0.01f;
    [SerializeField] private float rotateSpeed = 0.3f;
    [SerializeField] private float moveSpeedZ = 0.01f;
    [SerializeField] private float keyMoveSpeed = 0.05f;
    [SerializeField] private float keyRotateSpeed = 1.0f;
    [SerializeField] private Camera fovCamera;
    [SerializeField, Min(0f)] private float fovSpeed = 30f;
    [SerializeField, Range(1f, 179f)] private float minimumFov = 10f;
    [SerializeField, Range(1f, 179f)] private float maximumFov = 120f;
    [SerializeField, Min(0.001f)] private float mouseDeltaScale = 0.1f;
    // KeyCode preserves the reset key value already serialized in the scene;
    // actual input is read exclusively through the Input System.
    [SerializeField] private KeyCode resetKey = KeyCode.None;
    [SerializeField] private NesRuntimeProfileEditor profileEditor;

    private const float ScaleFactor = 1.2f;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;
    private Vector3 initialPositionZ;
    private Quaternion initialRotationZ;
    private float initialFov;
    private bool initialStateCaptured;
    private bool suppressMouseUntilRelease;

    private void Start()
    {
        if (profileEditor == null)
            profileEditor = FindFirstObjectByType<NesRuntimeProfileEditor>();
        CaptureInitialState();
    }

    private void CaptureInitialState()
    {
        if (target == null) target = transform;
        if (scaleTarget == null) scaleTarget = target;
        if (coordinateSystem == null) coordinateSystem = target;
        if (fovCamera == null) fovCamera = Camera.main;
        initialPosition = target.localPosition;
        initialRotation = target.localRotation;
        initialScale = scaleTarget.localScale;
        if (targetZ != null)
        {
            initialPositionZ = targetZ.localPosition;
            initialRotationZ = targetZ.localRotation;
        }
        if (fovCamera != null) initialFov = fovCamera.fieldOfView;
        initialStateCaptured = true;
    }

    public void ResetTransform()
    {
        if (!initialStateCaptured) CaptureInitialState();
        if (target == null) return;
        target.SetLocalPositionAndRotation(initialPosition, initialRotation);
        if (scaleTarget != null) scaleTarget.localScale = initialScale;
        if (targetZ != null)
            targetZ.SetLocalPositionAndRotation(initialPositionZ, initialRotationZ);
        if (fovCamera != null) fovCamera.fieldOfView = initialFov;
    }

    private void Update()
    {
        if (!initialStateCaptured) CaptureInitialState();
        if (target == null) return;

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        bool overEditor = mouse != null && profileEditor != null &&
            profileEditor.ContainsScreenPoint(mouse.position.ReadValue());
        bool mousePressed = mouse != null &&
            (mouse.leftButton.isPressed || mouse.rightButton.isPressed ||
             mouse.middleButton.isPressed);
        if (overEditor && mousePressed) suppressMouseUntilRelease = true;
        bool suppressMouse = overEditor || suppressMouseUntilRelease;
        if (!mousePressed) suppressMouseUntilRelease = false;

        Vector2 delta = mouse != null && !suppressMouse
            ? mouse.delta.ReadValue() * mouseDeltaScale : Vector2.zero;
        bool shift = keyboard != null &&
            (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        float coordinateScale = coordinateSystem != null
            ? Mathf.Abs(coordinateSystem.lossyScale.x) : 1f;

        if (!suppressMouse && mouse?.rightButton.isPressed == true)
        {
            if (shift)
                ResolveZTarget().Translate(0f, 0f,
                    delta.y * moveSpeedZ * coordinateScale, Space.Self);
            else
                target.Translate(delta.x * moveSpeed * coordinateScale,
                    delta.y * moveSpeed * coordinateScale, 0f, Space.Self);
        }
        if (!suppressMouse && mouse?.leftButton.isPressed == true)
        {
            if (shift)
                target.localEulerAngles += new Vector3(0f, 0f, -delta.x * rotateSpeed);
            else
                target.localEulerAngles +=
                    new Vector3(-delta.y * rotateSpeed, delta.x * rotateSpeed, 0f);
        }
        if (!suppressMouse && mouse?.middleButton.isPressed == true)
            ResolveZTarget().Translate(0f, 0f,
                delta.y * moveSpeedZ * coordinateScale, Space.Self);

        if (mouse != null && !suppressMouse)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0f) scaleTarget.localScale *= ScaleFactor;
            else if (scroll < 0f) scaleTarget.localScale /= ScaleFactor;
        }

        if (keyboard == null) return;
        if (WasResetPressed(keyboard)) ResetTransform();
        if (fovCamera != null && !fovCamera.orthographic)
        {
            float fovDelta = fovSpeed * Time.deltaTime;
            if (keyboard.digit1Key.isPressed)
                fovCamera.fieldOfView = Mathf.Max(minimumFov, fovCamera.fieldOfView - fovDelta);
            if (keyboard.digit2Key.isPressed)
                fovCamera.fieldOfView = Mathf.Min(maximumFov, fovCamera.fieldOfView + fovDelta);
        }
        float move = keyMoveSpeed * Time.deltaTime;
        float rotate = keyRotateSpeed * Time.deltaTime;
        Transform zTarget = ResolveZTarget();
        if (keyboard.wKey.isPressed) zTarget.Translate(0f, 0f, move, Space.Self);
        if (keyboard.sKey.isPressed) zTarget.Translate(0f, 0f, -move, Space.Self);
        if (keyboard.aKey.isPressed) target.Translate(-move, 0f, 0f, Space.Self);
        if (keyboard.dKey.isPressed) target.Translate(move, 0f, 0f, Space.Self);
        if (keyboard.rKey.isPressed) target.Translate(0f, move, 0f, Space.Self);
        if (keyboard.fKey.isPressed) target.Translate(0f, -move, 0f, Space.Self);
        if (keyboard.qKey.isPressed) target.localEulerAngles += new Vector3(0f, -rotate, 0f);
        if (keyboard.eKey.isPressed) target.localEulerAngles += new Vector3(0f, rotate, 0f);
        if (keyboard.tKey.isPressed) target.localEulerAngles += new Vector3(rotate, 0f, 0f);
        if (keyboard.gKey.isPressed) target.localEulerAngles += new Vector3(-rotate, 0f, 0f);
        if (keyboard.zKey.isPressed) target.localEulerAngles += new Vector3(0f, 0f, rotate);
        if (keyboard.cKey.isPressed) target.localEulerAngles += new Vector3(0f, 0f, -rotate);
    }

    private bool WasResetPressed(Keyboard keyboard)
    {
        if (resetKey == KeyCode.None) return false;
        if (resetKey >= KeyCode.Alpha0 && resetKey <= KeyCode.Alpha9)
        {
            string digitName = "Digit" + resetKey.ToString().Substring("Alpha".Length);
            return System.Enum.TryParse(digitName, out Key digitKey) &&
                keyboard[digitKey].wasPressedThisFrame;
        }
        return System.Enum.TryParse(resetKey.ToString(), out Key key) &&
            keyboard[key].wasPressedThisFrame;
    }

    private Transform ResolveZTarget() => targetZ != null ? targetZ : target;
}
