using System;
using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Nintendo MMC5. Implements PRG/CHR banking, banked WRAM, ExRAM and
    /// nametable modes, extended background attributes, scanline IRQ and the
    /// hardware multiplier. Vertical split and expansion audio are separate
    /// accuracy milestones.
    /// </summary>
    public sealed class Mapper005 : IMapper, INametableMemoryMapper,
        ISeparateChrMapper, IPpuFrameMapper, IVerticalSplitMapper,
        IExpansionAudioMapper, IFrameSequencedExpansionAudioMapper
    {
        private const int PrgBankSize = 8192;
        private const int ChrBankSize = 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chr;
        private readonly bool chrRam;
        private readonly byte[] prgRam = new byte[64 * 1024];
        private readonly byte[] ciram = new byte[2 * 1024];
        private readonly byte[] exRam = new byte[1024];
        private readonly byte[] prgBanks = new byte[5];
        private readonly ushort[] spriteChrBanks = new ushort[8];
        private readonly ushort[] backgroundChrBanks = new ushort[4];
        private readonly Mmc5Pulse pulse1 = new Mmc5Pulse();
        private readonly Mmc5Pulse pulse2 = new Mmc5Pulse();
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private byte prgMode = 3;
        private byte chrMode = 3;
        private byte protect1;
        private byte protect2;
        private byte exRamMode;
        private byte nametableMapping;
        private byte fillTile;
        private byte fillPalette;
        private byte chrHighBits;
        private byte splitControl;
        private byte splitScroll;
        private byte splitChrBank;
        private byte irqScanline;
        private bool irqEnabled;
        private bool irqPending;
        private bool inFrame;
        private int scanlineCounter;
        private byte multiplierA;
        private byte multiplierB;
        private byte pcmOutput;
        private bool pcmReadMode;
        private bool pcmIrqEnabled;
        private bool pcmIrqPending;
        private long audioCycles;
        private bool lastChrSetWasBackground;

        public ushort CpuAddressStart => 0x5000;
        public MirroringMode? MirroringOverride => null;
        public bool IrqPending => irqPending || pcmIrqPending;
        public float ExpansionAudioSample => pulse1.Output + pulse2.Output + pcmOutput * 0.5f;
        public byte PrgMode => prgMode;
        public byte ChrMode => chrMode;
        public byte ExRamMode => exRamMode;
        public byte IrqScanline => irqScanline;
        public bool InFrame => inFrame;

        public Mapper005(byte[] prgRom, byte[] chr, bool chrRam)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chr = chr ?? throw new ArgumentNullException(nameof(chr));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length > 1024 * 1024 ||
                prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException("MMC5 PRG ROM must contain 32KB to 1MB in complete 8KB banks.", nameof(prgRom));
            if (chr.Length < 8 * ChrBankSize || chr.Length > 1024 * 1024 ||
                chr.Length % ChrBankSize != 0)
                throw new ArgumentException("MMC5 CHR memory must contain 8KB to 1MB in complete 1KB banks.", nameof(chr));
            this.chrRam = chrRam;
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chr.Length / ChrBankSize;
            prgBanks[4] = 0xFF;
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x5000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address == 0x5010)
            {
                byte value = (byte)(pcmIrqPending ? 0x80 : 0);
                pcmIrqPending = false;
                return value;
            }
            if (address == 0x5015)
                return (byte)((pulse1.Active ? 1 : 0) | (pulse2.Active ? 2 : 0));
            if (address == 0x5204)
            {
                byte value = (byte)((irqPending ? 0x80 : 0) | (inFrame ? 0x40 : 0));
                irqPending = false;
                return value;
            }
            int product = multiplierA * multiplierB;
            if (address == 0x5205) return (byte)product;
            if (address == 0x5206) return (byte)(product >> 8);
            if (address >= 0x5C00 && address < 0x6000)
                return exRam[address - 0x5C00];
            if (address < 0x6000) return 0;
            if (address < 0x8000) return ReadPrgRam(prgBanks[0], address);
            byte programValue = ReadMappedPrg(address);
            if (pcmReadMode && address < 0xC000)
            {
                if (programValue == 0)
                {
                    if (pcmIrqEnabled) pcmIrqPending = true;
                }
                else pcmOutput = programValue;
            }
            return programValue;
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x5000) throw new ArgumentOutOfRangeException(nameof(address));
            if (address >= 0x5C00 && address < 0x6000)
            {
                if (exRamMode != 3) exRam[address - 0x5C00] = value;
                return;
            }
            if (address >= 0x6000 && address < 0x8000)
            {
                WritePrgRam(prgBanks[0], address, value);
                return;
            }
            if (address >= 0x8000)
            {
                WriteMappedPrgRam(address, value);
                return;
            }
            if (address >= 0x5000 && address <= 0x5003)
            {
                pulse1.Write(address - 0x5000, value);
                return;
            }
            if (address >= 0x5004 && address <= 0x5007)
            {
                pulse2.Write(address - 0x5004, value);
                return;
            }
            switch (address)
            {
                case 0x5010:
                    pcmReadMode = (value & 1) != 0;
                    pcmIrqEnabled = (value & 0x80) != 0;
                    if (!pcmIrqEnabled) pcmIrqPending = false;
                    return;
                case 0x5011:
                    if (value != 0) pcmOutput = value;
                    return;
                case 0x5015:
                    pulse1.SetEnabled((value & 1) != 0);
                    pulse2.SetEnabled((value & 2) != 0);
                    return;
                case 0x5100: prgMode = (byte)(value & 3); return;
                case 0x5101: chrMode = (byte)(value & 3); return;
                case 0x5102: protect1 = (byte)(value & 3); return;
                case 0x5103: protect2 = (byte)(value & 3); return;
                case 0x5104: exRamMode = (byte)(value & 3); return;
                case 0x5105: nametableMapping = value; return;
                case 0x5106: fillTile = value; return;
                case 0x5107: fillPalette = (byte)(value & 3); return;
                case 0x5113: prgBanks[0] = (byte)(value & 7); return;
                case 0x5114: case 0x5115: case 0x5116: case 0x5117:
                    prgBanks[address - 0x5113] = value; return;
                case 0x5130: chrHighBits = (byte)(value & 3); return;
                case 0x5200: splitControl = value; return;
                case 0x5201: splitScroll = value; return;
                case 0x5202: splitChrBank = value; return;
                case 0x5203: irqScanline = value; return;
                case 0x5204: irqEnabled = (value & 0x80) != 0; if (!irqEnabled) irqPending = false; return;
                case 0x5205: multiplierA = value; return;
                case 0x5206: multiplierB = value; return;
            }
            if (address >= 0x5120 && address <= 0x5127)
            {
                spriteChrBanks[address - 0x5120] = (ushort)((chrHighBits << 8) | value);
                lastChrSetWasBackground = false;
            }
            else if (address >= 0x5128 && address <= 0x512B)
            {
                backgroundChrBanks[address - 0x5128] = (ushort)((chrHighBits << 8) | value);
                lastChrSetWasBackground = true;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            return lastChrSetWasBackground
                ? ReadBackgroundPattern(address, 0x2000)
                : ReadSpritePattern(address);
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            if (!chrRam) return;
            int bank = MapChrBank(spriteChrBanks, address >> 10, false);
            chr[(bank % chrBankCount) * ChrBankSize + (address & 0x3FF)] = value;
        }

        public byte ReadBackgroundPattern(ushort address, ushort nametableAddress)
        {
            if (exRamMode == 1)
            {
                byte extended = exRam[(nametableAddress - 0x2000) & 0x3FF];
                int bank4k = (chrHighBits << 6) | (extended & 0x3F);
                return chr[(bank4k * 4096 + (address & 0x0FFF)) % chr.Length];
            }
            int bank = MapChrBank(backgroundChrBanks, (address >> 10) & 3, true);
            return chr[(bank % chrBankCount) * ChrBankSize + (address & 0x3FF)];
        }

        public byte ReadSpritePattern(ushort address)
        {
            int bank = MapChrBank(spriteChrBanks, address >> 10, false);
            return chr[(bank % chrBankCount) * ChrBankSize + (address & 0x3FF)];
        }

        public int GetBackgroundPalette(ushort nametableAddress, int standardPalette) =>
            exRamMode == 1
                ? exRam[(nametableAddress - 0x2000) & 0x3FF] >> 6
                : standardPalette;

        public bool TryGetSplitTile(int screenTileColumn, int scanline,
            out byte tile, out int palette, out int fineY)
        {
            tile = 0;
            palette = 0;
            fineY = 0;
            if ((splitControl & 0x80) == 0 || exRamMode >= 2) return false;

            int threshold = splitControl & 0x1F;
            bool rightSide = (splitControl & 0x40) != 0;
            bool inSplit = rightSide
                ? screenTileColumn >= threshold
                : screenTileColumn < threshold;
            if (!inSplit) return false;

            int splitY = (scanline + splitScroll) % 240;
            int row = splitY >> 3;
            int column = screenTileColumn & 31;
            tile = exRam[row * 32 + column];
            byte attribute = exRam[0x3C0 + (row >> 2) * 8 + (column >> 2)];
            int shift = ((row & 2) << 1) | (column & 2);
            palette = (attribute >> shift) & 3;
            fineY = splitY & 7;
            return true;
        }

        public byte ReadSplitPattern(ushort address) =>
            chr[(splitChrBank * 4096 + (address & 0x0FFF)) % chr.Length];

        public byte ReadNametable(ushort address)
        {
            int offset = (address - 0x2000) & 0x0FFF;
            int source = (nametableMapping >> ((offset >> 10) * 2)) & 3;
            int within = offset & 0x3FF;
            if (source < 2) return ciram[source * 0x400 + within];
            if (source == 2) return exRamMode <= 1 ? exRam[within] : (byte)0;
            if (within < 0x3C0) return fillTile;
            int palette = fillPalette & 3;
            return (byte)(palette | (palette << 2) | (palette << 4) | (palette << 6));
        }

        public void WriteNametable(ushort address, byte value)
        {
            int offset = (address - 0x2000) & 0x0FFF;
            int source = (nametableMapping >> ((offset >> 10) * 2)) & 3;
            int within = offset & 0x3FF;
            if (source < 2) ciram[source * 0x400 + within] = value;
            else if (source == 2 && exRamMode <= 1) exRam[within] = value;
        }

        public void BeginPpuFrame() { inFrame = true; scanlineCounter = 0; }
        public void EndPpuFrame() { inFrame = false; irqPending = false; }

        public void ClockScanline()
        {
            if (!inFrame) return;
            if (scanlineCounter == irqScanline && irqEnabled) irqPending = true;
            scanlineCounter++;
        }

        public void ClockAudio(int cycles)
        {
            if (cycles <= 0) return;
            int clocks = cycles / 2;
            if ((cycles & 1) != 0 && (audioCycles & 1) == 0) clocks++;
            audioCycles += cycles;
            pulse1.ClockTimer(clocks);
            pulse2.ClockTimer(clocks);
        }

        public void ClockAudioQuarterFrame()
        {
            pulse1.ClockEnvelope();
            pulse2.ClockEnvelope();
        }

        public void ClockAudioHalfFrame()
        {
            pulse1.ClockLength();
            pulse2.ClockLength();
        }

        private byte ReadMappedPrg(ushort address)
        {
            int slot = (address - 0x8000) >> 13;
            byte register = GetPrgRegister(slot);
            if (CanMapPrgRam(slot) && (register & 0x80) == 0)
                return ReadPrgRam(register, address);
            int bank = GetPrgRomBank(slot, register) % prgBankCount;
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        private byte GetPrgRegister(int slot)
        {
            switch (prgMode)
            {
                case 0: return prgBanks[4];
                case 1: return slot < 2 ? prgBanks[2] : prgBanks[4];
                case 2: return slot < 2 ? prgBanks[2] : prgBanks[slot + 1];
                default: return prgBanks[slot + 1];
            }
        }

        private int GetPrgRomBank(int slot, byte register)
        {
            int bank = register & 0x7F;
            if (prgMode == 0) return (bank & ~3) | slot;
            if (prgMode == 1 || (prgMode == 2 && slot < 2)) return (bank & ~1) | (slot & 1);
            return bank;
        }

        private bool CanMapPrgRam(int slot)
        {
            // $5117 always selects ROM. In the wider PRG modes it controls
            // every slot in its 32KB/16KB group, not only the final 8KB slot.
            if (prgMode == 0) return false;
            if (prgMode == 1) return slot < 2;
            return slot < 3;
        }

        private int MapChrBank(ushort[] banks, int slot, bool background)
        {
            int logicalSlot = background ? slot & 3 : slot & 7;
            int index;
            switch (chrMode)
            {
                case 0: index = banks.Length - 1; break;
                case 1: index = ((logicalSlot >> 2) << 2) + 3; break;
                case 2: index = ((logicalSlot >> 1) << 1) + 1; break;
                default: index = logicalSlot; break;
            }
            if (index >= banks.Length) index = banks.Length - 1;
            int bank = banks[index];
            int mask = chrMode == 0 ? 7 : chrMode == 1 ? 3 : chrMode == 2 ? 1 : 0;
            return (bank & ~mask) | (logicalSlot & mask);
        }

        private byte ReadPrgRam(byte bankRegister, ushort address) =>
            prgRam[((bankRegister & 7) * PrgBankSize) + (address & 0x1FFF)];

        private void WritePrgRam(byte bankRegister, ushort address, byte value)
        {
            if (protect1 == 2 && protect2 == 1)
                prgRam[((bankRegister & 7) * PrgBankSize) + (address & 0x1FFF)] = value;
        }

        private void WriteMappedPrgRam(ushort address, byte value)
        {
            int slot = (address - 0x8000) >> 13;
            byte register = GetPrgRegister(slot);
            if (CanMapPrgRam(slot) && (register & 0x80) == 0) WritePrgRam(register, address, value);
        }

        private sealed class Mmc5Pulse
        {
            private static readonly byte[] LengthTable =
                { 10,254,20,2,40,4,80,6,160,8,60,10,14,12,26,14,12,16,24,18,48,20,96,22,192,24,72,26,16,28,32,30 };
            private static readonly byte[] DutyPatterns = { 0x02, 0x06, 0x1E, 0xF9 };
            private bool enabled;
            private bool lengthHalt;
            private bool constantVolume;
            private bool envelopeStart;
            private int duty;
            private int volume;
            private int envelopeDivider;
            private int envelopeDecay;
            private int timerPeriod;
            private int timerCounter;
            private int sequence;
            private int lengthCounter;

            public bool Active => lengthCounter > 0;
            public int Output =>
                enabled && lengthCounter > 0 && ((DutyPatterns[duty] >> sequence) & 1) != 0
                    ? constantVolume ? volume : envelopeDecay
                    : 0;

            public void SetEnabled(bool value)
            {
                enabled = value;
                if (!value) lengthCounter = 0;
            }

            public void Write(int register, byte value)
            {
                if (register == 0)
                {
                    duty = value >> 6;
                    lengthHalt = (value & 0x20) != 0;
                    constantVolume = (value & 0x10) != 0;
                    volume = value & 15;
                }
                else if (register == 2) timerPeriod = (timerPeriod & 0x700) | value;
                else if (register == 3)
                {
                    timerPeriod = (timerPeriod & 0xFF) | ((value & 7) << 8);
                    if (enabled) lengthCounter = LengthTable[value >> 3];
                    sequence = 0;
                    envelopeStart = true;
                }
            }

            public void ClockTimer(int clocks)
            {
                if (clocks <= 0) return;
                if (clocks <= timerCounter) { timerCounter -= clocks; return; }
                clocks -= timerCounter + 1;
                int divider = timerPeriod + 1;
                int steps = 1 + clocks / divider;
                sequence = (sequence + steps) & 7;
                timerCounter = timerPeriod - clocks % divider;
            }

            public void ClockEnvelope()
            {
                if (envelopeStart)
                {
                    envelopeStart = false;
                    envelopeDecay = 15;
                    envelopeDivider = volume;
                }
                else if (envelopeDivider == 0)
                {
                    envelopeDivider = volume;
                    if (envelopeDecay > 0) envelopeDecay--;
                    else if (lengthHalt) envelopeDecay = 15;
                }
                else envelopeDivider--;
            }

            public void ClockLength()
            {
                if (!lengthHalt && lengthCounter > 0) lengthCounter--;
            }
        }
    }
}
