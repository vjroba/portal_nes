using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    public interface IMapper
    {
        MirroringMode? MirroringOverride { get; }
        ushort CpuAddressStart { get; }
        bool IrqPending { get; }
        void ClockScanline();
        byte CpuRead(ushort address);
        void CpuWrite(ushort address, byte value);
        byte PpuRead(ushort address);
        void PpuWrite(ushort address, byte value);
    }

    /// <summary>Optional side-effect-free CHR inspection used by diagnostics and 3D snapshots.</summary>
    public interface IPpuPeekMapper
    {
        byte PpuPeek(ushort address);
    }

    /// <summary>Optional mapper clock driven by elapsed CPU cycles.</summary>
    public interface ICpuClockedMapper
    {
        void ClockCpu(int cycles);
    }

    /// <summary>
    /// Optional cartridge-specific CIRAM wiring for mappers whose nametable
    /// selection cannot be represented by one global mirroring mode.
    /// </summary>
    public interface INametableMappingMapper
    {
        int MapNametableAddress(ushort address);
    }

    /// <summary>
    /// Optional mapper-owned nametable memory. Used when nametable pages may
    /// independently select cartridge CHR ROM or CIRAM.
    /// </summary>
    public interface INametableMemoryMapper
    {
        byte ReadNametable(ushort address);
        void WriteNametable(ushort address, byte value);
    }

    /// <summary>Optional cartridge audio source mixed with the internal APU.</summary>
    public interface IExpansionAudioMapper
    {
        void ClockAudio(int cycles);
        float ExpansionAudioSample { get; }
    }

    /// <summary>Expansion audio whose envelopes and length counters use the APU frame sequencer.</summary>
    public interface IFrameSequencedExpansionAudioMapper
    {
        void ClockAudioQuarterFrame();
        void ClockAudioHalfFrame();
    }

    /// <summary>Optional independent CHR mappings for background and sprite fetches.</summary>
    public interface ISeparateChrMapper
    {
        byte ReadBackgroundPattern(ushort address, ushort nametableAddress);
        byte ReadSpritePattern(ushort address);
        int GetBackgroundPalette(ushort nametableAddress, int standardPalette);
    }

    /// <summary>Optional notification for mappers whose counters track visible frames.</summary>
    public interface IPpuFrameMapper
    {
        void BeginPpuFrame();
        void EndPpuFrame();
    }

    /// <summary>Optional mapper-provided background region drawn independently of PPU scroll.</summary>
    public interface IVerticalSplitMapper
    {
        bool TryGetSplitTile(int screenTileColumn, int scanline,
            out byte tile, out int palette, out int fineY);
        byte ReadSplitPattern(ushort address);
    }
}
