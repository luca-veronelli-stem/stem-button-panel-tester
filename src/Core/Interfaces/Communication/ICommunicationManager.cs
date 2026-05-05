namespace Core.Interfaces.Communication
{
    /// <summary>
    /// Contratto per i gestori delle comunicazioni (CAN, BLE, Seriale).
    /// Minimale: connette, disconnette, spedisce, e riceve tramite eventi.
    /// </summary>
    public interface ICommunicationManager : IAsyncDisposable
    {
        // Evento sollevato quando lo stato della connessione cambia
        event EventHandler<bool> ConnectionStatusChanged;

        // Evento sollevato quando un pacchetto viene ricevuto (dopo reassembly)
        event EventHandler<byte[]> PacketReceived;

        /// <summary>
        /// Evento sollevato quando viene ricevuto un pacchetto raw (prima del reassembly).
        /// Il primo parametro è l'arbitration ID (per CAN), il secondo sono i dati raw.
        /// Utile per protocolli che non usano lo stack protocollare STEM.
        /// </summary>
        event Action<uint, byte[]>? RawPacketReceived;

        // Ottieni la dimensione massima del pacchetto supportata dal canale
        int MaxPacketSize { get; }

        // Ottieni lo stato di connessione attuale
        bool IsConnected { get; }

        // Si connette al canale con una configurazione (e.g. baud rate per CAN)
        Task<bool> ConnectAsync(string config, CancellationToken cancellationToken = default);

        // Si disconnette dal canale
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        // Manda dati attraverso il canale
        Task<bool> SendAsync(byte[] data, uint? arbitrationId = null);
    }
}
