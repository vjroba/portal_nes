using System;

namespace PortalNes.Emulator.Cpu
{
    [Flags]
    public enum CpuStatus : byte
    {
        Carry = 1 << 0,
        Zero = 1 << 1,
        InterruptDisable = 1 << 2,
        Decimal = 1 << 3,
        Break = 1 << 4,
        Unused = 1 << 5,
        Overflow = 1 << 6,
        Negative = 1 << 7
    }

    public struct CpuRegisters
    {
        public byte A, X, Y, StackPointer, Status;
        public ushort ProgramCounter;
    }
}
