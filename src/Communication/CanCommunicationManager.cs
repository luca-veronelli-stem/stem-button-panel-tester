using Communication.Protocol.Layers;
using Core.Interfaces.Infrastructure;
using Core.Models.Communication;

namespace Communication
{
    /// <summary>
    /// Gestore specifico per CAN. Gestisce connessione, invio e ricezione.
    /// Uses NetworkLayerReassembler for multi-frame CAN message reassembly.
    /// Logging is delegated to Microsoft.Extensions.Logging through manager factories.
    /// </summary>
    public class CanCommunicationManager : BaseCommunicationManager
    {
        private readonly ICanAdapter _adapter;
        private readonly NetworkLayerReassembler _reassembler = new();

        public override int MaxPacketSize => 8;

        public CanCommunicationManager(ICanAdapter adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));

            _adapter.ConnectionStatusChanged += OnAdapterConnectionChanged;
            _adapter.PacketReceived += OnAdapterPacketReceived;

            _reassembler.PacketReassembled += OnPacketReassembled;
            _reassembler.DiagnosticMessage += OnNetworkLayerDiagnosticMessage;
        }

        // Implementa ConnectAsync per CAN
        public override async Task<bool> ConnectAsync(string config, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Clear any pending reassembly state
            _reassembler.ClearReassemblyState();

            var result = await _adapter.ConnectAsync(config, cancellationToken).ConfigureAwait(false);
            return result;
        }

        // Implementa DisconnectAsync per CAN
        public override async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Clear reassembly state
            _reassembler.ClearReassemblyState();

            await _adapter.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }

        // Implementa SendAsync per CAN
        public override async Task<bool> SendAsync(byte[] data, uint? arbitrationId = null)
        {
            if (arbitrationId == null)
            {
                throw new ArgumentNullException(nameof(arbitrationId), "CAN requires Arbitration ID");
            }

            // Il protocollo STEM usa SEMPRE ID estesi (29-bit).
            // Anche se l'ID può stare in 11 bit (< 0x7FF), deve essere inviato come Extended.
            const bool useExtendedId = true;

            var result = await _adapter.Send(arbitrationId.Value, data, useExtendedId).ConfigureAwait(false);
            return result;
        }

        // Gestori degli eventi
        private void OnAdapterConnectionChanged(object? sender, bool connected)
        {
            RaiseConnectionChanged(connected);
        }

        private void OnAdapterPacketReceived(object? sender, CanPacket packet)
        {
            // Raise raw packet event before reassembly (for protocols that don't use STEM stack)
            RaiseRawPacketReceived(packet.ArbitrationId, packet.Data);

            // Delegate to reassembler instance for reassembly
            _reassembler.ProcessReceivedChunk(packet.Data);
        }

        private void OnPacketReassembled(byte[] data)
        {
            RaisePacketReceived(data);
        }

        private void OnNetworkLayerDiagnosticMessage(string message)
        {
            // Ignore - diagnostic messages are now handled via ILogger in actual implementations
        }

        // Implementa IAsyncDisposable
        public override async ValueTask DisposeAsync()
        {
            // Disiscriviti dagli eventi
            _adapter.ConnectionStatusChanged -= OnAdapterConnectionChanged;
            _adapter.PacketReceived -= OnAdapterPacketReceived;

            _reassembler.PacketReassembled -= OnPacketReassembled;
            _reassembler.DiagnosticMessage -= OnNetworkLayerDiagnosticMessage;

            // Dispose the reassembler
            _reassembler.Dispose();

            // Elimina l'adattatore
            await _adapter.DisposeAsync().ConfigureAwait(false);

            // Chiama il finalizzatore della classe base
            GC.SuppressFinalize(this);
        }
    }
}
