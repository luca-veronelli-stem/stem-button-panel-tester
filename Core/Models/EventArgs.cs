namespace Core.Models
{
    /// <summary>
    /// Argomenti evento per la decodifica del livello applicativo.
    /// Contiene il payload completo del pacchetto applicativo (header comando + dati).
    /// </summary>
    /// <param name="payload">
    /// Payload del pacchetto applicativo: i primi 2 byte sono l'header comando (cmdInit, cmdOpt),
    /// i restanti byte sono i dati applicativi.
    /// </param>
    public class AppLayerDecoderEventArgs(byte[] payload) : EventArgs
    {
        /// <summary>
        /// Payload completo del pacchetto applicativo.
        /// </summary>
        public byte[] Payload { get; } = payload;
    }

    /// <summary>
    /// Argomenti evento per gli errori del protocollo di comunicazione.
    /// </summary>
    /// <param name="message">Descrizione dell'errore verificatosi.</param>
    /// <param name="packet">Pacchetto che ha causato l'errore (opzionale, per debug).</param>
    public class ProtocolErrorEventArgs(string message, byte[]? packet = null) : EventArgs
    {
        /// <summary>
        /// Messaggio descrittivo dell'errore.
        /// </summary>
        public string Message { get; } = message;

        /// <summary>
        /// Pacchetto raw che ha causato l'errore (null se non disponibile).
        /// </summary>
        public byte[]? Packet { get; } = packet;
    }

    /// <summary>
    /// Argomenti evento per gli errori di comunicazione generici.
    /// </summary>
    /// <param name="message">Descrizione dell'errore di comunicazione.</param>
    public class CommunicationErrorEventArgs(string message) : EventArgs
    {
        /// <summary>
        /// Messaggio descrittivo dell'errore di comunicazione.
        /// </summary>
        public string Message { get; } = message;
    }
}
