# Services

> **Layer di business logic per test pulsantiere. Include servizi di test, battezzamento, comunicazione e FSM per workflow.**  
> **Ultimo aggiornamento:** 2026-04-03

---

## Panoramica

**Services** implementa la logica di business per il collaudo delle pulsantiere STEM. Il layer orchestra:

- **ButtonPanelTestService** — Esecuzione test pulsanti/LED/buzzer con FSM
- **BaptizeService** — Assegnazione indirizzi STEM ai dispositivi
- **CommunicationService** — Astrazione comunicazione multi-canale (CAN/BLE/Serial)
- **Heartbeat Monitoring** — Rilevamento perdita comunicazione e auto-recovery
- **Helpers** — Costruzione payload, parsing risposte, calcolo indirizzi

Il pattern **State Machine** gestisce il flusso dei test garantendo transizioni valide e gestione errori strutturata.

---

## Caratteristiche

| Feature | Stato | Descrizione |
|---------|-------|-------------|
| **Test Pulsanti** | ✅ | Test sequenziale con attesa pressione |
| **Test LED** | ✅ | Verifica visiva con conferma utente |
| **Test Buzzer** | ✅ | Verifica acustica con conferma utente |
| **Battezzamento** | ✅ | WHO_ARE_YOU → WHO_AM_I → SET_ADDRESS |
| **Heartbeat** | ✅ | Monitoraggio attivo ogni 1s |
| **Recovery** | ✅ | Riconnessione automatica CAN |
| **Result Pattern** | ✅ | Gestione errori esplicita |
| **State Machine** | ✅ | FSM per workflow test |

---

## Requisiti

- **.NET 10.0** o superiore
- Nessuna dipendenza esterna (zero NuGet packages)

### Dipendenze

| Package | Versione | Uso |
|---------|----------|-----|
| `Core` | (progetto) | Interfacce, modelli, Result Pattern |
| `Communication` | (progetto) | Stack protocollare STEM |

---

## Quick Start

```csharp
using Services;
using Core.Enums;

// Setup con DI
var communicationService = serviceProvider.GetRequiredService<ICommunicationService>();
var baptizeService = serviceProvider.GetRequiredService<IBaptizeService>();
var protocolRepo = serviceProvider.GetRequiredService<IProtocolRepository>();

var testService = new ButtonPanelTestService(
    communicationService,
    baptizeService,
    protocolRepo,
    logger);

// Abilita heartbeat monitoring
testService.SetCanAdapter(canAdapter);

// Sottoscrivi eventi
testService.CommunicationLost += () =>
    Console.WriteLine("Comunicazione CAN persa!");

// Esegui battezzamento
var baptizeResult = await testService.BaptizeDeviceAsync(
    ButtonPanelType.DIS0023789,
    cancellationToken: cts.Token);

if (baptizeResult.Success)
{
    Console.WriteLine($"Indirizzo assegnato: 0x{baptizeResult.AssignedAddress:X8}");
    
    // Esegui test completo
    var results = await testService.TestAllAsync(
        ButtonPanelType.DIS0023789,
        userConfirm: msg => ShowDialogAsync(msg),
        userPrompt: msg => UpdateUIAsync(msg),
        onButtonStart: i => HighlightButton(i),
        onButtonResult: (i, passed) => ShowResult(i, passed),
        cts.Token);
    
    foreach (var result in results)
    {
        Console.WriteLine($"{result.TestType}: {(result.Passed ? "✓" : "✗")} - {result.Message}");
    }
}
```

---

## Struttura

```
Services/
├── ButtonPanelTestService.cs     # Servizio principale test
├── BaptizeService.cs             # Servizio battezzamento dispositivi
├── CommunicationService.cs       # Astrazione comunicazione multi-canale
├── Helpers/
│   ├── PayloadBuilder.cs         # Costruzione payload comandi
│   ├── ResponseParser.cs         # Parsing risposte WHO_AM_I
│   ├── StemAddressHelper.cs      # Calcolo indirizzi STEM
│   └── TestResultFactory.cs      # Factory per risultati test
├── Lib/
│   ├── ButtonPanelTestState.cs       # Enum stati FSM
│   ├── ButtonPanelTestContext.cs     # Contesto dati FSM
│   ├── ButtonPanelTestStateMachine.cs # FSM per workflow test
│   └── CommunicationManagerFactory.cs # Factory manager comunicazione
└── Models/
    ├── ProtocolConstants.cs      # Costanti protocollo STEM
    └── PanelTypeConfiguration.cs # Configurazioni per tipo pannello
```

---

## API / Componenti

### ButtonPanelTestService

Servizio principale per esecuzione test:

```csharp
public class ButtonPanelTestService : IButtonPanelTestService
{
    // Eventi
    event Action<ButtonPanelTestState, ButtonPanelTestState> StateChanged;
    event Action CommunicationLost;
    
    // Proprietà
    ButtonPanelTestState CurrentState { get; }
    bool IsTestRunning { get; }
    
    // Test
    Task<List<ButtonPanelTestResult>> TestAllAsync(...);
    Task<ButtonPanelTestResult> TestButtonsAsync(...);
    Task<ButtonPanelTestResult> TestLedAsync(...);
    Task<ButtonPanelTestResult> TestBuzzerAsync(...);
    
    // Battezzamento
    Task<BaptizeResult> BaptizeDeviceAsync(ButtonPanelType, int timeout, CancellationToken);
    Task<BaptizeResult> ReassignAddressAsync(ButtonPanelType, int timeout, CancellationToken, bool forceFF);
    Task<List<byte[]>> ScanForUnbaptizedDevicesAsync(int timeout, CancellationToken);
    
    // Configurazione
    void SetProtocolRepository(IProtocolRepository);
    void SetCanAdapter(ICanAdapter?);  // Abilita heartbeat
    
    // Controllo
    void CancelTest();
    Task ForceDisconnectAsync();
}
```

### BaptizeService

Gestione assegnazione indirizzi STEM:

```csharp
public class BaptizeService : IBaptizeService
{
    // Battezzamento base (boardNumber = 0x01)
    Task<BaptizeResult> BaptizeAsync(ButtonPanelType, int timeout, CancellationToken);
    
    // Battezzamento con board number specifico
    Task<BaptizeResult> BaptizeWithBoardNumberAsync(ButtonPanelType, byte boardNumber, int timeout, CancellationToken);
    
    // Riassegnazione indirizzo (per dispositivi già battezzati)
    Task<BaptizeResult> ReassignAddressAsync(ButtonPanelType, int timeout, CancellationToken, bool forceFF);
    Task<BaptizeResult> ReassignAddressWithBoardNumberAsync(...);
    
    // Scansione dispositivi
    Task<List<byte[]>> ScanForDevicesAsync(int timeout, CancellationToken);
}
```

### CommunicationService

Astrazione comunicazione multi-canale:

```csharp
public class CommunicationService : ICommunicationService, IAsyncDisposable
{
    // Eventi
    event EventHandler<AppLayerDecoderEventArgs> CommandDecoded;
    event EventHandler<CommunicationErrorEventArgs> ErrorOccurred;
    event Action<uint, byte[]> RawPacketReceived;
    
    // Canale
    Task<Result> SetActiveChannelAsync(CommunicationChannel, string config, CancellationToken);
    Task<Result> DisconnectActiveChannelAsync(CancellationToken);
    bool IsChannelConnected();
    
    // Comunicazione
    Task<Result<byte[]>> SendCommandAsync(ushort command, byte[] payload, bool waitAnswer, ...);
    Task<Result> SendRawPacketAsync(uint arbitrationId, byte[] data, CancellationToken);
    void SetSenderRecipientIds(uint senderId, uint recipientId);
}
```

---

## Workflow Test

### Flusso TestAllAsync

```
┌─────────────────────────────────────────────────────────────┐
│                    TestAllAsync                              │
├─────────────────────────────────────────────────────────────┤
│  1. Connessione CAN (250 kbit/s)                            │
│  2. Battezzamento → assegnazione indirizzo STEM             │
│  3. Test Pulsanti (sequenziale per tutti i pulsanti)        │
│     ├── Prompt: "Premi pulsante X"                          │
│     ├── Attendi pressione (timeout 5s)                      │
│     └── Registra risultato (pass/fail)                      │
│  4. Test LED (se panel.HasLed)                              │
│     ├── Accendi LED verde                                   │
│     ├── Confirm: "LED verde visibile?"                      │
│     ├── Accendi LED rosso                                   │
│     └── Confirm: "LED rosso visibile?"                      │
│  5. Test Buzzer                                             │
│     ├── Attiva buzzer (SINGLE_BLINK)                        │
│     └── Confirm: "Buzzer udibile?"                          │
│  6. Ritorna List<ButtonPanelTestResult>                     │
└─────────────────────────────────────────────────────────────┘
```

### State Machine

```
                      ┌──────────┐
                      │   Idle   │
                      └────┬─────┘
                           │ StartTest()
                           ▼
                   ┌───────────────┐
                   │ Initializing  │
                   └───────┬───────┘
                           │ InitializationComplete()
                           ▼
               ┌─────────────────────────┐
        ┌──────│ AwaitingButtonPress     │◄────────┐
        │      └───────────┬─────────────┘         │
        │                  │ RecordButtonResult()  │
        │                  ▼                       │
        │      ┌─────────────────────────┐         │
        │      │ RecordingButtonResult   │─────────┘
        │      └───────────┬─────────────┘  (more buttons)
        │                  │ (all buttons done)
        │                  ▼
        │      ┌─────────────────────────┐
        │      │     TestingLed         │ (se HasLed)
        │      └───────────┬─────────────┘
        │                  │
        │                  ▼
        │      ┌─────────────────────────┐
        │      │    TestingBuzzer       │
        │      └───────────┬─────────────┘
        │                  │
        │                  ▼
        │      ┌─────────────────────────┐
        │      │     Completed          │
        │      └─────────────────────────┘
        │
        │ Cancel() / Error
        ▼
   ┌───────────┐     ┌───────────┐
   │Interrupted│     │   Error   │
   └───────────┘     └───────────┘
```

---

## Protocollo Battezzamento

### Sequenza WHO_ARE_YOU → SET_ADDRESS

```
Computer                                    Pulsantiera (vergine)
    │                                              │
    │──── WHO_ARE_YOU (0x0023) ──────────────────►│
    │     [MachineType, FwType_H, FwType_L, Reset] │
    │                                              │
    │◄─── WHO_AM_I (0x0024) ───────────────────────│
    │     [MachineType, FwType_H, FwType_L, UUID[12]]
    │                                              │
    │──── SET_ADDRESS (0x0025) ────────────────────►│
    │     [UUID[12], StemAddress[4]]               │
    │                                              │
    │◄─── ACK ─────────────────────────────────────│
    │                                              │
```

### Calcolo Indirizzo STEM

Formula: `(MachineType << 16) | ((FirmwareType & 0x03FF) << 6) | (BoardNumber & 0x003F)`

**Esempio:**
- MachineType: `0x03` (Eden)
- FirmwareType: `0x0004`
- BoardNumber: `0x01`
- Risultato: `0x00030101`

---

## Heartbeat Monitoring

### Configurazione

```csharp
// Abilita heartbeat
testService.SetCanAdapter(canAdapter);

// Sottoscrivi a perdita comunicazione
testService.CommunicationLost += () =>
{
    // Notifica utente
    // Tenta recovery
};
```

### Parametri

| Parametro | Valore | Descrizione |
|-----------|--------|-------------|
| `HeartbeatIntervalMs` | 1000ms | Intervallo tra ping |
| `HeartbeatTimeoutMs` | 500ms | Timeout risposta |
| `MaxMissedHeartbeats` | 3 | Heartbeat mancati prima di recovery |

### Flusso

```
1. Ogni 1s: invia CMD_HEARTBEAT (0x0000)
2. Attendi risposta CMD_HEARTBEAT_RESPONSE (0x8000) entro 500ms
3. Se timeout: incrementa missedHeartbeats
4. Se missedHeartbeats >= 3:
   a. Tenta recovery CAN (via ICanAdapter)
   b. Se fallisce: genera evento CommunicationLost
```

---

## Costanti Protocollo

```csharp
public static class ProtocolConstants
{
    // Comandi
    public const ushort CMD_WHO_ARE_YOU = 0x0023;
    public const ushort CMD_WHO_AM_I = 0x0024;
    public const ushort CMD_SET_ADDRESS = 0x0025;
    public const ushort CMD_HEARTBEAT = 0x0000;
    public const ushort CMD_HEARTBEAT_RESPONSE = 0x8000;
    
    // CAN IDs
    public const uint ComputerSenderId = 0x00030141;  // Eden madre
    public const uint VirginPanelId = 0x1FFFFFFF;     // Pulsantiere vergini
    
    // Timeout
    public const int DefaultTimeoutMs = 15000;
    public const int HeartbeatIntervalMs = 1000;
    public const int MaxMissedHeartbeats = 3;
}
```

---

## Configurazioni Pannelli

| Tipo | MachineType | FirmwareType | TargetAddress | Pulsanti | LED |
|------|-------------|--------------|---------------|----------|-----|
| DIS0023789 (Eden-XP) | 0x03 | 0x0004 | 0x00030101 | 8 | ✅ |
| DIS0025205 (Optimus) | 0x0A | 0x0004 | 0x000A0101 | 4 | ❌ |
| DIS0026166 (R3L-XP) | 0x0B | 0x0004 | 0x000B0101 | 8 | ✅ |
| DIS0026182 (Eden-BS8) | 0x0C | 0x0004 | 0x000C0101 | 8 | ✅ |

---

## Issue Correlate

→ [Services/ISSUES.md](./ISSUES.md)

**Issue Alta Priorità:**
- `SVC-001` — _heartbeatLock usa object invece di Lock (correlata a T-001)
- `SVC-002` — Task.Run fire-and-forget in NotifyCommunicationLost

---

## Links

- [ISSUES_TRACKER.md](../ISSUES_TRACKER.md) — Tracker globale issue
- [Core/README.md](../Core/README.md) — Interfacce IButtonPanelTestService, IBaptizeService
- [Communication/README.md](../Communication/README.md) — Stack protocollare STEM
- [Data/README.md](../Data/README.md) — Repository comandi/variabili
- [GUI.WinForms/README.md](../GUI.WinForms/README.md) — UI che consuma questi servizi
