using System;
using PortalNes.Rendering3D;
using UnityEngine;
using UnityEngine.InputSystem;

public class PortalgraphBGChanger : MonoBehaviour
{
    public Camera[] targetCameras = Array.Empty<Camera>();

    public NesSceneRenderer nesSceneRenderer;

    public bool useNesSceneRendererBackdropColor = true;

    void Awake()
    {
        if (nesSceneRenderer != null)
            nesSceneRenderer.BackdropColorChanged += OnBackdropColorChanged;
    }

    void OnDestroy()
    {
        if (nesSceneRenderer != null)
            nesSceneRenderer.BackdropColorChanged -= OnBackdropColorChanged;
    }

    private void OnBackdropColorChanged(Color32 color)
    {
        if(useNesSceneRendererBackdropColor)
            ChangeBGColor(color);
    }

    private void ChangeBGColor(Color32 color)
    {
        foreach (Camera targetCamera in targetCameras)
        {
            if (targetCamera != null)
                targetCamera.backgroundColor = color;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (nesSceneRenderer != null)
            ChangeBGColor(nesSceneRenderer.CurrentBackdropColor);
    }

    // Update is called once per frame
    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.f2Key.wasPressedThisFrame)
            {
                ToggleBackdropColor();
            }
        }
    }

    private void ToggleBackdropColor()
    {
        useNesSceneRendererBackdropColor = !useNesSceneRendererBackdropColor;
        Color32 color = useNesSceneRendererBackdropColor && nesSceneRenderer != null
            ? nesSceneRenderer.CurrentBackdropColor
            : Color.black;
        ChangeBGColor(color);
    }
}
