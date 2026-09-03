using System;
using PortalNes.Emulator.Mappers;
namespace PortalNes.Emulator.Cartridge
{
    public sealed class Cartridge
    {
        public int MapperNumber { get; }
        public int HeaderMapperNumber { get; }
        public int SubmapperNumber { get; }
        public MirroringMode Mirroring { get; }
        public bool HasBatteryBackedRam { get; }
        public bool HasChrRam { get; }
        public NesRegion Region { get; }
        public byte[] PrgRom { get; }
        public byte[] ChrRom { get; }
        public IMapper Mapper { get; }
        internal Cartridge(int mapperNumber, int headerMapperNumber, int submapperNumber,
            MirroringMode mirroring, bool battery,
            byte[] prgRom, byte[] chrRom, bool hasChrRam, NesRegion region,
            bool forceMapper210Namco340 = false)
        {
            MapperNumber = mapperNumber; HeaderMapperNumber = headerMapperNumber;
            SubmapperNumber = submapperNumber;
            Mirroring = mirroring; HasBatteryBackedRam = battery; HasChrRam = hasChrRam;
            Region = region;
            PrgRom = prgRom ?? throw new ArgumentNullException(nameof(prgRom));
            ChrRom = chrRom ?? throw new ArgumentNullException(nameof(chrRom));
            switch (mapperNumber)
            {
                case 0: Mapper = new Mapper000(PrgRom, ChrRom); break;
                case 1: Mapper = new Mapper001(PrgRom, ChrRom, hasChrRam); break;
                case 2: Mapper = new Mapper002(PrgRom, ChrRom, hasChrRam); break;
                case 3: Mapper = new Mapper003(PrgRom, ChrRom, submapperNumber != 1); break;
                case 4: Mapper = new Mapper004(PrgRom, ChrRom, hasChrRam); break;
                case 5: Mapper = new Mapper005(PrgRom, ChrRom, hasChrRam); break;
                case 7: Mapper = new Mapper007(PrgRom, ChrRom, hasChrRam); break;
                case 9: Mapper = new Mapper009(PrgRom, ChrRom); break;
                case 10: Mapper = new Mapper010(PrgRom, ChrRom); break;
                case 18: Mapper = new Mapper018(PrgRom, ChrRom, mirroring); break;
                case 19: Mapper = new Mapper019(PrgRom, ChrRom); break;
                case 21: Mapper = new Mapper021(PrgRom, ChrRom, mirroring); break;
                case 22: Mapper = new Mapper022(PrgRom, ChrRom, mirroring); break;
                case 23: Mapper = new Mapper023(PrgRom, ChrRom, mirroring); break;
                case 24: Mapper = new Mapper024(PrgRom, ChrRom, mirroring); break;
                case 25: Mapper = new Mapper025(PrgRom, ChrRom, mirroring); break;
                case 26: Mapper = new Mapper026(PrgRom, ChrRom, mirroring); break;
                case 32: Mapper = new Mapper032(PrgRom, ChrRom, mirroring, submapperNumber == 1); break;
                case 33: Mapper = new Mapper033(PrgRom, ChrRom, mirroring); break;
                case 48: Mapper = new Mapper048(PrgRom, ChrRom, mirroring); break;
                case 65: Mapper = new Mapper065(PrgRom, ChrRom, mirroring); break;
                case 66: Mapper = new Mapper066(PrgRom, ChrRom, hasChrRam); break;
                case 67: Mapper = new Mapper067(PrgRom, ChrRom, mirroring); break;
                case 68: Mapper = new Mapper068(PrgRom, ChrRom, mirroring); break;
                case 69: Mapper = new Mapper069(PrgRom, ChrRom, mirroring); break;
                case 70: Mapper = new Mapper070(PrgRom, ChrRom, mirroring, submapperNumber != 1); break;
                case 71: Mapper = new Mapper071(PrgRom, ChrRom); break;
                case 72: Mapper = new Mapper072(PrgRom, ChrRom); break;
                case 73: Mapper = new Mapper073(PrgRom, ChrRom); break;
                case 75: Mapper = new Mapper075(PrgRom, ChrRom, mirroring); break;
                case 76: Mapper = new Mapper076(PrgRom, ChrRom); break;
                case 78: Mapper = new Mapper078(PrgRom, ChrRom,
                    submapperNumber == 3 ||
                    (submapperNumber == 0 && mirroring != MirroringMode.FourScreen)); break;
                case 80: Mapper = new Mapper080(PrgRom, ChrRom, mirroring); break;
                case 85: Mapper = new Mapper085(PrgRom, ChrRom, hasChrRam, mirroring); break;
                case 86: Mapper = new Mapper086(PrgRom, ChrRom); break;
                case 87: Mapper = new Mapper087(PrgRom, ChrRom); break;
                case 88: Mapper = new Mapper088(PrgRom, ChrRom); break;
                case 89: Mapper = new Mapper089(PrgRom, ChrRom); break;
                case 93: Mapper = new Mapper093(PrgRom, ChrRom); break;
                case 95: Mapper = new Mapper095(PrgRom, ChrRom); break;
                case 97: Mapper = new Mapper097(PrgRom, ChrRom, mirroring); break;
                case 140: Mapper = new Mapper140(PrgRom, ChrRom); break;
                case 154: Mapper = new Mapper154(PrgRom, ChrRom); break;
                case 184: Mapper = new Mapper184(PrgRom, ChrRom); break;
                case 206: Mapper = new Mapper206(PrgRom, ChrRom); break;
                case 210: Mapper = new Mapper210(PrgRom, ChrRom, mirroring,
                    forceMapper210Namco340 || !battery); break;
                default: throw new NotSupportedException($"Mapper {mapperNumber} is not supported.");
            }
        }
    }
}
