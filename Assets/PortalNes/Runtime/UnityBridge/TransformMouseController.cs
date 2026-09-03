using UnityEngine;
using UnityEngine.InputSystem;

namespace PortalNes.UnityBridge
{
    /// <summary>
    /// Moves, rotates and scales the PortalNES presentation with the mouse or
    /// keyboard. Uses Unity's Input System and does not depend on Input Manager
    /// axes.
    /// </summary>
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
        [SerializeField, Min(0.001f)] private float mouseDeltaScale = 0.1f;
        [SerializeField] private Key resetKey = Key.Digit3;
        [SerializeField] private NesRuntimeProfileEditor profileEditor;

        private const float ScaleFactor = 1.2f;

        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 initialScale;
        private Vector3 initialPositionZ;
        private Quaternion initialRotationZ;
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

            initialPosition = target.localPosition;
            initialRotation = target.localRotation;
            initialScale = scaleTarget.localScale;
            if (targetZ != null)
            {
                initialPositionZ = targetZ.localPosition;
                initialRotationZ = targetZ.localRotation;
            }
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
        }

        private void Update()
        {
            if (!initialStateCaptured) CaptureInitialState();
            if (target == null) return;

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            bool mouseOverProfileEditor = mouse != null && profileEditor != null &&
                profileEditor.ContainsScreenPoint(mouse.position.ReadValue());
            bool anyMouseButtonPressed = mouse != null &&
                (mouse.leftButton.isPressed || mouse.rightButton.isPressed ||
                 mouse.middleButton.isPressed);
            if (mouseOverProfileEditor && anyMouseButtonPressed)
                suppressMouseUntilRelease = true;
            bool suppressMouse = mouseOverProfileEditor || suppressMouseUntilRelease;
            if (!anyMouseButtonPressed) suppressMouseUntilRelease = false;

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
                if (scroll > 0f) ScaleBy(ScaleFactor);
                else if (scroll < 0f) ScaleBy(1f / ScaleFactor);
            }

            if (keyboard == null) return;
            if (resetKey != Key.None && keyboard[resetKey].wasPressedThisFrame) ResetTransform();

            // Portalgraph owns the held 1/2-key scale controls. Handling the
            // same keys here caused an immediate one-shot scale followed by
            // Portalgraph restoring its cached value before continuous scaling.

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

        private Transform ResolveZTarget()
        {
            return targetZ != null ? targetZ : target;
        }

        private void ScaleBy(float multiplier)
        {
            if (scaleTarget != null) scaleTarget.localScale *= multiplier;
        }
    }
}
