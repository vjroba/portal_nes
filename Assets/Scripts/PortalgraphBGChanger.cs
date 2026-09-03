using System;
using PortalNes.Rendering3D;
using UnityEngine;
using Portalgraph;
using UnityEngine.InputSystem;

public class PortalgraphBGChanger : MonoBehaviour
{
    public Portalgraph.Portalgraph portalgraph;

    public NesSceneRenderer nesSceneRenderer;

    public bool useNesSceneRendererBackdropColor = true;

    void Awake()
    {
        nesSceneRenderer.BackdropColorChanged += OnBackdropColorChanged;
    }

    private void OnBackdropColorChanged(Color32 color)
    {
        if(useNesSceneRendererBackdropColor)
            ChangeBGColor(color);
    }

    private void ChangeBGColor(Color32 color)
    {
        foreach (ScreenController screen in portalgraph.Screens)
        {
            screen.leftCamera.backgroundColor = color;
            screen.rightCamera.backgroundColor = color;
            screen.centerCamera.backgroundColor = color;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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
        ChangeBGColor(useNesSceneRendererBackdropColor ? nesSceneRenderer.CurrentBackdropColor : Color.black);
    }
}
