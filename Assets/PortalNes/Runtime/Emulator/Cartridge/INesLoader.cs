using System;
using System.Security.Cryptography;
namespace PortalNes.Emulator.Cartridge
{
    public static class INesLoader
    {
        private const int HeaderSize = 16, TrainerSize = 512, PrgBankSize = 16 * 1024, ChrBankSize = 8 * 1024;
        public static Cartridge Load(byte[] romData, NesRegion? regionOverride = null)
        {
            if (romData == null) throw new ArgumentNullException(nameof(romData));
            if (romData.Length < HeaderSize) throw new INesFormatException("ROM is shorter than the 16-byte iNES header.");
            if (romData[0] != 'N' || romData[1] != 'E' || romData[2] != 'S' || romData[3] != 0x1A)
                throw new INesFormatException("Invalid iNES magic. Expected NES followed by 0x1A.");
            byte flags6 = romData[6], flags7 = romData[7];
            bool hasDiskDudeHeader = HasDiskDudeHeader(romData);
            // Some old dumping tools wrote "DiskDude!" over bytes 7-15 of
            // the iNES header. Byte 7 then falsely adds $40 to the mapper
            // number and byte 9 falsely marks NTSC games as PAL.
            if (hasDiskDudeHeader) flags7 = 0;
            bool isNes20 = !hasDiskDudeHeader && (flags7 & 0x0C) == 0x08;
            int submapperNumber = isNes20 ? romData[8] >> 4 : 0;
            int headerMapperNumber = (flags6 >> 4) | (flags7 & 0xF0) |
                (isNes20 ? (romData[8] & 0x0F) << 8 : 0);
            int mapperNumber = headerMapperNumber;
            if (isNes20)
            {
                if ((flags7 & 0x03) != 0)
                    throw new NotSupportedException(
                        $"NES 2.0 console type {flags7 & 0x03} is not supported; only standard NES/Famicom ROMs are supported.");
                if (submapperNumber != 0 &&
                    !(mapperNumber == 3 && (submapperNumber == 1 || submapperNumber == 2)) &&
                    !(mapperNumber == 32 && submapperNumber == 1) &&
                    !(mapperNumber == 70 && (submapperNumber == 1 || submapperNumber == 2)) &&
                    !(mapperNumber == 71 && submapperNumber == 1) &&
                    !(mapperNumber == 78 && (submapperNumber == 1 || submapperNumber == 3)))
                    throw new NotSupportedException(
                        $"NES 2.0 Mapper {mapperNumber} Submapper {submapperNumber} is not supported.");
                int expansionDevice = romData[15] & 0x3F;
                if (expansionDevice > 1)
                    throw new NotSupportedException(
                        $"NES 2.0 default expansion device {expansionDevice} is not supported.");
            }
            // Older Devil Man dumps encode the NAMCOT-3453 board as mapper 88
            // plus the otherwise impossible four-screen flag. The board uses
            // that extra signal for mapper-controlled one-screen nametables and
            // is now assigned mapper 154.
            if (mapperNumber == 88 && (flags6 & 0x08) != 0)
                mapperNumber = 154;
            int prgSize = isNes20
                ? DecodeNes20RomSize(romData[4], romData[9] & 0x0F, PrgBankSize, "PRG")
                : romData[4] * PrgBankSize;
            int chrSize = isNes20
                ? DecodeNes20RomSize(romData[5], romData[9] >> 4, ChrBankSize, "CHR")
                : romData[5] * ChrBankSize;
            if (prgSize % PrgBankSize != 0)
                throw new NotSupportedException(
                    $"NES 2.0 PRG ROM size {prgSize} bytes is not aligned to PortalNES's 16KB banks.");
            if (chrSize % ChrBankSize != 0)
                throw new NotSupportedException(
                    $"NES 2.0 CHR ROM size {chrSize} bytes is not aligned to PortalNES's 8KB banks.");
            int prgBanks = prgSize / PrgBankSize, chrBanks = chrSize / ChrBankSize;
            int offset = HeaderSize + (((flags6 & 0x04) != 0) ? TrainerSize : 0);
            long required = (long)offset + prgSize + chrSize;
            if (romData.Length < required) throw new INesFormatException($"ROM is truncated. Expected at least {required} bytes, found {romData.Length}.");
            if (isNes20 && romData[14] != 0)
                throw new NotSupportedException("NES 2.0 miscellaneous ROM areas are not supported.");
            bool forceMapper210Namco340 = false;
            if (mapperNumber == 19)
            {
                string contentSha1 = ComputeContentSha1(romData, offset, prgSize + chrSize);
                forceMapper210Namco340 = IsKnownLegacyMapper210(contentSha1);
                if (forceMapper210Namco340) mapperNumber = 210;
            }
            ValidateBanks(mapperNumber, prgBanks, chrBanks);
            var prg = new byte[prgSize];
            bool hasChrRam = chrBanks == 0;
            int chrRamSize = hasChrRam && isNes20
                ? DecodeNes20RamSize(romData[11] & 0x0F) +
                  DecodeNes20RamSize(romData[11] >> 4)
                : hasChrRam ? ChrBankSize : 0;
            if (hasChrRam && chrRamSize != ChrBankSize)
                throw new NotSupportedException(
                    $"NES 2.0 CHR RAM size {chrRamSize / 1024}KB is not supported; PortalNES currently requires 8KB.");
            var chr = new byte[hasChrRam ? chrRamSize : chrSize];
            Buffer.BlockCopy(romData, offset, prg, 0, prgSize);
            if (!hasChrRam) Buffer.BlockCopy(romData, offset + prgSize, chr, 0, chrSize);
            MirroringMode mirroring = (flags6 & 0x08) != 0
                ? MirroringMode.FourScreen
                : (flags6 & 1) != 0 ? MirroringMode.Vertical : MirroringMode.Horizontal;
            NesRegion headerRegion = isNes20
                ? DecodeNes20Region(romData[12])
                : (!hasDiskDudeHeader && (romData[9] & 1) != 0 ? NesRegion.Pal : NesRegion.Ntsc);
            return new Cartridge(mapperNumber, headerMapperNumber, submapperNumber, mirroring,
                (flags6 & 2) != 0, prg, chr, hasChrRam,
                regionOverride ?? headerRegion,
                forceMapper210Namco340);
        }

        private static int DecodeNes20RomSize(byte lowByte, int highNibble,
            int unitSize, string area)
        {
            long size;
            if (highNibble != 0x0F)
            {
                size = ((long)(highNibble << 8) | lowByte) * unitSize;
            }
            else
            {
                int exponent = lowByte >> 2;
                int multiplier = ((lowByte & 0x03) << 1) + 1;
                if (exponent >= 31)
                    throw new NotSupportedException($"NES 2.0 {area} ROM size is too large.");
                size = (1L << exponent) * multiplier;
            }
            if (size < 0 || size > int.MaxValue)
                throw new NotSupportedException($"NES 2.0 {area} ROM size is too large.");
            return (int)size;
        }

        private static int DecodeNes20RamSize(int shift)
        {
            if (shift == 0) return 0;
            long size = 64L << shift;
            if (size > int.MaxValue)
                throw new NotSupportedException("NES 2.0 RAM size is too large.");
            return (int)size;
        }

        private static NesRegion DecodeNes20Region(byte timing)
        {
            switch (timing & 0x03)
            {
                case 0: return NesRegion.Ntsc;
                case 1: return NesRegion.Pal;
                case 2: return NesRegion.Ntsc; // Multi-region; NTSC is the default.
                default:
                    throw new NotSupportedException("NES 2.0 Dendy timing is not supported.");
            }
        }

        private static bool HasDiskDudeHeader(byte[] romData)
        {
            const string signature = "DiskDude!";
            for (int i = 0; i < signature.Length; i++)
                if (romData[7 + i] != signature[i]) return false;
            return true;
        }

        private static string ComputeContentSha1(byte[] romData, int offset, int length)
        {
            var content = new byte[length];
            Buffer.BlockCopy(romData, offset, content, 0, length);
            using (SHA1 sha1 = SHA1.Create())
                return BitConverter.ToString(sha1.ComputeHash(content)).Replace("-", "");
        }

        private static bool IsKnownLegacyMapper210(string contentSha1)
        {
            switch (contentSha1)
            {
                case "E535C353C90C0866B8AFC681B938D3717D9EE049": // Splatterhouse: Wanpaku Graffiti
                case "97E7E61EECB73CB1EA0C15AE51E65EA56301A685": // Wagyan Land 2
                case "3D554F55411AB2DDD1A87E7583E643970DB784F3": // Wagyan Land 3
                    return true;
                default:
                    return false;
            }
        }

        private static void ValidateBanks(int mapperNumber, int prgBanks, int chrBanks)
        {
            switch (mapperNumber)
            {
                case 0:
                    if (prgBanks != 1 && prgBanks != 2) throw new NotSupportedException($"Mapper 0 requires 16KB or 32KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks != 1) throw new NotSupportedException($"Mapper 0 requires an 8KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 1:
                    if (prgBanks < 2 || prgBanks > 16)
                        throw new NotSupportedException($"Mapper 1 supports 32KB to 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks > 16)
                        throw new NotSupportedException($"Mapper 1 supports up to 128KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 2:
                    if (prgBanks < 2) throw new NotSupportedException($"Mapper 2 requires at least 32KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks > 1) throw new NotSupportedException($"Mapper 2 supports 8KB CHR RAM or one 8KB CHR ROM bank; header declares {chrBanks * 8}KB.");
                    return;
                case 3:
                    if (prgBanks != 1 && prgBanks != 2) throw new NotSupportedException($"Mapper 3 requires 16KB or 32KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1) throw new NotSupportedException("Mapper 3 requires at least one 8KB CHR ROM bank.");
                    return;
                case 4:
                    if (prgBanks < 2)
                        throw new NotSupportedException($"Mapper 4 requires at least 32KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks > 32)
                        throw new NotSupportedException($"Mapper 4 supports up to 256KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 5:
                    if (prgBanks < 2 || prgBanks > 64)
                        throw new NotSupportedException($"Mapper 5 supports 32KB to 1024KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks > 128)
                        throw new NotSupportedException($"Mapper 5 supports up to 1024KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 7:
                    if (prgBanks < 2 || (prgBanks & 1) != 0)
                        throw new NotSupportedException($"Mapper 7 requires complete 32KB PRG ROM banks; header declares {prgBanks * 16}KB.");
                    if (chrBanks > 1)
                        throw new NotSupportedException($"Mapper 7 supports 8KB CHR RAM or one 8KB CHR ROM bank; header declares {chrBanks * 8}KB.");
                    return;
                case 9:
                    if (prgBanks < 2 || prgBanks > 8)
                        throw new NotSupportedException($"Mapper 9 supports 32KB to 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 2 || chrBanks > 16)
                        throw new NotSupportedException($"Mapper 9 requires 16KB to 128KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 10:
                    if (prgBanks < 2 || prgBanks > 16)
                        throw new NotSupportedException($"Mapper 10 supports 32KB to 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 2 || chrBanks > 16)
                        throw new NotSupportedException($"Mapper 10 requires 16KB to 128KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 18:
                    if (prgBanks < 2 || prgBanks > 32)
                        throw new NotSupportedException($"Mapper 18 supports 32KB to 512KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 32)
                        throw new NotSupportedException($"Mapper 18 requires 8KB to 256KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 19:
                    if (prgBanks < 2 || prgBanks > 32)
                        throw new NotSupportedException($"Mapper 19 supports 32KB to 512KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 32)
                        throw new NotSupportedException($"Mapper 19 requires 8KB to 256KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 21:
                    if (prgBanks < 2 || prgBanks > 16)
                        throw new NotSupportedException($"Mapper 21 supports 32KB to 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 64)
                        throw new NotSupportedException($"Mapper 21 requires 8KB to 512KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 22:
                    if (prgBanks < 2 || prgBanks > 16)
                        throw new NotSupportedException($"Mapper 22 supports 32KB to 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 32)
                        throw new NotSupportedException($"Mapper 22 requires 8KB to 256KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 23:
                    if (prgBanks < 2 || prgBanks > 16)
                        throw new NotSupportedException($"Mapper 23 supports 32KB to 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 64)
                        throw new NotSupportedException($"Mapper 23 requires 8KB to 512KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 24:
                case 26:
                    if (prgBanks < 2 || prgBanks > 16)
                        throw new NotSupportedException($"Mapper {mapperNumber} supports 32KB to 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 32)
                        throw new NotSupportedException($"Mapper {mapperNumber} requires 8KB to 256KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 25:
                    if (prgBanks < 2 || prgBanks > 16)
                        throw new NotSupportedException($"Mapper 25 supports 32KB to 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 64)
                        throw new NotSupportedException($"Mapper 25 requires 8KB to 512KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 32:
                    if (prgBanks < 2 || prgBanks > 16)
                        throw new NotSupportedException($"Mapper 32 supports 32KB to 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 32)
                        throw new NotSupportedException($"Mapper 32 requires 8KB to 256KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 33:
                    if (prgBanks < 2 || prgBanks > 32)
                        throw new NotSupportedException($"Mapper 33 supports 32KB to 512KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 64)
                        throw new NotSupportedException($"Mapper 33 requires 8KB to 512KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 48:
                    if (prgBanks < 2 || prgBanks > 32)
                        throw new NotSupportedException($"Mapper 48 supports 32KB to 512KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 64)
                        throw new NotSupportedException($"Mapper 48 requires 8KB to 512KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 65:
                    if (prgBanks < 2 || prgBanks > 16)
                        throw new NotSupportedException($"Mapper 65 supports 32KB to 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 32)
                        throw new NotSupportedException($"Mapper 65 requires 8KB to 256KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 66:
                    if (prgBanks < 2 || (prgBanks & 1) != 0)
                        throw new NotSupportedException($"Mapper 66 requires complete 32KB PRG ROM banks; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1)
                        throw new NotSupportedException("Mapper 66 requires at least one 8KB CHR ROM bank.");
                    return;
                case 67:
                    if (prgBanks < 2 || prgBanks > 16)
                        throw new NotSupportedException($"Mapper 67 supports 32KB to 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 16)
                        throw new NotSupportedException($"Mapper 67 requires 8KB to 128KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 68:
                    if (prgBanks < 2 || prgBanks > 16)
                        throw new NotSupportedException($"Mapper 68 supports 32KB to 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 32)
                        throw new NotSupportedException($"Mapper 68 requires 8KB to 256KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 69:
                    if (prgBanks < 2 || prgBanks > 32)
                        throw new NotSupportedException($"Mapper 69 supports 32KB to 512KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 32)
                        throw new NotSupportedException($"Mapper 69 requires 8KB to 256KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 70:
                    if (prgBanks < 2 || prgBanks > 8)
                        throw new NotSupportedException($"Mapper 70 supports 32KB to 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 16)
                        throw new NotSupportedException($"Mapper 70 requires 8KB to 128KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 71:
                    if (prgBanks < 2 || prgBanks > 16)
                        throw new NotSupportedException($"Mapper 71 supports 32KB to 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks != 0)
                        throw new NotSupportedException($"Mapper 71 requires 8KB CHR RAM; header declares {chrBanks * 8}KB CHR ROM.");
                    return;
                case 72:
                    if (prgBanks != 8)
                        throw new NotSupportedException($"Mapper 72 requires 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks != 16)
                        throw new NotSupportedException($"Mapper 72 requires 128KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 73:
                    if (prgBanks < 2 || prgBanks > 8)
                        throw new NotSupportedException($"Mapper 73 supports 32KB to 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks != 0)
                        throw new NotSupportedException($"Mapper 73 requires 8KB CHR RAM; header declares {chrBanks * 8}KB CHR ROM.");
                    return;
                case 75:
                    if (prgBanks < 2 || prgBanks > 8)
                        throw new NotSupportedException($"Mapper 75 supports 32KB to 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 16)
                        throw new NotSupportedException($"Mapper 75 requires 8KB to 128KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 76:
                    if (prgBanks < 2 || prgBanks > 8)
                        throw new NotSupportedException($"Mapper 76 supports 32KB to 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 16)
                        throw new NotSupportedException($"Mapper 76 requires 8KB to 128KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 78:
                    if (prgBanks != 8)
                        throw new NotSupportedException($"Mapper 78 requires 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks != 16)
                        throw new NotSupportedException($"Mapper 78 requires 128KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 80:
                    if (prgBanks < 2 || prgBanks > 16)
                        throw new NotSupportedException($"Mapper 80 supports 32KB to 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 32)
                        throw new NotSupportedException($"Mapper 80 requires 8KB to 256KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 85:
                    if (prgBanks < 2 || prgBanks > 32)
                        throw new NotSupportedException($"Mapper 85 supports 32KB to 512KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks > 32)
                        throw new NotSupportedException($"Mapper 85 supports 8KB CHR RAM or up to 256KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 86:
                    if (prgBanks != 8)
                        throw new NotSupportedException($"Mapper 86 requires 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks != 8)
                        throw new NotSupportedException($"Mapper 86 requires 64KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 87:
                    if (prgBanks != 1 && prgBanks != 2)
                        throw new NotSupportedException($"Mapper 87 requires 16KB or 32KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 2 || chrBanks > 4)
                        throw new NotSupportedException($"Mapper 87 requires 16KB to 32KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 88:
                    if (prgBanks < 2 || prgBanks > 8)
                        throw new NotSupportedException($"Mapper 88 supports 32KB to 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 16)
                        throw new NotSupportedException($"Mapper 88 requires 8KB to 128KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 89:
                    if (prgBanks < 2 || prgBanks > 8)
                        throw new NotSupportedException($"Mapper 89 supports 32KB to 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 16)
                        throw new NotSupportedException($"Mapper 89 requires 8KB to 128KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 93:
                    if (prgBanks != 8)
                        throw new NotSupportedException($"Mapper 93 requires 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks != 0)
                        throw new NotSupportedException($"Mapper 93 requires 8KB CHR RAM; header declares {chrBanks * 8}KB CHR ROM.");
                    return;
                case 95:
                    if (prgBanks < 2 || prgBanks > 8)
                        throw new NotSupportedException($"Mapper 95 supports 32KB to 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 8)
                        throw new NotSupportedException($"Mapper 95 requires 8KB to 64KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 97:
                    if (prgBanks != 16)
                        throw new NotSupportedException($"Mapper 97 requires 256KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks != 0)
                        throw new NotSupportedException($"Mapper 97 requires 8KB CHR RAM; header declares {chrBanks * 8}KB CHR ROM.");
                    return;
                case 140:
                    if (prgBanks < 2 || prgBanks > 8 || (prgBanks & 1) != 0)
                        throw new NotSupportedException($"Mapper 140 requires 32KB to 128KB PRG ROM in complete 32KB banks; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 16)
                        throw new NotSupportedException($"Mapper 140 requires 8KB to 128KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 154:
                    if (prgBanks < 2 || prgBanks > 8)
                        throw new NotSupportedException($"Mapper 154 supports 32KB to 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 16)
                        throw new NotSupportedException($"Mapper 154 requires 8KB to 128KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 184:
                    if (prgBanks != 1 && prgBanks != 2)
                        throw new NotSupportedException($"Mapper 184 requires 16KB or 32KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 4)
                        throw new NotSupportedException($"Mapper 184 requires 8KB to 32KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 206:
                    if (prgBanks < 2 || prgBanks > 8)
                        throw new NotSupportedException($"Mapper 206 supports 32KB to 128KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 8)
                        throw new NotSupportedException($"Mapper 206 requires 8KB to 64KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                case 210:
                    if (prgBanks < 2 || prgBanks > 32)
                        throw new NotSupportedException($"Mapper 210 supports 32KB to 512KB PRG ROM; header declares {prgBanks * 16}KB.");
                    if (chrBanks < 1 || chrBanks > 32)
                        throw new NotSupportedException($"Mapper 210 requires 8KB to 256KB CHR ROM; header declares {chrBanks * 8}KB.");
                    return;
                default:
                    throw new NotSupportedException($"Mapper {mapperNumber} is not supported; available mappers are 0-5, 7, 9, 10, 18, 19, 21-26, 32, 33, 48, 65-73, 75, 76, 78, 80, 85-89, 93, 95, 97, 140, 154, 184, 206 and 210.");
            }
        }
    }
}
