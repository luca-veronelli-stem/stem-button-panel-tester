using Peak.Can.Basic;

namespace Infrastructure.Lib
{
    /// <summary>
    /// Contratto per l'astrazione delle operazioni PCAN.
    /// Definisce i metodi per la comunicazione con dispositivi CAN tramite hardware PEAK.
    /// </summary>
    /// <remarks>
    /// Questa interfaccia permette di disaccoppiare l'implementazione concreta del driver PCAN,
    /// facilitando il testing tramite mock e l'eventuale sostituzione dell'hardware.
    /// </remarks>
    public interface IPcanApi
    {
        /// <summary>
        /// Inizializza un canale CAN con il baud rate specificato.
        /// </summary>
        /// <param name="channel">Il canale PCAN da inizializzare.</param>
        /// <param name="baudRate">Il baud rate per la comunicazione CAN.</param>
        /// <returns>
        /// <see cref="PcanStatus.OK"/> se l'inizializzazione è riuscita,
        /// altrimenti un codice di errore specifico.
        /// </returns>
        PcanStatus Initialize(PcanChannel channel, Bitrate baudRate);

        /// <summary>
        /// Deinizializza un canale CAN precedentemente inizializzato.
        /// </summary>
        /// <param name="channel">Il canale PCAN da deinizializzare.</param>
        /// <returns>
        /// <see cref="PcanStatus.OK"/> se la deinizializzazione è riuscita,
        /// altrimenti un codice di errore specifico.
        /// </returns>
        PcanStatus Uninitialize(PcanChannel channel);

        /// <summary>
        /// Legge un messaggio CAN dal canale specificato.
        /// </summary>
        /// <param name="channel">Il canale PCAN da cui leggere.</param>
        /// <param name="message">Il messaggio CAN ricevuto.</param>
        /// <param name="timestampMicros">Timestamp del messaggio in microsecondi.</param>
        /// <returns>
        /// <see cref="PcanStatus.OK"/> se un messaggio è stato letto con successo,
        /// <see cref="PcanStatus.ReceiveQueueEmpty"/> se la coda di ricezione è vuota,
        /// altrimenti un codice di errore specifico.
        /// </returns>
        PcanStatus Read(PcanChannel channel, out PcanMessage message, out ulong timestampMicros);

        /// <summary>
        /// Scrive un messaggio CAN sul canale specificato.
        /// </summary>
        /// <param name="channel">Il canale PCAN su cui scrivere.</param>
        /// <param name="message">Il messaggio CAN da inviare.</param>
        /// <returns>
        /// <see cref="PcanStatus.OK"/> se il messaggio è stato inviato con successo,
        /// altrimenti un codice di errore specifico.
        /// </returns>
        PcanStatus Write(PcanChannel channel, PcanMessage message);

        /// <summary>
        /// Ottiene lo stato corrente del bus CAN.
        /// </summary>
        /// <param name="channel">Il canale PCAN di cui ottenere lo stato.</param>
        /// <returns>Lo stato attuale del bus CAN.</returns>
        PcanStatus GetStatus(PcanChannel channel);

        /// <summary>
        /// Imposta un parametro di configurazione sul canale PCAN.
        /// </summary>
        /// <param name="channel">Il canale PCAN da configurare.</param>
        /// <param name="parameter">Il parametro da impostare.</param>
        /// <param name="value">Il valore da assegnare al parametro.</param>
        /// <returns>
        /// <see cref="PcanStatus.OK"/> se il parametro è stato impostato con successo,
        /// altrimenti un codice di errore specifico.
        /// </returns>
        PcanStatus SetValue(PcanChannel channel, PcanParameter parameter, uint value);

        /// <summary>
        /// Ottiene il valore di un parametro di configurazione dal canale PCAN.
        /// </summary>
        /// <param name="channel">Il canale PCAN da interrogare.</param>
        /// <param name="parameter">Il parametro da leggere.</param>
        /// <param name="value">Il valore letto del parametro.</param>
        /// <returns>
        /// <see cref="PcanStatus.OK"/> se il parametro è stato letto con successo,
        /// altrimenti un codice di errore specifico.
        /// </returns>
        PcanStatus GetValue(PcanChannel channel, PcanParameter parameter, out uint value);

        /// <summary>
        /// Resetta il canale PCAN specificato.
        /// </summary>
        /// <param name="channel">Il canale PCAN da resettare.</param>
        /// <returns>
        /// <see cref="PcanStatus.OK"/> se il reset è riuscito,
        /// altrimenti un codice di errore specifico.
        /// </returns>
        /// <remarks>
        /// Il reset del canale può essere utile per recuperare da stati di errore
        /// del bus CAN come Bus-Off o Error-Passive.
        /// </remarks>
        PcanStatus Reset(PcanChannel channel);
    }
}
