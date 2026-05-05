namespace Communication.Protocol.Layers
{
    /// <summary>
    /// Classe base astratta per i livelli del protocollo di comunicazione STEM.
    /// Fornisce una struttura comune per la gestione dei dati di payload.
    /// </summary>
    /// <remarks>
    /// Ogni livello del protocollo (Application, Transport, Network) estende questa classe
    /// per gestire la costruzione e l'analisi dei rispettivi pacchetti.
    /// </remarks>
    public abstract class Layer
    {
        /// <summary>
        /// Buffer dati associato al livello (payload).
        /// </summary>
        private readonly byte[] _data;

        /// <summary>
        /// Inizializza una nuova istanza di <see cref="Layer"/> con i dati specificati.
        /// </summary>
        /// <param name="data">
        /// Array di byte che rappresenta il payload del livello.
        /// Se null, viene sostituito con un array vuoto.
        /// </param>
        protected Layer(byte[]? data)
        {
            _data = data ?? [];
        }

        /// <summary>
        /// Restituisce il buffer dati (payload) associato al livello.
        /// Garantisce di non restituire mai null.
        /// </summary>
        public byte[] Data => _data;
    }
}
