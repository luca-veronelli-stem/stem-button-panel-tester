using Core.Models;
using Core.Models.Communication;

namespace Core.Interfaces.Communication
{
    /// <summary>
    /// Contratto per la gestione del protocollo di comunicazione STEM.
    /// Definisce le operazioni di costruzione e analisi dei pacchetti protocollari.
    /// </summary>
    /// <remarks>
    /// Le implementazioni di questa interfaccia devono gestire l'intero stack protocollare:
    /// <list type="bullet">
    /// <item>Livello applicativo: comandi e payload</item>
    /// <item>Livello di trasporto: incapsulamento, CRC, identificazione mittente</item>
    /// <item>Livello di rete: chunking per adattamento al canale fisico</item>
    /// </list>
    /// </remarks>
    public interface IProtocolManager
    {
        /// <summary>
        /// Costruisce i pacchetti di rete suddividendo il payload in chunk pronti per la trasmissione.
        /// </summary>
        /// <param name="command">Comando a 16 bit (byte alto = cmdInit, byte basso = cmdOpt).</param>
        /// <param name="payload">Payload applicativo da trasmettere.</param>
        /// <param name="senderId">Identificatore del mittente.</param>
        /// <param name="recipientId">Identificatore del destinatario.</param>
        /// <param name="chunkSize">Dimensione massima di ciascun chunk in byte.</param>
        /// <returns>Lista di chunk di rete pronti per la trasmissione.</returns>
        List<NetworkPacketChunk> BuildPackets(
            ushort command,
            byte[] payload,
            uint senderId,
            uint recipientId,
            int chunkSize = 6);

        /// <summary>
        /// Elabora un pacchetto di trasporto ricevuto (già riassemblato) ed estrae il payload applicativo.
        /// </summary>
        /// <param name="rawPacket">Pacchetto di trasporto completo.</param>
        /// <returns>Payload applicativo estratto (escluso l'header comando).</returns>
        byte[] ProcessReceivedPacket(byte[] rawPacket);

        /// <summary>
        /// Evento sollevato quando un comando è stato decodificato con successo.
        /// Il payload include l'header comando (2 byte) seguito dai dati applicativi.
        /// </summary>
        event EventHandler<AppLayerDecoderEventArgs> CommandDecoded;

        /// <summary>
        /// Evento sollevato quando si verifica un errore durante l'elaborazione del protocollo.
        /// </summary>
        event EventHandler<ProtocolErrorEventArgs> ErrorOccurred;
    }
}
