using PortalNes.Rendering3D;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class NesCameraBackground : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private NesSceneRenderer nesSceneRenderer;

    private bool subscribed;

    private void OnEnable()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (nesSceneRenderer == null)
            nesSceneRenderer = FindFirstObjectByType<NesSceneRenderer>();

        if (nesSceneRenderer == null)
            return;

        nesSceneRenderer.BackdropColorChanged += ApplyColor;
        subscribed = true;
        ApplyColor(nesSceneRenderer.CurrentBackdropColor);
    }

    private void OnDisable()
    {
        if (subscribed && nesSceneRenderer != null)
            nesSceneRenderer.BackdropColorChanged -= ApplyColor;

        subscribed = false;
    }

    private void ApplyColor(Color32 color)
    {
        if (targetCamera == null)
            return;

        targetCamera.clearFlags = CameraClearFlags.SolidColor;
        targetCamera.backgroundColor = color;
    }
}
