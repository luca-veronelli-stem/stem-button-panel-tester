using Core.Enums;
using Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Services.Helpers;
using Services.Models;

namespace Services
{
    /// <summary>
    /// Servizio per il battezzamento (assegnazione ID) dei dispositivi sul bus CAN.
    /// Invia WHO_ARE_YOU e attende risposta WHO_AM_I con UUID.
    /// </summary>
    public class BaptizeService : IBaptizeService
    {
        private readonly ICommunicationService _communicationService;
        private readonly ILogger<BaptizeService> _logger;

        public BaptizeService(ICommunicationService communicationService, ILogger<BaptizeService> logger)
        {
            _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BaptizeResult> BaptizeAsync(
            ButtonPanelType panelType,
            int timeoutMs = ProtocolConstants.DefaultTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            return await BaptizeDeviceAsync(panelType, 0x01, timeoutMs, cancellationToken, useFinalMachineType: false).ConfigureAwait(false);
        }

        public async Task<BaptizeResult> BaptizeWithBoardNumberAsync(
            ButtonPanelType panelType,
            byte boardNumber,
            int timeoutMs = ProtocolConstants.DefaultTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            return await BaptizeDeviceAsync(panelType, boardNumber, timeoutMs, cancellationToken, useFinalMachineType: false).ConfigureAwait(false);
        }

        public Task<List<byte[]>> ScanForDevicesAsync(
            int timeoutMs = ProtocolConstants.DefaultTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<byte[]>());
        }

        public async Task<BaptizeResult> ReassignAddressAsync(
            ButtonPanelType panelType,
            int timeoutMs = ProtocolConstants.DefaultTimeoutMs,
            CancellationToken cancellationToken = default,
            bool forceLastByteToFF = false)
        {
            return await BaptizeDeviceAsync(panelType, forceLastByteToFF ? (byte)0xFF : (byte)0x01, timeoutMs, cancellationToken, forceLastByteToFF).ConfigureAwait(false);
        }

        public async Task<BaptizeResult> ReassignAddressWithBoardNumberAsync(
            ButtonPanelType panelType,
            byte boardNumber,
            int timeoutMs = ProtocolConstants.DefaultTimeoutMs,
            CancellationToken cancellationToken = default,
            bool forceLastByteToFF = false)
        {
            return await BaptizeDeviceAsync(panelType, forceLastByteToFF ? (byte)0xFF : boardNumber, timeoutMs, cancellationToken, forceLastByteToFF).ConfigureAwait(false);
        }

        /// <summary>
        /// Esegue il battezzamento del dispositivo inviando WHO_ARE_YOU e attendendo WHO_AM_I.
        /// </summary>
        private async Task<BaptizeResult> BaptizeDeviceAsync(
            ButtonPanelType panelType,
            byte boardNumber,
            int timeoutMs,
            CancellationToken cancellationToken,
            bool useFinalMachineType = false)
        {
            _logger.LogInformation("=== INIZIO BATTEZZAMENTO ===");
            _logger.LogInformation("Tipo dispositivo: {PanelType}, Board Number: {BoardNumber}, Finale: {IsFinal}",
                panelType, boardNumber, useFinalMachineType);

            try
            {
                // 1. Connetti al bus CAN
                var channelResult = await ConnectToCanBusAsync(cancellationToken).ConfigureAwait(false);
                if (!channelResult.IsSuccess)
                {
                    return CreateFailureResult(channelResult.Error.ToString());
                }

                // 2. Configura comunicazione per pulsantiera vergine (0x1FFFFFFF)
                ConfigureVirginPanelCommunication();

                // 3. Ottieni configurazione per il tipo di pannello
                var config = PanelTypeConfiguration.GetConfiguration(panelType);

                // 4. Invia WHO_ARE_YOU e attendi WHO_AM_I
                var whoAmIResponse = await SendWhoAreYouAsync(config, timeoutMs, cancellationToken).ConfigureAwait(false);
                if (!whoAmIResponse.HasValue)
                {
                    return CreateFailureResult("Nessuna risposta WHO_AM_I ricevuta");
                }

                LogWhoAmIResponse(whoAmIResponse.Value);

                // 5. Calcola indirizzo STEM
                var stemAddress = CalculateStemAddress(panelType, boardNumber, useFinalMachineType);
                LogStemAddress(stemAddress, useFinalMachineType);

                // 6. Invia SET_ADDRESS
                var setAddressResult = await SendSetAddressAsync(whoAmIResponse.Value.Uuid, stemAddress, timeoutMs, cancellationToken).ConfigureAwait(false);
                if (!setAddressResult)
                {
                    return CreateFailureResult("Errore invio SET_ADDRESS");
                }

                // 7. Aggiorna comunicazione per usare il nuovo indirizzo
                await Task.Delay(ProtocolConstants.DeviceReconfigurationDelayMs, cancellationToken).ConfigureAwait(false);
                _communicationService.SetSenderRecipientIds(ProtocolConstants.ComputerSenderId, stemAddress);
                _logger.LogInformation("RecipientId aggiornato a 0x{RecipientId:X8}", stemAddress);

                // 8. Verifica indirizzo finale se richiesto
                if (useFinalMachineType)
                {
                    var verificationResult = await VerifyDeviceAddressAsync(config, stemAddress, whoAmIResponse.Value.Uuid, timeoutMs, cancellationToken).ConfigureAwait(false);
                    if (!verificationResult)
                    {
                        return CreateFailureResult($"Il dispositivo non risponde al nuovo indirizzo 0x{stemAddress:X8}", whoAmIResponse.Value.Uuid, stemAddress);
                    }
                    _logger.LogInformation("VERIFICA RIUSCITA: Il dispositivo risponde correttamente");
                }

                _logger.LogInformation("=== BATTEZZAMENTO COMPLETATO CON SUCCESSO ===");

                return new BaptizeResult
                {
                    Success = true,
                    MacAddress = whoAmIResponse.Value.Uuid,
                    AssignedAddress = stemAddress,
                    Message = $"Dispositivo battezzato: FW=0x{whoAmIResponse.Value.FirmwareType:X4}, UUID={BitConverter.ToString(whoAmIResponse.Value.Uuid)}, Address=0x{stemAddress:X8}"
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Operazione di battezzamento annullata");
                return CreateFailureResult("Operazione annullata");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il battezzamento");
                return CreateFailureResult(ex.Message);
            }
        }

        private async Task<Core.Results.Result> ConnectToCanBusAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Connessione al bus CAN...");
            var result = await _communicationService.SetActiveChannelAsync(
                CommunicationChannel.Can,
                ProtocolConstants.DefaultCanConfig,
                cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Canale CAN attivo");
            }
            else
            {
                _logger.LogError("Errore attivazione canale CAN: {Error}", result.Error);
            }

            return result;
        }

        /// <summary>
        /// Configura la comunicazione per dispositivi vergini (post-riprogrammazione).
        /// Usa l'indirizzo 0x1FFFFFFF su cui le pulsantiere vergini ascoltano.
        /// </summary>
        private void ConfigureVirginPanelCommunication()
        {
            _communicationService.SetSenderRecipientIds(ProtocolConstants.ComputerSenderId, ProtocolConstants.VirginPanelId);
            _logger.LogInformation("IDs configurati per pulsantiera vergine: SenderId=0x{SenderId:X8}, RecipientId=0x{RecipientId:X8}",
                ProtocolConstants.ComputerSenderId, ProtocolConstants.VirginPanelId);
        }

        private async Task<WhoAmIResponse?> SendWhoAreYouAsync(
            PanelTypeConfiguration config,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            var payload = PayloadBuilder.BuildWhoAreYouPayload(
                config.MachineType,
                config.FirmwareType,
                ProtocolConstants.ResetAddressFlag);

            _logger.LogInformation("Invio comando WHO_ARE_YOU (0x{Command:X4})", ProtocolConstants.CMD_WHO_ARE_YOU);
            _logger.LogInformation("Payload: MACHINE_TYPE=0x{MachineType:X2}, FW_TYPE=0x{FwType:X4}, RESET_FLAG=0x{ResetFlag:X2}",
                config.MachineType, config.FirmwareType, ProtocolConstants.ResetAddressFlag);

            var sendResult = await _communicationService.SendCommandAsync(
                ProtocolConstants.CMD_WHO_ARE_YOU,
                payload,
                waitAnswer: true,
                responseValidator: data => ResponseParser.IsValidResponse(data, 15),
                timeoutMs: timeoutMs,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            if (!sendResult.IsSuccess)
            {
                _logger.LogError("Errore nell'invio/ricezione: {Error}", sendResult.Error);
                return null;
            }

            if (ResponseParser.TryParseWhoAmI(sendResult.Value, out var response))
            {
                return response;
            }

            _logger.LogWarning("Risposta WHO_AM_I non valida");
            return null;
        }

        private async Task<bool> SendSetAddressAsync(
            byte[] uuid,
            uint stemAddress,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Invio comando SET_ADDRESS (0x{Command:X4})", ProtocolConstants.CMD_SET_ADDRESS);

            var payload = PayloadBuilder.BuildSetAddressPayload(uuid, stemAddress);
            _logger.LogInformation("Payload SET_ADDRESS: {Payload}", BitConverter.ToString(payload));

            var result = await _communicationService.SendCommandAsync(
                ProtocolConstants.CMD_SET_ADDRESS,
                payload,
                waitAnswer: false,
                timeoutMs: timeoutMs,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Comando SET_ADDRESS inviato con successo");
                return true;
            }

            _logger.LogError("Errore nell'invio SET_ADDRESS: {Error}", result.Error);
            return false;
        }

        private async Task<bool> VerifyDeviceAddressAsync(
            PanelTypeConfiguration config,
            uint deviceAddress,
            byte[] expectedUuid,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("=== VERIFICA INDIRIZZO FINALE ===");
            _logger.LogInformation("Invio WHO_ARE_YOU unicast a 0x{Address:X8} per verifica...", deviceAddress);

            _communicationService.SetSenderRecipientIds(ProtocolConstants.ComputerSenderId, deviceAddress);

            var payload = PayloadBuilder.BuildWhoAreYouPayload(
                config.MachineType,
                config.FirmwareType,
                ProtocolConstants.NoResetFlag);

            var sendResult = await _communicationService.SendCommandAsync(
                ProtocolConstants.CMD_WHO_ARE_YOU,
                payload,
                waitAnswer: true,
                responseValidator: data => ValidateVerificationResponse(data, expectedUuid),
                timeoutMs: Math.Min(timeoutMs, ProtocolConstants.VerificationTimeoutMs),
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            return sendResult.IsSuccess;
        }

        private bool ValidateVerificationResponse(byte[] data, byte[] expectedUuid)
        {
            // Accetta ACK del comando WHO_ARE_YOU
            if (ResponseParser.IsAcknowledgment(data, ProtocolConstants.CMD_WHO_ARE_YOU))
            {
                _logger.LogInformation("Ricevuto ACK del comando WHO_ARE_YOU - dispositivo raggiungibile");
                return true;
            }

            // Oppure accetta WHO_AM_I completo con UUID corretto
            if (ResponseParser.TryParseWhoAmI(data, out var response))
            {
                if (response.Uuid.SequenceEqual(expectedUuid))
                {
                    _logger.LogInformation("Ricevuto WHO_AM_I completo con UUID corretto");
                    return true;
                }
                _logger.LogWarning("UUID non corrisponde alla verifica");
            }

            return false;
        }

        private uint CalculateStemAddress(ButtonPanelType panelType, byte boardNumber, bool useFinalMachineType)
        {
            var config = PanelTypeConfiguration.GetConfiguration(panelType);
            byte machineType = useFinalMachineType ? config.MachineType : (byte)0x00;
            ushort fwType = 0x0004;

            return StemAddressHelper.CalculateAddress(machineType, fwType, boardNumber);
        }

        private void LogWhoAmIResponse(WhoAmIResponse response)
        {
            _logger.LogInformation("=== RISPOSTA WHO_AM_I RICEVUTA ===");
            _logger.LogInformation("MACHINE_TYPE: 0x{MachineType:X2}", response.MachineType);
            _logger.LogInformation("FW_TYPE: 0x{FwType:X4}", response.FirmwareType);
            _logger.LogInformation("UUID completo (12 bytes): {UUID}", BitConverter.ToString(response.Uuid));

            uint uuid0 = BitConverter.ToUInt32(response.Uuid, 0);
            uint uuid1 = BitConverter.ToUInt32(response.Uuid, 4);
            uint uuid2 = BitConverter.ToUInt32(response.Uuid, 8);
            _logger.LogInformation("UUID0: 0x{UUID0:X8}, UUID1: 0x{UUID1:X8}, UUID2: 0x{UUID2:X8}", uuid0, uuid1, uuid2);
        }

        private void LogStemAddress(uint stemAddress, bool isFinal)
        {
            string addressType = isFinal ? "DEFINITIVO" : "TEMPORANEO";
            _logger.LogInformation("=== ASSEGNAZIONE INDIRIZZO {Type} ===", addressType);
            _logger.LogInformation("Indirizzo STEM: 0x{Address:X8}", stemAddress);
            _logger.LogInformation("  MACHINE: 0x{Machine:X2}", StemAddressHelper.ExtractMachineType(stemAddress));
            _logger.LogInformation("  FW_TYPE: 0x{FwType:X4}", StemAddressHelper.ExtractFirmwareType(stemAddress));
            _logger.LogInformation("  BOARD_NUMBER: 0x{BoardNum:X2}", StemAddressHelper.ExtractBoardNumber(stemAddress));
        }

        private static BaptizeResult CreateFailureResult(string message, byte[]? macAddress = null, uint? assignedAddress = null)
        {
            return new BaptizeResult
            {
                Success = false,
                Message = message,
                MacAddress = macAddress,
                AssignedAddress = assignedAddress ?? 0
            };
        }
    }
}
