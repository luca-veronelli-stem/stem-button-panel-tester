using Core.Enums;

namespace Communication.Protocol.Lib
{
    /// <summary>
    /// Rappresenta le informazioni di rete (NetInfo) nel protocollo di comunicazione STEM.
    /// L'header NetInfo contiene metadati per la gestione dei chunk a livello di rete.
    /// </summary>
    /// <remarks>
    /// Struttura del campo NetInfo (2 byte, little-endian):
    /// <list type="bullet">
    /// <item>Bit 15-6: Numero di chunk rimanenti (0-1023)</item>
    /// <item>Bit 5: Flag SetLength (indica se il primo chunk contiene la lunghezza totale)</item>
    /// <item>Bit 4-2: Identificatore del pacchetto (1-7)</item>
    /// <item>Bit 1-0: Versione del protocollo</item>
    /// </list>
    /// </remarks>
    /// <param name="RemainingChunks">Numero di chunk rimanenti da ricevere (0 = ultimo chunk).</param>
    /// <param name="SetLength">Indica se il pacchetto include la lunghezza nel primo chunk.</param>
    /// <param name="PacketId">Identificatore univoco del pacchetto (1-7).</param>
    /// <param name="Version">Versione del protocollo utilizzata.</param>
    public readonly record struct NetInfo(
        int RemainingChunks,
        bool SetLength,
        int PacketId,
        ProtocolVersion Version)
    {
        /// <summary>
        /// Numero massimo di chunk rimanenti rappresentabile (10 bit = 1023).
        /// </summary>
        private const int MaxRemainingChunks = 0x3FF;

        /// <summary>
        /// Converte questa istanza in un array di byte nel formato del protocollo (little-endian).
        /// </summary>
        /// <returns>Array di 2 byte rappresentante il NetInfo.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Se <see cref="PacketId"/> non è compreso tra <see cref="ProtocolConfig.MinPacketId"/> 
        /// e <see cref="ProtocolConfig.MaxPacketId"/>.
        /// </exception>
        public byte[] ToBytes()
        {
            ValidatePacketId(PacketId);
            ValidateRemainingChunks(RemainingChunks);

            ushort value = (ushort)(
                (RemainingChunks << 6) |
                (SetLength ? 1 << 5 : 0) |
                (PacketId << 2) |
                (byte)Version);

            return value.ToLittleEndianBytes();
        }

        /// <summary>
        /// Crea un'istanza di <see cref="NetInfo"/> a partire da un array di byte.
        /// </summary>
        /// <param name="bytes">Array di 2 byte contenente il NetInfo in formato little-endian.</param>
        /// <returns>Nuova istanza di <see cref="NetInfo"/> con i valori estratti.</returns>
        /// <exception cref="ArgumentException">Se l'array non contiene esattamente 2 byte.</exception>
        /// <exception cref="ProtocolException">Se il PacketId estratto non è valido.</exception>
        public static NetInfo FromBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != ProtocolConfig.NetInfoLength)
            {
                throw new ArgumentException(
                    $"L'array di byte deve contenere esattamente {ProtocolConfig.NetInfoLength} byte per NetInfo.",
                    nameof(bytes));
            }

            ushort netInfo = ProtocolHelpers.ReadUInt16LittleEndian(bytes);

            int remainingChunks = (netInfo >> 6) & MaxRemainingChunks;
            bool setLength = ((netInfo >> 5) & 0x01) != 0;
            int packetId = (netInfo >> 2) & 0x07;
            var version = (ProtocolVersion)(netInfo & 0x03);

            if (packetId < ProtocolConfig.MinPacketId || packetId > ProtocolConfig.MaxPacketId)
            {
                throw new ProtocolException($"Identificatore pacchetto non valido: {packetId}. Deve essere compreso tra {ProtocolConfig.MinPacketId} e {ProtocolConfig.MaxPacketId}.");
            }

            return new NetInfo(remainingChunks, setLength, packetId, version);
        }

        /// <summary>
        /// Crea un'istanza di <see cref="NetInfo"/> a partire da un array di byte.
        /// </summary>
        /// <param name="bytes">Array di 2 byte contenente il NetInfo.</param>
        /// <returns>Nuova istanza di <see cref="NetInfo"/>.</returns>
        public static NetInfo FromBytes(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            return FromBytes(bytes.AsSpan());
        }

        /// <summary>
        /// Valida che il PacketId sia nell'intervallo consentito.
        /// </summary>
        private static void ValidatePacketId(int packetId)
        {
            if (packetId < ProtocolConfig.MinPacketId || packetId > ProtocolConfig.MaxPacketId)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(packetId),
                    $"PacketId deve essere compreso tra {ProtocolConfig.MinPacketId} e {ProtocolConfig.MaxPacketId}.");
            }
        }

        /// <summary>
        /// Valida che il numero di chunk rimanenti sia nell'intervallo rappresentabile.
        /// </summary>
        private static void ValidateRemainingChunks(int remainingChunks)
        {
            if (remainingChunks < 0 || remainingChunks > MaxRemainingChunks)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(remainingChunks),
                    $"RemainingChunks deve essere compreso tra 0 e {MaxRemainingChunks}.");
            }
        }
    }
}
