using NUnit.Framework;
using PortalNes.Emulator;
using System.IO;

namespace PortalNes.Tests
{
    public sealed class NesSaveStateTests
    {
        [Test]
        public void SaveAndLoad_RestoresCpuBusPpuAndMapperState()
        {
            var machine = new NesMachine();
            machine.LoadRom(CreateNrom());
            machine.Reset();
            machine.RunFrame();
            long cycles = machine.Cpu.TotalCycles;
            long frame = machine.Ppu.FrameNumber;
            machine.Bus.WriteRam(0x0010, 0x42);
            machine.Ppu.CpuWriteRegister(0x2000, 0x90);
            machine.Ppu.CpuWriteRegister(0x2001, 0x18);
            byte[] state = machine.SaveState();
            Assert.That(state.Length, Is.LessThan(100000),
                "Presentation frame buffers must not be stored in save states.");

            machine.Bus.WriteRam(0x0010, 0x99);
            machine.Ppu.CpuWriteRegister(0x2000, 0x00);
            machine.Ppu.CpuWriteRegister(0x2001, 0x00);
            machine.RunFrame();
            Assert.That(machine.Cpu.TotalCycles, Is.GreaterThan(cycles));

            machine.LoadState(state);

            Assert.That(machine.Bus.ReadRam(0x0010), Is.EqualTo(0x42));
            Assert.That(machine.Cpu.TotalCycles, Is.EqualTo(cycles));
            Assert.That(machine.Ppu.FrameNumber, Is.EqualTo(frame));
            Assert.That(machine.Ppu.Registers.Control, Is.EqualTo(0x90));
            Assert.That(machine.Ppu.Registers.Mask, Is.EqualTo(0x18));
        }

        [Test]
        public void LoadState_RejectsVersion3WithActionableMessageAndPreservesMachine()
        {
            var machine = new NesMachine();
            machine.LoadRom(CreateNrom());
            machine.Reset();
            machine.RunFrame();
            machine.Bus.WriteRam(0x0010, 0x5A);
            long cycles = machine.Cpu.TotalCycles;
            byte[] oldState = machine.SaveState();
            oldState[4] = 3;

            var exception = Assert.Throws<InvalidDataException>(() => machine.LoadState(oldState));

            Assert.That(exception.Message, Does.Contain("Version 3"));
            Assert.That(machine.Bus.ReadRam(0x0010), Is.EqualTo(0x5A));
            Assert.That(machine.Cpu.TotalCycles, Is.EqualTo(cycles));
        }

        private static byte[] CreateNrom()
        {
            var rom = new byte[16 + 16384 + 8192];
            rom[0] = (byte)'N'; rom[1] = (byte)'E'; rom[2] = (byte)'S'; rom[3] = 0x1A;
            rom[4] = 1; rom[5] = 1;
            int prg = 16;
            rom[prg + 0] = 0xA9; rom[prg + 1] = 0x42;       // LDA #$42
            rom[prg + 2] = 0x85; rom[prg + 3] = 0x10;       // STA $10
            rom[prg + 4] = 0x4C; rom[prg + 5] = 0x00; rom[prg + 6] = 0x80; // JMP $8000
            rom[prg + 0x3FFA] = 0x00; rom[prg + 0x3FFB] = 0x80;
            rom[prg + 0x3FFC] = 0x00; rom[prg + 0x3FFD] = 0x80;
            rom[prg + 0x3FFE] = 0x00; rom[prg + 0x3FFF] = 0x80;
            return rom;
        }
    }
}
