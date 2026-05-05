using Communication.Protocol.Lib;

namespace Communication.Protocol.Layers
{
    /// <summary>
    /// Rappresenta il livello applicativo nel protocollo di comunicazione STEM.
    /// Gestisce la costruzione e l'analisi dei pacchetti applicativi.
    /// </summary>
    /// <remarks>
    /// Struttura del pacchetto applicativo:
    /// <list type="bullet">
    /// <item>Byte 0: Comando iniziale (cmdInit) - parte alta del comando a 16 bit</item>
    /// <item>Byte 1: Comando opzionale (cmdOpt) - parte bassa del comando a 16 bit</item>
    /// <item>Byte 2-N: Payload applicativo</item>
    /// </list>
    /// </remarks>
    public sealed class ApplicationLayer : Layer
    {
        /// <summary>
        /// Comando iniziale dell'header applicativo (byte alto del comando).
        /// </summary>
        public byte CmdInit { get; }

        /// <summary>
        /// Comando opzionale dell'header applicativo (byte basso del comando).
        /// </summary>
        public byte CmdOpt { get; }

        /// <summary>
        /// Header del livello applicativo (2 byte: cmdInit + cmdOpt).
        /// </summary>
        public byte[] ApplicationHeader { get; }

        /// <summary>
        /// Pacchetto applicativo completo (header + payload).
        /// </summary>
        public byte[] ApplicationPacket { get; }

        /// <summary>
        /// Comando completo a 16 bit (<c>cmdInit &lt;&lt; 8 | cmdOpt</c>).
        /// </summary>
        public ushort Command => (ushort)((CmdInit << 8) | CmdOpt);

        /// <summary>
        /// Costruttore privato per inizializzazione controllata.
        /// Utilizzare i metodi factory <c>Create</c> o <see cref="Parse(byte[])"/>.
        /// </summary>
        private ApplicationLayer(byte cmdInit, byte cmdOpt, byte[] payload, byte[] applicationPacket)
            : base(payload)
        {
            CmdInit = cmdInit;
            CmdOpt = cmdOpt;
            ApplicationHeader = [cmdInit, cmdOpt];
            ApplicationPacket = applicationPacket;
        }

        /// <summary>
        /// Crea un nuovo pacchetto applicativo a partire dal comando e dal payload.
        /// </summary>
        /// <param name="cmdInit">Comando iniziale (byte alto).</param>
        /// <param name="cmdOpt">Comando opzionale (byte basso).</param>
        /// <param name="payload">Payload applicativo da includere nel pacchetto.</param>
        /// <returns>Nuova istanza di <see cref="ApplicationLayer"/> con il pacchetto costruito.</returns>
        public static ApplicationLayer Create(byte cmdInit, byte cmdOpt, byte[] payload)
        {
            payload ??= [];
            byte[] applicationPacket = [cmdInit, cmdOpt, .. payload];
            return new ApplicationLayer(cmdInit, cmdOpt, payload, applicationPacket);
        }

        /// <summary>
        /// Crea un nuovo pacchetto applicativo a partire dal comando a 16 bit e dal payload.
        /// </summary>
        /// <param name="command">Comando a 16 bit (cmdInit nella parte alta, cmdOpt nella parte bassa).</param>
        /// <param name="payload">Payload applicativo da includere nel pacchetto.</param>
        /// <returns>Nuova istanza di <see cref="ApplicationLayer"/> con il pacchetto costruito.</returns>
        public static ApplicationLayer Create(ushort command, byte[] payload)
        {
            byte cmdInit = (byte)(command >> 8);
            byte cmdOpt = (byte)command;
            return Create(cmdInit, cmdOpt, payload);
        }

        /// <summary>
        /// Analizza un pacchetto applicativo esistente ed estrae i componenti.
        /// </summary>
        /// <param name="applicationPacket">
        /// Buffer contenente il pacchetto applicativo completo (header + payload).
        /// </param>
        /// <returns>Nuova istanza di <see cref="ApplicationLayer"/> con i dati estratti.</returns>
        /// <exception cref="ArgumentNullException">Se <paramref name="applicationPacket"/> è null.</exception>
        /// <exception cref="ProtocolException">
        /// Se il pacchetto è troppo corto per contenere l'header applicativo (minimo 2 byte).
        /// </exception>
        public static ApplicationLayer Parse(byte[] applicationPacket)
        {
            ArgumentNullException.ThrowIfNull(applicationPacket);

            if (applicationPacket.Length < ProtocolConfig.ApplicationHeaderLength)
            {
                throw new ProtocolException(
                    $"Il pacchetto applicativo è troppo corto: ricevuti {applicationPacket.Length} byte, " +
                    $"richiesti almeno {ProtocolConfig.ApplicationHeaderLength} byte per l'header.");
            }

            byte cmdInit = applicationPacket[0];
            byte cmdOpt = applicationPacket[1];
            byte[] payload = applicationPacket.Length > ProtocolConfig.ApplicationHeaderLength
                ? applicationPacket[ProtocolConfig.ApplicationHeaderLength..]
                : [];

            return new ApplicationLayer(cmdInit, cmdOpt, payload, applicationPacket);
        }
    }
}
