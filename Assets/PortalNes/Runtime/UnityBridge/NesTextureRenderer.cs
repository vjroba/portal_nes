using System;
using PortalNes.Emulator;
using PortalNes.Emulator.Ppu;
using UnityEngine;

namespace PortalNes.UnityBridge
{
    public sealed class NesTextureRenderer : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        private Texture2D texture;
        public Texture2D Texture => texture;

        private void Awake()
        {
            texture = new Texture2D(PpuFrameBuffer.Width, PpuFrameBuffer.Height, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "NES Frame Buffer"
            };
            if (targetRenderer != null) targetRenderer.material.mainTexture = texture;
        }

        public void Present(NesMachine machine)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            uint[] pixels = machine.GetFrameBufferArray();
            if (pixels.Length == 0) return;
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, false);
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }
    }
}
