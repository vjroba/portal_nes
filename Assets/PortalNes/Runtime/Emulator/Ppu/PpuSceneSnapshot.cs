namespace PortalNes.Emulator.Ppu
{
    public sealed class PpuSceneSnapshot
    {
        public long FrameNumber { get; internal set; }
        public uint BackdropColor { get; internal set; }
        public int ScrollX { get; internal set; }
        public int ScrollY { get; internal set; }
        public int SpriteHeight { get; internal set; } = 8;
        public uint[] BackgroundPixels { get; } = new uint[PpuFrameBuffer.Width * PpuFrameBuffer.Height];
        public uint[] SpritePixels { get; } = new uint[PpuFrameBuffer.Width * PpuFrameBuffer.Height];
        public ulong[] SpriteOpaqueMasks { get; } = new ulong[64];
        public ulong[] SpriteLowerOpaqueMasks { get; } = new ulong[64];
        public uint[] SpriteTileHashes { get; } = new uint[64];
        public byte[] BackgroundPattern { get; } = new byte[PpuFrameBuffer.Width * PpuFrameBuffer.Height];
        public byte[] BackgroundPalette { get; } = new byte[PpuFrameBuffer.Width * PpuFrameBuffer.Height];
        public byte[] BackgroundPatternColor { get; } = new byte[PpuFrameBuffer.Width * PpuFrameBuffer.Height];
        public uint[] BackgroundTileHash { get; } = new uint[PpuFrameBuffer.Width * PpuFrameBuffer.Height];
        public byte[] BackgroundOpaque { get; } = new byte[PpuFrameBuffer.Width * PpuFrameBuffer.Height];
        public byte[] BackgroundTileLocalX { get; } = new byte[PpuFrameBuffer.Width * PpuFrameBuffer.Height];
        public byte[] BackgroundTileLocalY { get; } = new byte[PpuFrameBuffer.Width * PpuFrameBuffer.Height];
        public byte[] Oam { get; } = new byte[256];
        public byte[] RenderedSpriteOam { get; } = new byte[256];
        public byte[] RenderedSpriteValid { get; } = new byte[64];
    }
}
