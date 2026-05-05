using System.Drawing;
using Core.Enums;
using Core.Models.Services;

namespace Core.Interfaces.GUI
{
    public interface IButtonPanelTestView
    {
        // Gestore dell'evento sollevato quando l'utente cambia il tipo di pulsantiera selezionato
        public event EventHandler<ButtonPanelType>? OnPanelTypeChanged;

        // Gestore dell'evento sollevato quando l'utente clicca sul pulsante per avviare il collaudo
        event EventHandler OnStartTestClicked;

        // Gestore dell'evento sollevato quando l'utente clicca sul pulsante per arrestare il collaudo
        event EventHandler OnStopTestClicked;

        // Gestore dell'evento sollevato quando l'utente clicca sul pulsante per salvare in nuovo file
        event EventHandler? OnSaveNewFileClicked;

        // Gestore dell'evento sollevato quando l'utente clicca sul pulsante per salvare in file esistente
        event EventHandler? OnSaveExistingFileClicked;

        // Metodo per ottenere il testo dei risultati del collaudo
        string GetResultsText();

        // Metodo per mostrare la finestra di dialogo per il salvataggio del file (nuovo file)
        string? ShowSaveNewFileDialog();

        // Metodo per mostrare la finestra di dialogo per selezionare un file esistente
        string? ShowOpenExistingFileDialog();

        // Metodo per impostare il path dell'ultimo file salvato
        void SetLastSavedFilePath(string? filePath);

        // Metodo per ottenere il path dell'ultimo file salvato
        string? GetLastSavedFilePath();

        // Metodo per mostrare un messaggio all'utente
        void ShowMessage(string message, string title);

        /// <summary>
        /// Mostra un dialogo di conferma per risultati non salvati.
        /// Restituisce true se l'utente vuole salvare (non procedere), false se vuole procedere senza salvare.
        /// </summary>
        /// <returns>True se l'utente vuole salvare, false se vuole procedere senza salvare.</returns>
        bool ShowUnsavedResultsWarning();

        // Metodo per ottenere il tipo di pulsantiera selezionato dall'utente
        ButtonPanelType GetSelectedPanelType();

        // Metodo per ottenere il tipo di collaudo selezionato dall'utente
        ButtonPanelTestType GetSelectedTestType();

        // Metodo per impostare lo stato di un indicatore a waiting
        public void SetButtonWaiting(int buttonIndex);

        // Metodo per impostare il risultato di un indicatore
        public void SetButtonResult(int buttonIndex, bool success);

        // Metodo per resettare tutti gli indicatori della vista
        public void ResetAllIndicators();

        // Metodo per mostrare un prompt all'utente
        Task ShowPromptAsync(string message);

        // Metodo per chiedere una conferma all'utente
        Task<bool> ShowConfirmAsync(string message, ButtonPanelTestType testType);

        // Metodo per visualizzare i risultati del collaudo
        void DisplayResults(List<ButtonPanelTestResult> results);

        /// <summary>
        /// Mostra nella richTextBoxTestResult che il collaudo è in corso.
        /// </summary>
        void ShowTestInProgress();

        // Metodo per modificare lo stato della vista durante l'esecuzione del collaudo
        void ShowProgress(string message);

        // Metodo per aggiornare il colore dell'ultimo prompt visualizzato
        void UpdateLastPromptColor(string lastMessage, Color color);

        // Metodo per visualizzare eventuali messaggi di errore
        void ShowError(string message);

        /// <summary>
        /// Imposta lo stato del battezzamento nella vista.
        /// </summary>
        /// <param name="status">Stato del battezzamento.</param>
        void SetBaptizeStatus(BaptizeStatus status);

        /// <summary>
        /// Mostra un dialogo di avviso per l'interruzione della comunicazione CAN.
        /// Informa l'utente che deve staccare e riattaccare il cavo USB.
        /// </summary>
        void ShowCommunicationLostDialog();
    }

    /// <summary>
    /// Stati del battezzamento per la UI.
    /// </summary>
    public enum BaptizeStatus
    {
        /// <summary>
        /// Nessun battezzamento in corso o completato.
        /// </summary>
        None,

        /// <summary>
        /// Battezzamento in corso.
        /// </summary>
        InProgress,

        /// <summary>
        /// Battezzamento completato con successo.
        /// </summary>
        Success,

        /// <summary>
        /// Battezzamento fallito.
        /// </summary>
        Failed,

        /// <summary>
        /// Battezzamento annullato dall'utente.
        /// </summary>
        Cancelled
    }
}
