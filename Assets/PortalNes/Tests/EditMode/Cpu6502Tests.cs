using NUnit.Framework;
using PortalNes.Emulator.Cpu;

namespace PortalNes.Tests
{
    public sealed class Cpu6502Tests
    {
        private byte[] memory;
        private Cpu6502 cpu;

        [SetUp]
        public void SetUp()
        {
            memory = new byte[65536];
            memory[0xFFFC] = 0x00; memory[0xFFFD] = 0x80;
            cpu = new Cpu6502(a => memory[a], (a, v) => memory[a] = v);
            cpu.Reset();
        }

        [Test]
        public void Reset_LoadsVectorAndInitialState()
        {
            Assert.That(cpu.Registers.ProgramCounter, Is.EqualTo(0x8000));
            Assert.That(cpu.Registers.StackPointer, Is.EqualTo(0xFD));
            Assert.That(cpu.Registers.Status & (byte)CpuStatus.InterruptDisable, Is.Not.Zero);
            Assert.That(cpu.TotalCycles, Is.EqualTo(7));
        }

        [Test]
        public void LdaTaxInx_UpdatesRegistersFlagsAndCycles()
        {
            Load(0xA9, 0x7F, 0xAA, 0xE8);
            Assert.That(cpu.Step(), Is.EqualTo(2));
            Assert.That(cpu.Step(), Is.EqualTo(2));
            Assert.That(cpu.Step(), Is.EqualTo(2));
            Assert.That(cpu.Registers.A, Is.EqualTo(0x7F));
            Assert.That(cpu.Registers.X, Is.EqualTo(0x80));
            Assert.That(cpu.Registers.Status & (byte)CpuStatus.Negative, Is.Not.Zero);
        }

        [Test]
        public void Adc_SetsCarryAndOverflowCorrectly()
        {
            Load(0xA9, 0x50, 0x69, 0x50);
            cpu.Step(); cpu.Step();
            Assert.That(cpu.Registers.A, Is.EqualTo(0xA0));
            Assert.That(cpu.Registers.Status & (byte)CpuStatus.Overflow, Is.Not.Zero);
            Assert.That(cpu.Registers.Status & (byte)CpuStatus.Carry, Is.Zero);
        }

        [Test]
        public void BranchTakenAcrossPage_AddsTwoCycles()
        {
            memory[0xFFFC] = 0xFD; memory[0xFFFD] = 0x80; cpu.Reset();
            memory[0x80FD] = 0xD0; memory[0x80FE] = 0x02;
            Assert.That(cpu.Step(), Is.EqualTo(4));
            Assert.That(cpu.Registers.ProgramCounter, Is.EqualTo(0x8101));
        }

        [Test]
        public void JmpIndirect_ReproducesPageBoundaryBug()
        {
            Load(0x6C, 0xFF, 0x30);
            memory[0x30FF] = 0xCD; memory[0x3000] = 0xAB; memory[0x3100] = 0xEE;
            cpu.Step();
            Assert.That(cpu.Registers.ProgramCounter, Is.EqualTo(0xABCD));
        }

        [Test]
        public void JsrAndRts_RestoreFollowingInstruction()
        {
            Load(0x20, 0x00, 0x90, 0xEA);
            memory[0x9000] = 0x60;
            Assert.That(cpu.Step(), Is.EqualTo(6));
            Assert.That(cpu.Step(), Is.EqualTo(6));
            Assert.That(cpu.Registers.ProgramCounter, Is.EqualTo(0x8003));
        }

        [Test]
        public void Nmi_PushesStateAndLoadsVector()
        {
            memory[0xFFFA] = 0x00; memory[0xFFFB] = 0x90;
            cpu.RequestNmi();
            Assert.That(cpu.Step(), Is.EqualTo(7));
            Assert.That(cpu.Registers.ProgramCounter, Is.EqualTo(0x9000));
            Assert.That(cpu.Registers.StackPointer, Is.EqualTo(0xFA));
            Assert.That(memory[0x01FB] & (byte)CpuStatus.Break, Is.Zero);
        }

        [Test]
        public void LateNmiEdge_ExecutesOneInstructionBeforeService()
        {
            Load(0xE8, 0xEA); // INX; NOP
            memory[0xFFFA] = 0x00; memory[0xFFFB] = 0x90;

            cpu.RequestNmi(delayOneInstruction: true);

            Assert.That(cpu.Step(), Is.EqualTo(2));
            Assert.That(cpu.Registers.X, Is.EqualTo(1));
            Assert.That(cpu.Registers.ProgramCounter, Is.EqualTo(0x8001));
            Assert.That(cpu.NmiServiceCount, Is.Zero);

            Assert.That(cpu.Step(), Is.EqualTo(7));
            Assert.That(cpu.Registers.ProgramCounter, Is.EqualTo(0x9000));
            Assert.That(cpu.NmiServiceCount, Is.EqualTo(1));
        }

        [Test]
        public void Rti_RestoresProgramCounterStatusAndStackPointer()
        {
            memory[0xFFFA] = 0x00; memory[0xFFFB] = 0x90;
            memory[0x9000] = 0x40;
            byte initialStackPointer = cpu.Registers.StackPointer;
            ushort returnAddress = cpu.Registers.ProgramCounter;
            cpu.RequestNmi();
            cpu.Step();
            cpu.Step();
            Assert.That(cpu.Registers.ProgramCounter, Is.EqualTo(returnAddress));
            Assert.That(cpu.Registers.StackPointer, Is.EqualTo(initialStackPointer));
        }

        [Test]
        public void Plp_ConsumesExactlyOneStackByte()
        {
            Load(0x08, 0x28);
            byte initialStackPointer = cpu.Registers.StackPointer;
            cpu.Step();
            cpu.Step();
            Assert.That(cpu.Registers.StackPointer, Is.EqualTo(initialStackPointer));
        }

        [Test]
        public void UnsupportedOpcode_ThrowsWithOpcodeAndAddress()
        {
            Load(0x02);
            Assert.That(() => cpu.Step(), Throws.InvalidOperationException.With.Message.Contains("$02").And.Message.Contains("8000"));
        }

        private void Load(params byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++) memory[0x8000 + i] = bytes[i];
        }
    }
}
