using Core.Enums;
using Core.Interfaces.Data;
using Core.Interfaces.Infrastructure;
using Core.Models.Services;

namespace Core.Interfaces.Services
{
    /// <summary>
    /// Contratto per il servizio di test delle pulsantiere.
    /// Fornisce metodi per eseguire test completi o specifici (pulsanti, LED, buzzer) su diverse tipologie di pulsantiere.
    /// Il logging strutturato viene gestito via Microsoft.Extensions.Logging nelle implementazioni concrete.
    /// </summary>
    public interface IButtonPanelTestService
    {
        /// <summary>
        /// Evento generato quando viene rilevata un'interruzione della comunicazione CAN.
        /// </summary>
        event Action? CommunicationLost;

        // Esegue tutti i test per una pulsantiera specifica e restituisce i risultati
        Task<List<ButtonPanelTestResult>> TestAllAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            Func<string, Task> userPrompt,
            Action<int>? onButtonStart = null,
            Action<int, bool>? onButtonResult = null,
            CancellationToken cancellationToken = default);

        // Esegue il test dei pulsanti per una pulsantiera specifica
        Task<ButtonPanelTestResult> TestButtonsAsync(
            ButtonPanelType panelType,
            Func<string, Task> userPrompt,
            Action<int>? onButtonStart = null,
            Action<int, bool>? onButtonResult = null,
            CancellationToken cancellationToken = default);

        // Esegue il test del LED per una pulsantiera specifica
        Task<ButtonPanelTestResult> TestLedAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            CancellationToken cancellationToken = default);

        // Esegue il test del buzzer per una pulsantiera specifica
        Task<ButtonPanelTestResult> TestBuzzerAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            CancellationToken cancellationToken = default);

        // Imposta il repository del protocollo da utilizzare per i test
        void SetProtocolRepository(IProtocolRepository repository);

        /// <summary>
        /// Imposta l'adattatore CAN per consentire operazioni di recovery automatico.
        /// Deve essere chiamato durante la configurazione del servizio per abilitare
        /// il monitoraggio della salute della comunicazione e il recovery automatico.
        /// </summary>
        /// <param name="canAdapter">Adattatore CAN o null per disabilitare il recovery.</param>
        void SetCanAdapter(ICanAdapter? canAdapter);

        /// <summary>
        /// Esegue il battezzamento (assegnazione indirizzo) di un dispositivo.
        /// </summary>
        /// <param name="panelType">Tipo di pulsantiera da battezzare.</param>
        /// <param name="timeoutMs">Timeout in millisecondi.</param>
        /// <param name="cancellationToken">Token per la cancellazione.</param>
        /// <returns>Risultato del battezzamento.</returns>
        Task<BaptizeResult> BaptizeDeviceAsync(
            ButtonPanelType panelType,
            int timeoutMs = 5000,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cerca dispositivi non battezzati sul bus CAN.
        /// </summary>
        /// <param name="timeoutMs">Timeout in millisecondi.</param>
        /// <param name="cancellationToken">Token per la cancellazione.</param>
        /// <returns>Lista degli indirizzi MAC dei dispositivi trovati.</returns>
        Task<List<byte[]>> ScanForUnbaptizedDevicesAsync(
            int timeoutMs = 5000,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reassign device address: perform unbaptize (set to broadcast) then baptize for the selected panel.
        /// Exposed here as a typed API to avoid reflection-based calls from UI.
        /// </summary>
        Task<BaptizeResult> ReassignAddressAsync(
            ButtonPanelType panelType,
            int timeoutMs = 5000,
            CancellationToken cancellationToken = default,
            bool forceLastByteToFF = false);

        /// <summary>
        /// Forza la disconnessione della comunicazione CAN e ferma tutti i monitoraggi in corso.
        /// Utile quando l'utente vuole interrompere manualmente il test.
        /// </summary>
        Task ForceDisconnectAsync();
    }
}
