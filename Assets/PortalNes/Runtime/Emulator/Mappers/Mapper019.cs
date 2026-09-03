using System;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Namco 129/163 with PRG/CHR/nametable banking, 15-bit cycle IRQ,
    /// 128 bytes of internal RAM and up to eight wavetable audio channels.
    /// </summary>
    public sealed class Mapper019 : IMapper, ICpuClockedMapper, IExpansionAudioMapper,
        INametableMemoryMapper
    {
        private const int PrgBankSize = 8 * 1024;
        private const int ChrBankSize = 1024;
        private readonly byte[] prgRom;
        private readonly byte[] chrRom;
        private readonly byte[] prgRam = new byte[8 * 1024];
        private readonly byte[] ciram = new byte[2 * 1024];
        private readonly byte[] internalRam = new byte[128];
        private readonly byte[] chrBanks = new byte[8];
        private readonly byte[] nametableBanks = new byte[4];
        private readonly byte[] prgBanks = new byte[3];
        private readonly int prgBankCount;
        private readonly int chrBankCount;
        private int irqCounter;
        private bool irqEnabled;
        private bool irqPending;
        private byte internalAddress;
        private bool internalAutoIncrement;
        private byte ramProtection;
        private bool lowChrRamDisabled;
        private bool highChrRamDisabled;
        private bool soundDisabled = true;
        private int audioDivider;
        private int audioChannel;
        private float audioOutput;
        private readonly float[] audioChannelOutputs = new float[8];
        private long irqTriggerCount;
        private long irqAcknowledgeCount;

        public ushort CpuAddressStart => 0x4800;
        public Cartridge.MirroringMode? MirroringOverride => null;
        public bool IrqPending => irqPending;
        public float ExpansionAudioSample => soundDisabled ? 0 : audioOutput;
        public int IrqCounter => irqCounter;
        public bool IrqEnabled => irqEnabled;
        public byte InternalAddress => internalAddress;
        public byte GetChrBank(int index) => chrBanks[index & 7];
        public byte GetNametableBank(int index) => nametableBanks[index & 3];
        public byte GetPrgBank(int index) => prgBanks[index % 3];
        public bool SoundDisabled => soundDisabled;
        public long IrqTriggerCount => irqTriggerCount;
        public long IrqAcknowledgeCount => irqAcknowledgeCount;

        public Mapper019(byte[] prgRom, byte[] chrRom)
        {
            this.prgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            this.chrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            if (prgRom.Length < 4 * PrgBankSize || prgRom.Length > 512 * 1024 ||
                prgRom.Length % PrgBankSize != 0)
                throw new ArgumentException(
                    "Mapper 19 PRG ROM must contain 32KB to 512KB in complete 8KB banks.",
                    nameof(prgRom));
            if (chrRom.Length < 8 * ChrBankSize || chrRom.Length > 256 * 1024 ||
                chrRom.Length % ChrBankSize != 0)
                throw new ArgumentException(
                    "Mapper 19 CHR ROM must contain 8KB to 256KB in complete 1KB banks.",
                    nameof(chrRom));
            prgBankCount = prgRom.Length / PrgBankSize;
            chrBankCount = chrRom.Length / ChrBankSize;
            for (int i = 0; i < nametableBanks.Length; i++)
                nametableBanks[i] = (byte)(0xE0 + (i & 1));
        }

        public byte CpuRead(ushort address)
        {
            if (address < 0x4800) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x5000) return ReadInternalRam();
            if (address < 0x5800) return (byte)irqCounter;
            if (address < 0x6000)
                return (byte)((irqCounter >> 8) | (irqEnabled ? 0x80 : 0));
            if (address < 0x8000) return prgRam[address - 0x6000];
            int slot = (address - 0x8000) / PrgBankSize;
            int bank = slot < 3 ? prgBanks[slot] % prgBankCount : prgBankCount - 1;
            return prgRom[bank * PrgBankSize + (address & 0x1FFF)];
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address < 0x4800) throw new ArgumentOutOfRangeException(nameof(address));
            if (address < 0x5000)
            {
                WriteInternalRam(value);
                return;
            }
            if (address < 0x5800)
            {
                irqCounter = (irqCounter & 0x7F00) | value;
                irqPending = false;
                irqAcknowledgeCount++;
                return;
            }
            if (address < 0x6000)
            {
                irqCounter = (irqCounter & 0x00FF) | ((value & 0x7F) << 8);
                irqEnabled = (value & 0x80) != 0;
                irqPending = false;
                irqAcknowledgeCount++;
                return;
            }
            if (address < 0x8000)
            {
                int window = (address - 0x6000) >> 11;
                bool protectionKey = (ramProtection & 0xF0) == 0x40;
                if (protectionKey && (ramProtection & (1 << window)) == 0)
                    prgRam[address - 0x6000] = value;
                return;
            }
            if (address < 0xC000)
            {
                chrBanks[(address - 0x8000) >> 11] = value;
                return;
            }
            if (address < 0xE000)
            {
                nametableBanks[(address - 0xC000) >> 11] = value;
                return;
            }
            if (address < 0xE800)
            {
                prgBanks[0] = (byte)(value & 0x3F);
                soundDisabled = (value & 0x40) != 0;
                return;
            }
            if (address < 0xF000)
            {
                prgBanks[1] = (byte)(value & 0x3F);
                lowChrRamDisabled = (value & 0x40) != 0;
                highChrRamDisabled = (value & 0x80) != 0;
                return;
            }
            if (address < 0xF800)
            {
                prgBanks[2] = (byte)(value & 0x3F);
                return;
            }

            ramProtection = value;
            internalAddress = (byte)(value & 0x7F);
            internalAutoIncrement = (value & 0x80) != 0;
        }

        public byte PpuRead(ushort address)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = address >> 10;
            byte bank = chrBanks[slot];
            if (PatternBankUsesCiram(address, bank))
                return ciram[((bank & 1) << 10) | (address & 0x03FF)];
            int mappedBank = bank % chrBankCount;
            return chrRom[mappedBank * ChrBankSize + (address & 0x03FF)];
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000) throw new ArgumentOutOfRangeException(nameof(address));
            int slot = address >> 10;
            byte bank = chrBanks[slot];
            if (PatternBankUsesCiram(address, bank))
                ciram[((bank & 1) << 10) | (address & 0x03FF)] = value;
        }

        public byte ReadNametable(ushort address)
        {
            int offset = (address - 0x2000) & 0x0FFF;
            byte bank = nametableBanks[offset >> 10];
            if (bank >= 0xE0)
                return ciram[((bank & 1) << 10) | (offset & 0x03FF)];
            int mappedBank = bank % chrBankCount;
            return chrRom[mappedBank * ChrBankSize + (offset & 0x03FF)];
        }

        public void WriteNametable(ushort address, byte value)
        {
            int offset = (address - 0x2000) & 0x0FFF;
            byte bank = nametableBanks[offset >> 10];
            if (bank >= 0xE0)
                ciram[((bank & 1) << 10) | (offset & 0x03FF)] = value;
        }

        public void ClockCpu(int cycles)
        {
            if (!irqEnabled || irqCounter >= 0x7FFF || cycles <= 0) return;
            irqCounter += cycles;
            if (irqCounter >= 0x7FFF)
            {
                irqCounter = 0x7FFF;
                irqPending = true;
                irqTriggerCount++;
            }
        }

        public void ClockAudio(int cycles)
        {
            if (soundDisabled || cycles <= 0)
            {
                audioOutput = 0;
                return;
            }
            audioDivider += cycles;
            while (audioDivider >= 15)
            {
                audioDivider -= 15;
                UpdateAudioChannel();
            }
        }

        public void ClockScanline() { }

        private bool PatternBankUsesCiram(ushort address, byte bank)
        {
            if (bank < 0xE0) return false;
            return address < 0x1000 ? !lowChrRamDisabled : !highChrRamDisabled;
        }

        private byte ReadInternalRam()
        {
            byte value = internalRam[internalAddress];
            IncrementInternalAddress();
            return value;
        }

        private void WriteInternalRam(byte value)
        {
            internalRam[internalAddress] = value;
            IncrementInternalAddress();
        }

        private void IncrementInternalAddress()
        {
            if (internalAutoIncrement && internalAddress < 0x7F) internalAddress++;
        }

        private void UpdateAudioChannel()
        {
            int enabledChannels = ((internalRam[0x7F] >> 4) & 7) + 1;
            if (audioChannel >= enabledChannels) audioChannel = 0;
            int register = 0x78 - audioChannel * 8;
            int frequency = internalRam[register] |
                (internalRam[register + 2] << 8) |
                ((internalRam[register + 4] & 3) << 16);
            int length = 256 - (internalRam[register + 4] & 0xFC);
            int phase = internalRam[register + 1] |
                (internalRam[register + 3] << 8) |
                (internalRam[register + 5] << 16);
            int modulus = length << 16;
            phase = modulus == 0 ? 0 : (phase + frequency) % modulus;
            internalRam[register + 1] = (byte)phase;
            internalRam[register + 3] = (byte)(phase >> 8);
            internalRam[register + 5] = (byte)(phase >> 16);

            int sampleIndex = ((phase >> 16) + internalRam[register + 6]) & 0xFF;
            byte packed = internalRam[sampleIndex >> 1];
            int sample = (packed >> ((sampleIndex & 1) * 4)) & 0x0F;
            int volume = internalRam[register + 7] & 0x0F;
            audioChannelOutputs[audioChannel] = (sample - 8) * volume;
            float mixed = 0;
            for (int i = 0; i < enabledChannels; i++)
                mixed += audioChannelOutputs[i];
            // The real chip multiplexes one channel at a time. Passing that
            // ultrasonic switching waveform directly to Unity aliases badly,
            // especially in the two commercial 8-channel games. Averaging the
            // held channel outputs is the standard low-pass approximation.
            audioOutput = mixed / enabledChannels;
            audioChannel++;
        }
    }
}
