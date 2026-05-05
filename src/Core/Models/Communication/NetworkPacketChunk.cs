namespace Core.Models.Communication
{
    /// <summary>
    /// Rappresenta un chunk di pacchetto di rete pronto per la trasmissione.
    /// Utilizzato dal livello di rete per suddividere i pacchetti di trasporto
    /// in unità adatte al MTU del canale di comunicazione.
    /// </summary>
    /// <param name="NetInfo">
    /// Header NetInfo (2 byte) contenente metadati per il riassemblaggio:
    /// chunk rimanenti, flag setLength, ID pacchetto e versione protocollo.
    /// </param>
    /// <param name="Id">
    /// Arbitration ID CAN per la trasmissione. In CAN bus, questo è l'identificatore
    /// del nodo mittente (sender ID), non del destinatario.
    /// </param>
    /// <param name="Chunk">
    /// Dati del chunk (porzione del pacchetto di trasporto).
    /// </param>
    public record NetworkPacketChunk(byte[] NetInfo, uint Id, byte[] Chunk);
}
