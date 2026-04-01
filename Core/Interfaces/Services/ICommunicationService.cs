using Core.Enums;
using Core.Models;
using Core.Results;

namespace Core.Interfaces.Services
{
    /// <summary>
    /// Contratto per il servizio di comunicazione.
    /// Usa il Result Pattern per gestione errori esplicita.
    /// </summary>
    public interface ICommunicationService
    {
        // Evento generato quando un comando viene decodificato
        event EventHandler<AppLayerDecoderEventArgs> CommandDecoded;

        // Evento generato quando si verifica un errore di comunicazione
        event EventHandler<CommunicationErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// Evento generato quando viene ricevuto un pacchetto raw dal canale di comunicazione.
        /// Utile per protocolli che non passano attraverso lo stack protocollare STEM (es. baptize).
        /// Il primo parametro è l'arbitration ID, il secondo sono i dati raw.
        /// </summary>
        event Action<uint, byte[]>? RawPacketReceived;

        /// <summary>
        /// Invia un comando asincrono e attende una risposta opzionale.
        /// Restituisce un Result per gestione errori esplicita.
        /// </summary>
        /// <param name="command">Comando da inviare.</param>
        /// <param name="payload">Payload del comando.</param>
        /// <param name="waitAnswer">Se true, attende una risposta.</param>
        /// <param name="responseValidator">Validatore opzionale per la risposta.</param>
        /// <param name="timeoutMs">Timeout in millisecondi.</param>
        /// <param name="cancellationToken">Token di cancellazione.</param>
        /// <returns>Result contenente la risposta o un errore.</returns>
        Task<Result<byte[]>> SendCommandAsync(
            ushort command,
            byte[] payload,
            bool waitAnswer,
            Func<byte[], bool>? responseValidator = null,
            int timeoutMs = 5000,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invia un pacchetto raw sul canale di comunicazione attivo.
        /// Bypassa lo stack protocollare STEM.
        /// </summary>
        /// <param name="arbitrationId">Arbitration ID per il frame CAN.</param>
        /// <param name="data">Dati raw da inviare.</param>
        /// <param name="cancellationToken">Token di cancellazione.</param>
        /// <returns>Result indicante successo o errore.</returns>
        Task<Result> SendRawPacketAsync(uint arbitrationId, byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Imposta il canale di comunicazione attivo.
        /// </summary>
        /// <param name="channel">Canale da attivare.</param>
        /// <param name="config">Configurazione del canale.</param>
        /// <param name="cancellationToken">Token di cancellazione.</param>
        /// <returns>Result indicante successo o errore.</returns>
        Task<Result> SetActiveChannelAsync(
            CommunicationChannel channel,
            string config,
            CancellationToken cancellationToken = default);

        // Imposta gli ID del mittente e del destinatario per la comunicazione
        void SetSenderRecipientIds(uint senderId, uint recipientId);

        /// <summary>
        /// Verifica se il canale di comunicazione attivo è connesso.
        /// </summary>
        /// <returns>True se il canale è connesso, false altrimenti.</returns>
        bool IsChannelConnected();

        /// <summary>
        /// Disconnette il canale di comunicazione attivo.
        /// Libera le risorse e chiude la connessione hardware.
        /// </summary>
        /// <param name="cancellationToken">Token di cancellazione.</param>
        /// <returns>Result indicante successo o errore.</returns>
        Task<Result> DisconnectActiveChannelAsync(CancellationToken cancellationToken = default);
    }
}
