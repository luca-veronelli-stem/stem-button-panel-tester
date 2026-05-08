using Core.Enums;

namespace Core.Interfaces.Services
{
    /// <summary>
    /// Contratto per il servizio di battezzamento dei dispositivi.
    /// Permette di assegnare un indirizzo STEM ai dispositivi connessi sul bus CAN.
    /// Supporta sia dispositivi gi� battezzati che sbattezzati.
    /// Il logging strutturato viene gestito via Microsoft.Extensions.Logging nelle implementazioni concrete.
    /// </summary>
    public interface IBaptizeService
    {
        /// <summary>
        /// Esegue il battezzamento di un dispositivo del tipo specificato.
        /// Usa boardNumber = 0 per default.
        /// </summary>
        /// <param name="panelType">Tipo di pulsantiera da battezzare.</param>
        /// <param name="timeoutMs">Timeout in millisecondi per ogni fase.</param>
        /// <param name="cancellationToken">Token per la cancellazione.</param>
        /// <returns>Risultato del battezzamento.</returns>
        Task<BaptizeResult> BaptizeAsync(
            ButtonPanelType panelType,
            int timeoutMs = 5000,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Esegue il battezzamento di un dispositivo con numero scheda specificato.
        /// L'indirizzo STEM viene calcolato come:
        /// <code>val = (MACHINE &lt;&lt; 16) | ((FIRMWARE_TYPE &amp; 0x03FF) &lt;&lt; 6) | (BOARD_NUMBER &amp; 0x003F)</code>
        /// </summary>
        /// <param name="panelType">Tipo di pulsantiera da battezzare.</param>
        /// <param name="boardNumber">Numero scheda (0-63).</param>
        /// <param name="timeoutMs">Timeout in millisecondi per ogni fase.</param>
        /// <param name="cancellationToken">Token per la cancellazione.</param>
        /// <returns>Risultato del battezzamento.</returns>
        Task<BaptizeResult> BaptizeWithBoardNumberAsync(
            ButtonPanelType panelType,
            byte boardNumber,
            int timeoutMs = 5000,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cerca dispositivi non battezzati sul bus.
        /// </summary>
        /// <param name="timeoutMs">Timeout in millisecondi.</param>
        /// <param name="cancellationToken">Token per la cancellazione.</param>
        /// <returns>Lista degli indirizzi MAC dei dispositivi trovati.</returns>
        Task<List<byte[]>> ScanForDevicesAsync(
            int timeoutMs = 5000,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reassegna l'indirizzo del dispositivo con la sequenza: unbaptize (set ID to broadcast) then baptize.
        /// Usa boardNumber = 0 per default.
        /// </summary>
        Task<BaptizeResult> ReassignAddressAsync(
            ButtonPanelType panelType,
            int timeoutMs = 5000,
            CancellationToken cancellationToken = default,
            bool forceLastByteToFF = false);

        /// <summary>
        /// Reassegna l'indirizzo del dispositivo con numero scheda specificato.
        /// Esegue la sequenza: unbaptize (set ID to broadcast) then baptize.
        /// </summary>
        Task<BaptizeResult> ReassignAddressWithBoardNumberAsync(
            ButtonPanelType panelType,
            byte boardNumber,
            int timeoutMs = 5000,
            CancellationToken cancellationToken = default,
            bool forceLastByteToFF = false);

        /// <summary>
        /// Returns the currently-connected panel to a fully virgin auto-addressing state.
        /// Sends WHO_ARE_YOU with MachineType=0xFF and ResetAddressFlag=1: the panel firmware
        /// (AutoAddressSlave.c) writes 0xFF into EEPROM->IDMachineType, sets its STEM address
        /// to broadcast (0xFFFFFFFF), and enters AAS_ANSWER_TO_MASTER so it re-announces itself.
        /// On the next boot, AA_Slave_Init sees IDMachineType==0xFF and starts AAS_STARTUP — at
        /// which point the next host (Eden, Optimus, R3L, ...) auto-addresses the panel with a
        /// fresh STEM address.
        /// SET_ADDRESS to 0x1FFFFFFF does *not* achieve this: the firmware retains its existing
        /// IDMachineType in EEPROM and stays in AAS_STAND_BY, so a fresh host never sees the panel.
        /// </summary>
        /// <param name="timeoutMs">Send timeout in milliseconds.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if WHO_ARE_YOU was sent successfully; <c>false</c> otherwise.</returns>
        Task<bool> ResetToVirginAddressAsync(
            int timeoutMs = 5000,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Risultato dell'operazione di battezzamento.
    /// </summary>
    public class BaptizeResult
    {
        /// <summary>
        /// Indica se il battezzamento � riuscito.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Indirizzo MAC del dispositivo battezzato (96 bit = 12 byte).
        /// </summary>
        public byte[]? MacAddress { get; init; }

        /// <summary>
        /// Indirizzo STEM assegnato al dispositivo.
        /// </summary>
        public uint AssignedAddress { get; init; }

        /// <summary>
        /// Codice di errore (0 = OK, != 0 = errore).
        /// </summary>
        public byte ErrorCode { get; init; }

        /// <summary>
        /// Messaggio descrittivo del risultato.
        /// </summary>
        public string Message { get; init; } = string.Empty;
    }
}
