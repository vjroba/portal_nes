using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>Konami VRC6b with CPU A0/A1 swapped.</summary>
    public sealed class Mapper026 : Vrc6Mapper
    {
        public Mapper026(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
            : base(prgRom, chrRom, initialMirroring, true)
        {
        }
    }
}
