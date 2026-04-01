namespace Communication.Protocol.Lib
{
    /// <summary>
    /// Eccezione specifica per errori relativi al protocollo di comunicazione STEM.
    /// Viene sollevata quando si verificano errori di parsing, validazione o formato dei pacchetti.
    /// </summary>
    public class ProtocolException : Exception
    {
        /// <summary>
        /// Inizializza una nuova istanza di <see cref="ProtocolException"/> con il messaggio specificato.
        /// </summary>
        /// <param name="message">Messaggio descrittivo dell'errore.</param>
        public ProtocolException(string message) : base(message) { }

        /// <summary>
        /// Inizializza una nuova istanza di <see cref="ProtocolException"/> con il messaggio e l'eccezione interna.
        /// </summary>
        /// <param name="message">Messaggio descrittivo dell'errore.</param>
        /// <param name="innerException">Eccezione originale che ha causato l'errore.</param>
        public ProtocolException(string message, Exception innerException) : base(message, innerException) { }
    }
}
