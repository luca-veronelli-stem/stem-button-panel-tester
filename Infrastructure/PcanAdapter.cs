using Core.Interfaces.Infrastructure;
using Core.Models.Communication;
using Infrastructure.Lib;
using Microsoft.Extensions.Logging;
using Peak.Can.Basic;

namespace Infrastructure
{
    /// <summary>
    /// Adattatore per driver PEAK PCAN.
    /// Gestisce le operazioni hardware legate al canale CAN, inclusa la connessione,
    /// disconnessione, invio e ricezione di messaggi CAN.
    /// Include auto-recovery per gestire disconnessioni e errori del bus.
    /// </summary>
    public sealed class PcanAdapter : ICanAdapter
    {
        private const int POLL_INTERVAL_MS = 10;
        private const int STATUS_LOG_INTERVAL_LOOPS = 500;  // ~5 secondi
        private const int MAX_CONSECUTIVE_ERRORS = 10;
        private const int RECOVERY_DELAY_MS = 500;
        private const int MAX_RECOVERY_ATTEMPTS = 3;
        private const int NO_TRAFFIC_WARNING_MS = 30000;   // Aumentato perché ora usiamo heartbeat attivo nel service
        private const int NO_TRAFFIC_RECOVERY_MS = 60000;  // Aumentato - solo come fallback, l'heartbeat nel service rileverà prima
        private const int POST_RECOVERY_VERIFY_MS = 1500;
        private const int HEALTH_CHECK_INTERVAL_LOOPS = 100; // Aumentato a ~1 secondo

        private readonly IPcanApi _api;
        private readonly PcanChannel _channel;
        private readonly ILogger<PcanAdapter> _logger;
        private CancellationTokenSource? _cts;
        private Task? _readingTask;
        private string? _lastConfig;
        private DateTime _lastMessageTime;
        private DateTime _lastTxTime;
        private DateTime _lastRecoveryTime;
        private int _recoveryAttempts;
        private readonly object _recoveryLock = new();
        private bool _isRecovering;
        private volatile bool _isDisposed;
        private bool _recoveryVerificationPending;
        private long _rxCountAtRecovery;

        // Contatori diagnostici
        private long _totalRxCount;
        private long _totalTxCount;
        private long _totalTxFailCount;
        private long _totalReadErrors;
        private long _totalRecoveryAttempts;
        private long _failedRecoveryAttempts;
        private DateTime _connectionStartTime;
        private PcanStatus _lastBusStatus;
        private bool _noTrafficWarningLogged;

        /// <summary>
        /// Indica se l'adattatore è attualmente connesso al canale CAN.
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Evento generato quando lo stato della connessione cambia.
        /// </summary>
        public event EventHandler<bool>? ConnectionStatusChanged;

        /// <summary>
        /// Evento generato quando viene ricevuto un pacchetto CAN.
        /// </summary>
        public event EventHandler<CanPacket>? PacketReceived;

        /// <summary>
        /// Evento generato quando viene tentato un recovery automatico.
        /// Il parametro indica lo stato del tentativo (es. "Attempt 1/5", "SUCCESS", "FAILED").
        /// </summary>
        public event Action<string>? RecoveryAttempted;

        /// <summary>
        /// Evento generato quando è necessario un intervento fisico dell'utente (ricollegare USB).
        /// </summary>
        public event Action? PhysicalReconnectRequired;

        public PcanAdapter(IPcanApi api, ILogger<PcanAdapter> logger, PcanChannel channel = PcanChannel.Usb01)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _channel = channel;
            _lastMessageTime = DateTime.UtcNow;
            _lastTxTime = DateTime.UtcNow;
            _lastRecoveryTime = DateTime.MinValue;
        }

        /// <summary>
        /// Stabilisce la connessione al canale CAN con la configurazione specificata.
        /// </summary>
        public Task<bool> ConnectAsync(string config, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryParseConfig(config, out var baudRate))
            {
                _logger.LogWarning("Invalid CAN configuration: '{Config}'", config);
                return Task.FromResult(false);
            }

            _lastConfig = config;
            _recoveryAttempts = 0;

            // Reset contatori diagnostici
            _totalRxCount = 0;
            _totalTxCount = 0;
            _totalTxFailCount = 0;
            _totalReadErrors = 0;
            _totalRecoveryAttempts = 0;
            _failedRecoveryAttempts = 0;
            _noTrafficWarningLogged = false;
            _recoveryVerificationPending = false;

            return ConnectInternalAsync(baudRate, cancellationToken);
        }

        /// <summary>
        /// Connessione interna riutilizzabile per il recovery.
        /// </summary>
        private async Task<bool> ConnectInternalAsync(Bitrate baudRate, CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                return false;

            _logger.LogInformation("PCAN: Connecting to {Channel} at {BaudRate}", _channel, baudRate);

            // Reset completo del canale
            var uninitStatus = _api.Uninitialize(_channel);
            _logger.LogDebug("PCAN: Uninitialize result: {Status}", uninitStatus);

            // Attesa più lunga dopo uninitialize per dare tempo al driver
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);

            var status = _api.Initialize(_channel, baudRate);
            if (status != PcanStatus.OK)
            {
                _logger.LogError("PCAN: Initialization FAILED: {Status}", status);
                IsConnected = false;
                ConnectionStatusChanged?.Invoke(this, false);
                return false;
            }

            _logger.LogDebug("PCAN: Initialize OK, resetting bus...");

            // Reset del bus per eliminare errori residui
            var resetStatus = _api.Reset(_channel);
            _logger.LogDebug("PCAN: Reset result: {Status}", resetStatus);

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

            var busStatus = _api.GetStatus(_channel);
            _lastBusStatus = busStatus;
            _logger.LogInformation("PCAN: Connected to {Channel}. Initial bus status: {BusStatus}", _channel, busStatus);

            cancellationToken.ThrowIfCancellationRequested();

            IsConnected = true;
            _lastMessageTime = DateTime.UtcNow;
            _lastTxTime = DateTime.UtcNow;
            _connectionStartTime = DateTime.UtcNow;
            _noTrafficWarningLogged = false;
            ConnectionStatusChanged?.Invoke(this, true);

            // Atomically swap and dispose old CTS
            var oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
            if (oldCts != null)
            {
                try
                {
                    oldCts.Cancel();
                }
                catch (ObjectDisposedException) { }
                oldCts.Dispose();
            }

            _readingTask = StartReadingLoopAsync(_cts.Token);

            // Attendi che il loop di lettura sia effettivamente partito
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

            return true;
        }

        /// <summary>
        /// Disconnette l'adattatore dal canale CAN.
        /// </summary>
        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                return;

            cancellationToken.ThrowIfCancellationRequested();

            // Log statistiche finali
            var uptime = DateTime.UtcNow - _connectionStartTime;
            _logger.LogInformation(
                "PCAN: Disconnecting. Session stats: Uptime={Uptime:hh\\:mm\\:ss}, RX={RxCount}, TX={TxCount}, TxFail={TxFail}, ReadErrors={ReadErrors}, Recoveries={Recoveries} (Failed={FailedRecoveries})",
                uptime, _totalRxCount, _totalTxCount, _totalTxFailCount, _totalReadErrors, _totalRecoveryAttempts, _failedRecoveryAttempts);

            // Atomically get and clear the CTS to prevent race conditions
            var cts = Interlocked.Exchange(ref _cts, null);
            if (cts != null)
            {
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
            }

            if (_readingTask is not null)
            {
                try
                {
                    await _readingTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PCAN: Error stopping read loop");
                }
            }

            _api.Uninitialize(_channel);

            IsConnected = false;
            ConnectionStatusChanged?.Invoke(this, false);

            // Dispose the CTS after the reading task has completed
            cts?.Dispose();

            _logger.LogInformation("PCAN: Disconnected from {Channel}", _channel);
        }

        /// <summary>
        /// Invia un messaggio CAN sul bus.
        /// </summary>
        public Task<bool> Send(uint arbitrationId, byte[] data, bool isExtended = false)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("PCAN TX: Attempted send while disconnected. ID=0x{ArbitrationId:X8}", arbitrationId);
                return Task.FromResult(false);
            }

            if (data is null || data.Length == 0 || data.Length > 8)
            {
                _logger.LogWarning("PCAN TX: Invalid data. ID=0x{ArbitrationId:X8}, DataLength={Length}",
                    arbitrationId, data?.Length ?? -1);
                return Task.FromResult(false);
            }

            var msg = new PcanMessage
            {
                ID = arbitrationId,
                MsgType = isExtended ? MessageType.Extended : MessageType.Standard,
                DLC = (byte)data.Length,
            };

            for (int i = 0; i < data.Length; i++)
            {
                msg.Data[i] = data[i];
            }

            var status = _api.Write(_channel, msg);
            _lastTxTime = DateTime.UtcNow;

            if (status != PcanStatus.OK)
            {
                _totalTxFailCount++;

                // Log dettagliato dell'errore
                var postStatus = _api.GetStatus(_channel);
                _logger.LogWarning(
                    "PCAN TX FAILED: ID=0x{ArbitrationId:X8}, WriteStatus={WriteStatus}, BusStatus={BusStatus}, TotalTxFail={TotalFail}",
                    arbitrationId, status, postStatus, _totalTxFailCount);

                // Se l'invio fallisce, potrebbe essere necessario un recovery
                if (ShouldAttemptRecovery(status))
                {
                    _logger.LogWarning("PCAN: TX failure triggers recovery attempt");
                    _ = TryRecoveryAsync();
                }

                return Task.FromResult(false);
            }

            _totalTxCount++;
            _logger.LogDebug("PCAN TX OK: ID=0x{ArbitrationId:X8}, DLC={DLC}", arbitrationId, msg.DLC);

            return Task.FromResult(true);
        }

        /// <summary>
        /// Avvia il loop di lettura asincrono con monitoraggio della salute del bus.
        /// </summary>
        private async Task StartReadingLoopAsync(CancellationToken ct)
        {
            int loopCount = 0;
            int consecutiveErrors = 0;
            int consecutiveEmptyReads = 0;
            DateTime loopStartTime = DateTime.UtcNow;

            _logger.LogInformation("PCAN: Read loop STARTED on {Channel}", _channel);

            // Log immediato dello stato iniziale del bus
            var initialStatus = _api.GetStatus(_channel);
            _logger.LogDebug("PCAN: Initial bus status in read loop: {Status}", initialStatus);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    loopCount++;

                    // Health check periodico (ogni ~1 secondo)
                    if (loopCount % HEALTH_CHECK_INTERVAL_LOOPS == 0)
                    {
                        await PerformHealthCheckAsync(loopCount, consecutiveErrors, consecutiveEmptyReads, ct).ConfigureAwait(false);
                    }

                    // Log dettagliato periodico (ogni ~5 secondi)
                    if (loopCount % STATUS_LOG_INTERVAL_LOOPS == 0)
                    {
                        var busStatus = _api.GetStatus(_channel);
                        var timeSinceLastRx = DateTime.UtcNow - _lastMessageTime;
                        var timeSinceLastTx = DateTime.UtcNow - _lastTxTime;

                        _logger.LogDebug(
                            "PCAN STATUS: Loop={Loop}, TotalRX={TotalRx}, ConsecErr={ConsecErr}, ConsecEmpty={ConsecEmpty}, " +
                            "Bus={BusStatus}, LastRxAgo={LastRxMs}ms, LastTxAgo={LastTxMs}ms",
                            loopCount, _totalRxCount, consecutiveErrors, consecutiveEmptyReads,
                            busStatus, (int)timeSinceLastRx.TotalMilliseconds, (int)timeSinceLastTx.TotalMilliseconds);

                        // Aggiorna lo stato del bus memorizzato
                        if (busStatus != _lastBusStatus)
                        {
                            _logger.LogWarning("PCAN: Bus status CHANGED from {OldStatus} to {NewStatus}", _lastBusStatus, busStatus);
                            _lastBusStatus = busStatus;
                        }

                        // Verifica se il bus è in stato di errore grave
                        if (IsBusInErrorState(busStatus))
                        {
                            _logger.LogError("PCAN: Bus in ERROR state: {Status}, attempting recovery", busStatus);
                            await TryRecoveryAsync().ConfigureAwait(false);
                        }
                    }

                    var status = _api.Read(_channel, out var msg, out var timestamp);

                    if (status == PcanStatus.OK)
                    {
                        _totalRxCount++;
                        consecutiveErrors = 0;
                        consecutiveEmptyReads = 0;
                        _lastMessageTime = DateTime.UtcNow;
                        _recoveryAttempts = 0;
                        _noTrafficWarningLogged = false;

                        // Se eravamo in attesa di verifica recovery, ora sappiamo che ha funzionato
                        if (_recoveryVerificationPending)
                        {
                            _recoveryVerificationPending = false;
                            _logger.LogInformation("PCAN: Recovery VERIFIED - messages are being received again");
                        }

                        var payload = new byte[msg.DLC];
                        Array.Copy(msg.Data, payload, msg.DLC);

                        var packet = new CanPacket(
                            ArbitrationId: msg.ID,
                            IsExtended: msg.MsgType.HasFlag(MessageType.Extended),
                            Data: payload,
                            TimestampMicroseconds: timestamp);

                        _logger.LogDebug("PCAN RX: ID=0x{ArbitrationId:X8}, DLC={DLC}, TotalRX={Total}",
                            msg.ID, msg.DLC, _totalRxCount);

                        PacketReceived?.Invoke(this, packet);
                    }
                    else if (status == PcanStatus.ReceiveQueueEmpty)
                    {
                        // Nessun messaggio disponibile - normale
                        consecutiveErrors = 0;
                        consecutiveEmptyReads++;
                    }
                    else
                    {
                        consecutiveErrors++;
                        consecutiveEmptyReads = 0;
                        _totalReadErrors++;

                        // Log errori di lettura (primi 3, poi ogni 50)
                        if (consecutiveErrors <= 3 || consecutiveErrors % 50 == 0)
                        {
                            var busStatus = _api.GetStatus(_channel);
                            _logger.LogWarning(
                                "PCAN READ ERROR: Status={Status}, BusStatus={BusStatus}, Consecutive={ConsecErr}, TotalErrors={TotalErr}",
                                status, busStatus, consecutiveErrors, _totalReadErrors);
                        }

                        // Troppi errori consecutivi - tenta recovery
                        if (consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                        {
                            _logger.LogError("PCAN: Too many consecutive read errors ({Count}), attempting recovery", consecutiveErrors);

                            if (await TryRecoveryAsync().ConfigureAwait(false))
                            {
                                consecutiveErrors = 0;
                            }
                            else
                            {
                                // Recovery fallito, aspetta prima di riprovare
                                await Task.Delay(RECOVERY_DELAY_MS * 2, ct).ConfigureAwait(false);
                            }
                        }
                    }

                    try
                    {
                        await Task.Delay(POLL_INTERVAL_MS, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PCAN: Read loop CRASHED with exception");
                throw;
            }
            finally
            {
                var loopDuration = DateTime.UtcNow - loopStartTime;
                _logger.LogInformation(
                    "PCAN: Read loop STOPPED. Duration={Duration:hh\\:mm\\:ss}, Loops={LoopCount}, TotalRX={TotalRx}, TotalErrors={TotalErrors}",
                    loopDuration, loopCount, _totalRxCount, _totalReadErrors);
            }
        }

        /// <summary>
        /// Esegue un health check periodico per rilevare problemi di comunicazione.
        /// </summary>
        private async Task PerformHealthCheckAsync(int loopCount, int consecutiveErrors, int consecutiveEmptyReads, CancellationToken ct)
        {
            var timeSinceLastRx = DateTime.UtcNow - _lastMessageTime;

            // Verifica se il recovery precedente ha funzionato
            if (_recoveryVerificationPending)
            {
                var timeSinceRecovery = DateTime.UtcNow - _lastRecoveryTime;
                if (timeSinceRecovery.TotalMilliseconds > POST_RECOVERY_VERIFY_MS)
                {
                    // Il recovery è stato fatto ma non sono arrivati nuovi messaggi
                    if (_totalRxCount == _rxCountAtRecovery)
                    {
                        _failedRecoveryAttempts++;
                        _logger.LogError(
                            "PCAN: Recovery FAILED verification - no new messages received after {Seconds:F1}s. " +
                            "Physical USB reconnection may be required. FailedRecoveries={Failed}",
                            timeSinceRecovery.TotalSeconds, _failedRecoveryAttempts);

                        _recoveryVerificationPending = false;

                        // Se abbiamo fallito troppi recovery, notifica l'utente
                        if (_failedRecoveryAttempts >= MAX_RECOVERY_ATTEMPTS)
                        {
                            _logger.LogCritical(
                                "PCAN: Multiple recovery attempts failed. PHYSICAL USB RECONNECTION REQUIRED.");
                            PhysicalReconnectRequired?.Invoke();
                        }
                        else
                        {
                            // Prova un altro recovery con reset più aggressivo
                            await TryAggressiveRecoveryAsync().ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        _recoveryVerificationPending = false;
                        _logger.LogInformation("PCAN: Recovery verified via health check - messages resumed");
                    }
                }
            }

            // Warning se non riceviamo messaggi da troppo tempo
            if (timeSinceLastRx.TotalMilliseconds > NO_TRAFFIC_WARNING_MS && !_noTrafficWarningLogged)
            {
                _noTrafficWarningLogged = true;
                var busStatus = _api.GetStatus(_channel);
                _logger.LogWarning(
                    "PCAN: NO TRAFFIC for {Seconds:F1} seconds! BusStatus={BusStatus}, TotalRX={TotalRx}, ConsecEmpty={ConsecEmpty}",
                    timeSinceLastRx.TotalSeconds, busStatus, _totalRxCount, consecutiveEmptyReads);
            }

            // Recovery automatico se non riceviamo messaggi per troppo tempo
            if (timeSinceLastRx.TotalMilliseconds > NO_TRAFFIC_RECOVERY_MS && !_recoveryVerificationPending)
            {
                var busStatus = _api.GetStatus(_channel);
                _logger.LogError(
                    "PCAN: NO TRAFFIC TIMEOUT ({Seconds:F1}s), forcing recovery. BusStatus={BusStatus}",
                    timeSinceLastRx.TotalSeconds, busStatus);

                // Reset il flag per permettere nuovi warning dopo il recovery
                _noTrafficWarningLogged = false;

                await TryRecoveryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Verifica se il bus è in uno stato di errore che richiede recovery.
        /// </summary>
        private static bool IsBusInErrorState(PcanStatus status)
        {
            return status == PcanStatus.BusLight ||
                   status == PcanStatus.BusHeavy ||
                   status == PcanStatus.BusPassive ||
                   status == PcanStatus.BusOff ||
                   status == PcanStatus.Unknown;
        }

        /// <summary>
        /// Verifica se lo stato di errore giustifica un tentativo di recovery.
        /// </summary>
        private static bool ShouldAttemptRecovery(PcanStatus status)
        {
            return status == PcanStatus.BusOff ||
                   status == PcanStatus.BusHeavy ||
                   status == PcanStatus.BusPassive ||
                   status == PcanStatus.Initialize ||
                   status == PcanStatus.Unknown;
        }

        /// <summary>
        /// Tenta il recovery della connessione CAN.
        /// </summary>
        private async Task<bool> TryRecoveryAsync()
        {
            // Don't attempt recovery if disposing
            if (_isDisposed)
                return false;

            lock (_recoveryLock)
            {
                if (_isRecovering)
                {
                    _logger.LogDebug("PCAN: Recovery already in progress, skipping");
                    return false;
                }
                _isRecovering = true;
            }

            try
            {
                _totalRecoveryAttempts++;
                _recoveryAttempts++;

                if (_recoveryAttempts > MAX_RECOVERY_ATTEMPTS)
                {
                    _logger.LogError("PCAN: Max recovery attempts ({Max}) reached. PHYSICAL USB RECONNECTION REQUIRED.",
                        MAX_RECOVERY_ATTEMPTS);
                    RecoveryAttempted?.Invoke("FAILED: Max attempts reached - reconnect USB");
                    PhysicalReconnectRequired?.Invoke();
                    return false;
                }

                // Log stato dettagliato prima del recovery
                var preStatus = _api.GetStatus(_channel);
                _logger.LogWarning(
                    "PCAN: Starting recovery (attempt {Attempt}/{Max}). PreStatus={PreStatus}, TotalRecoveries={Total}",
                    _recoveryAttempts, MAX_RECOVERY_ATTEMPTS, preStatus, _totalRecoveryAttempts);
                RecoveryAttempted?.Invoke($"Attempt {_recoveryAttempts}/{MAX_RECOVERY_ATTEMPTS}");

                // Salva il conteggio RX corrente per la verifica
                _rxCountAtRecovery = _totalRxCount;
                _lastRecoveryTime = DateTime.UtcNow;

                // Prova direttamente il full reconnect (il semplice reset non funziona per questo problema)
                return await PerformFullReconnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PCAN: Recovery attempt threw exception");
                return false;
            }
            finally
            {
                lock (_recoveryLock)
                {
                    _isRecovering = false;
                }
            }
        }

        /// <summary>
        /// Tenta un recovery più aggressivo con multiple re-inizializzazioni.
        /// </summary>
        private async Task<bool> TryAggressiveRecoveryAsync()
        {
            if (_isDisposed)
                return false;

            lock (_recoveryLock)
            {
                if (_isRecovering)
                    return false;
                _isRecovering = true;
            }

            try
            {
                _logger.LogWarning("PCAN: Attempting AGGRESSIVE recovery (multiple reinit cycles)");
                RecoveryAttempted?.Invoke("Aggressive recovery");

                // Ciclo di uninit/init multiplo per tentare di "svegliare" il driver
                for (int i = 0; i < 3; i++)
                {
                    _logger.LogDebug("PCAN: Aggressive recovery cycle {Cycle}/3", i + 1);

                    _api.Uninitialize(_channel);
                    await Task.Delay(250).ConfigureAwait(false);  // Ridotto da 500 a 250

                    if (_isDisposed) return false;
                }

                // Attesa più lunga prima della re-inizializzazione finale
                await Task.Delay(500).ConfigureAwait(false);  // Ridotto da 1000 a 500

                if (_isDisposed) return false;

                var success = await PerformFullReconnectAsync().ConfigureAwait(false);

                if (success)
                {
                    _logger.LogInformation("PCAN: Aggressive recovery completed - waiting for verification");
                }
                else
                {
                    _logger.LogError("PCAN: Aggressive recovery FAILED. Physical USB reconnection required.");
                    PhysicalReconnectRequired?.Invoke();
                }

                return success;
            }
            finally
            {
                lock (_recoveryLock)
                {
                    _isRecovering = false;
                }
            }
        }

        /// <summary>
        /// Esegue un full reconnect (uninit + init).
        /// </summary>
        private async Task<bool> PerformFullReconnectAsync()
        {
            _logger.LogDebug("PCAN: Performing full reconnect");

            var uninitStatus = _api.Uninitialize(_channel);
            _logger.LogDebug("PCAN: Uninitialize returned: {Status}", uninitStatus);

            await Task.Delay(RECOVERY_DELAY_MS).ConfigureAwait(false);

            if (_isDisposed) return false;

            if (_lastConfig != null && TryParseConfig(_lastConfig, out var baudRate))
            {
                var initStatus = _api.Initialize(_channel, baudRate);
                _logger.LogDebug("PCAN: Re-initialize returned: {Status}", initStatus);

                if (initStatus == PcanStatus.OK)
                {
                    var resetStatus = _api.Reset(_channel);
                    _logger.LogDebug("PCAN: Reset returned: {Status}", resetStatus);

                    await Task.Delay(200).ConfigureAwait(false);

                    var finalStatus = _api.GetStatus(_channel);
                    _logger.LogDebug("PCAN: Final status after full reconnect: {Status}", finalStatus);

                    if (finalStatus == PcanStatus.OK || finalStatus == PcanStatus.ReceiveQueueEmpty)
                    {
                        _logger.LogInformation("PCAN: Full reconnect completed - waiting for message verification");
                        RecoveryAttempted?.Invoke("Reconnected - verifying...");
                        _lastBusStatus = finalStatus;
                        _recoveryVerificationPending = true;
                        _rxCountAtRecovery = _totalRxCount;
                        _lastRecoveryTime = DateTime.UtcNow;
                        return true;
                    }
                }
            }

            _logger.LogWarning("PCAN: Full reconnect failed");
            return false;
        }

        /// <summary>
        /// Forza un tentativo di recovery manuale.
        /// </summary>
        public async Task<bool> ForceRecoveryAsync()
        {
            _logger.LogInformation("PCAN: MANUAL recovery requested");
            _recoveryAttempts = 0;
            _failedRecoveryAttempts = 0;
            return await TryAggressiveRecoveryAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Ottiene le statistiche diagnostiche correnti.
        /// </summary>
        public string GetDiagnostics()
        {
            var timeSinceLastRx = DateTime.UtcNow - _lastMessageTime;
            var uptime = IsConnected ? DateTime.UtcNow - _connectionStartTime : TimeSpan.Zero;
            var busStatus = IsConnected ? _api.GetStatus(_channel) : PcanStatus.Unknown;

            return $"PCAN Diagnostics:\n" +
                   $"  Connected: {IsConnected}\n" +
                   $"  Channel: {_channel}\n" +
                   $"  Bus Status: {busStatus}\n" +
                   $"  Uptime: {uptime:hh\\:mm\\:ss}\n" +
                   $"  Total RX: {_totalRxCount}\n" +
                   $"  Total TX: {_totalTxCount}\n" +
                   $"  TX Failures: {_totalTxFailCount}\n" +
                   $"  Read Errors: {_totalReadErrors}\n" +
                   $"  Recovery Attempts: {_totalRecoveryAttempts}\n" +
                   $"  Failed Recoveries: {_failedRecoveryAttempts}\n" +
                   $"  Last RX: {timeSinceLastRx.TotalSeconds:F1}s ago\n" +
                   $"  Is Recovering: {_isRecovering}\n" +
                   $"  Recovery Pending Verification: {_recoveryVerificationPending}";
        }

        /// <summary>
        /// Tenta di interpretare la stringa di configurazione e restituisce il baud rate corrispondente.
        /// </summary>
        private static bool TryParseConfig(string config, out Bitrate baudRate)
        {
            baudRate = Bitrate.Pcan250;

            if (string.IsNullOrWhiteSpace(config) || !int.TryParse(config.Trim(), out var value))
            {
                return false;
            }

            if (value <= 1000)
                value *= 1000;

            baudRate = value switch
            {
                100000 => Bitrate.Pcan100,
                125000 => Bitrate.Pcan125,
                250000 => Bitrate.Pcan250,
                500000 => Bitrate.Pcan500,
                800000 => Bitrate.Pcan800,
                1000000 => Bitrate.Pcan1000,
                _ => Bitrate.Pcan250
            };

            return true;
        }

        /// <summary>
        /// Libera le risorse utilizzate dall'adattatore.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _logger.LogDebug("PCAN: DisposeAsync called");

            await DisconnectAsync().ConfigureAwait(false);

            // Final cleanup - atomically get and dispose any remaining CTS
            var cts = Interlocked.Exchange(ref _cts, null);
            cts?.Dispose();
        }
    }
}
