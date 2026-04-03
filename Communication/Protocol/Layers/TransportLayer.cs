using Communication.Protocol.Lib;
using Core.Enums;

namespace Communication.Protocol.Layers
{
    /// <summary>
    /// Rappresenta il livello di trasporto nel protocollo di comunicazione STEM.
    /// Gestisce la costruzione e l'analisi dei pacchetti di trasporto con verifica CRC.
    /// </summary>
    /// <remarks>
    /// Struttura del pacchetto di trasporto:
    /// <list type="bullet">
    /// <item>Byte 0: Flag di crittografia (<see cref="CryptType"/>)</item>
    /// <item>Byte 1-4: Identificatore mittente (big-endian)</item>
    /// <item>Byte 5-6: Lunghezza pacchetto applicativo - lPack (big-endian)</item>
    /// <item>Byte 7-(7+lPack-1): Pacchetto applicativo</item>
    /// <item>Ultimi 2 byte: CRC-16 Modbus (big-endian)</item>
    /// </list>
    /// </remarks>
    public sealed class TransportLayer : Layer
    {
        /// <summary>
        /// Flag di crittografia del pacchetto.
        /// </summary>
        public CryptType CryptFlag { get; }

        /// <summary>
        /// Identificatore del mittente (4 byte).
        /// </summary>
        public uint SenderId { get; }

        /// <summary>
        /// Lunghezza del pacchetto applicativo (lPack).
        /// </summary>
        public ushort PacketLength { get; }

        /// <summary>
        /// Header del livello di trasporto (7 byte).
        /// </summary>
        public byte[] TransportHeader { get; }

        /// <summary>
        /// Pacchetto di trasporto completo (header + pacchetto applicativo + CRC).
        /// </summary>
        public byte[] TransportPacket { get; }

        /// <summary>
        /// Pacchetto applicativo contenuto nel pacchetto di trasporto.
        /// </summary>
        public byte[] ApplicationPacket { get; }

        /// <summary>
        /// CRC calcolato/estratto dal pacchetto (2 byte).
        /// </summary>
        public byte[] Crc { get; }

        /// <summary>
        /// Indica se il pacchetto è valido (CRC verificato correttamente).
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Messaggio di errore in caso di validazione fallita, altrimenti null.
        /// </summary>
        public string? ValidationError { get; }

        /// <summary>
        /// Costruttore privato per inizializzazione controllata.
        /// Utilizzare i metodi factory <see cref="Create"/> o <see cref="Parse"/>.
        /// </summary>
        private TransportLayer(
            CryptType cryptFlag,
            uint senderId,
            ushort packetLength,
            byte[] transportHeader,
            byte[] applicationPacket,
            byte[] crc,
            byte[] transportPacket,
            bool isValid,
            string? validationError) : base(applicationPacket)
        {
            CryptFlag = cryptFlag;
            SenderId = senderId;
            PacketLength = packetLength;
            TransportHeader = transportHeader;
            ApplicationPacket = applicationPacket;
            Crc = crc;
            TransportPacket = transportPacket;
            IsValid = isValid;
            ValidationError = validationError;
        }

        /// <summary>
        /// Crea un nuovo pacchetto di trasporto a partire dai parametri specificati.
        /// Usa big-endian per senderId, lPack e CRC (formato protocollo STEM).
        /// </summary>
        /// <param name="cryptFlag">Flag di crittografia.</param>
        /// <param name="senderId">Identificatore del mittente.</param>
        /// <param name="applicationPacket">Pacchetto applicativo da incapsulare.</param>
        /// <returns>Nuova istanza di <see cref="TransportLayer"/> con il pacchetto costruito.</returns>
        /// <exception cref="ArgumentNullException">Se <paramref name="applicationPacket"/> è null.</exception>
        public static TransportLayer Create(CryptType cryptFlag, uint senderId, byte[] applicationPacket)
        {
            ArgumentNullException.ThrowIfNull(applicationPacket);

            ushort lPack = (ushort)applicationPacket.Length;

            // Costruzione header di trasporto (big-endian come nel protocollo STEM)
            byte[] transportHeader = BuildHeader(cryptFlag, senderId, lPack);

            // Calcolo CRC su header + pacchetto applicativo
            byte[] dataForCrc = [.. transportHeader, .. applicationPacket];
            ushort crcValue = ProtocolHelpers.CalculateCrc(dataForCrc);
            // CRC in big-endian (byte-swapped come nel progetto originale)
            byte[] crc = [(byte)(crcValue >> 8), (byte)crcValue];

            // Assemblaggio pacchetto completo
            byte[] transportPacket = [.. transportHeader, .. applicationPacket, .. crc];

            return new TransportLayer(
                cryptFlag,
                senderId,
                lPack,
                transportHeader,
                applicationPacket,
                crc,
                transportPacket,
                isValid: true,
                validationError: null);
        }

        /// <summary>
        /// Crea un nuovo pacchetto di trasporto con crittografia disabilitata.
        /// </summary>
        /// <param name="senderId">Identificatore del mittente.</param>
        /// <param name="applicationPacket">Pacchetto applicativo da incapsulare.</param>
        /// <returns>Nuova istanza di <see cref="TransportLayer"/> con il pacchetto costruito.</returns>
        public static TransportLayer Create(uint senderId, byte[] applicationPacket)
        {
            return Create(CryptType.None, senderId, applicationPacket);
        }

        /// <summary>
        /// Crea un nuovo pacchetto di trasporto a partire dai parametri specificati.
        /// Overload che accetta recipientId per compatibilità API (recipientId viene ignorato
        /// nel formato del pacchetto ma può essere usato per routing a livello superiore).
        /// </summary>
        /// <param name="cryptFlag">Flag di crittografia.</param>
        /// <param name="senderId">Identificatore del mittente.</param>
        /// <param name="recipientId">Identificatore del destinatario (usato solo per routing, non serializzato).</param>
        /// <param name="applicationPacket">Pacchetto applicativo da incapsulare.</param>
        /// <returns>Nuova istanza di <see cref="TransportLayer"/> con il pacchetto costruito.</returns>
        public static TransportLayer Create(CryptType cryptFlag, uint senderId, uint recipientId, byte[] applicationPacket)
        {
            // Nel protocollo STEM, il recipientId non è parte del transport layer.
            // L'indirizzamento avviene tramite l'arbitration ID CAN o altri meccanismi.
            return Create(cryptFlag, senderId, applicationPacket);
        }

        /// <summary>
        /// Analizza un pacchetto di trasporto esistente e verifica il CRC.
        /// Usa big-endian per senderId e lPack (formato RX del protocollo STEM).
        /// </summary>
        /// <param name="transportPacket">Buffer contenente il pacchetto di trasporto completo.</param>
        /// <returns>
        /// Nuova istanza di <see cref="TransportLayer"/> con i dati estratti.
        /// Controllare <see cref="IsValid"/> per verificare l'integrità del pacchetto.
        /// </returns>
        /// <exception cref="ArgumentNullException">Se <paramref name="transportPacket"/> è null.</exception>
        /// <exception cref="ProtocolException">
        /// Se il pacchetto è troppo corto per contenere l'header di trasporto.
        /// </exception>
        public static TransportLayer Parse(byte[] transportPacket)
        {
            ArgumentNullException.ThrowIfNull(transportPacket);

            // Verifica lunghezza minima per header
            if (transportPacket.Length < ProtocolConfig.TransportHeaderLength)
            {
                throw new ProtocolException(
                    $"Il pacchetto di trasporto è troppo corto: ricevuti {transportPacket.Length} byte, " +
                    $"richiesti almeno {ProtocolConfig.TransportHeaderLength} byte per l'header.");
            }

            // Estrazione header (big-endian per RX - formato usato dalla pulsantiera)
            var cryptFlag = (CryptType)transportPacket[0];
            uint senderId = ProtocolHelpers.ReadUInt32BigEndian(transportPacket.AsSpan(), 1);
            ushort lPack = ProtocolHelpers.ReadUInt16BigEndian(transportPacket.AsSpan(), 5);
            byte[] transportHeader = transportPacket[..ProtocolConfig.TransportHeaderLength];

            // Verifica lunghezza totale
            int expectedLength = ProtocolConfig.TransportHeaderLength + lPack + ProtocolConfig.CrcLength;
            if (transportPacket.Length < expectedLength)
            {
                throw new ProtocolException(
                    $"Lunghezza pacchetto non corrispondente: attesi {expectedLength} byte " +
                    $"(header={ProtocolConfig.TransportHeaderLength}, lPack={lPack}, CRC={ProtocolConfig.CrcLength}), " +
                    $"ricevuti {transportPacket.Length} byte.");
            }

            // Estrazione pacchetto applicativo e CRC
            byte[] applicationPacket = transportPacket.AsSpan(
                ProtocolConfig.TransportHeaderLength,
                lPack).ToArray();

            byte[] crc = transportPacket.AsSpan(
                ProtocolConfig.TransportHeaderLength + lPack,
                ProtocolConfig.CrcLength).ToArray();

            // Validazione CRC (big-endian per RX)
            var (isValid, validationError) = ValidateCrc(transportPacket, lPack, crc);

            return new TransportLayer(
                cryptFlag,
                senderId,
                lPack,
                transportHeader,
                applicationPacket,
                crc,
                transportPacket,
                isValid,
                validationError);
        }

        /// <summary>
        /// Costruisce l'header del livello di trasporto (big-endian come nel protocollo STEM originale).
        /// </summary>
        private static byte[] BuildHeader(CryptType cryptFlag, uint senderId, ushort lPack)
        {
            byte[] header = new byte[ProtocolConfig.TransportHeaderLength];
            header[0] = (byte)cryptFlag;
            // SenderId in big-endian (formato protocollo STEM)
            header[1] = (byte)(senderId >> 24);
            header[2] = (byte)(senderId >> 16);
            header[3] = (byte)(senderId >> 8);
            header[4] = (byte)senderId;
            // lPack in big-endian (formato protocollo STEM)
            header[5] = (byte)(lPack >> 8);
            header[6] = (byte)lPack;
            return header;
        }

        /// <summary>
        /// Valida il CRC del pacchetto di trasporto (big-endian per RX).
        /// </summary>
        private static (bool IsValid, string? Error) ValidateCrc(
            byte[] transportPacket,
            ushort lPack,
            byte[] storedCrc)
        {
            int dataLength = ProtocolConfig.TransportHeaderLength + lPack;
            ReadOnlySpan<byte> dataToCheck = transportPacket.AsSpan(0, dataLength);
            ushort computedCrcValue = ProtocolHelpers.CalculateCrc(dataToCheck);
            ushort storedCrcValue = ProtocolHelpers.ReadUInt16BigEndian(storedCrc);

            if (computedCrcValue != storedCrcValue)
            {
                return (false, $"CRC non valido: calcolato=0x{computedCrcValue:X4}, memorizzato=0x{storedCrcValue:X4}");
            }

            return (true, null);
        }
    }
}
