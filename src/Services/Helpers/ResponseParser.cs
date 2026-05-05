namespace Services.Helpers
{
    /// <summary>
    /// Helper per analizzare e validare risposte dei comandi protocollari.
    /// </summary>
    public static class ResponseParser
    {
        /// <summary>
        /// Valida e analizza una risposta WHO_AM_I.
        /// Formato atteso: MACHINE_TYPE (1) + FW_TYPE (2) + UUID (12 bytes) = 15 bytes totali
        /// </summary>
        public static bool TryParseWhoAmI(byte[] response, out WhoAmIResponse result)
        {
            result = default;

            if (response.Length < 15)
            {
                return false;
            }

            result = new WhoAmIResponse
            {
                MachineType = response[0],
                FirmwareType = (ushort)((response[1] << 8) | response[2]), // Big-endian
                Uuid = ExtractUuid(response, 3)
            };

            return true;
        }

        /// <summary>
        /// Valida una risposta generica del protocollo.
        /// </summary>
        public static bool IsValidResponse(byte[] data, int minLength)
        {
            return data != null && data.Length >= minLength;
        }

        /// <summary>
        /// Verifica se una risposta è un ACK per un comando specifico.
        /// Formato ACK: 0x80, COMMAND_ID
        /// </summary>
        public static bool IsAcknowledgment(byte[] data, ushort commandId)
        {
            return data.Length >= 2 &&
                   data[0] == 0x80 &&
                   data[1] == (byte)(commandId & 0xFF);
        }

        /// <summary>
        /// Estrae UUID (12 bytes) da una risposta a partire da un offset.
        /// </summary>
        private static byte[] ExtractUuid(byte[] data, int offset)
        {
            byte[] uuid = new byte[12];
            Array.Copy(data, offset, uuid, 0, 12);
            return uuid;
        }
    }

    /// <summary>
    /// Risultato del parsing di una risposta WHO_AM_I.
    /// </summary>
    public struct WhoAmIResponse
    {
        public byte MachineType { get; init; }
        public ushort FirmwareType { get; init; }
        public byte[] Uuid { get; init; }
    }
}
