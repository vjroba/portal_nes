using System;
using System.IO;

namespace PortalNes.Emulator.Cpu
{
    public sealed class Cpu6502
    {
        private readonly Func<ushort, byte> read;
        private readonly Action<ushort, byte> write;
        private bool nmiPending;
        private bool delayNmiOneInstruction;
        private bool irqPending;

        public CpuRegisters Registers { get; private set; }
        public long TotalCycles { get; private set; }
        public long NmiServiceCount { get; private set; }
        public long IrqServiceCount { get; private set; }

        public Cpu6502(Func<ushort, byte> read, Action<ushort, byte> write)
        {
            this.read = read ?? throw new ArgumentNullException(nameof(read));
            this.write = write ?? throw new ArgumentNullException(nameof(write));
        }

        public void Reset()
        {
            Registers = new CpuRegisters
            {
                StackPointer = 0xFD,
                Status = (byte)(CpuStatus.Unused | CpuStatus.InterruptDisable),
                ProgramCounter = ReadWord(0xFFFC)
            };
            nmiPending = delayNmiOneInstruction = irqPending = false;
            NmiServiceCount = IrqServiceCount = 0;
            TotalCycles = 7;
        }

        public void RequestNmi(bool delayOneInstruction = false)
        {
            nmiPending = true;
            // An NMI edge caused by a PPUCTRL write occurs on the final CPU
            // cycle of that write. It is too late for the interrupt poll of
            // the current instruction, so one more instruction executes
            // before the CPU services it.
            delayNmiOneInstruction |= delayOneInstruction;
        }
        public void SetIrqLine(bool asserted) => irqPending = asserted;
        public void AddStallCycles(int cycles)
        {
            if (cycles < 0) throw new ArgumentOutOfRangeException(nameof(cycles));
            TotalCycles += cycles;
        }

        internal byte[] CaptureState()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Registers.A);
                writer.Write(Registers.X);
                writer.Write(Registers.Y);
                writer.Write(Registers.StackPointer);
                writer.Write(Registers.Status);
                writer.Write(Registers.ProgramCounter);
                writer.Write(TotalCycles);
                writer.Write(NmiServiceCount);
                writer.Write(IrqServiceCount);
                writer.Write(nmiPending);
                writer.Write(irqPending);
                return stream.ToArray();
            }
        }

        internal void RestoreState(byte[] data)
        {
            using (var stream = new MemoryStream(data ?? throw new ArgumentNullException(nameof(data)), false))
            using (var reader = new BinaryReader(stream))
            {
                Registers = new CpuRegisters
                {
                    A = reader.ReadByte(),
                    X = reader.ReadByte(),
                    Y = reader.ReadByte(),
                    StackPointer = reader.ReadByte(),
                    Status = reader.ReadByte(),
                    ProgramCounter = reader.ReadUInt16()
                };
                TotalCycles = reader.ReadInt64();
                NmiServiceCount = reader.ReadInt64();
                IrqServiceCount = reader.ReadInt64();
                nmiPending = reader.ReadBoolean();
                delayNmiOneInstruction = false;
                irqPending = reader.ReadBoolean();
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("CPU save-state data has trailing bytes.");
            }
        }

        public int Step()
        {
            if (nmiPending && !delayNmiOneInstruction)
            {
                nmiPending = false;
                NmiServiceCount++;
                return Interrupt(0xFFFA, false);
            }
            delayNmiOneInstruction = false;
            if (irqPending && !Flag(CpuStatus.InterruptDisable))
            {
                IrqServiceCount++;
                return Interrupt(0xFFFE, false);
            }

            byte opcode = Fetch();
            int cycles;
            switch (opcode)
            {
                // ADC
                case 0x69: Adc(Fetch()); cycles=2; break; case 0x65: Adc(read(Zp())); cycles=3; break; case 0x75: Adc(read(ZpX())); cycles=4; break;
                case 0x6D: Adc(read(Abs())); cycles=4; break; case 0x7D: { var a=AbsX(out bool p); Adc(read(a)); cycles=4+(p?1:0); break; } case 0x79: { var a=AbsY(out bool p); Adc(read(a)); cycles=4+(p?1:0); break; }
                case 0x61: Adc(read(IndX())); cycles=6; break; case 0x71: { var a=IndY(out bool p); Adc(read(a)); cycles=5+(p?1:0); break; }
                // AND
                case 0x29: Registers=WithA((byte)(Registers.A & Fetch())); cycles=2; break; case 0x25: Registers=WithA((byte)(Registers.A & read(Zp()))); cycles=3; break; case 0x35: Registers=WithA((byte)(Registers.A & read(ZpX()))); cycles=4; break;
                case 0x2D: Registers=WithA((byte)(Registers.A & read(Abs()))); cycles=4; break; case 0x3D: {var a=AbsX(out bool p);Registers=WithA((byte)(Registers.A&read(a)));cycles=4+(p?1:0);break;} case 0x39:{var a=AbsY(out bool p);Registers=WithA((byte)(Registers.A&read(a)));cycles=4+(p?1:0);break;}
                case 0x21: Registers=WithA((byte)(Registers.A & read(IndX()))); cycles=6; break; case 0x31:{var a=IndY(out bool p);Registers=WithA((byte)(Registers.A&read(a)));cycles=5+(p?1:0);break;}
                // ASL
                case 0x0A: Registers=WithA(Asl(Registers.A)); cycles=2; break; case 0x06: Rmw(Zp(),Asl);cycles=5;break; case 0x16:Rmw(ZpX(),Asl);cycles=6;break;case 0x0E:Rmw(Abs(),Asl);cycles=6;break;case 0x1E:Rmw(AbsX(out _),Asl);cycles=7;break;
                // SLO (stable unofficial opcode: ASL memory, then ORA)
                case 0x07:Rmw(Zp(),Slo);cycles=5;break;case 0x17:Rmw(ZpX(),Slo);cycles=6;break;
                case 0x0F:Rmw(Abs(),Slo);cycles=6;break;case 0x1F:Rmw(AbsX(out _),Slo);cycles=7;break;
                case 0x1B:Rmw(AbsY(out _),Slo);cycles=7;break;case 0x03:Rmw(IndX(),Slo);cycles=8;break;
                case 0x13:Rmw(IndY(out _),Slo);cycles=8;break;
                // branches
                case 0x90: cycles=Branch(!Flag(CpuStatus.Carry));break; case 0xB0:cycles=Branch(Flag(CpuStatus.Carry));break; case 0xF0:cycles=Branch(Flag(CpuStatus.Zero));break; case 0x30:cycles=Branch(Flag(CpuStatus.Negative));break;
                case 0xD0:cycles=Branch(!Flag(CpuStatus.Zero));break;case 0x10:cycles=Branch(!Flag(CpuStatus.Negative));break;case 0x50:cycles=Branch(!Flag(CpuStatus.Overflow));break;case 0x70:cycles=Branch(Flag(CpuStatus.Overflow));break;
                // BIT / BRK
                case 0x24: Bit(read(Zp()));cycles=3;break;case 0x2C:Bit(read(Abs()));cycles=4;break;case 0x00: Fetch(); cycles=Interrupt(0xFFFE,true,false);break;
                // flags
                case 0x18:Set(CpuStatus.Carry,false);cycles=2;break;case 0xD8:Set(CpuStatus.Decimal,false);cycles=2;break;case 0x58:Set(CpuStatus.InterruptDisable,false);cycles=2;break;case 0xB8:Set(CpuStatus.Overflow,false);cycles=2;break;
                case 0x38:Set(CpuStatus.Carry,true);cycles=2;break;case 0xF8:Set(CpuStatus.Decimal,true);cycles=2;break;case 0x78:Set(CpuStatus.InterruptDisable,true);cycles=2;break;
                // CMP/CPX/CPY
                case 0xC9:Compare(Registers.A,Fetch());cycles=2;break;case 0xC5:Compare(Registers.A,read(Zp()));cycles=3;break;case 0xD5:Compare(Registers.A,read(ZpX()));cycles=4;break;case 0xCD:Compare(Registers.A,read(Abs()));cycles=4;break;
                case 0xDD:{var a=AbsX(out bool p);Compare(Registers.A,read(a));cycles=4+(p?1:0);break;}case 0xD9:{var a=AbsY(out bool p);Compare(Registers.A,read(a));cycles=4+(p?1:0);break;}case 0xC1:Compare(Registers.A,read(IndX()));cycles=6;break;case 0xD1:{var a=IndY(out bool p);Compare(Registers.A,read(a));cycles=5+(p?1:0);break;}
                case 0xE0:Compare(Registers.X,Fetch());cycles=2;break;case 0xE4:Compare(Registers.X,read(Zp()));cycles=3;break;case 0xEC:Compare(Registers.X,read(Abs()));cycles=4;break;
                case 0xC0:Compare(Registers.Y,Fetch());cycles=2;break;case 0xC4:Compare(Registers.Y,read(Zp()));cycles=3;break;case 0xCC:Compare(Registers.Y,read(Abs()));cycles=4;break;
                // DEC/DEX/DEY
                case 0xC6:Rmw(Zp(),Dec);cycles=5;break;case 0xD6:Rmw(ZpX(),Dec);cycles=6;break;case 0xCE:Rmw(Abs(),Dec);cycles=6;break;case 0xDE:Rmw(AbsX(out _),Dec);cycles=7;break;
                case 0xCA:Registers=WithX((byte)(Registers.X-1));cycles=2;break;case 0x88:Registers=WithY((byte)(Registers.Y-1));cycles=2;break;
                // EOR
                case 0x49:Registers=WithA((byte)(Registers.A^Fetch()));cycles=2;break;case 0x45:Registers=WithA((byte)(Registers.A^read(Zp())));cycles=3;break;case 0x55:Registers=WithA((byte)(Registers.A^read(ZpX())));cycles=4;break;case 0x4D:Registers=WithA((byte)(Registers.A^read(Abs())));cycles=4;break;
                case 0x5D:{var a=AbsX(out bool p);Registers=WithA((byte)(Registers.A^read(a)));cycles=4+(p?1:0);break;}case 0x59:{var a=AbsY(out bool p);Registers=WithA((byte)(Registers.A^read(a)));cycles=4+(p?1:0);break;}case 0x41:Registers=WithA((byte)(Registers.A^read(IndX())));cycles=6;break;case 0x51:{var a=IndY(out bool p);Registers=WithA((byte)(Registers.A^read(a)));cycles=5+(p?1:0);break;}
                // INC/INX/INY
                case 0xE6:Rmw(Zp(),Inc);cycles=5;break;case 0xF6:Rmw(ZpX(),Inc);cycles=6;break;case 0xEE:Rmw(Abs(),Inc);cycles=6;break;case 0xFE:Rmw(AbsX(out _),Inc);cycles=7;break;
                case 0xE8:Registers=WithX((byte)(Registers.X+1));cycles=2;break;case 0xC8:Registers=WithY((byte)(Registers.Y+1));cycles=2;break;
                // ISC/ISB (stable unofficial opcode: INC memory, then SBC)
                case 0xE7:Rmw(Zp(),Isc);cycles=5;break;case 0xF7:Rmw(ZpX(),Isc);cycles=6;break;
                case 0xEF:Rmw(Abs(),Isc);cycles=6;break;case 0xFF:Rmw(AbsX(out _),Isc);cycles=7;break;
                case 0xFB:Rmw(AbsY(out _),Isc);cycles=7;break;case 0xE3:Rmw(IndX(),Isc);cycles=8;break;
                case 0xF3:Rmw(IndY(out _),Isc);cycles=8;break;
                // JMP/JSR
                case 0x4C:SetPc(Abs());cycles=3;break;case 0x6C:SetPc(ReadWordBug(Abs()));cycles=5;break;case 0x20:{ushort target=Abs();PushWord((ushort)(Registers.ProgramCounter-1));SetPc(target);cycles=6;break;}
                // LDA
                case 0xA9:Registers=WithA(Fetch());cycles=2;break;case 0xA5:Registers=WithA(read(Zp()));cycles=3;break;case 0xB5:Registers=WithA(read(ZpX()));cycles=4;break;case 0xAD:Registers=WithA(read(Abs()));cycles=4;break;
                case 0xBD:{var a=AbsX(out bool p);Registers=WithA(read(a));cycles=4+(p?1:0);break;}case 0xB9:{var a=AbsY(out bool p);Registers=WithA(read(a));cycles=4+(p?1:0);break;}case 0xA1:Registers=WithA(read(IndX()));cycles=6;break;case 0xB1:{var a=IndY(out bool p);Registers=WithA(read(a));cycles=5+(p?1:0);break;}
                // LDX
                case 0xA2:Registers=WithX(Fetch());cycles=2;break;case 0xA6:Registers=WithX(read(Zp()));cycles=3;break;case 0xB6:Registers=WithX(read(ZpY()));cycles=4;break;case 0xAE:Registers=WithX(read(Abs()));cycles=4;break;case 0xBE:{var a=AbsY(out bool p);Registers=WithX(read(a));cycles=4+(p?1:0);break;}
                // LAX (stable unofficial opcode: load A and X together)
                case 0xA7:Lax(read(Zp()));cycles=3;break;case 0xB7:Lax(read(ZpY()));cycles=4;break;
                case 0xAF:Lax(read(Abs()));cycles=4;break;case 0xBF:{var a=AbsY(out bool p);Lax(read(a));cycles=4+(p?1:0);break;}
                case 0xA3:Lax(read(IndX()));cycles=6;break;case 0xB3:{var a=IndY(out bool p);Lax(read(a));cycles=5+(p?1:0);break;}
                // LDY
                case 0xA0:Registers=WithY(Fetch());cycles=2;break;case 0xA4:Registers=WithY(read(Zp()));cycles=3;break;case 0xB4:Registers=WithY(read(ZpX()));cycles=4;break;case 0xAC:Registers=WithY(read(Abs()));cycles=4;break;case 0xBC:{var a=AbsX(out bool p);Registers=WithY(read(a));cycles=4+(p?1:0);break;}
                // LSR
                case 0x4A:Registers=WithA(Lsr(Registers.A));cycles=2;break;case 0x46:Rmw(Zp(),Lsr);cycles=5;break;case 0x56:Rmw(ZpX(),Lsr);cycles=6;break;case 0x4E:Rmw(Abs(),Lsr);cycles=6;break;case 0x5E:Rmw(AbsX(out _),Lsr);cycles=7;break;
                // NOP / ORA
                // Stable unofficial NOPs are used by some commercial cartridges.
                // Preserve their operand reads and page-cross timing even though
                // they do not modify registers or flags.
                case 0xEA:case 0x1A:case 0x3A:case 0x5A:case 0x7A:case 0xDA:case 0xFA:cycles=2;break;
                case 0x80:case 0x82:case 0x89:case 0xC2:case 0xE2:Fetch();cycles=2;break;
                case 0x04:case 0x44:case 0x64:read(Zp());cycles=3;break;
                case 0x14:case 0x34:case 0x54:case 0x74:case 0xD4:case 0xF4:read(ZpX());cycles=4;break;
                case 0x0C:read(Abs());cycles=4;break;
                case 0x1C:case 0x3C:case 0x5C:case 0x7C:case 0xDC:case 0xFC:{var a=AbsX(out bool p);read(a);cycles=4+(p?1:0);break;}
                case 0x09:Registers=WithA((byte)(Registers.A|Fetch()));cycles=2;break;case 0x05:Registers=WithA((byte)(Registers.A|read(Zp())));cycles=3;break;case 0x15:Registers=WithA((byte)(Registers.A|read(ZpX())));cycles=4;break;case 0x0D:Registers=WithA((byte)(Registers.A|read(Abs())));cycles=4;break;
                case 0x1D:{var a=AbsX(out bool p);Registers=WithA((byte)(Registers.A|read(a)));cycles=4+(p?1:0);break;}case 0x19:{var a=AbsY(out bool p);Registers=WithA((byte)(Registers.A|read(a)));cycles=4+(p?1:0);break;}case 0x01:Registers=WithA((byte)(Registers.A|read(IndX())));cycles=6;break;case 0x11:{var a=IndY(out bool p);Registers=WithA((byte)(Registers.A|read(a)));cycles=5+(p?1:0);break;}
                // stack
                case 0x48:Push(Registers.A);cycles=3;break;case 0x08:Push((byte)(Registers.Status|(byte)(CpuStatus.Break|CpuStatus.Unused)));cycles=3;break;case 0x68:Registers=WithA(Pop());cycles=4;break;case 0x28:{byte status=Pop();var r=Registers;r.Status=(byte)((status&~(byte)CpuStatus.Break)|(byte)CpuStatus.Unused);Registers=r;cycles=4;break;}
                // ROL/ROR
                case 0x2A:Registers=WithA(Rol(Registers.A));cycles=2;break;case 0x26:Rmw(Zp(),Rol);cycles=5;break;case 0x36:Rmw(ZpX(),Rol);cycles=6;break;case 0x2E:Rmw(Abs(),Rol);cycles=6;break;case 0x3E:Rmw(AbsX(out _),Rol);cycles=7;break;
                case 0x6A:Registers=WithA(Ror(Registers.A));cycles=2;break;case 0x66:Rmw(Zp(),Ror);cycles=5;break;case 0x76:Rmw(ZpX(),Ror);cycles=6;break;case 0x6E:Rmw(Abs(),Ror);cycles=6;break;case 0x7E:Rmw(AbsX(out _),Ror);cycles=7;break;
                // RTI/RTS
                case 0x40:{byte status=Pop();ushort pc=PopWord();var r=Registers;r.Status=(byte)((status&~(byte)CpuStatus.Break)|(byte)CpuStatus.Unused);r.ProgramCounter=pc;Registers=r;cycles=6;break;}
                case 0x60:SetPc((ushort)(PopWord()+1));cycles=6;break;
                // SBC
                case 0xE9:case 0xEB:Sbc(Fetch());cycles=2;break;case 0xE5:Sbc(read(Zp()));cycles=3;break;case 0xF5:Sbc(read(ZpX()));cycles=4;break;case 0xED:Sbc(read(Abs()));cycles=4;break;
                case 0xFD:{var a=AbsX(out bool p);Sbc(read(a));cycles=4+(p?1:0);break;}case 0xF9:{var a=AbsY(out bool p);Sbc(read(a));cycles=4+(p?1:0);break;}case 0xE1:Sbc(read(IndX()));cycles=6;break;case 0xF1:{var a=IndY(out bool p);Sbc(read(a));cycles=5+(p?1:0);break;}
                // STA/STX/STY
                case 0x85:write(Zp(),Registers.A);cycles=3;break;case 0x95:write(ZpX(),Registers.A);cycles=4;break;case 0x8D:write(Abs(),Registers.A);cycles=4;break;case 0x9D:write(AbsX(out _),Registers.A);cycles=5;break;case 0x99:write(AbsY(out _),Registers.A);cycles=5;break;case 0x81:write(IndX(),Registers.A);cycles=6;break;case 0x91:write(IndY(out _),Registers.A);cycles=6;break;
                case 0x86:write(Zp(),Registers.X);cycles=3;break;case 0x96:write(ZpY(),Registers.X);cycles=4;break;case 0x8E:write(Abs(),Registers.X);cycles=4;break;
                case 0x84:write(Zp(),Registers.Y);cycles=3;break;case 0x94:write(ZpX(),Registers.Y);cycles=4;break;case 0x8C:write(Abs(),Registers.Y);cycles=4;break;
                // transfers
                case 0xAA:Registers=WithX(Registers.A);cycles=2;break;case 0xA8:Registers=WithY(Registers.A);cycles=2;break;case 0xBA:Registers=WithX(Registers.StackPointer);cycles=2;break;
                case 0x8A:Registers=WithA(Registers.X);cycles=2;break;case 0x9A:{var r=Registers;r.StackPointer=r.X;Registers=r;cycles=2;break;}case 0x98:Registers=WithA(Registers.Y);cycles=2;break;
                default: throw new InvalidOperationException($"Unsupported opcode ${opcode:X2} at ${(ushort)(Registers.ProgramCounter-1):X4}. Unofficial opcodes are out of scope.");
            }
            TotalCycles += cycles;
            return cycles;
        }

        private byte Fetch(){byte v=read(Registers.ProgramCounter);SetPc((ushort)(Registers.ProgramCounter+1));return v;}
        private ushort Zp()=>Fetch(); private ushort ZpX()=>(byte)(Fetch()+Registers.X); private ushort ZpY()=>(byte)(Fetch()+Registers.Y);
        private ushort Abs(){byte lo=Fetch(),hi=Fetch();return (ushort)(lo|(hi<<8));}
        private ushort AbsX(out bool page){ushort b=Abs(),a=(ushort)(b+Registers.X);page=(b&0xFF00)!=(a&0xFF00);return a;}
        private ushort AbsY(out bool page){ushort b=Abs(),a=(ushort)(b+Registers.Y);page=(b&0xFF00)!=(a&0xFF00);return a;}
        private ushort IndX(){byte p=(byte)(Fetch()+Registers.X);return (ushort)(read(p)|(read((byte)(p+1))<<8));}
        private ushort IndY(out bool page){byte p=Fetch();ushort b=(ushort)(read(p)|(read((byte)(p+1))<<8)),a=(ushort)(b+Registers.Y);page=(b&0xFF00)!=(a&0xFF00);return a;}
        private ushort ReadWord(ushort a)=>(ushort)(read(a)|(read((ushort)(a+1))<<8));
        private ushort ReadWordBug(ushort a)=>(ushort)(read(a)|(read((ushort)((a&0xFF00)|((a+1)&0x00FF)))<<8));
        private void SetPc(ushort pc){var r=Registers;r.ProgramCounter=pc;Registers=r;}
        private bool Flag(CpuStatus f)=>(Registers.Status&(byte)f)!=0;
        private void Set(CpuStatus f,bool on){var r=Registers;r.Status=on?(byte)(r.Status|(byte)f):(byte)(r.Status&~(byte)f);r.Status|=(byte)CpuStatus.Unused;Registers=r;}
        private void Zn(byte v){Set(CpuStatus.Zero,v==0);Set(CpuStatus.Negative,(v&0x80)!=0);}
        private CpuRegisters WithA(byte v){var r=Registers;r.A=v;Registers=r;Zn(v);return Registers;} private CpuRegisters WithX(byte v){var r=Registers;r.X=v;Registers=r;Zn(v);return Registers;} private CpuRegisters WithY(byte v){var r=Registers;r.Y=v;Registers=r;Zn(v);return Registers;}
        private void Adc(byte v){int a=Registers.A,sum=a+v+(Flag(CpuStatus.Carry)?1:0);Set(CpuStatus.Carry,sum>255);Set(CpuStatus.Overflow,((~(a^v)&(a^sum))&0x80)!=0);Registers=WithA((byte)sum);}
        private void Sbc(byte v)=>Adc((byte)~v);
        private void Compare(byte a,byte v){int d=a-v;Set(CpuStatus.Carry,a>=v);Zn((byte)d);}
        private void Bit(byte v){Set(CpuStatus.Zero,(Registers.A&v)==0);Set(CpuStatus.Overflow,(v&0x40)!=0);Set(CpuStatus.Negative,(v&0x80)!=0);}
        private byte Asl(byte v){Set(CpuStatus.Carry,(v&0x80)!=0);v=(byte)(v<<1);Zn(v);return v;} private byte Lsr(byte v){Set(CpuStatus.Carry,(v&1)!=0);v>>=1;Zn(v);return v;}
        private byte Slo(byte v){v=Asl(v);Registers=WithA((byte)(Registers.A|v));return v;}
        private byte Isc(byte v){v=Inc(v);Sbc(v);return v;}
        private void Lax(byte v){var r=Registers;r.A=v;r.X=v;Registers=r;Zn(v);}
        private byte Rol(byte v){bool c=Flag(CpuStatus.Carry);Set(CpuStatus.Carry,(v&0x80)!=0);v=(byte)((v<<1)|(c?1:0));Zn(v);return v;} private byte Ror(byte v){bool c=Flag(CpuStatus.Carry);Set(CpuStatus.Carry,(v&1)!=0);v=(byte)((v>>1)|(c?0x80:0));Zn(v);return v;}
        private byte Inc(byte v){v++;Zn(v);return v;} private byte Dec(byte v){v--;Zn(v);return v;} private void Rmw(ushort a,Func<byte,byte> op)=>write(a,op(read(a)));
        private int Branch(bool take){sbyte d=(sbyte)Fetch();if(!take)return 2;ushort old=Registers.ProgramCounter;SetPc((ushort)(old+d));return 3+(((old^Registers.ProgramCounter)&0xFF00)!=0?1:0);}
        private void Push(byte v){write((ushort)(0x0100|Registers.StackPointer),v);var r=Registers;r.StackPointer--;Registers=r;} private byte Pop(){var r=Registers;r.StackPointer++;Registers=r;return read((ushort)(0x0100|r.StackPointer));}
        private void PushWord(ushort v){Push((byte)(v>>8));Push((byte)v);} private ushort PopWord(){byte lo=Pop(),hi=Pop();return (ushort)(lo|(hi<<8));}
        private int Interrupt(ushort vector,bool brk,bool count=true){PushWord(Registers.ProgramCounter);byte s=(byte)(Registers.Status|(byte)CpuStatus.Unused);s=brk?(byte)(s|(byte)CpuStatus.Break):(byte)(s&~(byte)CpuStatus.Break);Push(s);Set(CpuStatus.InterruptDisable,true);SetPc(ReadWord(vector));if(count)TotalCycles+=7;return 7;}
    }
}
