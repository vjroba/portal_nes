using NUnit.Framework;
using PortalNes.Emulator.Bus;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;
using PortalNes.Emulator.Ppu;

namespace PortalNes.Tests
{
    public sealed class Ppu2C02Tests
    {
        private Mapper000 mapper;
        private Ppu2C02 ppu;

        [SetUp]
        public void SetUp()
        {
            mapper = new Mapper000(new byte[16384], new byte[8192]);
            ppu = new Ppu2C02(mapper, MirroringMode.Vertical);
            ppu.Reset();
        }

        [Test]
        public void VerticalMirroring_MapsTablesZeroAndTwoTogether()
        {
            ppu.PpuWrite(0x2000, 0x12);
            ppu.PpuWrite(0x2400, 0x34);
            Assert.That(ppu.PpuRead(0x2800), Is.EqualTo(0x12));
            Assert.That(ppu.PpuRead(0x2C00), Is.EqualTo(0x34));
        }

        [Test]
        public void FourScreenMirroring_KeepsAllFourTablesIndependent()
        {
            ppu = new Ppu2C02(mapper, MirroringMode.FourScreen);
            ppu.PpuWrite(0x2000, 0x10);
            ppu.PpuWrite(0x2400, 0x11);
            ppu.PpuWrite(0x2800, 0x12);
            ppu.PpuWrite(0x2C00, 0x13);

            Assert.That(ppu.PpuRead(0x2000), Is.EqualTo(0x10));
            Assert.That(ppu.PpuRead(0x2400), Is.EqualTo(0x11));
            Assert.That(ppu.PpuRead(0x2800), Is.EqualTo(0x12));
            Assert.That(ppu.PpuRead(0x2C00), Is.EqualTo(0x13));
            Assert.That(ppu.PpuRead(0x3000), Is.EqualTo(0x10));
        }

        [Test]
        public void PaletteUniversalEntries_AreMirrored()
        {
            ppu.PpuWrite(0x3F00, 0x21);
            Assert.That(ppu.PpuRead(0x3F10), Is.EqualTo(0x21));
        }

        [Test]
        public void PpuData_UsesReadBufferOutsidePaletteRange()
        {
            ppu.PpuWrite(0x2000, 0x5A);
            SetAddress(0x2000);
            Assert.That(ppu.CpuReadRegister(0x2007), Is.Zero);
            Assert.That(ppu.CpuReadRegister(0x2007), Is.EqualTo(0x5A));
        }

        [Test]
        public void StatusRead_ClearsVblankAndAddressLatch()
        {
            ppu.CpuWriteRegister(0x2006, 0x21);
            ClockToVblank();
            Assert.That(ppu.CpuReadRegister(0x2002) & 0x80, Is.Not.Zero);
            Assert.That(ppu.CpuReadRegister(0x2002) & 0x80, Is.Zero);
            ppu.CpuWriteRegister(0x2006, 0x20); ppu.CpuWriteRegister(0x2006, 0x00);
            ppu.CpuWriteRegister(0x2007, 0x77);
            Assert.That(ppu.PpuRead(0x2000), Is.EqualTo(0x77));
        }

        [Test]
        public void Vblank_WithControlEnabled_RequestsNmi()
        {
            ppu.CpuWriteRegister(0x2000, 0x80);
            ClockToVblank();
            Assert.That(ppu.NmiRequested, Is.True);
            Assert.That(ppu.Registers.Status & 0x80, Is.Not.Zero);
        }

        [Test]
        public void FrameComplete_RemainsLatchedUntilConsumed()
        {
            int safety = 262 * 341 + 100;
            while (!ppu.FrameComplete && safety-- > 0) ppu.Clock();
            Assert.That(ppu.FrameComplete, Is.True);
            for (int i = 0; i < 20; i++) ppu.Clock();
            Assert.That(ppu.FrameComplete, Is.True);
            Assert.That(ppu.ConsumeFrameComplete(), Is.True);
            Assert.That(ppu.FrameComplete, Is.False);
            Assert.That(ppu.ConsumeFrameComplete(), Is.False);
        }

        [Test]
        public void OamDma_CopiesFullPageAndCostsParityDependentCyclesAtMachineLevel()
        {
            var bus = new CpuBus(mapper, ppu);
            for (int i = 0; i < 256; i++) bus.Write((ushort)(0x0200 + i), (byte)i);
            bus.Write(0x4014, 0x02);
            Assert.That(bus.ExecutePendingDma(), Is.True);
            Assert.That(ppu.Oam[0], Is.Zero);
            Assert.That(ppu.Oam[255], Is.EqualTo(255));
            Assert.That(bus.ExecutePendingDma(), Is.False);
        }

        [Test]
        public void BackgroundRenderer_UsesPatternAndPaletteData()
        {
            byte[] chr = new byte[8192];
            chr[16] = 0x80; // tile 1, first row, first pixel = color 1
            ppu = new Ppu2C02(new Mapper000(new byte[16384], chr), MirroringMode.Vertical);
            ppu.PpuWrite(0x2000, 1);
            ppu.PpuWrite(0x3F00, 0x0F);
            ppu.PpuWrite(0x3F01, 0x30);
            // Enable background rendering, including the normally clipped left 8 pixels.
            ppu.CpuWriteRegister(0x2001, 0x0A);
            ClockToVblank();
            Assert.That(ppu.FrameBuffer.Pixels[0], Is.Not.EqualTo(ppu.FrameBuffer.Pixels[1]));
            Assert.That(ppu.SceneSnapshot.BackgroundPatternColor[0], Is.EqualTo(1));
            Assert.That(ppu.SceneSnapshot.BackgroundPatternColor[1], Is.Zero);
        }

        [Test]
        public void RenderingResetsOamAddressAtSpriteFetchStart()
        {
            ppu.CpuWriteRegister(0x2003, 4);
            ppu.CpuWriteRegister(0x2001, 0x18);

            while (ppu.Scanline == 0 && ppu.Dot <= 257) ppu.Clock();

            Assert.That(ppu.Registers.OamAddress, Is.Zero);
        }

        [Test]
        public void PalFrameUsesThreeHundredAndTwelveScanlines()
        {
            var palPpu = new Ppu2C02(mapper, MirroringMode.Vertical, NesRegion.Pal);
            palPpu.Reset();
            int clocks = 0;
            while (!palPpu.FrameComplete && clocks <= 312 * 341) { palPpu.Clock(); clocks++; }
            Assert.That(clocks, Is.EqualTo(312 * 341));
        }

        [Test]
        public void SceneSnapshotTileHashChangesWithChrBank()
        {
            byte[] chr = new byte[16384];
            chr[0] = 0x80;
            chr[8192] = 0x40;
            byte[] prg = new byte[32768];
            prg[0] = 0xFF; // Allow the bank-select write through CNROM bus conflicts.
            var bankedMapper = new Mapper003(prg, chr);
            ppu = new Ppu2C02(bankedMapper, MirroringMode.Vertical);
            ppu.CpuWriteRegister(0x2001, 0x0A);
            while (ppu.Scanline == 0 && ppu.Dot < 2) ppu.Clock();
            uint firstBankHash = ppu.SceneSnapshot.BackgroundTileHash[0];

            bankedMapper.CpuWrite(0x8000, 1);
            while (ppu.Scanline < 1 || ppu.Dot < 2) ppu.Clock();
            uint secondBankHash = ppu.SceneSnapshot.BackgroundTileHash[PpuFrameBuffer.Width];

            Assert.That(firstBankHash, Is.Not.Zero);
            Assert.That(secondBankHash, Is.Not.EqualTo(firstBankHash));
        }

        [Test]
        public void SpriteZeroHit_RequiresActualOpaqueOverlap()
        {
            byte[] chr = new byte[8192];
            for (int row = 0; row < 8; row++) chr[row] = 0xFF;
            ppu = new Ppu2C02(new Mapper000(new byte[16384], chr), MirroringMode.Vertical);
            ppu.CpuWriteRegister(0x2001, 0x18);
            ppu.CpuWriteRegister(0x2003, 0);
            ppu.CpuWriteRegister(0x2004, 10); // Y
            ppu.CpuWriteRegister(0x2004, 0);  // tile
            ppu.CpuWriteRegister(0x2004, 0);  // attributes
            ppu.CpuWriteRegister(0x2004, 20); // X
            int safety = 341 * 30;
            while ((ppu.Registers.Status & 0x40) == 0 && safety-- > 0) ppu.Clock();
            Assert.That(ppu.Registers.Status & 0x40, Is.Not.Zero);
        }

        [Test]
        public void SpriteZeroHit_UsesLowerTileInEightBySixteenMode()
        {
            byte[] chr = new byte[8192];
            for (int row = 0; row < 8; row++)
            {
                chr[row] = 0xFF;             // Opaque background tile 0 from $0000.
                chr[0x1010 + row] = 0xFF;    // Lower half of 8x16 sprite tile $01.
            }
            ppu = new Ppu2C02(new Mapper000(new byte[16384], chr), MirroringMode.Vertical);
            ppu.CpuWriteRegister(0x2000, 0x20); // 8x16 sprites.
            ppu.CpuWriteRegister(0x2001, 0x1E);
            ppu.CpuWriteRegister(0x2003, 0);
            ppu.CpuWriteRegister(0x2004, 10);
            ppu.CpuWriteRegister(0x2004, 1);  // Bank $1000, tiles $00/$01.
            ppu.CpuWriteRegister(0x2004, 0);
            ppu.CpuWriteRegister(0x2004, 20);

            int safety = 341 * 40;
            while ((ppu.Registers.Status & 0x40) == 0 && safety-- > 0) ppu.Clock();
            Assert.That(ppu.Registers.Status & 0x40, Is.Not.Zero);
            Assert.That(ppu.Scanline, Is.GreaterThanOrEqualTo(19), "Only the lower 8x8 tile is opaque.");
        }

        [Test]
        public void TransparentBackground_DoesNotProduceSpriteZeroHit()
        {
            byte[] chr = new byte[8192];
            for (int row = 0; row < 8; row++) chr[row] = 0xFF; // sprite tile 0
            ppu = new Ppu2C02(new Mapper000(new byte[16384], chr), MirroringMode.Vertical);
            for (int i = 0; i < 32 * 30; i++)
                ppu.PpuWrite((ushort)(0x2000 + i), 1); // transparent background tile 1
            ppu.CpuWriteRegister(0x2001, 0x1E);
            ppu.CpuWriteRegister(0x2003, 0);
            ppu.CpuWriteRegister(0x2004, 10);
            ppu.CpuWriteRegister(0x2004, 0);
            ppu.CpuWriteRegister(0x2004, 0);
            ppu.CpuWriteRegister(0x2004, 20);

            for (int i = 0; i < 341 * 30; i++) ppu.Clock();
            Assert.That(ppu.Registers.Status & 0x40, Is.Zero);
        }

        [Test]
        public void SceneSnapshot_SeparatesBackgroundAndSpriteLayers()
        {
            byte[] chr = new byte[8192];
            for (int row = 0; row < 8; row++) chr[row] = 0xFF;
            ppu = new Ppu2C02(new Mapper000(new byte[16384], chr), MirroringMode.Vertical);
            ppu.PpuWrite(0x3F01, 0x30);
            ppu.PpuWrite(0x3F11, 0x16);
            ppu.CpuWriteRegister(0x2001, 0x1E);
            ppu.CpuWriteRegister(0x2003, 0);
            ppu.CpuWriteRegister(0x2004, 10);
            ppu.CpuWriteRegister(0x2004, 0);
            ppu.CpuWriteRegister(0x2004, 0);
            ppu.CpuWriteRegister(0x2004, 20);

            for (int i = 0; i < 341 * 30; i++) ppu.Clock();

            int overlap = 11 * PpuFrameBuffer.Width + 20;
            Assert.That(ppu.SceneSnapshot.BackgroundPixels[overlap], Is.Not.Zero);
            Assert.That(ppu.SceneSnapshot.SpritePixels[overlap], Is.Not.Zero);
            Assert.That(ppu.SceneSnapshot.BackgroundOpaque[overlap], Is.EqualTo(1));
            Assert.That(ppu.SceneSnapshot.BackgroundPattern[overlap], Is.Zero);
            Assert.That(ppu.SceneSnapshot.BackgroundPixels[overlap],
                Is.Not.EqualTo(ppu.SceneSnapshot.SpritePixels[overlap]));
        }

        [Test]
        public void SceneSnapshot_UsesTransparentPixelsForEmptyBackground()
        {
            ppu.CpuWriteRegister(0x2001, 0x0A);
            for (int i = 0; i < 3; i++) ppu.Clock();

            Assert.That(ppu.FrameBuffer.Pixels[0], Is.Not.Zero,
                "The normal 2D output keeps the NES backdrop color.");
            Assert.That(ppu.SceneSnapshot.BackgroundPixels[0], Is.Zero,
                "The 3D background layer exposes empty backdrop pixels as transparent.");
        }

        [Test]
        public void ScrollWriteDuringScanline_AppliesOnFollowingScanline()
        {
            ppu.CpuWriteRegister(0x2001, 0x08);
            ppu.Clock(); // scanline 0, dot 0 latches scroll
            ppu.CpuWriteRegister(0x2005, 24);
            ppu.CpuWriteRegister(0x2005, 0);
            Assert.That(ppu.ScrollX, Is.EqualTo(24));
            for (int i = 0; i < 340; i++) ppu.Clock();
            Assert.That(ppu.Scanline, Is.EqualTo(1));
        }

        private void SetAddress(ushort address)
        {
            ppu.CpuWriteRegister(0x2006, (byte)(address >> 8));
            ppu.CpuWriteRegister(0x2006, (byte)address);
        }

        private void ClockToVblank()
        {
            int safety = 262 * 341;
            while ((ppu.Registers.Status & 0x80) == 0 && safety-- > 0) ppu.Clock();
            Assert.That(safety, Is.GreaterThan(0), "PPU did not enter VBlank.");
        }
    }
}
