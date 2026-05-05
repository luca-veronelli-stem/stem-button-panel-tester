using Communication.Protocol.Layers;
using Communication.Protocol.Lib;
using Core.Enums;
using Core.Interfaces.Communication;
using Core.Models;
using Core.Models.Communication;

namespace Communication.Protocol
{
    /// <summary>
    /// Gestisce il protocollo STEM per la costruzione e l'analisi dei pacchetti di comunicazione.
    /// Implementa <see cref="IProtocolManager"/> fornendo un'interfaccia unificata per la gestione
    /// dello stack protocollare (Application → Transport → Network).
    /// </summary>
    /// <remarks>
    /// Questa classe coordina i tre livelli del protocollo:
    /// <list type="bullet">
    /// <item><see cref="ApplicationLayer"/>: gestione comandi e payload applicativi</item>
    /// <item><see cref="TransportLayer"/>: incapsulamento, CRC e gestione mittente</item>
    /// <item><see cref="NetworkLayer"/>: chunking per adattamento al MTU del canale</item>
    /// </list>
    /// </remarks>
    public sealed class StemProtocolManager : IProtocolManager
    {
        /// <summary>
        /// Evento sollevato quando un comando è stato decodificato con successo.
        /// </summary>
        public event EventHandler<AppLayerDecoderEventArgs>? CommandDecoded;

        /// <summary>
        /// Evento sollevato quando si verifica un errore durante l'elaborazione del protocollo.
        /// </summary>
        public event EventHandler<ProtocolErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Inizializza una nuova istanza di <see cref="StemProtocolManager"/>.
        /// </summary>
        public StemProtocolManager()
        {
        }

        /// <summary>
        /// Costruisce i pacchetti di rete a partire da un comando e payload applicativo.
        /// </summary>
        /// <param name="command">
        /// Comando a 16 bit (byte alto = cmdInit, byte basso = cmdOpt).
        /// </param>
        /// <param name="payload">Payload applicativo da trasmettere.</param>
        /// <param name="senderId">Identificatore del mittente nel transport layer.</param>
        /// <param name="recipientId">
        /// Identificatore del destinatario. Nel protocollo STEM CAN, questo viene usato
        /// anche come CAN Arbitration ID per la trasmissione.
        /// </param>
        /// <param name="chunkSize">
        /// Dimensione massima di ciascun chunk in byte (default: 6 per CAN bus).
        /// </param>
        /// <returns>
        /// Lista di <see cref="NetworkPacketChunk"/> pronti per la trasmissione sul canale fisico.
        /// </returns>
        /// <exception cref="ArgumentNullException">Se <paramref name="payload"/> è null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="chunkSize"/> non è positivo.</exception>
        public List<NetworkPacketChunk> BuildPackets(
            ushort command,
            byte[] payload,
            uint senderId,
            uint recipientId,
            int chunkSize = 6)
        {
            ArgumentNullException.ThrowIfNull(payload);

            if (chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chunkSize),
                    "La dimensione del chunk deve essere un valore positivo.");
            }

            // Costruzione livello applicativo
            var appLayer = ApplicationLayer.Create(command, payload);

            // Costruzione livello di trasporto
            var transportLayer = TransportLayer.Create(CryptType.None, senderId, recipientId, appLayer.ApplicationPacket);

            // Costruzione livello di rete (chunking)
            // Nel protocollo STEM CAN, l'arbitration ID è il recipientId (destinatario),
            // non il senderId. Il mittente è codificato nel transport layer.
            var networkLayer = NetworkLayer.Create(recipientId, transportLayer.TransportPacket, chunkSize);

            return [.. networkLayer.NetworkPackets];
        }

        /// <summary>
        /// Elabora un pacchetto di trasporto ricevuto (già riassemblato) ed estrae il payload.
        /// </summary>
        /// <param name="transportPacket">
        /// Pacchetto di trasporto completo contenente:
        /// cryptType (1) + senderId (4) + lPack (2) + pacchetto applicativo + CRC (2).
        /// </param>
        /// <returns>
        /// Payload applicativo estratto (escluso l'header del comando).
        /// Restituisce un array vuoto in caso di errore.
        /// </returns>
        /// <remarks>
        /// In caso di errore viene sollevato l'evento <see cref="ErrorOccurred"/>.
        /// In caso di successo viene sollevato l'evento <see cref="CommandDecoded"/>.
        /// </remarks>
        public byte[] ProcessReceivedPacket(byte[]? transportPacket)
        {
            if (transportPacket == null || transportPacket.Length < ProtocolConfig.MinTransportPacketLength)
            {
                RaiseError(
                    $"Pacchetto di trasporto troppo corto o nullo: {transportPacket?.Length ?? 0} byte " +
                    $"(minimo richiesto: {ProtocolConfig.MinTransportPacketLength}).",
                    transportPacket);
                return [];
            }

            try
            {
                return ProcessTransportPacket(transportPacket);
            }
            catch (ProtocolException ex)
            {
                RaiseError($"Errore di protocollo: {ex.Message}", transportPacket);
                return [];
            }
            catch (Exception ex)
            {
                RaiseError($"Errore imprevisto durante l'elaborazione: {ex.Message}", transportPacket);
                return [];
            }
        }

        /// <summary>
        /// Elaborazione interna del pacchetto di trasporto.
        /// </summary>
        private byte[] ProcessTransportPacket(byte[] transportPacket)
        {
            // Parsing del livello di trasporto
            var transportLayer = TransportLayer.Parse(transportPacket);

            if (!transportLayer.IsValid)
            {
                RaiseError(
                    transportLayer.ValidationError ?? "Validazione livello di trasporto fallita (CRC non valido).",
                    transportPacket);
                return [];
            }

            // Parsing del livello applicativo
            var appLayer = ApplicationLayer.Parse(transportLayer.ApplicationPacket);

            // Notifica del comando decodificato
            CommandDecoded?.Invoke(this, new AppLayerDecoderEventArgs(appLayer.ApplicationPacket));

            return appLayer.Data;
        }

        /// <summary>
        /// Solleva l'evento di errore con il messaggio e il pacchetto specificati.
        /// </summary>
        private void RaiseError(string message, byte[]? packet)
        {
            ErrorOccurred?.Invoke(this, new ProtocolErrorEventArgs(message, packet));
        }
    }
}
