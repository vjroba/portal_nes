namespace PortalNes.Emulator.Ppu { public sealed class PpuFrameBuffer { public const int Width = 256, Height = 240; public uint[] Pixels { get; } = new uint[Width * Height]; } }
