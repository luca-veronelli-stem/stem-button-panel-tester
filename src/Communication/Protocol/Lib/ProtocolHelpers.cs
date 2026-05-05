namespace Communication.Protocol.Lib
{
    /// <summary>
    /// Fornisce metodi di utilità per la gestione del protocollo di comunicazione STEM.
    /// Centralizza tutte le operazioni di conversione byte e calcolo CRC.
    /// </summary>
    public static class ProtocolHelpers
    {
        #region Conversioni Little-Endian

        /// <summary>
        /// Converte un valore <see cref="ushort"/> in un array di byte in ordine little-endian.
        /// </summary>
        /// <param name="value">Valore da convertire.</param>
        /// <returns>Array di 2 byte in formato little-endian.</returns>
        public static byte[] ToLittleEndianBytes(this ushort value)
        {
            return [(byte)value, (byte)(value >> 8)];
        }

        /// <summary>
        /// Converte un valore <see cref="uint"/> in un array di byte in ordine little-endian.
        /// </summary>
        /// <param name="value">Valore da convertire.</param>
        /// <returns>Array di 4 byte in formato little-endian.</returns>
        public static byte[] ToLittleEndianBytes(this uint value)
        {
            return
            [
                (byte)value,
                (byte)(value >> 8),
                (byte)(value >> 16),
                (byte)(value >> 24)
            ];
        }

        /// <summary>
        /// Legge un valore <see cref="ushort"/> da un buffer in formato little-endian.
        /// </summary>
        /// <param name="buffer">Buffer contenente i dati.</param>
        /// <param name="offset">Offset da cui iniziare la lettura.</param>
        /// <returns>Valore letto, oppure 0 se il buffer è insufficiente.</returns>
        public static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> buffer, int offset = 0)
        {
            if (buffer.Length < offset + 2) return 0;
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        /// <summary>
        /// Converte un array di 2 byte in un <see cref="ushort"/>, interpretando i byte in little-endian.
        /// </summary>
        /// <param name="bytes">Array di 2 byte da convertire.</param>
        /// <returns>Valore risultante dalla conversione.</returns>
        /// <exception cref="ArgumentException">Se l'array è null o la sua lunghezza non è 2.</exception>
        public static ushort ToUInt16(this byte[] bytes)
        {
            if (bytes == null || bytes.Length != 2)
                throw new ArgumentException("L'array di byte deve contenere esattamente 2 byte.", nameof(bytes));
            return (ushort)(bytes[0] | (bytes[1] << 8));
        }

        /// <summary>
        /// Converte un array di 4 byte in un <see cref="int"/>, interpretando i byte in little-endian.
        /// </summary>
        /// <param name="bytes">Array di 4 byte da convertire.</param>
        /// <returns>Valore risultante dalla conversione.</returns>
        /// <exception cref="ArgumentException">Se l'array è null o la sua lunghezza non è 4.</exception>
        public static int ToInt32(this byte[] bytes)
        {
            if (bytes == null || bytes.Length != 4)
                throw new ArgumentException("L'array di byte deve contenere esattamente 4 byte.", nameof(bytes));
            return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24);
        }

        /// <summary>
        /// Converte un array di 4 byte in un <see cref="uint"/>, interpretando i byte in little-endian.
        /// </summary>
        /// <param name="bytes">Array di 4 byte da convertire.</param>
        /// <returns>Valore risultante dalla conversione.</returns>
        /// <exception cref="ArgumentException">Se l'array è null o la sua lunghezza non è 4.</exception>
        public static uint ToUInt32(this byte[] bytes)
        {
            if (bytes == null || bytes.Length != 4)
                throw new ArgumentException("L'array di byte deve contenere esattamente 4 byte.", nameof(bytes));
            return (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
        }

        #endregion

        #region Conversioni Big-Endian

        /// <summary>
        /// Converte un valore <see cref="ushort"/> in un array di byte in ordine big-endian.
        /// </summary>
        /// <param name="value">Valore da convertire.</param>
        /// <returns>Array di 2 byte in formato big-endian.</returns>
        public static byte[] ToBigEndianBytes(this ushort value)
        {
            return [(byte)(value >> 8), (byte)value];
        }

        /// <summary>
        /// Converte un valore <see cref="uint"/> in un array di byte in ordine big-endian.
        /// </summary>
        /// <param name="value">Valore da convertire.</param>
        /// <returns>Array di 4 byte in formato big-endian.</returns>
        public static byte[] ToBigEndianBytes(this uint value)
        {
            return
            [
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value
            ];
        }

        /// <summary>
        /// Legge un valore <see cref="ushort"/> da un buffer in formato big-endian.
        /// </summary>
        /// <param name="buffer">Buffer contenente i dati.</param>
        /// <param name="offset">Offset da cui iniziare la lettura.</param>
        /// <returns>Valore letto, oppure 0 se il buffer è insufficiente.</returns>
        public static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> buffer, int offset = 0)
        {
            if (buffer.Length < offset + 2) return 0;
            return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
        }

        /// <summary>
        /// Legge un valore <see cref="uint"/> da un buffer in formato big-endian.
        /// </summary>
        /// <param name="buffer">Buffer contenente i dati.</param>
        /// <param name="offset">Offset da cui iniziare la lettura.</param>
        /// <returns>Valore letto, oppure 0 se il buffer è insufficiente.</returns>
        public static uint ReadUInt32BigEndian(ReadOnlySpan<byte> buffer, int offset = 0)
        {
            if (buffer.Length < offset + 4) return 0;
            return (uint)((buffer[offset] << 24) | (buffer[offset + 1] << 16) |
                          (buffer[offset + 2] << 8) | buffer[offset + 3]);
        }

        /// <summary>
        /// Scrive un valore <see cref="ushort"/> in un buffer in formato big-endian.
        /// </summary>
        /// <param name="buffer">Buffer di destinazione.</param>
        /// <param name="offset">Offset da cui iniziare la scrittura.</param>
        /// <param name="value">Valore da scrivere.</param>
        public static void WriteUInt16BigEndian(Span<byte> buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;
        }

        /// <summary>
        /// Scrive un valore <see cref="uint"/> in un buffer in formato big-endian.
        /// </summary>
        /// <param name="buffer">Buffer di destinazione.</param>
        /// <param name="offset">Offset da cui iniziare la scrittura.</param>
        /// <param name="value">Valore da scrivere.</param>
        public static void WriteUInt32BigEndian(Span<byte> buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        #endregion

        #region CRC

        /// <summary>
        /// Calcola il CRC-16 Modbus per un array di byte.
        /// Utilizza il polinomio 0xA001 con inizializzazione 0xFFFF, come specificato dal protocollo STEM.
        /// </summary>
        /// <param name="data">Array di byte su cui calcolare il CRC.</param>
        /// <returns>Valore CRC-16 calcolato.</returns>
        /// <exception cref="ArgumentNullException">Se <paramref name="data"/> è null.</exception>
        public static ushort CalculateCrc(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;

            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }

            return crc;
        }

        /// <summary>
        /// Calcola il CRC-16 Modbus per un array di byte (overload per compatibilità).
        /// </summary>
        /// <param name="data">Array di byte su cui calcolare il CRC.</param>
        /// <returns>Valore CRC-16 calcolato.</returns>
        /// <exception cref="ArgumentNullException">Se <paramref name="data"/> è null.</exception>
        public static ushort CalculateCrc(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            return CalculateCrc(data.AsSpan());
        }

        #endregion
    }
}