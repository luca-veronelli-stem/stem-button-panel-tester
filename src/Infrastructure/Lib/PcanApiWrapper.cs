using Microsoft.Extensions.Logging;
using Peak.Can.Basic;

namespace Infrastructure.Lib
{
    /// <summary>
    /// Wrapper per isolare le chiamate alla libreria Peak.Can.Basic.
    /// Usa l'API moderna (Peak.Can.Basic.Api) invece del layer di backward compatibility.
    /// </summary>
    /// <remarks>
    /// Questa classe fornisce un livello di astrazione sull'API PCAN nativa,
    /// facilitando il testing tramite mock e centralizzando il logging delle operazioni.
    /// </remarks>
    public class PcanApiWrapper : IPcanApi
    {
        private readonly ILogger<PcanApiWrapper> _logger;

        /// <summary>
        /// Inizializza una nuova istanza di <see cref="PcanApiWrapper"/>.
        /// </summary>
        /// <param name="logger">Logger per la registrazione delle operazioni PCAN.</param>
        /// <exception cref="ArgumentNullException">Se <paramref name="logger"/> è <c>null</c>.</exception>
        public PcanApiWrapper(ILogger<PcanApiWrapper> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Inizializza un canale CAN con il baud rate specificato.
        /// </summary>
        /// <param name="channel">Il canale PCAN da inizializzare.</param>
        /// <param name="baudRate">Il baud rate per la comunicazione CAN.</param>
        /// <returns>Lo stato dell'operazione PCAN.</returns>
        public PcanStatus Initialize(PcanChannel channel, Bitrate baudRate)
        {
            _logger.LogDebug("Initialize: channel={Channel}, baudRate={BaudRate}", channel, baudRate);

            PcanStatus status = Api.Initialize(channel, baudRate);

            _logger.LogDebug("Initialize result: {Status}", status);

            return status;
        }

        /// <summary>
        /// Deinizializza un canale CAN precedentemente inizializzato.
        /// </summary>
        /// <param name="channel">Il canale PCAN da deinizializzare.</param>
        /// <returns>Lo stato dell'operazione PCAN.</returns>
        public PcanStatus Uninitialize(PcanChannel channel)
        {
            _logger.LogDebug("Uninitialize: channel={Channel}", channel);

            PcanStatus status = Api.Uninitialize(channel);

            _logger.LogDebug("Uninitialize result: {Status}", status);

            return status;
        }

        /// <summary>
        /// Legge un messaggio CAN dal canale specificato.
        /// </summary>
        /// <param name="channel">Il canale PCAN da cui leggere.</param>
        /// <param name="message">Il messaggio CAN ricevuto.</param>
        /// <param name="timestampMicros">Timestamp del messaggio in microsecondi.</param>
        /// <returns>Lo stato dell'operazione PCAN.</returns>
        /// <remarks>
        /// I dati del messaggio vengono copiati in un nuovo array per garantire stabilità
        /// e prevenire problemi con buffer riutilizzati dall'API nativa.
        /// </remarks>
        public PcanStatus Read(PcanChannel channel, out PcanMessage message, out ulong timestampMicros)
        {
            PcanStatus status = Api.Read(channel, out message, out timestampMicros);

            if (status == PcanStatus.OK)
            {
                // Copia i dati per garantire stabilità del buffer
                byte[] data = new byte[8];
                for (int i = 0; i < message.DLC && i < 8; i++)
                {
                    data[i] = message.Data[i];
                }

                _logger.LogTrace("Read OK: ID=0x{Id:X8}, DLC={Dlc}, Type={MsgType}, Data={Data}",
                    message.ID, message.DLC, message.MsgType, BitConverter.ToString(data, 0, message.DLC));

                // Crea un nuovo messaggio con i dati copiati
                message = new PcanMessage
                {
                    ID = message.ID,
                    DLC = message.DLC,
                    MsgType = message.MsgType,
                    Data = data
                };
            }
            else
            {
                message = new();
                timestampMicros = 0;
            }

            return status;
        }

        /// <summary>
        /// Scrive un messaggio CAN sul canale specificato.
        /// </summary>
        /// <param name="channel">Il canale PCAN su cui scrivere.</param>
        /// <param name="message">Il messaggio CAN da inviare.</param>
        /// <returns>Lo stato dell'operazione PCAN.</returns>
        public PcanStatus Write(PcanChannel channel, PcanMessage message)
        {
            _logger.LogDebug("Write: ID=0x{Id:X8}, DLC={Dlc}, Type={MsgType}",
                message.ID, message.DLC, message.MsgType);

            PcanStatus status = Api.Write(channel, message);

            _logger.LogDebug("Write result: {Status}", status);

            return status;
        }

        /// <summary>
        /// Ottiene lo stato corrente del bus CAN.
        /// </summary>
        /// <param name="channel">Il canale PCAN di cui ottenere lo stato.</param>
        /// <returns>Lo stato del bus CAN.</returns>
        public PcanStatus GetStatus(PcanChannel channel)
        {
            return Api.GetStatus(channel);
        }

        /// <summary>
        /// Imposta un parametro di configurazione sul canale PCAN.
        /// </summary>
        /// <param name="channel">Il canale PCAN da configurare.</param>
        /// <param name="parameter">Il parametro da impostare.</param>
        /// <param name="value">Il valore da assegnare al parametro.</param>
        /// <returns>Lo stato dell'operazione PCAN.</returns>
        public PcanStatus SetValue(PcanChannel channel, PcanParameter parameter, uint value)
        {
            return Api.SetValue(channel, parameter, value);
        }

        /// <summary>
        /// Ottiene il valore di un parametro di configurazione dal canale PCAN.
        /// </summary>
        /// <param name="channel">Il canale PCAN da interrogare.</param>
        /// <param name="parameter">Il parametro da leggere.</param>
        /// <param name="value">Il valore letto del parametro.</param>
        /// <returns>Lo stato dell'operazione PCAN.</returns>
        public PcanStatus GetValue(PcanChannel channel, PcanParameter parameter, out uint value)
        {
            return Api.GetValue(channel, parameter, out value);
        }

        /// <summary>
        /// Resetta il canale PCAN specificato.
        /// </summary>
        /// <param name="channel">Il canale PCAN da resettare.</param>
        /// <returns>Lo stato dell'operazione PCAN.</returns>
        public PcanStatus Reset(PcanChannel channel)
        {
            return Api.Reset(channel);
        }
    }
}
