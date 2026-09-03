namespace PortalNes.Emulator.Input
{
    public sealed class NesController
    {
        private byte state;
        private byte shiftRegister;
        private bool strobe;

        public byte State
        {
            get => state;
            set
            {
                state = value;
                if (strobe) shiftRegister = state;
            }
        }

        public void WriteStrobe(byte value)
        {
            bool next = (value & 1) != 0;
            if (strobe && !next) shiftRegister = state;
            strobe = next;
            if (strobe) shiftRegister = state;
        }

        public byte Read()
        {
            byte result = (byte)(shiftRegister & 1);
            if (!strobe) shiftRegister = (byte)((shiftRegister >> 1) | 0x80);
            return result;
        }
    }
}
