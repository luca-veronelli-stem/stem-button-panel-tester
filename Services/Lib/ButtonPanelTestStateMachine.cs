using Core.Enums;
using Core.Models.Services;

namespace Services.Lib
{
    /// <summary>
    /// Macchina a stati finiti per la gestione del test delle pulsantiere.
    /// Gestisce le transizioni di stato e le azioni associate.
    /// </summary>
    public class ButtonPanelTestStateMachine
    {
        private ButtonPanelTestState _currentState;
        private readonly ButtonPanelTestContext _context;
        private readonly object _stateLock = new();

        /// <summary>
        /// Stato corrente della macchina.
        /// </summary>
        public ButtonPanelTestState CurrentState
        {
            get { lock (_stateLock) return _currentState; }
            private set { lock (_stateLock) _currentState = value; }
        }

        /// <summary>
        /// Contesto del test corrente.
        /// </summary>
        public ButtonPanelTestContext Context => _context;

        /// <summary>
        /// Evento generato quando lo stato cambia.
        /// </summary>
        public event Action<ButtonPanelTestState, ButtonPanelTestState>? StateChanged;

        /// <summary>
        /// Evento generato per messaggi diagnostici.
        /// </summary>
        public event Action<string>? DiagnosticMessage;

        /// <summary>
        /// Inizializza una nuova istanza della macchina a stati.
        /// </summary>
        public ButtonPanelTestStateMachine()
        {
            _currentState = ButtonPanelTestState.Idle;
            _context = new ButtonPanelTestContext();
        }

        /// <summary>
        /// Avvia un nuovo test per la pulsantiera specificata.
        /// </summary>
        /// <param name="panelType">Tipo di pulsantiera.</param>
        /// <param name="testType">Tipo di test da eseguire.</param>
        /// <param name="panel">Configurazione della pulsantiera.</param>
        /// <returns>True se la transizione è valida, false altrimenti.</returns>
        public bool StartTest(ButtonPanelType panelType, ButtonPanelTestType testType, ButtonPanel panel)
        {
            if (CurrentState != ButtonPanelTestState.Idle)
            {
                Log($"Cannot start test: current state is {CurrentState}, expected Idle");
                return false;
            }

            _context.Reset();
            _context.PanelType = panelType;
            _context.TestType = testType;
            _context.TotalButtons = panel.ButtonCount;
            _context.HasLed = panel.HasLed;

            TransitionTo(ButtonPanelTestState.Initializing);
            return true;
        }

        /// <summary>
        /// Segnala che l'inizializzazione è completata con successo.
        /// </summary>
        public bool InitializationComplete()
        {
            if (CurrentState != ButtonPanelTestState.Initializing)
            {
                Log($"Cannot complete initialization: current state is {CurrentState}");
                return false;
            }

            // Determina il prossimo stato in base al tipo di test
            var nextState = _context.TestType switch
            {
                ButtonPanelTestType.Buttons => ButtonPanelTestState.AwaitingButtonPress,
                ButtonPanelTestType.Led => ButtonPanelTestState.TestingLed,
                ButtonPanelTestType.Buzzer => ButtonPanelTestState.TestingBuzzer,
                ButtonPanelTestType.Complete => ButtonPanelTestState.AwaitingButtonPress,
                _ => ButtonPanelTestState.Error
            };

            TransitionTo(nextState);
            return true;
        }

        /// <summary>
        /// Segnala che l'inizializzazione è fallita.
        /// </summary>
        /// <param name="errorMessage">Messaggio di errore.</param>
        public bool InitializationFailed(string errorMessage)
        {
            if (CurrentState != ButtonPanelTestState.Initializing)
            {
                return false;
            }

            _context.ErrorMessage = errorMessage;
            TransitionTo(ButtonPanelTestState.Error);
            return true;
        }

        /// <summary>
        /// Registra il risultato della pressione di un pulsante.
        /// </summary>
        /// <param name="pressed">True se il pulsante è stato premuto, false se timeout.</param>
        public bool RecordButtonResult(bool pressed)
        {
            if (CurrentState != ButtonPanelTestState.AwaitingButtonPress)
            {
                Log($"Cannot record button result: current state is {CurrentState}");
                return false;
            }

            TransitionTo(ButtonPanelTestState.RecordingButtonResult);

            _context.ButtonResults.Add(pressed);
            _context.AllButtonsPassed &= pressed;
            _context.CurrentButtonIndex++;

            // Determina il prossimo stato
            if (_context.CurrentButtonIndex < _context.TotalButtons)
            {
                // Altri pulsanti da testare
                TransitionTo(ButtonPanelTestState.AwaitingButtonPress);
            }
            else
            {
                // Tutti i pulsanti testati
                MoveToNextTestPhase();
            }

            return true;
        }

        /// <summary>
        /// Registra il risultato di una fase del test LED.
        /// </summary>
        /// <param name="passed">True se l'utente ha confermato positivamente.</param>
        public bool RecordLedResult(bool passed)
        {
            if (CurrentState != ButtonPanelTestState.TestingLed)
            {
                Log($"Cannot record LED result: current state is {CurrentState}");
                return false;
            }

            _context.LedTestPassed &= passed;

            // Avanza alla prossima fase del test LED
            _context.CurrentLedPhase = _context.CurrentLedPhase switch
            {
                LedTestPhase.GreenOn => LedTestPhase.GreenOff,
                LedTestPhase.GreenOff => LedTestPhase.RedOn,
                LedTestPhase.RedOn => LedTestPhase.RedOff,
                LedTestPhase.RedOff => LedTestPhase.BothOn,
                LedTestPhase.BothOn => LedTestPhase.Complete,
                _ => LedTestPhase.Complete
            };

            if (_context.CurrentLedPhase == LedTestPhase.Complete)
            {
                // Test LED completato, passa al buzzer
                if (_context.TestType == ButtonPanelTestType.Led)
                {
                    // Solo test LED richiesto
                    TransitionTo(ButtonPanelTestState.Completed);
                }
                else
                {
                    // Test completo, passa al buzzer
                    TransitionTo(ButtonPanelTestState.TestingBuzzer);
                }
            }

            return true;
        }

        /// <summary>
        /// Registra il risultato del test buzzer.
        /// </summary>
        /// <param name="passed">True se l'utente ha confermato di aver sentito il buzzer.</param>
        public bool RecordBuzzerResult(bool passed)
        {
            if (CurrentState != ButtonPanelTestState.TestingBuzzer)
            {
                Log($"Cannot record buzzer result: current state is {CurrentState}");
                return false;
            }

            _context.BuzzerTestPassed = passed;
            _context.EndTime = DateTime.UtcNow;
            TransitionTo(ButtonPanelTestState.Completed);
            return true;
        }

        /// <summary>
        /// Interrompe il test corrente.
        /// </summary>
        public bool Cancel()
        {
            if (CurrentState == ButtonPanelTestState.Idle ||
                CurrentState == ButtonPanelTestState.Completed ||
                CurrentState == ButtonPanelTestState.Interrupted ||
                CurrentState == ButtonPanelTestState.Error)
            {
                Log($"Cannot cancel: already in terminal state {CurrentState}");
                return false;
            }

            _context.EndTime = DateTime.UtcNow;
            TransitionTo(ButtonPanelTestState.Interrupted);
            return true;
        }

        /// <summary>
        /// Segnala un errore durante il test.
        /// </summary>
        /// <param name="errorMessage">Messaggio di errore.</param>
        public bool SetError(string errorMessage)
        {
            if (CurrentState == ButtonPanelTestState.Idle ||
                CurrentState == ButtonPanelTestState.Completed ||
                CurrentState == ButtonPanelTestState.Interrupted ||
                CurrentState == ButtonPanelTestState.Error)
            {
                return false;
            }

            _context.ErrorMessage = errorMessage;
            _context.EndTime = DateTime.UtcNow;
            TransitionTo(ButtonPanelTestState.Error);
            return true;
        }

        /// <summary>
        /// Resetta la macchina allo stato Idle.
        /// </summary>
        public void Reset()
        {
            _context.Reset();
            TransitionTo(ButtonPanelTestState.Idle);
        }

        /// <summary>
        /// Verifica se il test è in corso.
        /// </summary>
        public bool IsRunning => CurrentState != ButtonPanelTestState.Idle &&
                                  CurrentState != ButtonPanelTestState.Completed &&
                                  CurrentState != ButtonPanelTestState.Interrupted &&
                                  CurrentState != ButtonPanelTestState.Error;

        /// <summary>
        /// Verifica se il test è in uno stato terminale.
        /// </summary>
        public bool IsTerminal => CurrentState == ButtonPanelTestState.Completed ||
                                   CurrentState == ButtonPanelTestState.Interrupted ||
                                   CurrentState == ButtonPanelTestState.Error;

        /// <summary>
        /// Ottiene il risultato complessivo del test.
        /// </summary>
        public bool OverallPassed => _context.AllButtonsPassed &&
                                      _context.LedTestPassed &&
                                      _context.BuzzerTestPassed;

        /// <summary>
        /// Passa alla prossima fase del test dopo il completamento dei pulsanti.
        /// </summary>
        private void MoveToNextTestPhase()
        {
            if (_context.TestType == ButtonPanelTestType.Buttons)
            {
                // Solo test pulsanti richiesto
                _context.EndTime = DateTime.UtcNow;
                TransitionTo(ButtonPanelTestState.Completed);
            }
            else if (_context.HasLed && _context.TestType != ButtonPanelTestType.Buzzer)
            {
                // Test LED richiesto e la pulsantiera ha LED
                TransitionTo(ButtonPanelTestState.TestingLed);
            }
            else
            {
                // Passa direttamente al buzzer
                TransitionTo(ButtonPanelTestState.TestingBuzzer);
            }
        }

        /// <summary>
        /// Esegue la transizione a un nuovo stato.
        /// </summary>
        private void TransitionTo(ButtonPanelTestState newState)
        {
            var oldState = CurrentState;

            if (oldState == newState)
            {
                return;
            }

            Log($"Transition: {oldState} -> {newState}");
            CurrentState = newState;
            StateChanged?.Invoke(oldState, newState);
        }

        /// <summary>
        /// Log di messaggi diagnostici.
        /// </summary>
        private void Log(string message)
        {
            DiagnosticMessage?.Invoke($"[FSM] {message}");
        }
    }
}
