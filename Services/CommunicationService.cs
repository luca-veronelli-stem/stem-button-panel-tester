using Core.Enums;
using Core.Interfaces.Communication;
using Core.Interfaces.Services;
using Core.Models;
using Core.Results;
using Services.Models;

namespace Services
{
    /// <summary>
    /// Fornisce servizi di comunicazione astratti su vari canali (CAN, BLE, Serial).
    /// Usa il Result Pattern per gestione errori esplicita.
    /// </summary>
    public class CommunicationService : ICommunicationService, IAsyncDisposable
    {
        private readonly IProtocolManager _protocolManager;
        private readonly ICommunicationManagerFactory _managerFactory;

        private ICommunicationManager? _currentManager;
        private CommunicationChannel _activeChannel;
        private bool _disposed;

        private uint _senderId;
        private uint _recipientId;

        public event EventHandler<AppLayerDecoderEventArgs>? CommandDecoded;
        public event EventHandler<CommunicationErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Evento generato quando viene ricevuto un pacchetto raw dal canale di comunicazione.
        /// Utile per protocolli che non passano attraverso lo stack protocollare STEM (es. baptize).
        /// </summary>
        public event Action<uint, byte[]>? RawPacketReceived;

        public CommunicationService(
            IProtocolManager protocolManager,
            ICommunicationManagerFactory managerFactory)
        {
            _protocolManager = protocolManager ?? throw new ArgumentNullException(nameof(protocolManager));
            _managerFactory = managerFactory ?? throw new ArgumentNullException(nameof(managerFactory));

            _protocolManager.CommandDecoded += OnCommandDecoded;
            _protocolManager.ErrorOccurred += OnProtocolError;
        }

        public async Task<Result> SetActiveChannelAsync(
            CommunicationChannel channel,
            string config,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return Result.Failure(ErrorCodes.InvalidOperation, "Service has been disposed.");

            if (cancellationToken.IsCancellationRequested)
                return Result.Failure(ErrorCodes.Cancelled, "Operation was cancelled.");

            if (_currentManager != null && _activeChannel == channel && _currentManager.IsConnected)
            {
                return Result.Success();
            }

            // Disconnect and cleanup old manager (but don't dispose - it's a singleton)
            if (_currentManager != null)
            {
                await CleanupCurrentManagerAsync(cancellationToken).ConfigureAwait(false);
            }

            if (cancellationToken.IsCancellationRequested)
                return Result.Failure(ErrorCodes.Cancelled, "Operation was cancelled.");

            var newManager = _managerFactory.Create(channel);

            try
            {
                SubscribeToManagerEvents(newManager);

                bool connected = await newManager.ConnectAsync(config, cancellationToken).ConfigureAwait(false);
                if (!connected)
                {
                    UnsubscribeFromManagerEvents(newManager);
                    // NON disporre il manager - è un singleton e può essere riutilizzato
                    return Result.Failure(ErrorCodes.ConnectionFailed, $"Failed to connect to channel {channel}.");
                }

                _currentManager = newManager;
                _activeChannel = channel;
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                UnsubscribeFromManagerEvents(newManager);
                // NON disporre il manager
                return Result.Failure(ErrorCodes.Cancelled, "Connection was cancelled.");
            }
            catch (Exception ex)
            {
                UnsubscribeFromManagerEvents(newManager);
                // NON disporre il manager
                return Result.Failure(ex, ErrorCodes.ConnectionFailed);
            }
        }

        public async Task<Result<byte[]>> SendCommandAsync(
            ushort command,
            byte[] payload,
            bool waitAnswer,
            Func<byte[], bool>? responseValidator = null,
            int timeoutMs = ProtocolConstants.DefaultTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return Result<byte[]>.Failure(ErrorCodes.InvalidOperation, "Service has been disposed.");

            var channelResult = EnsureConnectedChannel();
            if (channelResult.IsFailure)
                return Result<byte[]>.Failure(channelResult.Error);

            try
            {
                var packets = _protocolManager.BuildPackets(command, payload, _senderId, _recipientId, _currentManager!.MaxPacketSize - 2);

                foreach (var packet in packets)
                {
                    bool success = await _currentManager.SendAsync([.. packet.NetInfo, .. packet.Chunk], packet.Id).ConfigureAwait(false);
                    if (!success)
                    {
                        return Result<byte[]>.Failure(ErrorCodes.SendFailed, "Failed to send packet.");
                    }
                }

                if (!waitAnswer)
                {
                    return Result<byte[]>.Success([]);
                }

                return await WaitForResponseAsync(responseValidator, timeoutMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Result<byte[]>.Failure(ErrorCodes.Cancelled, "Operation was cancelled.");
            }
            catch (TimeoutException)
            {
                return Result<byte[]>.Failure(ErrorCodes.Timeout, "Response timeout.");
            }
            catch (Exception ex)
            {
                var error = Error.FromException(ex, ErrorCodes.SendFailed);
                ErrorOccurred?.Invoke(this, new CommunicationErrorEventArgs(error.Message));
                return Result<byte[]>.Failure(error);
            }
        }

        private async Task<Result<byte[]>> WaitForResponseAsync(
            Func<byte[], bool>? responseValidator,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<byte[]>();

            void OnDecoded(object? sender, AppLayerDecoderEventArgs e)
            {
                if (responseValidator?.Invoke(e.Payload) ?? true)
                {
                    tcs.TrySetResult([.. e.Payload.Skip(2)]);
                }
            }

            _protocolManager.CommandDecoded += OnDecoded;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeoutMs);

                using var registration = cts.Token.Register(() => tcs.TrySetCanceled(cts.Token));

                var response = await tcs.Task.ConfigureAwait(false);
                return Result<byte[]>.Success(response);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Result<byte[]>.Failure(ErrorCodes.Timeout, "Response timeout.");
            }
            catch (OperationCanceledException)
            {
                return Result<byte[]>.Failure(ErrorCodes.Cancelled, "Operation was cancelled.");
            }
            finally
            {
                _protocolManager.CommandDecoded -= OnDecoded;
            }
        }

        /// <summary>
        /// Invia un pacchetto raw sul canale di comunicazione attivo.
        /// Bypassa lo stack protocollare STEM.
        /// </summary>
        public async Task<Result> SendRawPacketAsync(uint arbitrationId, byte[] data, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return Result.Failure(ErrorCodes.InvalidOperation, "Service has been disposed.");

            var channelResult = EnsureConnectedChannel();
            if (channelResult.IsFailure)
                return channelResult;

            if (cancellationToken.IsCancellationRequested)
                return Result.Failure(ErrorCodes.Cancelled, "Operation was cancelled.");

            try
            {
                bool success = await _currentManager!.SendAsync(data, arbitrationId).ConfigureAwait(false);

                return success
                    ? Result.Success()
                    : Result.Failure(ErrorCodes.SendFailed, "Failed to send raw packet.");
            }
            catch (Exception ex)
            {
                return Result.Failure(ex, ErrorCodes.SendFailed);
            }
        }

        public void SetSenderRecipientIds(uint senderId, uint recipientId)
        {
            _senderId = senderId;
            _recipientId = recipientId;
        }

        /// <summary>
        /// Verifica se il canale di comunicazione attivo è connesso.
        /// </summary>
        /// <returns>True se il canale è connesso, false altrimenti.</returns>
        public bool IsChannelConnected()
        {
            return _currentManager != null && _currentManager.IsConnected;
        }

        /// <summary>
        /// Disconnette il canale di comunicazione attivo.
        /// Libera le risorse e chiude la connessione hardware.
        /// </summary>
        public async Task<Result> DisconnectActiveChannelAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return Result.Failure(ErrorCodes.InvalidOperation, "Service has been disposed.");

            if (_currentManager == null)
                return Result.Success(); // Già disconnesso

            try
            {
                await CleanupCurrentManagerAsync(cancellationToken).ConfigureAwait(false);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex, ErrorCodes.ConnectionFailed);
            }
        }

        private Result EnsureConnectedChannel()
        {
            if (_currentManager == null)
                return Result.Failure(ErrorCodes.NoActiveChannel, "No active communication channel.");

            if (!_currentManager.IsConnected)
                return Result.Failure(ErrorCodes.ChannelNotConnected, "Communication channel is not connected.");

            return Result.Success();
        }

        private void SubscribeToManagerEvents(ICommunicationManager manager)
        {
            manager.PacketReceived += OnPacketReceived;
            manager.RawPacketReceived += OnRawPacketReceived;
            manager.ConnectionStatusChanged += OnConnectionStatusChanged;
        }

        private void UnsubscribeFromManagerEvents(ICommunicationManager manager)
        {
            manager.PacketReceived -= OnPacketReceived;
            manager.RawPacketReceived -= OnRawPacketReceived;
            manager.ConnectionStatusChanged -= OnConnectionStatusChanged;
        }

        private async Task CleanupCurrentManagerAsync(CancellationToken cancellationToken)
        {
            if (_currentManager == null) return;

            UnsubscribeFromManagerEvents(_currentManager);
            await _currentManager.DisconnectAsync(cancellationToken).ConfigureAwait(false);

            // NON disporre il manager - è un singleton gestito dal DI container
            // e deve poter essere riutilizzato per connessioni successive
            _currentManager = null;
        }

        private static async Task DisposeManagerAsync(ICommunicationManager manager)
        {
            if (manager is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
        }

        private void OnPacketReceived(object? sender, byte[] data)
        {
            var result = _protocolManager.ProcessReceivedPacket(data);
        }

        private void OnRawPacketReceived(uint arbitrationId, byte[] data)
        {
            RawPacketReceived?.Invoke(arbitrationId, data);
        }

        private void OnConnectionStatusChanged(object? sender, bool isConnected)
        {
        }

        private void OnCommandDecoded(object? sender, AppLayerDecoderEventArgs e)
        {
            CommandDecoded?.Invoke(this, e);
        }

        private void OnProtocolError(object? sender, ProtocolErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, new CommunicationErrorEventArgs($"Protocol error: {e.Message}"));
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Unsubscribe from protocol manager events
            _protocolManager.CommandDecoded -= OnCommandDecoded;
            _protocolManager.ErrorOccurred -= OnProtocolError;

            // Cleanup current manager
            if (_currentManager != null)
            {
                UnsubscribeFromManagerEvents(_currentManager);

                try
                {
                    await _currentManager.DisconnectAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Ignore disconnect errors during disposal
                }

                await DisposeManagerAsync(_currentManager).ConfigureAwait(false);
            }

            GC.SuppressFinalize(this);
        }
    }
}