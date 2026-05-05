namespace Services.Helpers
{
    /// <summary>
    /// Helper per calcoli relativi agli indirizzi STEM.
    /// </summary>
    public static class StemAddressHelper
    {
        /// <summary>
        /// Calcola l'indirizzo STEM in base a machine type, firmware type e board number.
        /// Formula: <c>(MACHINE &lt;&lt; 16) | ((FIRMWARE_TYPE &amp; 0x03FF) &lt;&lt; 6) | (BOARD_NUMBER &amp; 0x003F)</c>
        /// </summary>
        public static uint CalculateAddress(byte machineType, ushort firmwareType, byte boardNumber)
        {
            return ((uint)machineType << 16) |
                   ((uint)(firmwareType & 0x03FF) << 6) |
                   ((uint)(boardNumber & 0x003F));
        }

        /// <summary>
        /// Estrae il machine type da un indirizzo STEM.
        /// </summary>
        public static byte ExtractMachineType(uint stemAddress)
        {
            return (byte)((stemAddress >> 16) & 0xFF);
        }

        /// <summary>
        /// Estrae il firmware type da un indirizzo STEM.
        /// </summary>
        public static ushort ExtractFirmwareType(uint stemAddress)
        {
            return (ushort)((stemAddress >> 6) & 0x03FF);
        }

        /// <summary>
        /// Estrae il board number da un indirizzo STEM.
        /// </summary>
        public static byte ExtractBoardNumber(uint stemAddress)
        {
            return (byte)(stemAddress & 0x003F);
        }
    }
}
