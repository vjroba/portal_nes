using PortalNes.Emulator.Cartridge;

namespace PortalNes.Emulator.Mappers
{
    /// <summary>
    /// Konami VRC4a/VRC4c. Uses the shared VRC4 implementation with
    /// A1/A2 or A6/A7 connected as the internal register-select lines.
    /// </summary>
    public sealed class Mapper021 : Vrc2Vrc4Mapper
    {
        public Mapper021(byte[] prgRom, byte[] chrRom, MirroringMode initialMirroring)
            : base(prgRom, chrRom, initialMirroring, 2)
        {
        }
    }
}
