using System.Collections.Concurrent;
using Communication.Protocol.Lib;
using Core.Enums;
using Core.Models.Communication;

namespace Communication.Protocol.Layers
{
    /// <summary>
    /// Rappresenta il livello di rete nel protocollo di comunicazione STEM.
    /// Gestisce la suddivisione dei pacchetti di trasporto in chunk per la trasmissione.
    /// </summary>
    /// <remarks>
    /// Il livello di rete è responsabile del chunking dei dati per adattarli al MTU
    /// del canale di comunicazione sottostante (es. CAN bus con payload 8 byte).
    /// Ogni chunk include un header NetInfo che permette la riassemblaggio lato ricevente.
    /// </remarks>
    public sealed class NetworkLayer
    {
        private readonly uint _arbitrationId;
        private readonly byte[] _transportPacket;
        private readonly int _chunkSize;
        private readonly List<NetworkPacketChunk> _networkPackets;

        /// <summary>
        /// Contatore globale thread-safe per l'ID dei pacchetti.
        /// </summary>
        private static int s_currentPacketId = ProtocolConfig.MinPacketId - 1;

        /// <summary>
        /// Elenco dei chunk di rete generati, pronti per la trasmissione.
        /// </summary>
        public IReadOnlyList<NetworkPacketChunk> NetworkPackets => _networkPackets;

        /// <summary>
        /// Costruttore privato per inizializzazione controllata.
        /// Utilizzare il metodo factory <see cref="Create"/>.
        /// </summary>
        private NetworkLayer(
            uint arbitrationId,
            byte[] transportPacket,
            int chunkSize,
            List<NetworkPacketChunk> networkPackets)
        {
            _arbitrationId = arbitrationId;
            _transportPacket = transportPacket;
            _chunkSize = chunkSize;
            _networkPackets = networkPackets;
        }

        /// <summary>
        /// Crea un nuovo livello di rete suddividendo il pacchetto di trasporto in chunk.
        /// </summary>
        /// <param name="arbitrationId">
        /// Identificatore CAN per la trasmissione (arbitration ID del mittente).
        /// In CAN bus, l'arbitration ID identifica il nodo che trasmette il messaggio.
        /// </param>
        /// <param name="transportPacket">Pacchetto di trasporto da suddividere.</param>
        /// <param name="chunkSize">Dimensione massima di ciascun chunk in byte.</param>
        /// <returns>Nuova istanza di <see cref="NetworkLayer"/> con i chunk generati.</returns>
        /// <exception cref="ArgumentNullException">Se <paramref name="transportPacket"/> è null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="chunkSize"/> non è positivo.</exception>
        public static NetworkLayer Create(uint arbitrationId, byte[] transportPacket, int chunkSize)
        {
            ArgumentNullException.ThrowIfNull(transportPacket);

            if (chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chunkSize),
                    "La dimensione del chunk deve essere un valore positivo.");
            }

            List<NetworkPacketChunk> networkPackets = BuildChunks(arbitrationId, transportPacket, chunkSize);
            return new NetworkLayer(arbitrationId, transportPacket, chunkSize, networkPackets);
        }

        /// <summary>
        /// Costruisce i chunk di rete a partire dal pacchetto di trasporto.
        /// </summary>
        private static List<NetworkPacketChunk> BuildChunks(
            uint arbitrationId,
            byte[] transportPacket,
            int chunkSize)
        {
            var chunks = new List<NetworkPacketChunk>();

            if (transportPacket.Length == 0)
            {
                return chunks;
            }

            int numChunks = (transportPacket.Length + chunkSize - 1) / chunkSize;
            int packetId = ProtocolConfig.GetNextPacketId(ref s_currentPacketId);

            for (int i = 0; i < numChunks; i++)
            {
                int offset = i * chunkSize;
                int remaining = numChunks - i - 1;

                var netInfo = new NetInfo(
                    RemainingChunks: remaining,
                    SetLength: false,
                    PacketId: packetId,
                    Version: ProtocolVersion.V1);

                byte[] netInfoBytes = netInfo.ToBytes();
                byte[] chunkData = ExtractChunk(transportPacket, offset, chunkSize);

                chunks.Add(new NetworkPacketChunk(netInfoBytes, arbitrationId, chunkData));
            }

            return chunks;
        }

        /// <summary>
        /// Estrae un chunk dal pacchetto di trasporto.
        /// </summary>
        private static byte[] ExtractChunk(byte[] data, int offset, int maxLength)
        {
            int length = Math.Min(maxLength, data.Length - offset);
            return data.AsSpan(offset, length).ToArray();
        }
    }

    /// <summary>
    /// Gestisce il riassemblaggio dei chunk di rete in pacchetti di trasporto completi.
    /// Ogni istanza mantiene uno stato di riassemblaggio isolato per un singolo canale di comunicazione.
    /// </summary>
    /// <remarks>
    /// Questa classe è thread-safe e gestisce automaticamente la correlazione dei chunk
    /// basandosi sul PacketId contenuto nell'header NetInfo.
    /// </remarks>
    public sealed class NetworkLayerReassembler : IDisposable
    {
        private readonly ConcurrentDictionary<int, ReassemblyBuffer> _packetQueues = new();
        private readonly object _reassemblyLock = new();
        private bool _disposed;

        /// <summary>
        /// Evento sollevato per messaggi diagnostici durante il riassemblaggio.
        /// Utile per debug e logging.
        /// </summary>
        public event Action<string>? DiagnosticMessage;

        /// <summary>
        /// Evento sollevato quando un pacchetto di trasporto è stato riassemblato completamente.
        /// Il parametro contiene i byte del pacchetto di trasporto riassemblato.
        /// </summary>
        public event Action<byte[]>? PacketReassembled;

        /// <summary>
        /// Elabora un chunk di rete ricevuto e lo aggiunge allo stato di riassemblaggio.
        /// Quando tutti i chunk di un pacchetto sono stati ricevuti, solleva <see cref="PacketReassembled"/>.
        /// </summary>
        /// <param name="data">
        /// Frame di rete ricevuto, incluso l'header NetInfo (primi 2 byte).
        /// </param>
        public void ProcessReceivedChunk(byte[] data)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (data == null || data.Length < ProtocolConfig.NetInfoLength)
            {
                LogDiagnostic($"Chunk ignorato: lunghezza insufficiente ({data?.Length ?? 0} byte)");
                return;
            }

            LogDiagnostic($"RX chunk: {BitConverter.ToString(data)}");

            // Parsing dell'header NetInfo
            NetInfo netInfo;
            try
            {
                netInfo = NetInfo.FromBytes(data.AsSpan(0, ProtocolConfig.NetInfoLength));
            }
            catch (Exception ex)
            {
                LogDiagnostic($"Errore parsing NetInfo: {ex.Message}");
                return;
            }

            LogDiagnostic($"NetInfo: remaining={netInfo.RemainingChunks}, setLength={netInfo.SetLength}, packetId={netInfo.PacketId}");

            byte[] chunkData = data.AsSpan(ProtocolConfig.NetInfoLength).ToArray();

            lock (_reassemblyLock)
            {
                ProcessChunkInternal(netInfo, chunkData);
            }
        }

        /// <summary>
        /// Elaborazione interna del chunk (deve essere chiamato con lock acquisito).
        /// </summary>
        private void ProcessChunkInternal(NetInfo netInfo, byte[] chunkData)
        {
            bool hasExistingData = _packetQueues.TryGetValue(netInfo.PacketId, out ReassemblyBuffer? existingBuffer)
                                   && existingBuffer?.Chunks.Count > 0;

            // Se è l'inizio di una nuova sequenza o un pacchetto singolo, resetta il buffer
            if (netInfo.SetLength || (netInfo.RemainingChunks == 0 && !hasExistingData))
            {
                _packetQueues.TryRemove(netInfo.PacketId, out _);
            }

            ReassemblyBuffer buffer = _packetQueues.GetOrAdd(netInfo.PacketId, _ => new ReassemblyBuffer());
            buffer.Chunks.Add(chunkData);

            LogDiagnostic($"Aggiunto chunk {buffer.Chunks.Count} per packetId={netInfo.PacketId}, rimanenti={netInfo.RemainingChunks}");

            // Verifica se il pacchetto è completo
            if (netInfo.RemainingChunks == 0)
            {
                CompleteReassembly(netInfo, buffer);
            }
        }

        /// <summary>
        /// Completa il riassemblaggio e solleva l'evento.
        /// </summary>
        private void CompleteReassembly(NetInfo netInfo, ReassemblyBuffer buffer)
        {
            byte[] reassembledData = ConcatenateChunks(buffer.Chunks);
            _packetQueues.TryRemove(netInfo.PacketId, out _);

            // Se setLength è attivo, i primi 2 byte indicano la lunghezza effettiva
            if (netInfo.SetLength && reassembledData.Length >= 2)
            {
                ushort length = ProtocolHelpers.ReadUInt16LittleEndian(reassembledData);
                reassembledData = reassembledData.AsSpan(2, Math.Min(length, reassembledData.Length - 2)).ToArray();
            }

            LogDiagnostic($"Pacchetto completo: {reassembledData.Length} byte da {buffer.Chunks.Count} chunk");

            // Verifica lunghezza minima per un pacchetto di trasporto valido
            if (reassembledData.Length >= ProtocolConfig.MinTransportPacketLength)
            {
                PacketReassembled?.Invoke(reassembledData);
            }
            else
            {
                LogDiagnostic($"Pacchetto scartato: {reassembledData.Length} byte (minimo richiesto: {ProtocolConfig.MinTransportPacketLength})");
            }
        }

        /// <summary>
        /// Concatena tutti i chunk in un singolo array.
        /// </summary>
        private static byte[] ConcatenateChunks(IReadOnlyList<byte[]> chunks)
        {
            int totalLength = 0;
            foreach (byte[] chunk in chunks)
            {
                totalLength += chunk?.Length ?? 0;
            }

            byte[] result = new byte[totalLength];
            int position = 0;

            foreach (byte[] chunk in chunks)
            {
                if (chunk == null)
                {
                    continue;
                }

                Buffer.BlockCopy(chunk, 0, result, position, chunk.Length);
                position += chunk.Length;
            }

            return result;
        }

        /// <summary>
        /// Pulisce tutto lo stato di riassemblaggio in sospeso.
        /// Da chiamare in caso di reset della connessione o timeout.
        /// </summary>
        public void ClearReassemblyState()
        {
            lock (_reassemblyLock)
            {
                _packetQueues.Clear();
            }
        }

        /// <summary>
        /// Rilascia le risorse e pulisce lo stato interno.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _packetQueues.Clear();
        }

        /// <summary>
        /// Emette un messaggio diagnostico se sono presenti subscriber.
        /// </summary>
        private void LogDiagnostic(string message)
        {
            DiagnosticMessage?.Invoke($"[REASSEMBLY] {message}");
        }

        /// <summary>
        /// Buffer interno per l'accumulo dei chunk di un singolo pacchetto.
        /// </summary>
        private sealed class ReassemblyBuffer
        {
            public List<byte[]> Chunks { get; } = [];
        }
    }
}
