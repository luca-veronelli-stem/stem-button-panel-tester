using System.Text;
using Core.Enums;
using Core.Interfaces.Data;
using Core.Interfaces.Infrastructure;
using Core.Interfaces.Services;
using Core.Models;
using Core.Models.Services;
using Core.Results;
using Microsoft.Extensions.Logging;
using Services.Helpers;
using Services.Lib;
using Services.Models;

namespace Services
{
    /// <summary>
    /// Implementazione del servizio di test delle pulsantiere.
    /// Utilizza una macchina a stati finiti per gestire il flusso del test.
    /// Include funzionalità di battezzamento per assegnare indirizzi STEM ai dispositivi.
    /// Include monitoraggio della comunicazione tramite heartbeat attivo e recovery in caso di interruzione.
    /// </summary>
    public class ButtonPanelTestService : IButtonPanelTestService
    {
        private readonly ICommunicationService _communicationService;
        private readonly IBaptizeService _baptizeService;
        private readonly ILogger<ButtonPanelTestService>? _logger;
        private IProtocolRepository _protocolRepository;
        private readonly TimeSpan _buttonPressTimeout;
        private uint? _lastRecipientId;
        private ButtonPanelType? _currentPanelType;
        private byte[]? _currentDeviceUuid;

        private ICanAdapter? _canAdapter;
        private readonly object _heartbeatLock = new();
        private bool _heartbeatEnabled;
        private bool _communicationLostNotified;
        private int _missedHeartbeats;
        private CancellationTokenSource? _heartbeatCts;
        private Task? _heartbeatTask;

        // Macchina a stati per la gestione del test
        private readonly ButtonPanelTestStateMachine _stateMachine;

        /// <summary>
        /// Evento generato quando lo stato del test cambia.
        /// </summary>
        public event Action<ButtonPanelTestState, ButtonPanelTestState>? StateChanged;

        /// <summary>
        /// Evento generato quando viene rilevata un'interruzione della comunicazione CAN.
        /// </summary>
        public event Action? CommunicationLost;

        /// <summary>
        /// Stato corrente del test.
        /// </summary>
        public ButtonPanelTestState CurrentState => _stateMachine.CurrentState;

        /// <summary>
        /// Indica se un test è attualmente in esecuzione.
        /// </summary>
        public bool IsTestRunning => _stateMachine.IsRunning;

        public ButtonPanelTestService(
            ICommunicationService communicationService,
            IBaptizeService baptizeService,
            IProtocolRepository protocolRepository,
            ILogger<ButtonPanelTestService>? logger = null,
            TimeSpan? buttonPressTimeout = null)
        {
            _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
            _baptizeService = baptizeService ?? throw new ArgumentNullException(nameof(baptizeService));
            _protocolRepository = protocolRepository ?? throw new ArgumentNullException(nameof(protocolRepository));
            _logger = logger;
            _buttonPressTimeout = buttonPressTimeout ?? TimeSpan.FromSeconds(5);
            _heartbeatEnabled = false;
            _communicationLostNotified = false;
            _missedHeartbeats = 0;

            // Inizializza la macchina a stati
            _stateMachine = new ButtonPanelTestStateMachine();
            _stateMachine.StateChanged += OnStateChanged;
        }

        public void SetCanAdapter(ICanAdapter? canAdapter)
        {
            // Rimuovi sottoscrizione dal vecchio adapter se presente
            if (_canAdapter != null)
            {
                _canAdapter.PhysicalReconnectRequired -= OnPhysicalReconnectRequired;
            }

            _canAdapter = canAdapter;

            if (_canAdapter != null)
            {
                // Sottoscrivi all'evento di riconnessione fisica richiesta
                _canAdapter.PhysicalReconnectRequired += OnPhysicalReconnectRequired;
                _logger?.LogInformation("CAN adapter configurato per recovery automatico");
            }
        }

        /// <summary>
        /// Gestisce l'evento di riconnessione fisica richiesta dall'adattatore CAN.
        /// </summary>
        private void OnPhysicalReconnectRequired()
        {
            _logger?.LogError("L'adattatore CAN richiede la riconnessione fisica del dispositivo USB");
            NotifyCommunicationLost();
        }

        /// <summary>
        /// Notifica la perdita di comunicazione all'interfaccia utente e disconnette la comunicazione.
        /// </summary>
        private void NotifyCommunicationLost()
        {
            lock (_heartbeatLock)
            {
                if (!_communicationLostNotified)
                {
                    _communicationLostNotified = true;
                    _logger?.LogError("Comunicazione CAN interrotta. È necessario l'intervento dell'utente.");

                    // Ferma il loop di heartbeat
                    StopHeartbeat();

                    // Cancella il test e resetta la macchina a stati
                    _stateMachine.Cancel();
                    _stateMachine.Reset();

                    // Disconnetti la comunicazione in modo asincrono
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await DisconnectCommunicationAsync().ConfigureAwait(false);
                            _logger?.LogInformation("Comunicazione CAN disconnessa dopo perdita comunicazione");
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Errore durante la disconnessione dopo perdita comunicazione");
                        }
                    });

                    // Notifica l'interfaccia utente
                    CommunicationLost?.Invoke();
                }
            }
        }

        private void OnStateChanged(ButtonPanelTestState oldState, ButtonPanelTestState newState)
        {
            StateChanged?.Invoke(oldState, newState);
        }

        /// <summary>
        /// Esegue il battezzamento di un dispositivo, assegnandogli l'indirizzo STEM corretto.
        /// Dopo il battezzamento, aggiorna automaticamente il recipientId per la comunicazione.
        /// </summary>
        public async Task<BaptizeResult> BaptizeDeviceAsync(
            ButtonPanelType panel_type,
            int timeoutMs = ProtocolConstants.DefaultTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            BaptizeResult result = await _baptizeService.BaptizeAsync(panel_type, timeoutMs, cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                UpdateDeviceInfo(result.AssignedAddress, result.MacAddress);
            }

            return result;
        }

        /// <summary>
        /// Cerca dispositivi non battezzati sul bus CAN.
        /// </summary>
        public async Task<List<byte[]>> ScanForUnbaptizedDevicesAsync(
            int timeoutMs = ProtocolConstants.DefaultTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            return await _baptizeService.ScanForDevicesAsync(timeoutMs, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Collauda l'intera pulsantiera usando la macchina a stati.
        /// </summary>
        public async Task<List<ButtonPanelTestResult>> TestAllAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            Func<string, Task> userPrompt,
            Action<int>? onButtonStart = null,
            Action<int, bool>? onButtonResult = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<ButtonPanelTestResult>();
            var panel = ButtonPanel.GetByType(panelType);

            // Avvia la FSM
            if (!_stateMachine.StartTest(panelType, ButtonPanelTestType.Complete, panel))
            {
                return [TestResultFactory.CreateError(panelType, ButtonPanelTestType.Complete, "Test già in esecuzione", _currentDeviceUuid)];
            }

            try
            {
                // STATO: Initializing
                Result setupResult = await EnsureCommunicationSetupAsync(panelType, cancellationToken).ConfigureAwait(false);
                if (setupResult.IsFailure)
                {
                    _stateMachine.SetError(setupResult.Error.Message);
                    return [TestResultFactory.CreateError(panelType, ButtonPanelTestType.Complete, setupResult.Error.ToString(), _currentDeviceUuid)];
                }
                _stateMachine.InitializationComplete();

                // STATO: AwaitingButtonPress (test pulsanti)
                ButtonPanelTestResult buttonsResult = await ExecuteButtonTestsAsync(panel, userPrompt, onButtonStart, onButtonResult, cancellationToken).ConfigureAwait(false);
                results.Add(buttonsResult);
                if (buttonsResult.Interrupted)
                {
                    return results;
                }

                // STATO: TestingLed (se la pulsantiera ha LED)
                if (_stateMachine.CurrentState == ButtonPanelTestState.TestingLed)
                {
                    ButtonPanelTestResult ledResult = await ExecuteLedTestAsync(panelType, userConfirm, cancellationToken).ConfigureAwait(false);
                    results.Add(ledResult);
                    if (ledResult.Interrupted)
                    {
                        return results;
                    }
                }

                // STATO: TestingBuzzer
                if (_stateMachine.CurrentState == ButtonPanelTestState.TestingBuzzer)
                {
                    ButtonPanelTestResult buzzerResult = await ExecuteBuzzerTestAsync(panelType, userConfirm, cancellationToken).ConfigureAwait(false);
                    results.Add(buzzerResult);
                }

                return results;
            }
            catch (OperationCanceledException)
            {
                _stateMachine.Cancel();
                results.Add(TestResultFactory.CreateInterrupted(panelType, ButtonPanelTestType.Complete, _currentDeviceUuid));
                return results;
            }
            catch (Exception ex)
            {
                _stateMachine.SetError(ex.Message);
                results.Add(TestResultFactory.CreateError(panelType, ButtonPanelTestType.Complete, $"Errore: {ex.Message}", _currentDeviceUuid));
                return results;
            }
            finally
            {
                if (_stateMachine.IsTerminal)
                {
                    _stateMachine.Reset();
                }

                // Reset panel to virgin so the next host machine handles auto-addressing.
                await ResetPanelToVirginAsync().ConfigureAwait(false);

                // Disconnetti il canale CAN al termine del test
                await DisconnectCommunicationAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Collauda solo i pulsanti della pulsantiera.
        /// </summary>
        public async Task<ButtonPanelTestResult> TestButtonsAsync(
            ButtonPanelType panelType,
            Func<string, Task> userPrompt,
            Action<int>? onButtonStart = null,
            Action<int, bool>? onButtonResult = null,
            CancellationToken cancellationToken = default)
        {
            var panel = ButtonPanel.GetByType(panelType);

            if (!_stateMachine.StartTest(panelType, ButtonPanelTestType.Buttons, panel))
            {
                return TestResultFactory.CreateError(panelType, ButtonPanelTestType.Buttons, "Test già in esecuzione", _currentDeviceUuid);
            }

            try
            {
                Result setupResult = await EnsureCommunicationSetupAsync(panelType, cancellationToken).ConfigureAwait(false);
                if (setupResult.IsFailure)
                {
                    _stateMachine.SetError(setupResult.Error.Message);
                    return TestResultFactory.CreateError(panelType, ButtonPanelTestType.Buttons, setupResult.Error.ToString(), _currentDeviceUuid);
                }
                _stateMachine.InitializationComplete();

                return await ExecuteButtonTestsAsync(panel, userPrompt, onButtonStart, onButtonResult, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _stateMachine.Cancel();
                return TestResultFactory.CreateInterrupted(panelType, ButtonPanelTestType.Buttons, _currentDeviceUuid);
            }
            catch (Exception ex)
            {
                _stateMachine.SetError(ex.Message);
                return TestResultFactory.CreateError(panelType, ButtonPanelTestType.Buttons, ex.Message, _currentDeviceUuid);
            }
            finally
            {
                if (_stateMachine.IsTerminal)
                {
                    _stateMachine.Reset();
                }

                // Reset panel to virgin so the next host machine handles auto-addressing.
                await ResetPanelToVirginAsync().ConfigureAwait(false);

                // Disconnetti il canale CAN al termine del test
                await DisconnectCommunicationAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Collauda i LED della pulsantiera.
        /// </summary>
        public async Task<ButtonPanelTestResult> TestLedAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            CancellationToken cancellationToken = default)
        {
            var panel = ButtonPanel.GetByType(panelType);

            if (!panel.HasLed)
            {
                return TestResultFactory.CreateSkipped(panelType, ButtonPanelTestType.Led, "No LED on this panel", _currentDeviceUuid);
            }

            if (!_stateMachine.StartTest(panelType, ButtonPanelTestType.Led, panel))
            {
                return TestResultFactory.CreateError(panelType, ButtonPanelTestType.Led, "Test già in esecuzione", _currentDeviceUuid);
            }

            try
            {
                Result setupResult = await EnsureCommunicationSetupAsync(panelType, cancellationToken).ConfigureAwait(false);
                if (setupResult.IsFailure)
                {
                    _stateMachine.SetError(setupResult.Error.Message);
                    return TestResultFactory.CreateError(panelType, ButtonPanelTestType.Led, setupResult.Error.ToString(), _currentDeviceUuid);
                }
                _stateMachine.InitializationComplete();

                return await ExecuteLedTestAsync(panelType, userConfirm, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _stateMachine.Cancel();
                return TestResultFactory.CreateInterrupted(panelType, ButtonPanelTestType.Led, _currentDeviceUuid);
            }
            catch (Exception ex)
            {
                _stateMachine.SetError(ex.Message);
                return TestResultFactory.CreateError(panelType, ButtonPanelTestType.Led, ex.Message, _currentDeviceUuid);
            }
            finally
            {
                if (_stateMachine.IsTerminal)
                {
                    _stateMachine.Reset();
                }

                // Reset panel to virgin so the next host machine handles auto-addressing.
                await ResetPanelToVirginAsync().ConfigureAwait(false);

                // Disconnetti il canale CAN al termine del test
                await DisconnectCommunicationAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Collauda il buzzer della pulsantiera.
        /// </summary>
        public async Task<ButtonPanelTestResult> TestBuzzerAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            CancellationToken cancellationToken = default)
        {
            var panel = ButtonPanel.GetByType(panelType);

            if (!_stateMachine.StartTest(panelType, ButtonPanelTestType.Buzzer, panel))
            {
                return TestResultFactory.CreateError(panelType, ButtonPanelTestType.Buzzer, "Test già in esecuzione", _currentDeviceUuid);
            }

            try
            {
                Result setupResult = await EnsureCommunicationSetupAsync(panelType, cancellationToken).ConfigureAwait(false);
                if (setupResult.IsFailure)
                {
                    _stateMachine.SetError(setupResult.Error.Message);
                    return TestResultFactory.CreateError(panelType, ButtonPanelTestType.Buzzer, setupResult.Error.ToString(), _currentDeviceUuid);
                }
                _stateMachine.InitializationComplete();

                return await ExecuteBuzzerTestAsync(panelType, userConfirm, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _stateMachine.Cancel();
                return TestResultFactory.CreateInterrupted(panelType, ButtonPanelTestType.Buzzer, _currentDeviceUuid);
            }
            catch (Exception ex)
            {
                _stateMachine.SetError(ex.Message);
                return TestResultFactory.CreateError(panelType, ButtonPanelTestType.Buzzer, ex.Message, _currentDeviceUuid);
            }
            finally
            {
                if (_stateMachine.IsTerminal)
                {
                    _stateMachine.Reset();
                }

                // Reset panel to virgin so the next host machine handles auto-addressing.
                await ResetPanelToVirginAsync().ConfigureAwait(false);

                // Disconnetti il canale CAN al termine del test
                await DisconnectCommunicationAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Annulla il test in corso.
        /// </summary>
        public void CancelTest()
        {
            _stateMachine.Cancel();
        }

        /// <summary>
        /// Forza la disconnessione della comunicazione CAN e ferma tutti i monitoraggi in corso.
        /// Resetta anche la macchina a stati per permettere l'avvio di un nuovo test.
        /// </summary>
        public async Task ForceDisconnectAsync()
        {
            _logger?.LogInformation("Richiesta di disconnessione forzata");

            // Cancella il test in corso e resetta la macchina a stati
            _stateMachine.Cancel();
            _stateMachine.Reset();

            // Ferma il loop di heartbeat
            StopHeartbeat();

            // Disconnetti la comunicazione
            await DisconnectCommunicationAsync().ConfigureAwait(false);

            _logger?.LogInformation("Disconnessione forzata completata");
        }

        public void SetProtocolRepository(IProtocolRepository repository)
        {
            _protocolRepository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// Reassign device address: unbaptize then baptize for the specified panel.
        /// </summary>
        public async Task<BaptizeResult> ReassignAddressAsync(
            ButtonPanelType panelType,
            int timeoutMs = ProtocolConstants.DefaultTimeoutMs,
            CancellationToken cancellationToken = default,
            bool forceLastByteToFF = false)
        {
            BaptizeResult result = await _baptizeService.ReassignAddressAsync(panelType, timeoutMs, cancellationToken, forceLastByteToFF).ConfigureAwait(false);

            if (result.Success)
            {
                UpdateDeviceInfo(result.AssignedAddress, result.MacAddress);
                _currentPanelType = panelType;
                _logger?.LogInformation("ReassignAddress completato: RecipientId aggiornato a 0x{RecipientId:X8}", result.AssignedAddress);
            }

            return result;
        }

        // ====================
        // METODI PRIVATI
        // ====================

        private async Task<ButtonPanelTestResult> ExecuteButtonTestsAsync(
            ButtonPanel panel,
            Func<string, Task> userPrompt,
            Action<int>? onButtonStart,
            Action<int, bool>? onButtonResult,
            CancellationToken cancellationToken)
        {
            var messageBuilder = new StringBuilder();
            bool wasInterrupted = false;

            while (_stateMachine.CurrentState == ButtonPanelTestState.AwaitingButtonPress)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Controlla se la comunicazione è stata persa
                if (_communicationLostNotified)
                {
                    wasInterrupted = true;
                    messageBuilder.AppendLine("Test interrotto: comunicazione CAN persa");
                    break;
                }

                int buttonIndex = _stateMachine.Context.CurrentButtonIndex;
                onButtonStart?.Invoke(buttonIndex);

                string buttonName = panel.Buttons[buttonIndex];

                // Check for button press with any of the valid button status variable IDs
                bool pressed = await AwaitButtonPressAsync(
                    panel.ButtonStatusVariableIds,
                    panel.ButtonMasks[buttonIndex],
                    (int)_buttonPressTimeout.TotalMilliseconds,
                    userPrompt,
                    $"Premi il pulsante {buttonName} entro {_buttonPressTimeout.TotalSeconds} secondi.",
                    cancellationToken).ConfigureAwait(false);

                // Controlla nuovamente se la comunicazione è stata persa durante l'attesa
                if (_communicationLostNotified)
                {
                    wasInterrupted = true;
                    messageBuilder.AppendLine("Test interrotto: comunicazione CAN persa");
                    break;
                }

                _stateMachine.RecordButtonResult(pressed);
                onButtonResult?.Invoke(buttonIndex, pressed);
                messageBuilder.AppendLine($"- Pulsante {buttonName}: {(pressed ? "PASSATO" : "FALLITO")}");
            }

            // Se è stato interrotto, restituisci un risultato di interruzione
            if (wasInterrupted || _communicationLostNotified)
            {
                return new ButtonPanelTestResult
                {
                    PanelType = _stateMachine.Context.PanelType,
                    TestType = ButtonPanelTestType.Buttons,
                    Passed = false,
                    Message = messageBuilder.ToString().Trim(),
                    Interrupted = true,
                    DeviceUuid = _currentDeviceUuid
                };
            }

            return new ButtonPanelTestResult
            {
                PanelType = _stateMachine.Context.PanelType,
                TestType = ButtonPanelTestType.Buttons,
                Passed = _stateMachine.Context.AllButtonsPassed,
                Message = messageBuilder.ToString().Trim(),
                Interrupted = false,
                DeviceUuid = _currentDeviceUuid
            };
        }

        /// <summary>
        /// Esegue il test LED usando la FSM.
        /// </summary>
        private async Task<ButtonPanelTestResult> ExecuteLedTestAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            CancellationToken cancellationToken)
        {
            var messageBuilder = new StringBuilder();
            bool wasInterrupted = false;

            try
            {
                ushort writeCmd = _protocolRepository.GetCommand(ProtocolConstants.WriteVariableCommand);
                ushort greenVar = _protocolRepository.GetVariable(ProtocolConstants.GreenLedVariable);
                ushort redVar = _protocolRepository.GetVariable(ProtocolConstants.RedLedVariable);
                byte[] onValue = _protocolRepository.GetValue(ProtocolConstants.OnValue);
                byte[] offValue = _protocolRepository.GetValue(ProtocolConstants.OffValue);

                while (_stateMachine.CurrentState == ButtonPanelTestState.TestingLed &&
                       _stateMachine.Context.CurrentLedPhase != LedTestPhase.Complete)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Controlla se la comunicazione è stata persa
                    if (_communicationLostNotified)
                    {
                        wasInterrupted = true;
                        messageBuilder.AppendLine("Test interrotto: comunicazione CAN persa");
                        break;
                    }

                    LedTestPhase phase = _stateMachine.Context.CurrentLedPhase;
                    bool conf;

                    switch (phase)
                    {
                        case LedTestPhase.GreenOn:
                            await SendWriteCommandAsync(writeCmd, greenVar, onValue, cancellationToken).ConfigureAwait(false);
                            conf = await userConfirm("Il LED verde è acceso?").ConfigureAwait(false);
                            messageBuilder.AppendLine($"- LED verde acceso: {(conf ? "PASSATO" : "FALLITO")}");
                            _stateMachine.RecordLedResult(conf);
                            break;

                        case LedTestPhase.GreenOff:
                            await SendWriteCommandAsync(writeCmd, greenVar, offValue, cancellationToken).ConfigureAwait(false);
                            conf = await userConfirm("Il LED verde è spento?").ConfigureAwait(false);
                            messageBuilder.AppendLine($"- LED verde spento: {(conf ? "PASSATO" : "FALLITO")}");
                            _stateMachine.RecordLedResult(conf);
                            break;

                        case LedTestPhase.RedOn:
                            await SendWriteCommandAsync(writeCmd, redVar, onValue, cancellationToken).ConfigureAwait(false);
                            conf = await userConfirm("Il LED rosso è acceso?").ConfigureAwait(false);
                            messageBuilder.AppendLine($"- LED rosso acceso: {(conf ? "PASSATO" : "FALLITO")}");
                            _stateMachine.RecordLedResult(conf);
                            break;

                        case LedTestPhase.RedOff:
                            await SendWriteCommandAsync(writeCmd, redVar, offValue, cancellationToken).ConfigureAwait(false);
                            conf = await userConfirm("Il LED rosso è spento?").ConfigureAwait(false);
                            messageBuilder.AppendLine($"- LED rosso spento: {(conf ? "PASSATO" : "FALLITO")}");
                            _stateMachine.RecordLedResult(conf);
                            break;

                        case LedTestPhase.BothOn:
                            await SendWriteCommandAsync(writeCmd, greenVar, onValue, cancellationToken).ConfigureAwait(false);
                            await SendWriteCommandAsync(writeCmd, redVar, onValue, cancellationToken).ConfigureAwait(false);
                            conf = await userConfirm("Entrambi i LED sono accesi?").ConfigureAwait(false);
                            messageBuilder.AppendLine($"- LED verde e rosso accesi: {(conf ? "PASSATO" : "FALLITO")}");
                            _stateMachine.RecordLedResult(conf);
                            // Spegni i LED
                            await SendWriteCommandAsync(writeCmd, redVar, offValue, cancellationToken).ConfigureAwait(false);
                            await SendWriteCommandAsync(writeCmd, greenVar, offValue, cancellationToken).ConfigureAwait(false);
                            break;
                    }

                    // Controlla nuovamente se la comunicazione è stata persa dopo ogni operazione
                    if (_communicationLostNotified)
                    {
                        wasInterrupted = true;
                        messageBuilder.AppendLine("Test interrotto: comunicazione CAN persa");
                        break;
                    }
                }

                // Se è stato interrotto, restituisci un risultato di interruzione
                if (wasInterrupted || _communicationLostNotified)
                {
                    return new ButtonPanelTestResult
                    {
                        PanelType = panelType,
                        TestType = ButtonPanelTestType.Led,
                        Passed = false,
                        Message = messageBuilder.ToString().Trim(),
                        Interrupted = true,
                        DeviceUuid = _currentDeviceUuid
                    };
                }

                return new ButtonPanelTestResult
                {
                    PanelType = panelType,
                    TestType = ButtonPanelTestType.Led,
                    Passed = _stateMachine.Context.LedTestPassed,
                    Message = messageBuilder.ToString().Trim(),
                    Interrupted = false,
                    DeviceUuid = _currentDeviceUuid
                };
            }
            catch (OperationCanceledException)
            {
                _stateMachine.Cancel();
                return TestResultFactory.CreateInterrupted(panelType, ButtonPanelTestType.Led, _currentDeviceUuid);
            }
            catch (Exception ex)
            {
                _stateMachine.SetError(ex.Message);
                return TestResultFactory.CreateError(panelType, ButtonPanelTestType.Led, ex.Message, _currentDeviceUuid);
            }
        }

        /// <summary>
        /// Esegue il test buzzer usando la FSM.
        /// </summary>
        private async Task<ButtonPanelTestResult> ExecuteBuzzerTestAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            CancellationToken cancellationToken)
        {
            try
            {
                // Controlla se la comunicazione è stata persa
                if (_communicationLostNotified)
                {
                    return new ButtonPanelTestResult
                    {
                        PanelType = panelType,
                        TestType = ButtonPanelTestType.Buzzer,
                        Passed = false,
                        Message = "Test interrotto: comunicazione CAN persa",
                        Interrupted = true,
                        DeviceUuid = _currentDeviceUuid
                    };
                }

                ushort writeCmd = _protocolRepository.GetCommand(ProtocolConstants.WriteVariableCommand);
                ushort buzzerVar = _protocolRepository.GetVariable(ProtocolConstants.BuzzerVariable);
                byte[] blinkValue = _protocolRepository.GetValue(ProtocolConstants.SingleBlinkValue);

                await SendWriteCommandAsync(writeCmd, buzzerVar, blinkValue, cancellationToken).ConfigureAwait(false);

                // Controlla nuovamente se la comunicazione è stata persa dopo l'invio
                if (_communicationLostNotified)
                {
                    return new ButtonPanelTestResult
                    {
                        PanelType = panelType,
                        TestType = ButtonPanelTestType.Buzzer,
                        Passed = false,
                        Message = "Test interrotto: comunicazione CAN persa",
                        Interrupted = true,
                        DeviceUuid = _currentDeviceUuid
                    };
                }

                bool passed = await userConfirm("Hai sentito il buzzer suonare?").ConfigureAwait(false);
                _stateMachine.RecordBuzzerResult(passed);

                return new ButtonPanelTestResult
                {
                    PanelType = panelType,
                    TestType = ButtonPanelTestType.Buzzer,
                    Passed = passed,
                    Message = $"- Buzzer suonato: {(passed ? "PASSATO" : "FALLITO")}",
                    Interrupted = false,
                    DeviceUuid = _currentDeviceUuid
                };
            }
            catch (OperationCanceledException)
            {
                _stateMachine.Cancel();
                return TestResultFactory.CreateInterrupted(panelType, ButtonPanelTestType.Buzzer, _currentDeviceUuid);
            }
            catch (Exception ex)
            {
                _stateMachine.SetError(ex.Message);
                return TestResultFactory.CreateError(panelType, ButtonPanelTestType.Buzzer, ex.Message, _currentDeviceUuid);
            }
        }

        /// <summary>
        /// Returns the currently-tested panel to a virgin auto-addressing state at the end of
        /// each test, so the next host machine (Eden, Optimus, R3L, ...) re-assigns its STEM
        /// address through auto-addressing. Skipped if communication has been lost.
        /// </summary>
        private async Task ResetPanelToVirginAsync()
        {
            if (_communicationLostNotified || !_communicationService.IsChannelConnected())
            {
                _logger?.LogDebug("Skipping virgin reset: CAN channel not available.");
                return;
            }

            try
            {
                // Use CancellationToken.None: the test's token may already be cancelled
                // (operator interrupted), but we still want to leave the panel in virgin state.
                bool ok = await _baptizeService.ResetToVirginAddressAsync(
                    ProtocolConstants.DefaultTimeoutMs,
                    CancellationToken.None).ConfigureAwait(false);

                if (ok)
                {
                    // After WHO_ARE_YOU+reset, the panel's SP_Address becomes 0xFFFFFFFF until a
                    // fresh host re-addresses it. Drop the cached recipient so we don't talk to
                    // a stale address if anything else fires before disconnect.
                    _lastRecipientId = null;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Virgin reset failed; panel may retain its tester address.");
            }
        }

        /// <summary>
        /// Attende la pressione di un pulsante specifico.
        /// Accepts multiple possible button status variable IDs (e.g., 0x8000 or 0x803E)
        /// </summary>
        private async Task<bool> AwaitButtonPressAsync(
            ushort[] buttonStatusVariableIds,
            byte buttonMask,
            int timeoutMs,
            Func<string, Task> userPrompt,
            string promptMessage,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            void OnCommandDecoded(object? sender, AppLayerDecoderEventArgs e)
            {
                _logger?.LogDebug("BUTTON PRESS: Received payload length={Length}, data={Data}",
                    e.Payload.Length, BitConverter.ToString(e.Payload));

                // Check if payload matches expected format: 0x00, 0x02, VAR_ID_H, VAR_ID_L, BUTTON_MASK
                if (e.Payload.Length >= 5 &&
                    e.Payload[0] == 0x00 &&
                    e.Payload[1] == 0x02)
                {
                    // Extract the variable ID from payload
                    ushort receivedVariableId = (ushort)((e.Payload[2] << 8) | e.Payload[3]);
                    byte receivedMask = e.Payload[4];

                    // Check if the variable ID matches any of the valid IDs for this panel
                    if (buttonStatusVariableIds.Contains(receivedVariableId) &&
                        (receivedMask & buttonMask) == buttonMask)
                    {
                        _logger?.LogInformation("BUTTON PRESS: SUCCESS! Button detected with variableId=0x{VarId:X4}", receivedVariableId);
                        tcs.TrySetResult(true);
                    }
                }
            }

            using CancellationTokenRegistration registration = cts.Token.Register(() => tcs.TrySetResult(false));

            _communicationService.CommandDecoded += OnCommandDecoded;

            try
            {
                await userPrompt(promptMessage).ConfigureAwait(false);
                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                _communicationService.CommandDecoded -= OnCommandDecoded;
            }
        }

        /// <summary>
        /// Loop di heartbeat attivo che invia periodicamente il comando 0x0000 e attende la risposta 0x8000.
        /// Se la risposta non arriva entro il timeout, incrementa il contatore di heartbeat mancanti.
        /// Dopo MaxMissedHeartbeats mancati, notifica la perdita di comunicazione.
        /// </summary>
        private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogDebug("HEARTBEAT: Loop avviato");
            int heartbeatCount = 0;
            DateTime loopStartTime = DateTime.UtcNow;

            while (!cancellationToken.IsCancellationRequested && _heartbeatEnabled)
            {
                try
                {
                    // Attendi l'intervallo tra heartbeat
                    await Task.Delay(ProtocolConstants.HeartbeatIntervalMs, cancellationToken).ConfigureAwait(false);

                    if (!_heartbeatEnabled || cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    heartbeatCount++;
                    DateTime heartbeatStartTime = DateTime.UtcNow;

                    // Invia il comando heartbeat (0x0000) e attendi la risposta (0x8000)
                    bool heartbeatReceived = await SendHeartbeatAndWaitResponseAsync(cancellationToken).ConfigureAwait(false);

                    TimeSpan heartbeatDuration = DateTime.UtcNow - heartbeatStartTime;

                    lock (_heartbeatLock)
                    {
                        if (heartbeatReceived)
                        {
                            if (_missedHeartbeats > 0)
                            {
                                _logger?.LogInformation("HEARTBEAT: Comunicazione ripristinata dopo {Missed} heartbeat mancanti", _missedHeartbeats);
                            }
                            _missedHeartbeats = 0;

                            // Log periodico dello stato (ogni 10 heartbeat OK)
                            if (heartbeatCount % 10 == 0)
                            {
                                TimeSpan uptime = DateTime.UtcNow - loopStartTime;
                                _logger?.LogDebug("HEARTBEAT: OK #{Count}, Uptime={Uptime:mm\\:ss}, ResponseTime={ResponseMs}ms",
                                    heartbeatCount, uptime, (int)heartbeatDuration.TotalMilliseconds);
                            }
                        }
                        else
                        {
                            _missedHeartbeats++;
                            _logger?.LogWarning("HEARTBEAT: Mancato #{Count}! ({Missed}/{Max}), ResponseTime={ResponseMs}ms",
                                heartbeatCount, _missedHeartbeats, ProtocolConstants.MaxMissedHeartbeats, (int)heartbeatDuration.TotalMilliseconds);

                            // Log diagnostico aggiuntivo quando manca un heartbeat
                            LogHeartbeatDiagnostics();

                            if (_missedHeartbeats >= ProtocolConstants.MaxMissedHeartbeats)
                            {
                                TimeSpan uptime = DateTime.UtcNow - loopStartTime;
                                _logger?.LogError("HEARTBEAT: Troppi heartbeat mancanti! TotalHeartbeats={Total}, Uptime={Uptime:mm\\:ss}",
                                    heartbeatCount, uptime);
                                NotifyCommunicationLost();
                                break;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "HEARTBEAT: Errore nel loop al heartbeat #{Count}", heartbeatCount);
                }
            }

            TimeSpan totalUptime = DateTime.UtcNow - loopStartTime;
            _logger?.LogDebug("HEARTBEAT: Loop terminato. TotalHeartbeats={Total}, Uptime={Uptime:mm\\:ss}", heartbeatCount, totalUptime);
        }

        /// <summary>
        /// Log diagnostico quando un heartbeat fallisce.
        /// </summary>
        private void LogHeartbeatDiagnostics()
        {
            try
            {
                // Log dello stato del CAN adapter se disponibile
                if (_canAdapter != null)
                {
                    string diagnostics = _canAdapter.GetDiagnostics();
                    _logger?.LogWarning("HEARTBEAT DIAGNOSTICS:\n{Diagnostics}", diagnostics);
                }

                // Log dello stato del communication service
                bool isConnected = _communicationService.IsChannelConnected();
                _logger?.LogWarning("HEARTBEAT DIAGNOSTICS: CommunicationService.IsChannelConnected={IsConnected}", isConnected);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "HEARTBEAT DIAGNOSTICS: Errore durante la raccolta diagnostica");
            }
        }

        /// <summary>
        /// Invia il comando heartbeat 0x0000 e attende la risposta 0x8000.
        /// </summary>
        /// <returns>True se la risposta è stata ricevuta, false altrimenti.</returns>
        private async Task<bool> SendHeartbeatAndWaitResponseAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ProtocolConstants.HeartbeatTimeoutMs);

            void OnCommandDecoded(object? sender, AppLayerDecoderEventArgs e)
            {
                // Verifica se la risposta è 0x8000 (heartbeat response)
                if (e.Payload.Length >= 2)
                {
                    ushort responseCommand = (ushort)((e.Payload[0] << 8) | e.Payload[1]);
                    if (responseCommand == ProtocolConstants.CMD_HEARTBEAT_RESPONSE)
                    {
                        _logger?.LogDebug("HEARTBEAT: Risposta 0x{Response:X4} ricevuta", responseCommand);
                        tcs.TrySetResult(true);
                    }
                }
            }

            using CancellationTokenRegistration registration = cts.Token.Register(() =>
            {
                _logger?.LogDebug("HEARTBEAT: Timeout raggiunto ({Timeout}ms), nessuna risposta", ProtocolConstants.HeartbeatTimeoutMs);
                tcs.TrySetResult(false);
            });

            _communicationService.CommandDecoded += OnCommandDecoded;

            try
            {
                // Invia il comando heartbeat 0x0000
                _logger?.LogDebug("HEARTBEAT: Invio comando 0x{Cmd:X4}", ProtocolConstants.CMD_HEARTBEAT);

                DateTime sendStartTime = DateTime.UtcNow;
                Result<byte[]> result = await _communicationService.SendCommandAsync(
                    ProtocolConstants.CMD_HEARTBEAT,
                    Array.Empty<byte>(),
                    waitAnswer: false,
                    cancellationToken: cts.Token).ConfigureAwait(false);
                TimeSpan sendDuration = DateTime.UtcNow - sendStartTime;

                if (result.IsFailure)
                {
                    _logger?.LogWarning("HEARTBEAT: Invio fallito dopo {Duration}ms: {Error}",
                        (int)sendDuration.TotalMilliseconds, result.Error);
                    return false;
                }

                _logger?.LogDebug("HEARTBEAT: Comando inviato in {Duration}ms, attesa risposta...", (int)sendDuration.TotalMilliseconds);

                // Attendi la risposta
                return await tcs.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("HEARTBEAT: Operazione cancellata");
                return false;
            }
            finally
            {
                _communicationService.CommandDecoded -= OnCommandDecoded;
            }
        }

        private async Task SendWriteCommandAsync(ushort command, ushort variable, byte[] value, CancellationToken cancellationToken)
        {
            byte[] payload = PayloadBuilder.BuildWriteVariablePayload(variable, value);

            _communicationService.SetSenderRecipientIds(ProtocolConstants.ComputerSenderId, ProtocolConstants.PanelListenId);

            _logger?.LogInformation("Invio comando 0x{Cmd:X4} a 0x{Recipient:X8}", command, ProtocolConstants.PanelListenId);
            _logger?.LogInformation("  Variabile: 0x{Var:X4}, Valore: {Value}", variable, BitConverter.ToString(value));

            Result<byte[]> result = await _communicationService.SendCommandAsync(command, payload, waitAnswer: false, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.IsFailure)
            {
                _logger?.LogWarning("Invio comando fallito: {Error}", result.Error);
            }

            await Task.Delay(ProtocolConstants.CommandDelayMs, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Result> EnsureCommunicationSetupAsync(ButtonPanelType panelType, CancellationToken cancellationToken)
        {
            Result channelResult = await _communicationService.SetActiveChannelAsync(CommunicationChannel.Can, ProtocolConstants.DefaultCanConfig, cancellationToken).ConfigureAwait(false);

            if (channelResult.IsFailure)
            {
                return Result.Failure(
                    ErrorCodes.ConnectionFailed,
                    $"Impossibile impostare il canale di comunicazione su CAN: {channelResult.Error.Message}");
            }

            if (!_communicationService.IsChannelConnected())
            {
                return Result.Failure(
                    ErrorCodes.ConnectionFailed,
                    "Dispositivo CAN non connesso. Assicurati che il dispositivo sia connesso al bus CAN.");
            }

            // Non sovrascrivere il RecipientId se è già stato impostato dal battezzamento
            if (_lastRecipientId == null || _currentPanelType != panelType)
            {
                _communicationService.SetSenderRecipientIds(ProtocolConstants.ComputerSenderId, ProtocolConstants.PanelListenId);
                _lastRecipientId = ProtocolConstants.PanelListenId;
                _currentPanelType = panelType;

                _logger?.LogInformation("Setup comunicazione (default): SenderId=0x{Sender:X8}, RecipientId=0x{Recipient:X8}",
                    ProtocolConstants.ComputerSenderId, ProtocolConstants.PanelListenId);
            }
            else
            {
                _logger?.LogInformation("Setup comunicazione (da battezzamento): SenderId=0x{Sender:X8}, RecipientId=0x{Recipient:X8}",
                    ProtocolConstants.ComputerSenderId, _lastRecipientId.Value);
            }

            // Avvia il loop di heartbeat
            StartHeartbeat();

            return Result.Success();
        }

        /// <summary>
        /// Avvia il loop di heartbeat.
        /// </summary>
        private void StartHeartbeat()
        {
            lock (_heartbeatLock)
            {
                // Ferma eventuali heartbeat precedenti
                StopHeartbeat();

                _heartbeatEnabled = true;
                _communicationLostNotified = false;
                _missedHeartbeats = 0;

                _heartbeatCts = new CancellationTokenSource();
                _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_heartbeatCts.Token));

                _logger?.LogInformation("HEARTBEAT: Monitoraggio avviato (intervallo={Interval}ms, timeout={Timeout}ms, max_missed={Max})",
                    ProtocolConstants.HeartbeatIntervalMs, ProtocolConstants.HeartbeatTimeoutMs, ProtocolConstants.MaxMissedHeartbeats);
            }
        }

        /// <summary>
        /// Ferma il loop di heartbeat.
        /// </summary>
        private void StopHeartbeat()
        {
            lock (_heartbeatLock)
            {
                _heartbeatEnabled = false;

                if (_heartbeatCts != null)
                {
                    try
                    {
                        _heartbeatCts.Cancel();
                        _heartbeatCts.Dispose();
                    }
                    catch { }
                    _heartbeatCts = null;
                }

                _heartbeatTask = null;
                _logger?.LogDebug("HEARTBEAT: Monitoraggio fermato");
            }
        }

        private void UpdateDeviceInfo(uint recipientId, byte[]? macAddress)
        {
            _lastRecipientId = recipientId;
            _currentDeviceUuid = macAddress;
            _communicationService.SetSenderRecipientIds(ProtocolConstants.ComputerSenderId, recipientId);
        }

        /// <summary>
        /// Disconnette il canale di comunicazione al termine del test.
        /// </summary>
        private async Task DisconnectCommunicationAsync()
        {
            try
            {
                // Ferma il loop di heartbeat
                StopHeartbeat();

                _logger?.LogInformation("Disconnessione canale CAN al termine del test...");
                Result result = await _communicationService.DisconnectActiveChannelAsync().ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    _logger?.LogInformation("Canale CAN disconnesso con successo");
                }
                else
                {
                    _logger?.LogWarning("Errore durante la disconnessione del canale CAN: {Error}", result.Error);
                }

                // Reset dello stato per il prossimo test
                _lastRecipientId = null;
                _currentPanelType = null;
                _currentDeviceUuid = null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Eccezione durante la disconnessione del canale CAN");
            }
        }
    }
}
