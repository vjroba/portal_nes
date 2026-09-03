using NUnit.Framework;
using PortalNes.Emulator.Cartridge;
using PortalNes.Emulator.Mappers;

namespace PortalNes.Tests
{
    public sealed class Mapper085Tests
    {
        [Test] public void MapsPrgChrAndRam()
        {
            var m=Create(); m.CpuWrite(0x8000,2);m.CpuWrite(0x8010,3);m.CpuWrite(0x9000,4);
            m.CpuWrite(0xA000,5);m.CpuWrite(0xA010,6);m.CpuWrite(0xE000,0x80);m.CpuWrite(0x6000,0xA5);
            Assert.That(m.CpuRead(0x8000),Is.EqualTo(0x12)); Assert.That(m.CpuRead(0xA000),Is.EqualTo(0x13));
            Assert.That(m.CpuRead(0xC000),Is.EqualTo(0x14)); Assert.That(m.PpuRead(0),Is.EqualTo(0x45));
            Assert.That(m.PpuRead(0x400),Is.EqualTo(0x46)); Assert.That(m.CpuRead(0x6000),Is.EqualTo(0xA5));
        }
        [Test] public void ProducesFmAudio()
        {
            var m=Create(); m.CpuWrite(0x9010,0x10);m.CpuWrite(0x9030,0x80);
            m.CpuWrite(0x9010,0x20);m.CpuWrite(0x9030,0x19);
            m.CpuWrite(0x9010,0x30);m.CpuWrite(0x9030,0x10);
            m.ClockAudio(1000); Assert.That(m.ExpansionAudioSample,Is.Not.EqualTo(0));
        }
        private static Mapper085 Create()
        {
            var p=new byte[16*8192];var c=new byte[32*1024];
            for(int b=0;b<16;b++)for(int i=0;i<8192;i++)p[b*8192+i]=(byte)(0x10+b);
            for(int b=0;b<32;b++)for(int i=0;i<1024;i++)c[b*1024+i]=(byte)(0x40+b);
            return new Mapper085(p,c,false,MirroringMode.Vertical);
        }
    }
}
