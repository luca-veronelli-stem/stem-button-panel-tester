using Core.Models.Communication;

namespace Core.Interfaces.Infrastructure
{
    /// <summary>
    /// Contratto per gli adattatori CAN.
    /// </summary>
    public interface ICanAdapter : IAdapter, IAsyncDisposable
    {
        /// <summary>
        /// Evento generato quando viene ricevuto un pacchetto CAN.
        /// </summary>
        event EventHandler<CanPacket> PacketReceived;

        /// <summary>
        /// Evento generato quando viene tentato un recovery automatico.
        /// Il parametro indica lo stato del tentativo (es. "Attempt 1/5", "SUCCESS", "FAILED").
        /// </summary>
        event Action<string>? RecoveryAttempted;

        /// <summary>
        /// Evento generato quando è necessario un intervento fisico dell'utente.
        /// Indica che i tentativi di recovery software sono falliti e l'utente deve
        /// scollegare e ricollegare fisicamente il cavo USB del dispositivo CAN.
        /// </summary>
        event Action? PhysicalReconnectRequired;

        /// <summary>
        /// Invia un messaggio CAN sul bus.
        /// </summary>
        /// <param name="arbitrationId">Identificatore di arbitrazione del messaggio.</param>
        /// <param name="data">Dati da inviare (1-8 byte).</param>
        /// <param name="isExtended">True per ID esteso (29 bit), false per standard (11 bit).</param>
        /// <returns>True se l'invio è riuscito, false altrimenti.</returns>
        Task<bool> Send(uint arbitrationId, byte[] data, bool isExtended = false);

        /// <summary>
        /// Forza un tentativo di recovery manuale della connessione CAN.
        /// Utile quando l'utente rileva problemi di comunicazione.
        /// </summary>
        /// <returns>True se il recovery è riuscito, false altrimenti.</returns>
        Task<bool> ForceRecoveryAsync();

        /// <summary>
        /// Ottiene una stringa con le statistiche diagnostiche correnti dell'adattatore.
        /// Utile per il debugging di problemi di comunicazione.
        /// </summary>
        /// <returns>Stringa formattata con le statistiche diagnostiche.</returns>
        string GetDiagnostics();
    }
}
