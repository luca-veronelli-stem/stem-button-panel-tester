namespace Services.Models
{
    /// <summary>
    /// Costanti del protocollo STEM CAN.
    /// </summary>
    public static class ProtocolConstants
    {
        // Protocol Commands
        public const ushort CMD_WHO_ARE_YOU = 0x0023;
        public const ushort CMD_WHO_AM_I = 0x0024;
        public const ushort CMD_SET_ADDRESS = 0x0025;
        public const ushort CMD_HEARTBEAT = 0x0000;           // Comando heartbeat/ping
        public const ushort CMD_HEARTBEAT_RESPONSE = 0x8000;  // Risposta heartbeat (bit 7 set)

        // Protocol Command Names
        public const string WriteVariableCommand = "Scrivi variabile logica";

        // Variable Names
        public const string GreenLedVariable = "Comando Led Verde";
        public const string RedLedVariable = "Comando Led Rosso";
        public const string BuzzerVariable = "Comando Buzzer";

        // Values
        public const string OnValue = "ON";
        public const string OffValue = "OFF";
        public const string SingleBlinkValue = "SINGLE_BLINK";

        // CAN Configuration
        public const string DefaultCanConfig = "250";

        // CAN IDs - Nel protocollo STEM CAN:
        // - CAN Arbitration ID: identifica il destinatario del messaggio
        // - Transport SenderId: identifica il mittente logico nel pacchetto
        public const uint ComputerSenderId = 0x00030141;  // Eden madre - SenderId del computer
        public const uint VirginPanelId = 0x1FFFFFFF;     // Indirizzo pulsantiere post-riprogrammazione (vergini)
        public const uint BroadcastId = 0xFFFFFFFF;       // Broadcast generico (non usato per battezzamento)
        public const uint PanelListenId = 0x0000013F;     // Arbitration ID su cui la pulsantiera ascolta
        public const uint PanelTransmitId = 0x00000101;   // Arbitration ID su cui la pulsantiera trasmette

        // Reset Flags
        public const byte ResetAddressFlag = 0x01;
        public const byte NoResetFlag = 0x00;

        // Timeouts
        public const int DefaultTimeoutMs = 15000;  // Aumentato per pulsantiere già battezzate che devono fare auto-reset (~5-6 sec)
        public const int VerificationTimeoutMs = 2000;
        public const int CommandDelayMs = 100;
        public const int DeviceReconfigurationDelayMs = 500;

        // Heartbeat
        public const int HeartbeatIntervalMs = 1000;      // Intervallo tra heartbeat (1 secondo)
        public const int HeartbeatTimeoutMs = 500;        // Timeout per risposta heartbeat
        public const int MaxMissedHeartbeats = 3;         // Numero massimo di heartbeat mancati prima di recovery
    }
}
