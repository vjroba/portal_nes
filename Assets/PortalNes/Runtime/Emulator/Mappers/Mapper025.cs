using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Konami VRC2c/VRC4b/VRC4d. Functionally equivalent to the VRC4
    /// implementation used by mapper 23, with different CPU address wiring.
    /// </summary>
    public sealed class Mapper025 : Vrc2Vrc4Mapper
    {
        public Mapper025(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
            : base(prgRom, chrRom, initialMirroring, 1)
        {
        }
    }
}
