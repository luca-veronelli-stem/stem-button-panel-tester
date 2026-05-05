namespace Communication.Protocol.Lib
{
    /// <summary>
    /// Centralizza i parametri configurabili del protocollo di comunicazione STEM.
    /// Fornisce costanti e metodi per la gestione degli identificatori di pacchetto.
    /// </summary>
    public sealed class ProtocolConfig
    {
        /// <summary>
        /// Identificativo minimo valido per un pacchetto (incluso).
        /// </summary>
        public const int MinPacketId = 1;

        /// <summary>
        /// Identificativo massimo valido per un pacchetto (incluso).
        /// </summary>
        public const int MaxPacketId = 7;

        /// <summary>
        /// Lunghezza dell'header del livello di trasporto in byte.
        /// Formato: [cryptFlag (1)] [senderId (4)] [lPack (2)] = 7 byte.
        /// </summary>
        public const int TransportHeaderLength = 7;

        /// <summary>
        /// Lunghezza del CRC in byte (CRC-16 = 2 byte).
        /// </summary>
        public const int CrcLength = 2;

        /// <summary>
        /// Lunghezza dell'header del livello applicativo in byte.
        /// Formato: [cmdInit (1)] [cmdOpt (1)] = 2 byte.
        /// </summary>
        public const int ApplicationHeaderLength = 2;

        /// <summary>
        /// Lunghezza dell'header NetInfo del livello di rete in byte.
        /// </summary>
        public const int NetInfoLength = 2;

        /// <summary>
        /// Lunghezza minima di un pacchetto di trasporto valido.
        /// Include header di trasporto (7) + header applicativo (2) + CRC (2) = 11 byte.
        /// </summary>
        public const int MinTransportPacketLength = TransportHeaderLength + ApplicationHeaderLength + CrcLength;

        /// <summary>
        /// Calcola in modo thread-safe il prossimo identificativo di pacchetto.
        /// Il valore è ciclico: dopo <see cref="MaxPacketId"/> ritorna a <see cref="MinPacketId"/>.
        /// </summary>
        /// <param name="currentId">
        /// Riferimento all'identificativo di pacchetto corrente; viene aggiornato atomicamente.
        /// </param>
        /// <returns>Il prossimo identificativo di pacchetto assegnato.</returns>
        /// <remarks>
        /// L'implementazione utilizza <see cref="Interlocked.CompareExchange(ref int, int, int)"/> per garantire
        /// aggiornamenti atomici in scenari multithread.
        /// </remarks>
        public static int GetNextPacketId(ref int currentId)
        {
            while (true)
            {
                int current = currentId;
                int next = current >= MaxPacketId ? MinPacketId : current + 1;

                if (Interlocked.CompareExchange(ref currentId, next, current) == current)
                {
                    return next;
                }
            }
        }
    }
}
