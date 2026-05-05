using Core.Interfaces.Communication;

namespace Communication
{
    /// <summary>
    /// Base astratta per i gestori di comunicazione, Implementa la logica condivisa da ICommunicationManager
    /// Le implementazioni concrete sovrascrivono quelle astratte per comportamenti specifici del canale
    /// </summary>
    public abstract class BaseCommunicationManager : ICommunicationManager
    {
        public event EventHandler<bool>? ConnectionStatusChanged;
        public event EventHandler<byte[]>? PacketReceived;
        public event Action<uint, byte[]>? RawPacketReceived;

        public abstract int MaxPacketSize { get; }

        public bool IsConnected { get; protected set; }

        // Solleva l'evento al cambio di stato della connessione
        protected void RaiseConnectionChanged(bool isConnected)
        {
            IsConnected = isConnected;
            ConnectionStatusChanged?.Invoke(this, isConnected);
        }

        // Solleva l'evento alla ricezione di un pacchetto (dopo reassembly)
        protected void RaisePacketReceived(byte[] data)
        {
            PacketReceived?.Invoke(this, data);
        }

        /// <summary>
        /// Solleva l'evento alla ricezione di un pacchetto raw (prima del reassembly).
        /// </summary>
        protected void RaiseRawPacketReceived(uint arbitrationId, byte[] data)
        {
            RawPacketReceived?.Invoke(arbitrationId, data);
        }

        public abstract Task<bool> ConnectAsync(string config, CancellationToken cancellationToken = default);
        public abstract Task DisconnectAsync(CancellationToken cancellationToken = default);
        public abstract Task<bool> SendAsync(byte[] data, uint? arbitrationId = null);

        // Virtual DisposeAsync da sovrascrivere per le sottoclassi
        public abstract ValueTask DisposeAsync();
    }
}
