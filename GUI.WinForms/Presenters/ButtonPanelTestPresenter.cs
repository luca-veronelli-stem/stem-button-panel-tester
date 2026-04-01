using Core.Enums;
using Core.Interfaces.Data;
using Core.Interfaces.GUI;
using Core.Interfaces.Services;
using Core.Models.Services;

namespace GUI.Windows.Presenters
{
    internal class ButtonPanelTestPresenter
    {
        private readonly IButtonPanelTestView _view;
        private readonly IButtonPanelTestService _service;
        private readonly IProtocolRepositoryFactory _repositoryFactory;
        private CancellationTokenSource? _cts;
        private readonly CancellationTokenSource? _baptizeCts = null;
        private string _lastPromptMessage = string.Empty;
        private List<ButtonPanelTestResult>? _latestResults;
        private bool _resultsSaved = true; // Flag per tracciare se i risultati sono stati salvati

        // Costruttore che inizializza la vista e il servizio
        public ButtonPanelTestPresenter(
            IButtonPanelTestView view,
            IButtonPanelTestService service,
            IProtocolRepositoryFactory repositoryFactory)
        {
            _view = view;
            _service = service;
            _repositoryFactory = repositoryFactory;
            _view.OnPanelTypeChanged += HandlePanelTypeChanged;
            _view.OnStartTestClicked += HandleStartTestAsync;
            _view.OnStopTestClicked += HandleStopTestAsync;
            _view.OnSaveNewFileClicked += HandleSaveNewFileClicked;
            _view.OnSaveExistingFileClicked += HandleSaveExistingFileClicked;

            // Sottoscrivi all'evento di interruzione comunicazione
            _service.CommunicationLost += HandleCommunicationLost;
        }

        private void HandleCommunicationLost()
        {
            // Mostra il dialogo di avviso all'utente
            _view.ShowCommunicationLostDialog();

            // Cancella il test in corso
            _cts?.Cancel();
        }

        // Gestore per il click su "Salva nuovo file"
        private void HandleSaveNewFileClicked(object? sender, EventArgs e)
        {
            if (_latestResults == null || _latestResults.Count == 0)
            {
                _view.ShowError("Nessun risultato da salvare. Esegui prima un collaudo.");
                return;
            }

            var filePath = _view.ShowSaveNewFileDialog();

            if (string.IsNullOrEmpty(filePath))
            {
                _view.ShowProgress("Salvataggio annullato.");
                return;
            }

            try
            {
                string content = _view.GetResultsText();
                File.WriteAllText(filePath, content);

                _view.SetLastSavedFilePath(filePath);
                _view.ShowMessage("Risultati salvati con successo.", "Successo");
                _view.ShowProgress($"Risultati salvati in: {filePath}");
                _resultsSaved = true; // Marca i risultati come salvati
            }
            catch (IOException ex)
            {
                _view.ShowMessage($"Errore I/O durante il salvataggio: {ex.Message}", "Errore");
            }
            catch (UnauthorizedAccessException ex)
            {
                _view.ShowMessage($"Accesso negato durante il salvataggio: {ex.Message}", "Errore");
            }
            catch (Exception ex)
            {
                _view.ShowMessage($"Errore durante il salvataggio: {ex.Message}", "Errore");
            }
        }

        // Gestore per il click su "Salva file esistente"
        private void HandleSaveExistingFileClicked(object? sender, EventArgs e)
        {
            if (_latestResults == null || _latestResults.Count == 0)
            {
                _view.ShowError("Nessun risultato da salvare. Esegui prima un collaudo.");
                return;
            }

            // Usa il file precedentemente selezionato se disponibile, altrimenti chiedi di selezionarne uno
            var filePath = _view.GetLastSavedFilePath();

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                // Se non c'è un file precedente o non esiste più, chiedi di selezionarne uno
                filePath = _view.ShowOpenExistingFileDialog();

                if (string.IsNullOrEmpty(filePath))
                {
                    _view.ShowProgress("Salvataggio annullato.");
                    return;
                }
            }

            try
            {
                string content = _view.GetResultsText();

                // Aggiungi un separatore e poi i nuovi risultati
                string separator = Environment.NewLine +
                                 "========================================" + Environment.NewLine +
                                 $"Collaudo aggiunto il {DateTime.Now:yyyy-MM-dd HH:mm:ss}" + Environment.NewLine +
                                 "========================================" + Environment.NewLine +
                                 Environment.NewLine;
                File.AppendAllText(filePath, separator + content);

                _view.SetLastSavedFilePath(filePath);
                _view.ShowMessage("Risultati aggiunti con successo.", "Successo");
                _view.ShowProgress($"Risultati aggiunti a: {filePath}");
                _resultsSaved = true; // Marca i risultati come salvati
            }
            catch (IOException ex)
            {
                _view.ShowMessage($"Errore I/O durante il salvataggio: {ex.Message}", "Errore");
            }
            catch (UnauthorizedAccessException ex)
            {
                _view.ShowMessage($"Accesso negato durante il salvataggio: {ex.Message}", "Errore");
            }
            catch (Exception ex)
            {
                _view.ShowMessage($"Errore durante il salvataggio: {ex.Message}", "Errore");
            }
        }

        // Metodo per gestire il cambiamento del tipo di pulsantiera
        private async void HandlePanelTypeChanged(object? sender, ButtonPanelType panelType)
        {
            // Aggiorna il repository del protocollo
            uint recipientId = GetRecipientIdForPanel(panelType);
            IProtocolRepository newRepository = _repositoryFactory.Create(recipientId);
            _service.SetProtocolRepository(newRepository);
        }

        // Metodo per gestire l'evento di avvio del collaudo
        private async void HandleStartTestAsync(object? sender, EventArgs e)
        {
            // Controlla se ci sono risultati non salvati
            if (_latestResults != null && _latestResults.Count > 0 && !_resultsSaved)
            {
                // Mostra il dialogo di warning
                bool wantsToSave = _view.ShowUnsavedResultsWarning();

                if (wantsToSave)
                {
                    // L'utente vuole salvare, non procedere con il nuovo collaudo
                    return;
                }
                // Altrimenti procedi con il nuovo collaudo
            }

            ButtonPanelType panelType;
            ButtonPanelTestType testType;

            // Verifica che l'utente abbia selezionato il tipo di pulsantiera e il tipo di test
            try
            {
                panelType = _view.GetSelectedPanelType();
                testType = _view.GetSelectedTestType();
            }
            catch (InvalidOperationException ex)
            {
                _view.ShowError(ex.Message);
                return;
            }

            _cts = new CancellationTokenSource();

            // Mostra nella richTextBoxTestResult che il collaudo è in corso
            _view.ShowTestInProgress();

            // Before starting, perform unbaptize+baptize (reassign) to ensure device has correct STEM ID
            try
            {
                _view.ShowProgress("Preparazione battezzamento e impostazione indirizzo del dispositivo...");
                _view.SetBaptizeStatus(BaptizeStatus.InProgress);

                var reassign = await _service.ReassignAddressAsync(panelType, 5000, _cts.Token).ConfigureAwait(false);
                if (reassign.Success)
                {
                    _view.ShowProgress($"Dispositivo impostato a 0x{reassign.AssignedAddress:X8}");
                    _view.SetBaptizeStatus(BaptizeStatus.Success);
                }
                else
                {
                    _view.ShowProgress($"Riassegnazione indirizzo fallita: {reassign.Message}");
                    _view.SetBaptizeStatus(BaptizeStatus.Failed);
                }
            }
            catch (OperationCanceledException)
            {
                _view.ShowProgress("Battezzamento annullato dall'utente.");
                _view.SetBaptizeStatus(BaptizeStatus.Cancelled);
            }
            catch (Exception ex)
            {
                _view.ShowProgress($"Errore battezzamento: {ex.Message}");
                _view.SetBaptizeStatus(BaptizeStatus.Failed);
            }

            var panel = ButtonPanel.GetByType(panelType);

            _view.ShowProgress($"Avvio collaudo {testType} per pulsantiera {panelType}...");
            _view.ResetAllIndicators();

            List<ButtonPanelTestResult> results = [];

            try
            {
                async Task promptFunc(string msg)
                {
                    _lastPromptMessage = msg;
                    await _view.ShowPromptAsync(msg);
                }

                Task<bool> confirmFunc(string msg) => _view.ShowConfirmAsync(msg, testType);

                void onButtonStart(int i) => _view.SetButtonWaiting(i);

                void onButtonResult(int i, bool passed)
                {
                    _view.SetButtonResult(i, passed);
                    Color resultColor = passed ? Color.LimeGreen : Color.Red;
                    _view.UpdateLastPromptColor(_lastPromptMessage, resultColor);
                }

                switch (testType)
                {
                    case ButtonPanelTestType.Complete:
                        results = await _service.TestAllAsync(panelType, confirmFunc, promptFunc, onButtonStart, onButtonResult, _cts.Token);
                        break;

                    case ButtonPanelTestType.Buttons:
                        results.Add(await _service.TestButtonsAsync(panelType, promptFunc, onButtonStart, onButtonResult, _cts.Token));
                        break;

                    case ButtonPanelTestType.Led:
                        results.Add(await _service.TestLedAsync(panelType, confirmFunc, _cts.Token));
                        break;

                    case ButtonPanelTestType.Buzzer:
                        results.Add(await _service.TestBuzzerAsync(panelType, confirmFunc, _cts.Token));
                        break;

                    default:
                        throw new NotSupportedException("Tipo di collaudo non supportato.");
                }

                _latestResults = results;
                _resultsSaved = false; // I nuovi risultati non sono ancora salvati
                _view.DisplayResults(results);

                string progressMessage = results.Any(r => r.Interrupted)
                    ? "Collaudo interrotto dall'utente."
                    : $"Collaudo {testType} completato." + Environment.NewLine;
                _view.ShowProgress(progressMessage);
            }
            catch (OperationCanceledException)
            {
                _view.ShowProgress("Collaudo interrotto dall'utente.");
            }
            catch (TimeoutException ex)
            {
                _view.ShowError($"Timeout durante il collaudo: {ex.Message}");
                _view.ShowProgress("Collaudo interrotto a causa di timeout.");
            }
            catch (Exception ex)
            {
                _view.ShowError($"Errore durante il collaudo: {ex.Message}");
                _view.ShowProgress("Collaudo interrotto.");
            }
            finally
            {
                if (_latestResults != null && _latestResults.Count != 0)
                {
                    _view.DisplayResults(_latestResults);
                }

                _cts = null;
            }
        }

        // Metodo per gestire l'evento di arresto del collaudo
        private async void HandleStopTestAsync(object? sender, EventArgs e)
        {
            // Se non c'è nessun collaudo o battezzamento in corso, non fare nulla
            if (_cts == null && _baptizeCts == null)
            {
                return;
            }

            if (_cts != null)
            {
                _cts.Cancel();
                _view.ShowProgress("Richiesta di arresto collaudo...");
            }

            if (_baptizeCts != null)
            {
                _baptizeCts.Cancel();
                _view.ShowProgress("Richiesta di arresto battezzamento...");
            }

            // Forza la disconnessione della comunicazione CAN
            try
            {
                await _service.ForceDisconnectAsync().ConfigureAwait(false);
                _view.ShowProgress("Comunicazione CAN disconnessa.");
            }
            catch (Exception ex)
            {
                _view.ShowProgress($"Errore durante la disconnessione: {ex.Message}");
            }
        }

        // Metodo per ottenere l'ID del destinatario in base al tipo di pulsantiera
        private static uint GetRecipientIdForPanel(ButtonPanelType panelType)
        {
            return panelType switch
            {
                ButtonPanelType.DIS0023789 => 0x00030101,
                ButtonPanelType.DIS0025205 => 0x000A0101,
                ButtonPanelType.DIS0026166 => 0x000B0101,
                ButtonPanelType.DIS0026182 => 0x000C0101,
                _ => 0x00000000
            };
        }
    }
}
