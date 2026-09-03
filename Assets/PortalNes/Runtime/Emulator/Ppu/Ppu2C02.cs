using System;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Emulator.Ppu
{
    public sealed class Ppu2C02
    {
        // The CPU core executes one whole instruction before the PPU is advanced,
        // so CPU register writes are observed at the beginning rather than the
        // final cycle of that instruction. Delay the scanline-level MMC3 signal
        // by one CPU write phase to keep raster handlers on the same side of the
        // dot-256 vertical increment as they are on hardware.
        private const int MapperScanlineClockDot = 268;
        private static readonly uint[] NesPalette =
        {
            C(84,84,84),C(0,30,116),C(8,16,144),C(48,0,136),C(68,0,100),C(92,0,48),C(84,4,0),C(60,24,0),C(32,42,0),C(8,58,0),C(0,64,0),C(0,60,0),C(0,50,60),C(0,0,0),C(0,0,0),C(0,0,0),
            C(152,150,152),C(8,76,196),C(48,50,236),C(92,30,228),C(136,20,176),C(160,20,100),C(152,34,32),C(120,60,0),C(84,90,0),C(40,114,0),C(8,124,0),C(0,118,40),C(0,102,120),C(0,0,0),C(0,0,0),C(0,0,0),
            C(236,238,236),C(76,154,236),C(120,124,236),C(176,98,236),C(228,84,236),C(236,88,180),C(236,106,100),C(212,136,32),C(160,170,0),C(116,196,0),C(76,208,32),C(56,204,108),C(56,180,204),C(60,60,60),C(0,0,0),C(0,0,0),
            C(236,238,236),C(168,204,236),C(188,188,236),C(212,178,236),C(236,174,236),C(236,174,212),C(236,180,176),C(228,196,144),C(204,210,120),C(180,222,120),C(168,226,144),C(152,226,180),C(160,214,228),C(160,162,160),C(0,0,0),C(0,0,0)
        };

        private readonly IMapper mapper;
        private readonly bool usesLatchChrBanking;
        private readonly MirroringMode cartridgeMirroring;
        private readonly int preRenderScanline;
        // Four-screen cartridges supply an additional 2KB of nametable RAM.
        // Keeping 4KB here for every cartridge is inexpensive and lets the
        // mirroring mode decide which portion is addressable.
        private readonly byte[] nameTables = new byte[4096];
        private readonly byte[] paletteRam = new byte[32];
        private readonly byte[] oam = new byte[256];
        private readonly byte[] frameOam = new byte[256];
        private readonly byte[] frameRenderedSpriteOam = new byte[256];
        private readonly byte[] frameRenderedSpriteValid = new byte[64];
        private readonly ulong[] frameSpriteOpaqueMasks = new ulong[64];
        private readonly ulong[] frameSpriteLowerOpaqueMasks = new ulong[64];
        private readonly uint[] frameSpriteTileHashes = new uint[64];
        private int frameSpriteHeight = 8;
        private ushort v, t;
        private byte fineX;
        private bool writeToggle;
        private byte readBuffer;
        private byte openBus;
        private long frameNumber;
        private readonly byte[] scanlineSprites = new byte[8];
        private readonly byte[] scanlineSpriteY = new byte[8];
        private readonly byte[] scanlineSpriteTile = new byte[8];
        private readonly byte[] scanlineSpriteAttributes = new byte[8];
        private readonly byte[] scanlineSpriteX = new byte[8];
        private int scanlineSpriteCount;
        private ushort cachedBackgroundPatternAddress = ushort.MaxValue;
        private ushort cachedBackgroundRowAddress = ushort.MaxValue;
        private byte cachedBackgroundLo;
        private byte cachedBackgroundHi;
        private uint cachedBackgroundTileHash;
        private readonly byte[] scanlineSpriteLo = new byte[8];
        private readonly byte[] scanlineSpriteHi = new byte[8];
        private readonly ushort[] prefetchedBackgroundRows = new ushort[2];
        private readonly byte[] prefetchedBackgroundLo = new byte[2];
        private readonly byte[] prefetchedBackgroundHi = new byte[2];
        private readonly uint[] prefetchedBackgroundHashes = new uint[2];
        private int prefetchedBackgroundIndex;
        private readonly string[] timingEvents = new string[12];
        private int timingEventIndex;
        private int lastMapperIrqScanline = -1;
        private int lastMapperIrqDot = -1;
        private ushort visibleStartV;
        private ushort splitStartV;

        public int Scanline { get; private set; }
        public int Dot { get; private set; }
        public bool FrameComplete { get; private set; }
        public bool NmiRequested { get; private set; }
        public bool DelayNmiOneCpuInstruction { get; private set; }
        public PpuRegisters Registers { get; private set; }
        public PpuFrameBuffer FrameBuffer { get; } = new PpuFrameBuffer();
        public PpuSceneSnapshot SceneSnapshot { get; } = new PpuSceneSnapshot();
        public ReadOnlySpan<byte> Oam => oam;
        public ReadOnlySpan<byte> PaletteRam => paletteRam;
        public long FrameNumber => frameNumber;
        public int ScrollX => ((t & 0x001F) << 3) | fineX;
        public int ScrollY => (((t >> 5) & 0x001F) << 3) | ((t >> 12) & 7);
        public string TimingDiagnostics
        {
            get
            {
                string result = $"IRQ={lastMapperIrqScanline},{lastMapperIrqDot} " +
                    $"VStart=${visibleStartV:X4} V177=${splitStartV:X4} Writes=[";
                int count = Math.Min(timingEventIndex, timingEvents.Length);
                int start = Math.Max(0, timingEventIndex - timingEvents.Length);
                for (int i = 0; i < count; i++)
                {
                    if (i > 0) result += " ";
                    result += timingEvents[(start + i) % timingEvents.Length];
                }
                return result + "]";
            }
        }

        public Ppu2C02(IMapper mapper, MirroringMode mirroring, NesRegion region = NesRegion.Ntsc)
        {
            this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            usesLatchChrBanking = mapper is Mapper009 || mapper is Mapper010;
            cartridgeMirroring = mirroring;
            preRenderScanline = region == NesRegion.Pal ? 311 : 261;
        }

        public void Reset()
        {
            Registers = default; v = t = 0; fineX = readBuffer = openBus = 0; writeToggle = false;
            Scanline = 0; Dot = 0; FrameComplete = NmiRequested = DelayNmiOneCpuInstruction = false; frameNumber = 0;
        }

        public void Clock()
        {
            if (Scanline == 0 && Dot == 0)
            {
                if (mapper is IPpuFrameMapper frameMapper) frameMapper.BeginPpuFrame();
                visibleStartV = v;
                // Freeze the OAM positions used by this visible frame. OAM is
                // commonly replaced during vblank, so copying it at frame end
                // would pair the completed pixels with next frame's positions.
                Array.Copy(oam, frameOam, oam.Length);
                Array.Clear(frameRenderedSpriteValid, 0, frameRenderedSpriteValid.Length);
                frameSpriteHeight = (Registers.Control & 0x20) != 0 ? 16 : 8;
                CaptureSpriteOpaqueMasks();
            }
            else if (Scanline == 177 && Dot == 0)
            {
                splitStartV = v;
            }
            bool rendering = (Registers.Mask & 0x18) != 0;
            if (Scanline < 240 && Dot == 0)
            {
                cachedBackgroundPatternAddress = ushort.MaxValue;
                EvaluateScanlineSprites(Scanline);
                if (rendering && usesLatchChrBanking) PrefetchBackgroundStart();
                else prefetchedBackgroundIndex = 2;
            }
            if (Scanline < 240 && Dot >= 1 && Dot <= 256)
            {
                RenderPixel(Dot - 1, Scanline);
                if (rendering && (((Dot - 1 + fineX) & 7) == 7)) IncrementHorizontal();
            }

            if (rendering && (Scanline < 240 || Scanline == preRenderScanline))
            {
                if (Scanline < 240 && Dot == MapperScanlineClockDot)
                {
                    bool wasPending = mapper.IrqPending;
                    mapper.ClockScanline();
                    if (!wasPending && mapper.IrqPending)
                    {
                        lastMapperIrqScanline = Scanline;
                        lastMapperIrqDot = Dot;
                    }
                }
                if (Dot == 256) IncrementVertical();
                else if (Dot == 257)
                {
                    CopyHorizontalFromTemporary();
                    // During sprite fetches the real PPU forces OAMADDR back
                    // to zero. Games may write a few bytes through $2004 and
                    // then rely on this before the next $4014 DMA; retaining
                    // that old address rotates the entire OAM page.
                    var r = Registers;
                    r.OamAddress = 0;
                    Registers = r;
                }
                if (Scanline == preRenderScanline && Dot >= 280 && Dot <= 304)
                    CopyVerticalFromTemporary();
            }

            if (Scanline == 241 && Dot == 1)
            {
                var r = Registers; r.Status |= 0x80; Registers = r;
                if ((Registers.Control & 0x80) != 0)
                {
                    NmiRequested = true;
                    DelayNmiOneCpuInstruction = false;
                }
            }
            else if (Scanline == preRenderScanline && Dot == 1)
            {
                var r = Registers; r.Status &= 0x1F; Registers = r;
                NmiRequested = false;
            }

            if (++Dot > 340)
            {
                Dot = 0;
                if (++Scanline > preRenderScanline)
                {
                    Scanline = 0;
                    CompleteFrame();
                }
            }
        }

        private void CompleteFrame()
        {
            if (mapper is IPpuFrameMapper frameMapper) frameMapper.EndPpuFrame();
            // A CPU instruction can advance the PPU a few dots into the next
            // frame before RunFrame observes FrameComplete. Publish the sprite
            // metadata here, before scanline 0 starts modifying the working
            // buffers for that next frame.
            Array.Copy(frameOam, SceneSnapshot.Oam, frameOam.Length);
            Array.Copy(frameRenderedSpriteOam, SceneSnapshot.RenderedSpriteOam,
                frameRenderedSpriteOam.Length);
            Array.Copy(frameRenderedSpriteValid, SceneSnapshot.RenderedSpriteValid,
                frameRenderedSpriteValid.Length);
            Array.Copy(frameSpriteOpaqueMasks, SceneSnapshot.SpriteOpaqueMasks,
                frameSpriteOpaqueMasks.Length);
            Array.Copy(frameSpriteLowerOpaqueMasks, SceneSnapshot.SpriteLowerOpaqueMasks,
                frameSpriteLowerOpaqueMasks.Length);
            Array.Copy(frameSpriteTileHashes, SceneSnapshot.SpriteTileHashes,
                frameSpriteTileHashes.Length);
            SceneSnapshot.SpriteHeight = frameSpriteHeight;
            FrameComplete = true;
            frameNumber++;
            SceneSnapshot.FrameNumber = frameNumber;
            SceneSnapshot.BackdropColor = Color(paletteRam[0]);
            SceneSnapshot.ScrollX = ScrollX;
            SceneSnapshot.ScrollY = ScrollY;
        }

        public bool ConsumeFrameComplete()
        {
            if (!FrameComplete) return false;
            FrameComplete = false;
            return true;
        }

        public void ClearNmiRequest()
        {
            NmiRequested = false;
            DelayNmiOneCpuInstruction = false;
        }

        public byte CpuReadRegister(ushort address)
        {
            int register = address & 7;
            byte result = openBus;
            switch (register)
            {
                case 2:
                    result = (byte)((Registers.Status & 0xE0) | (openBus & 0x1F));
                    var r = Registers; r.Status &= 0x7F; Registers = r; writeToggle = false;
                    break;
                case 4: result = oam[Registers.OamAddress]; break;
                case 7:
                    byte value = PpuRead(v);
                    if ((v & 0x3FFF) < 0x3F00) { result = readBuffer; readBuffer = value; }
                    else { result = value; readBuffer = PpuRead((ushort)(v - 0x1000)); }
                    v += (ushort)(((Registers.Control & 0x04) != 0) ? 32 : 1);
                    break;
            }
            openBus = result;
            return result;
        }

        public void CpuWriteRegister(ushort address, byte value)
        {
            openBus = value;
            var r = Registers;
            int register = address & 7;
            if (register == 0 || register == 5 || register == 6)
            {
                timingEvents[timingEventIndex % timingEvents.Length] =
                    $"{Scanline}:{Dot}/200{register}={value:X2}";
                timingEventIndex++;
            }
            switch (register)
            {
                case 0:
                    bool nmiWasOff = (r.Control & 0x80) == 0;
                    r.Control = value; Registers = r; t = (ushort)((t & 0xF3FF) | ((value & 3) << 10));
                    if (nmiWasOff && (value & 0x80) != 0 && (r.Status & 0x80) != 0)
                    {
                        NmiRequested = true;
                        DelayNmiOneCpuInstruction = true;
                    }
                    return;
                case 1: r.Mask = value; Registers = r; return;
                case 3: r.OamAddress = value; Registers = r; return;
                case 4: oam[r.OamAddress++] = value; Registers = r; return;
                case 5:
                    if (!writeToggle) { fineX = (byte)(value & 7); t = (ushort)((t & 0xFFE0) | (value >> 3)); }
                    else { t = (ushort)((t & 0x8FFF) | ((value & 7) << 12)); t = (ushort)((t & 0xFC1F) | ((value & 0xF8) << 2)); }
                    writeToggle = !writeToggle; return;
                case 6:
                    if (!writeToggle) t = (ushort)((t & 0x00FF) | ((value & 0x3F) << 8));
                    else { t = (ushort)((t & 0xFF00) | value); v = t; }
                    writeToggle = !writeToggle; return;
                case 7: PpuWrite(v, value); v += (ushort)(((r.Control & 0x04) != 0) ? 32 : 1); return;
            }
        }

        public void WriteOamDma(ReadOnlySpan<byte> data)
        {
            if (data.Length != 256) throw new ArgumentException("OAM DMA requires exactly 256 bytes.", nameof(data));
            for (int i = 0; i < 256; i++) oam[(byte)(Registers.OamAddress + i)] = data[i];
        }

        public byte PpuRead(ushort address)
        {
            address &= 0x3FFF;
            if (address < 0x2000) return mapper.PpuRead(address);
            if (address < 0x3F00)
            {
                if (mapper is INametableMemoryMapper nametableMemory)
                    return nametableMemory.ReadNametable(address);
                return nameTables[MirrorNameTable(address)];
            }
            return paletteRam[MirrorPalette(address)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            address &= 0x3FFF;
            if (address < 0x2000) mapper.PpuWrite(address, value);
            else if (address < 0x3F00)
            {
                if (mapper is INametableMemoryMapper nametableMemory)
                    nametableMemory.WriteNametable(address, value);
                else nameTables[MirrorNameTable(address)] = value;
            }
            else paletteRam[MirrorPalette(address)] = (byte)(value & 0x3F);
        }

        private int MirrorNameTable(ushort address)
        {
            if (mapper is INametableMappingMapper nametableMapper)
                return nametableMapper.MapNametableAddress(address);

            int offset = (address - 0x2000) & 0x0FFF, table = offset / 0x400, within = offset & 0x3FF;
            MirroringMode mirroring = mapper.MirroringOverride ?? cartridgeMirroring;
            int physical;
            switch (mirroring)
            {
                case MirroringMode.Vertical: physical = table & 1; break;
                case MirroringMode.FourScreen: physical = table; break;
                case MirroringMode.SingleScreenLower: physical = 0; break;
                case MirroringMode.SingleScreenUpper: physical = 1; break;
                default: physical = table >> 1; break;
            }
            return physical * 0x400 + within;
        }

        private static int MirrorPalette(ushort address)
        {
            int index = (address - 0x3F00) & 0x1F;
            if (index == 0x10 || index == 0x14 || index == 0x18 || index == 0x1C) index -= 0x10;
            return index;
        }

        private void RenderPixel(int sx, int sy)
        {
            uint color = Color(paletteRam[0]);
            bool backgroundOpaque = false;
            int outputIndex = sy * 256 + sx;
            byte backgroundPattern = 0;
            byte backgroundPalette = 0;
            byte backgroundPatternColor = 0;
            uint backgroundTileHash = 0;
            byte backgroundLocalX = (byte)(sx & 7);
            byte backgroundLocalY = (byte)(sy & 7);
            if ((Registers.Mask & 0x08) != 0 && (sx >= 8 || (Registers.Mask & 0x02) != 0))
            {
                int tileX = v & 0x1F, tileY = (v >> 5) & 0x1F;
                ushort nt = (ushort)(0x2000 | (v & 0x0FFF));
                byte tile = PpuRead(nt);
                byte attribute = PpuRead((ushort)(0x23C0 | (v & 0x0C00) | ((v >> 4) & 0x38) | ((v >> 2) & 0x07)));
                int shift = ((tileY & 2) << 1) | (tileX & 2);
                int palette = (attribute >> shift) & 3;
                bool splitBackground = false;
                int splitFineY = 0;
                if (mapper is IVerticalSplitMapper verticalSplit &&
                    verticalSplit.TryGetSplitTile((sx + fineX) >> 3, sy,
                        out byte splitTile, out int splitPalette, out splitFineY))
                {
                    tile = splitTile;
                    palette = splitPalette;
                    splitBackground = true;
                }
                if (mapper is ISeparateChrMapper separateChr)
                    if (!splitBackground) palette = separateChr.GetBackgroundPalette(nt, palette);
                backgroundPattern = tile;
                backgroundPalette = (byte)palette;
                int row = splitBackground ? splitFineY : (v >> 12) & 7;
                int bit = 7 - ((sx + fineX) & 7), patternBase = (Registers.Control & 0x10) != 0 ? 0x1000 : 0;
                backgroundLocalX = (byte)(7 - bit);
                backgroundLocalY = (byte)row;
                ushort tileAddress = (ushort)(patternBase + tile * 16);
                ushort rowAddress = (ushort)(tileAddress + row);
                int localX = (sx + fineX) & 7;
                if (splitBackground)
                {
                    var splitMapper = (IVerticalSplitMapper)mapper;
                    cachedBackgroundLo = splitMapper.ReadSplitPattern(rowAddress);
                    cachedBackgroundHi = splitMapper.ReadSplitPattern((ushort)(rowAddress + 8));
                    cachedBackgroundPatternAddress = tileAddress;
                    cachedBackgroundTileHash = ComputeSplitTileHash(splitMapper, tileAddress);
                }
                else if (sx == 0 || localX == 0 || cachedBackgroundRowAddress != rowAddress)
                {
                    cachedBackgroundRowAddress = rowAddress;
                    if (prefetchedBackgroundIndex < 2 &&
                        prefetchedBackgroundRows[prefetchedBackgroundIndex] == rowAddress)
                    {
                        cachedBackgroundLo = prefetchedBackgroundLo[prefetchedBackgroundIndex];
                        cachedBackgroundHi = prefetchedBackgroundHi[prefetchedBackgroundIndex];
                        cachedBackgroundTileHash =
                            prefetchedBackgroundHashes[prefetchedBackgroundIndex];
                        cachedBackgroundPatternAddress = tileAddress;
                        prefetchedBackgroundIndex++;
                    }
                    else
                    {
                        prefetchedBackgroundIndex = 2;
                        cachedBackgroundLo = ReadBackgroundPattern(rowAddress, nt);
                        cachedBackgroundHi = ReadBackgroundPattern((ushort)(rowAddress + 8), nt);
                    }
                }
                byte lo = cachedBackgroundLo;
                byte hi = cachedBackgroundHi;
                if (cachedBackgroundPatternAddress != tileAddress)
                {
                    cachedBackgroundPatternAddress = tileAddress;
                    cachedBackgroundTileHash = ComputeBackgroundTileHash(tileAddress, nt);
                }
                backgroundTileHash = cachedBackgroundTileHash;
                int pixel = ((lo >> bit) & 1) | (((hi >> bit) & 1) << 1);
                backgroundPatternColor = (byte)pixel;
                if (pixel != 0) { color = Color(paletteRam[palette * 4 + pixel]); backgroundOpaque = true; }
            }
            // The 2D framebuffer keeps the universal backdrop color. The 3D scene
            // layer uses alpha zero for background color 0 so empty sky becomes a
            // true opening into the Unity/Portalgraph world.
            SceneSnapshot.BackgroundPixels[outputIndex] = backgroundOpaque ? color : 0u;
            SceneSnapshot.BackgroundPattern[outputIndex] = backgroundPattern;
            SceneSnapshot.BackgroundPalette[outputIndex] = backgroundPalette;
            SceneSnapshot.BackgroundPatternColor[outputIndex] = backgroundPatternColor;
            SceneSnapshot.BackgroundTileHash[outputIndex] = backgroundTileHash;
            SceneSnapshot.BackgroundOpaque[outputIndex] = backgroundOpaque ? (byte)1 : (byte)0;
            SceneSnapshot.BackgroundTileLocalX[outputIndex] = backgroundLocalX;
            SceneSnapshot.BackgroundTileLocalY[outputIndex] = backgroundLocalY;
            SceneSnapshot.SpritePixels[outputIndex] = 0;
            if ((Registers.Mask & 0x10) != 0 && (sx >= 8 || (Registers.Mask & 0x04) != 0))
            {
                int spriteHeight = (Registers.Control & 0x20) != 0 ? 16 : 8;
                // First opaque sprite in OAM order owns this pixel.
                for (int slot = 0; slot < scanlineSpriteCount; slot++)
                {
                    int i = scanlineSprites[slot];
                    int o = i * 4;
                    int y = scanlineSpriteY[slot] + 1;
                    int tile = scanlineSpriteTile[slot];
                    int attr = scanlineSpriteAttributes[slot];
                    int x = scanlineSpriteX[slot];
                    if (sx < x || sx >= x + 8 || sy < y || sy >= y + spriteHeight) continue;
                    bool flipX = (attr & 0x40) != 0, flipY = (attr & 0x80) != 0, behind = (attr & 0x20) != 0;
                    int px = sx - x, py = sy - y;
                    int row = flipY ? spriteHeight - 1 - py : py, bit = flipX ? px : 7 - px;
                    byte lo = scanlineSpriteLo[slot];
                    byte hi = scanlineSpriteHi[slot];
                    int pixel = ((lo >> bit) & 1) | (((hi >> bit) & 1) << 1); if (pixel == 0) continue;
                    if (frameRenderedSpriteValid[i] == 0)
                    {
                        frameRenderedSpriteOam[o] = scanlineSpriteY[slot];
                        frameRenderedSpriteOam[o + 1] = scanlineSpriteTile[slot];
                        frameRenderedSpriteOam[o + 2] = scanlineSpriteAttributes[slot];
                        frameRenderedSpriteOam[o + 3] = scanlineSpriteX[slot];
                        frameRenderedSpriteValid[i] = 1;
                    }
                    if (i == 0 && backgroundOpaque && sx < 255) { var r=Registers;r.Status|=0x40;Registers=r; }
                    uint spriteColor = Color(paletteRam[0x10 + (attr & 3) * 4 + pixel]);
                    if (!behind || !backgroundOpaque)
                    {
                        SceneSnapshot.SpritePixels[outputIndex] = spriteColor;
                        color = spriteColor;
                    }
                    break;
                }
            }
            FrameBuffer.Pixels[outputIndex] = color;
        }

        private void EvaluateScanlineSprites(int scanline)
        {
            scanlineSpriteCount = 0;
            bool overflow = false;
            int spriteHeight = (Registers.Control & 0x20) != 0 ? 16 : 8;
            for (int i = 0; i < 64; i++)
            {
                int y = oam[i * 4] + 1;
                if (scanline < y || scanline >= y + spriteHeight) continue;
                if (scanlineSpriteCount < 8)
                {
                    int slot = scanlineSpriteCount++;
                    int o = i * 4;
                    scanlineSprites[slot] = (byte)i;
                    // Secondary OAM conceptually freezes the selected sprite for
                    // this scanline. Keep all four bytes together so later CPU
                    // OAM writes cannot pair old pattern data with a new position.
                    scanlineSpriteY[slot] = oam[o];
                    scanlineSpriteTile[slot] = oam[o + 1];
                    scanlineSpriteAttributes[slot] = oam[o + 2];
                    scanlineSpriteX[slot] = oam[o + 3];
                }
                else { overflow = true; break; }
            }
            if (overflow) { var r = Registers; r.Status |= 0x20; Registers = r; }
            if ((Registers.Mask & 0x18) == 0) return;
            for (int slot = 0; slot < scanlineSpriteCount; slot++)
            {
                int y = scanlineSpriteY[slot] + 1;
                int row = scanline - y;
                if ((scanlineSpriteAttributes[slot] & 0x80) != 0) row = spriteHeight - 1 - row;
                ushort address = GetSpritePatternAddress(scanlineSpriteTile[slot], row, spriteHeight);
                scanlineSpriteLo[slot] = ReadSpritePattern(address);
                scanlineSpriteHi[slot] = ReadSpritePattern((ushort)(address + 8));
            }
        }

        private void PrefetchBackgroundStart()
        {
            prefetchedBackgroundIndex = 0;
            ushort fetchV = v;
            int patternBase = (Registers.Control & 0x10) != 0 ? 0x1000 : 0;
            for (int slot = 0; slot < 2; slot++)
            {
                byte tile = PpuRead((ushort)(0x2000 | (fetchV & 0x0FFF)));
                int row = (fetchV >> 12) & 7;
                ushort rowAddress = (ushort)(patternBase + tile * 16 + row);
                prefetchedBackgroundRows[slot] = rowAddress;
                ushort nt = (ushort)(0x2000 | (fetchV & 0x0FFF));
                prefetchedBackgroundHashes[slot] =
                    ComputeBackgroundTileHash((ushort)(patternBase + tile * 16), nt);
                prefetchedBackgroundLo[slot] = ReadBackgroundPattern(rowAddress, nt);
                prefetchedBackgroundHi[slot] = ReadBackgroundPattern((ushort)(rowAddress + 8), nt);
                IncrementHorizontal(ref fetchV);
            }
        }

        private void IncrementHorizontal()
        {
            IncrementHorizontal(ref v);
        }

        private static void IncrementHorizontal(ref ushort address)
        {
            if ((address & 0x001F) == 31)
                address = (ushort)((address & ~0x001F) ^ 0x0400);
            else address++;
        }

        private void CaptureSpriteOpaqueMasks()
        {
            int spriteHeight = (Registers.Control & 0x20) != 0 ? 16 : 8;
            for (int i = 0; i < 64; i++)
            {
                int o = i * 4;
                int tile = oam[o + 1], attr = oam[o + 2];
                bool flipX = (attr & 0x40) != 0, flipY = (attr & 0x80) != 0;
                ulong upperMask = 0, lowerMask = 0;
                for (int py = 0; py < spriteHeight; py++)
                {
                    int row = flipY ? spriteHeight - 1 - py : py;
                    ushort patternAddress = GetSpritePatternAddress(tile, row, spriteHeight);
                    byte lo = PeekSpritePattern(patternAddress);
                    byte hi = PeekSpritePattern((ushort)(patternAddress + 8));
                    for (int px = 0; px < 8; px++)
                    {
                        int bit = flipX ? px : 7 - px;
                        if ((((lo >> bit) & 1) | (((hi >> bit) & 1) << 1)) != 0)
                        {
                            int localY = py & 7;
                            if (py < 8) upperMask |= 1UL << (localY * 8 + px);
                            else lowerMask |= 1UL << (localY * 8 + px);
                        }
                    }
                }
                frameSpriteOpaqueMasks[i] = upperMask;
                frameSpriteLowerOpaqueMasks[i] = lowerMask;
                int firstDisplayedRow = flipY ? spriteHeight - 1 : 0;
                frameSpriteTileHashes[i] = ComputeSpriteTileHash(
                    (ushort)(GetSpritePatternAddress(tile, firstDisplayedRow, spriteHeight) & 0xFFF0));
            }
        }

        private uint ComputeBackgroundTileHash(ushort tileAddress, ushort nametableAddress)
        {
            const uint offsetBasis = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offsetBasis;
            for (int i = 0; i < 16; i++)
                hash = (hash ^ PeekBackgroundPattern((ushort)(tileAddress + i), nametableAddress)) * prime;
            return hash;
        }

        private uint ComputeSpriteTileHash(ushort tileAddress)
        {
            const uint offsetBasis = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offsetBasis;
            for (int i = 0; i < 16; i++) hash = (hash ^ PeekSpritePattern((ushort)(tileAddress + i))) * prime;
            return hash;
        }

        private static uint ComputeSplitTileHash(IVerticalSplitMapper split, ushort tileAddress)
        {
            const uint offsetBasis = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offsetBasis;
            for (int i = 0; i < 16; i++)
                hash = (hash ^ split.ReadSplitPattern((ushort)(tileAddress + i))) * prime;
            return hash;
        }

        private byte PeekSpritePattern(ushort address)
        {
            if (mapper is ISeparateChrMapper separate) return separate.ReadSpritePattern(address);
            return mapper is IPpuPeekMapper peekMapper
                ? peekMapper.PpuPeek(address)
                : mapper.PpuRead(address);
        }

        private byte PeekBackgroundPattern(ushort address, ushort nametableAddress)
        {
            if (mapper is ISeparateChrMapper separate)
                return separate.ReadBackgroundPattern(address, nametableAddress);
            return mapper is IPpuPeekMapper peekMapper
                ? peekMapper.PpuPeek(address)
                : mapper.PpuRead(address);
        }

        private byte ReadBackgroundPattern(ushort address, ushort nametableAddress) =>
            mapper is ISeparateChrMapper separate
                ? separate.ReadBackgroundPattern(address, nametableAddress)
                : PpuRead(address);

        private byte ReadSpritePattern(ushort address) =>
            mapper is ISeparateChrMapper separate
                ? separate.ReadSpritePattern(address)
                : PpuRead(address);

        private ushort GetSpritePatternAddress(int tile, int row, int spriteHeight)
        {
            if (spriteHeight == 16)
            {
                int patternBase = (tile & 1) << 12;
                int tileIndex = (tile & 0xFE) + (row >> 3);
                return (ushort)(patternBase + tileIndex * 16 + (row & 7));
            }
            int baseAddress = (Registers.Control & 0x08) != 0 ? 0x1000 : 0;
            return (ushort)(baseAddress + tile * 16 + row);
        }

        private void IncrementVertical()
        {
            if ((v & 0x7000) != 0x7000) { v += 0x1000; return; }
            v &= 0x0FFF;
            int coarseY = (v & 0x03E0) >> 5;
            if (coarseY == 29) { coarseY = 0; v ^= 0x0800; }
            else if (coarseY == 31) coarseY = 0;
            else coarseY++;
            v = (ushort)((v & ~0x03E0) | (coarseY << 5));
        }

        private void CopyHorizontalFromTemporary() => v = (ushort)((v & ~0x041F) | (t & 0x041F));
        private void CopyVerticalFromTemporary() => v = (ushort)((v & ~0x7BE0) | (t & 0x7BE0));

        private static uint Color(byte index) => NesPalette[index & 0x3F];
        private static uint C(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16) | (255 << 24));
    }
}
