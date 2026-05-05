namespace Services.Helpers
{
    /// <summary>
    /// Helper per costruire payload di comandi protocollari.
    /// </summary>
    public static class PayloadBuilder
    {
        /// <summary>
        /// Costruisce un payload per il comando WHO_ARE_YOU.
        /// Formato: MACHINE_TYPE (1 byte) + FW_TYPE_H (1 byte) + FW_TYPE_L (1 byte) + RESET_FLAG (1 byte)
        /// </summary>
        public static byte[] BuildWhoAreYouPayload(byte machineType, ushort firmwareType, byte resetFlag)
        {
            return new byte[]
            {
                machineType,
                (byte)((firmwareType >> 8) & 0xFF), // FW_TYPE_H (big-endian)
                (byte)(firmwareType & 0xFF),        // FW_TYPE_L
                resetFlag
            };
        }

        /// <summary>
        /// Costruisce un payload per il comando SET_ADDRESS.
        /// Formato: UUID (12 bytes) + STEM_ADDRESS (4 bytes in big-endian)
        /// </summary>
        public static byte[] BuildSetAddressPayload(byte[] uuid, uint stemAddress)
        {
            if (uuid.Length != 12)
            {
                throw new ArgumentException("UUID deve essere di 12 bytes", nameof(uuid));
            }

            byte[] payload = new byte[16];
            Array.Copy(uuid, 0, payload, 0, 12);

            // STEM address in big-endian (il firmware farà OSdwordSwap)
            payload[12] = (byte)((stemAddress >> 24) & 0xFF);
            payload[13] = (byte)((stemAddress >> 16) & 0xFF);
            payload[14] = (byte)((stemAddress >> 8) & 0xFF);
            payload[15] = (byte)(stemAddress & 0xFF);

            return payload;
        }

        /// <summary>
        /// Costruisce un payload per scrivere una variabile.
        /// Formato: VARIABLE_ID_H (1 byte) + VARIABLE_ID_L (1 byte) + VALUE (n bytes)
        /// </summary>
        public static byte[] BuildWriteVariablePayload(ushort variableId, byte[] value)
        {
            byte varHigh = (byte)(variableId >> 8);
            byte varLow = (byte)(variableId & 0xFF);
            return [varHigh, varLow, .. value];
        }

        /// <summary>
        /// Costruisce un payload atteso per la pressione di un pulsante.
        /// Formato: 0x00, 0x02, VAR_ID_H, VAR_ID_L, BUTTON_MASK
        /// </summary>
        public static byte[] BuildButtonPressExpectedPayload(ushort buttonStatusVariableId, byte buttonMask)
        {
            byte varHigh = (byte)(buttonStatusVariableId >> 8);
            byte varLow = (byte)(buttonStatusVariableId & 0xFF);
            return new byte[] { 0x00, 0x02, varHigh, varLow, buttonMask };
        }
    }
}
