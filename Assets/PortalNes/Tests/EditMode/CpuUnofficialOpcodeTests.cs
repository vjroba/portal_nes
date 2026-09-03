using NUnit.Framework;
using PortalNes.Emulator.Cpu;

namespace PortalNes.Tests
{
    public sealed class CpuUnofficialOpcodeTests
    {
        [Test]
        public void SloZeroPage_ShiftsMemoryAndOrsAccumulator()
        {
            var memory = new byte[65536];
            memory[0xFFFC] = 0; memory[0xFFFD] = 0x80;
            memory[0x8000] = 0xA9; memory[0x8001] = 0x01; // LDA #$01
            memory[0x8002] = 0x07; memory[0x8003] = 0x20; // SLO $20
            memory[0x20] = 0x81;
            var cpu = new Cpu6502(a => memory[a], (a, v) => memory[a] = v);
            cpu.Reset();

            Assert.That(cpu.Step(), Is.EqualTo(2));
            Assert.That(cpu.Step(), Is.EqualTo(5));
            Assert.That(memory[0x20], Is.EqualTo(0x02));
            Assert.That(cpu.Registers.A, Is.EqualTo(0x03));
            Assert.That(cpu.Registers.Status & (byte)CpuStatus.Carry, Is.Not.Zero);
            Assert.That(cpu.Registers.Status & (byte)CpuStatus.Zero, Is.Zero);
            Assert.That(cpu.Registers.Status & (byte)CpuStatus.Negative, Is.Zero);
        }

        [TestCase(0xA7, 0x20, 0x00, 3)] // zero page
        [TestCase(0xAF, 0x34, 0x12, 4)] // absolute
        public void Lax_LoadsAccumulatorAndXAndSetsFlags(byte opcode, byte low, byte high, int cycles)
        {
            var memory = new byte[65536];
            memory[0xFFFC] = 0; memory[0xFFFD] = 0x80;
            memory[0x8000] = opcode; memory[0x8001] = low; memory[0x8002] = high;
            ushort address = opcode == 0xA7 ? low : (ushort)(low | (high << 8));
            memory[address] = 0x80;
            var cpu = new Cpu6502(a => memory[a], (a, v) => memory[a] = v);
            cpu.Reset();

            Assert.That(cpu.Step(), Is.EqualTo(cycles));
            Assert.That(cpu.Registers.A, Is.EqualTo(0x80));
            Assert.That(cpu.Registers.X, Is.EqualTo(0x80));
            Assert.That(cpu.Registers.Status & (byte)CpuStatus.Negative, Is.Not.Zero);
            Assert.That(cpu.Registers.Status & (byte)CpuStatus.Zero, Is.Zero);
        }

        [Test]
        public void IscAbsolute_IncrementsMemoryThenSubtractsWithCarry()
        {
            var memory = new byte[65536];
            memory[0xFFFC] = 0; memory[0xFFFD] = 0x80;
            memory[0x8000] = 0xA9; memory[0x8001] = 0x10; // LDA #$10
            memory[0x8002] = 0x38;                         // SEC
            memory[0x8003] = 0xEF; memory[0x8004] = 0x34; memory[0x8005] = 0x12;
            memory[0x1234] = 0x02;
            var cpu = new Cpu6502(a => memory[a], (a, v) => memory[a] = v);
            cpu.Reset();

            Assert.That(cpu.Step(), Is.EqualTo(2));
            Assert.That(cpu.Step(), Is.EqualTo(2));
            Assert.That(cpu.Step(), Is.EqualTo(6));
            Assert.That(memory[0x1234], Is.EqualTo(0x03));
            Assert.That(cpu.Registers.A, Is.EqualTo(0x0D));
            Assert.That(cpu.Registers.Status & (byte)CpuStatus.Carry, Is.Not.Zero);
        }

        [TestCase(0x02)]
        [TestCase(0x8B)]
        public void OutOfScopeOpcode_StopsAtFirstOccurrenceWithoutSilentExecution(byte opcode)
        {
            var memory = new byte[65536];
            memory[0xFFFC] = 0; memory[0xFFFD] = 0x80; memory[0x8000] = opcode;
            var cpu = new Cpu6502(a => memory[a], (a, v) => memory[a] = v);
            cpu.Reset();
            Assert.That(() => cpu.Step(), Throws.InvalidOperationException.With.Message.Contains($"${opcode:X2}").And.Message.Contains("8000"));
        }

        [Test]
        public void ZeroPageNop_ReadsOperandWithoutChangingRegistersOrFlags()
        {
            var memory = new byte[65536];
            memory[0xFFFC] = 0; memory[0xFFFD] = 0x80;
            memory[0x8000] = 0x04; memory[0x8001] = 0x20;
            memory[0x20] = 0xFF;
            var reads = 0;
            var cpu = new Cpu6502(
                a =>
                {
                    if (a == 0x20) reads++;
                    return memory[a];
                },
                (a, v) => memory[a] = v);
            cpu.Reset();
            var before = cpu.Registers;

            Assert.That(cpu.Step(), Is.EqualTo(3));
            Assert.That(cpu.Registers.ProgramCounter, Is.EqualTo(0x8002));
            Assert.That(cpu.Registers.A, Is.EqualTo(before.A));
            Assert.That(cpu.Registers.X, Is.EqualTo(before.X));
            Assert.That(cpu.Registers.Y, Is.EqualTo(before.Y));
            Assert.That(cpu.Registers.Status, Is.EqualTo(before.Status));
            Assert.That(reads, Is.EqualTo(1));
        }

        [TestCase(0x1A, 1, 2)]
        [TestCase(0x80, 2, 2)]
        [TestCase(0x14, 2, 4)]
        [TestCase(0x0C, 3, 4)]
        public void StableNop_ConsumesExpectedBytesAndCycles(byte opcode, int length, int cycles)
        {
            var memory = new byte[65536];
            memory[0xFFFC] = 0; memory[0xFFFD] = 0x80;
            memory[0x8000] = opcode;
            var cpu = new Cpu6502(a => memory[a], (a, v) => memory[a] = v);
            cpu.Reset();

            Assert.That(cpu.Step(), Is.EqualTo(cycles));
            Assert.That(cpu.Registers.ProgramCounter, Is.EqualTo(0x8000 + length));
        }

        [Test]
        public void AbsoluteXNop_AddsCycleWhenAddressCrossesPage()
        {
            var memory = new byte[65536];
            memory[0xFFFC] = 0; memory[0xFFFD] = 0x80;
            memory[0x8000] = 0xA2; memory[0x8001] = 0x01; // LDX #$01
            memory[0x8002] = 0x1C; memory[0x8003] = 0xFF; memory[0x8004] = 0x20;
            var cpu = new Cpu6502(a => memory[a], (a, v) => memory[a] = v);
            cpu.Reset();

            Assert.That(cpu.Step(), Is.EqualTo(2));
            Assert.That(cpu.Step(), Is.EqualTo(5));
            Assert.That(cpu.Registers.ProgramCounter, Is.EqualTo(0x8005));
        }
    }
}
