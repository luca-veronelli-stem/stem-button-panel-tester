# Core

> **Libreria di dominio puro per Stem.ButtonPanel.Tester. Contiene modelli, interfacce, enumerazioni e tipi Result — zero dipendenze esterne.**  
> **Ultimo aggiornamento:** 2026-04-03

---

## Panoramica

**Core** è il cuore del progetto Stem.ButtonPanel.Tester. Implementa il pattern **Clean Architecture** come layer più interno, definendo:

- **Modelli di dominio** per pulsantiere, pacchetti CAN, risultati test
- **Interfacce** per servizi, repository, comunicazione e infrastruttura
- **Enumerazioni** per tipi pulsantiera, stati, canali di comunicazione
- **Result Pattern** per gestione errori esplicita senza eccezioni
- **Eccezioni tipizzate** per errori di comunicazione

Nessun progetto della soluzione dipende da librerie esterne attraverso Core — è completamente **dependency-free**.

---

## Caratteristiche

| Feature | Stato | Descrizione |
|---------|-------|-------------|
| **Modelli Pulsantiera** | ✅ | 4 tipi: Eden-XP, Optimus-XP, R3L-XP, Eden-BS8 |
| **Result Pattern** | ✅ | `Result<T>` e `Result` per gestione errori funzionale |
| **Interfacce Clean** | ✅ | Contratti per Services, Data, Communication, Infrastructure |
| **Enums Tipizzati** | ✅ | `ButtonPanelType`, `CommunicationChannel`, `IndicatorState` |
| **Eccezioni Strutturate** | ✅ | Gerarchia `CommunicationException` con error codes |

---

## Requisiti

- **.NET 10.0** o superiore
- Nessuna dipendenza esterna (zero NuGet packages)

---

## Quick Start

```csharp
using Core.Enums;
using Core.Models.Services;
using Core.Results;

// Ottenere configurazione pulsantiera per tipo
var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0023789);
Console.WriteLine($"Pulsantiera: {panel.Type}, Pulsanti: {panel.ButtonCount}, LED: {panel.HasLed}");

// Usare Result Pattern per operazioni che possono fallire
Result<int> result = Result<int>.Success(42);
if (result.IsSuccess)
{
    Console.WriteLine($"Valore: {result.Value}");
}

// Pattern matching su Result
var outcome = result.Match(
    onSuccess: v => $"OK: {v}",
    onFailure: e => $"Errore: {e.Message}"
);
```

---

## Struttura

```
Core/
├── Enums/
│   ├── ButtonPanelEnums.cs      # ButtonPanelType, ButtonPanelTestType, IndicatorState
│   ├── CommunicationEnums.cs    # CommunicationChannel
│   └── ProtocolEnums.cs         # CryptType, ProtocolVersion
├── Exceptions/
│   └── CommunicationExceptions.cs  # Gerarchia eccezioni tipizzate
├── Interfaces/
│   ├── Communication/           # ICommunicationManager, IProtocolManager
│   ├── Data/                    # IExcelRepository, IProtocolRepository
│   ├── GUI/                     # IButtonPanelTestView
│   ├── Infrastructure/          # ICanAdapter, IAdapter
│   └── Services/                # IButtonPanelTestService, IBaptizeService
├── Models/
│   ├── Communication/           # CanPacket, NetworkPacketChunk
│   ├── Data/                    # StemProtocolData, StemRowData
│   ├── Services/                # ButtonPanel, ButtonPanelTestResult
│   ├── ButtonIndicator.cs       # Stato visivo indicatore
│   └── EventArgs.cs             # AppLayerDecoderEventArgs, etc.
└── Results/
    ├── Error.cs                 # Tipo errore strutturato
    ├── Result.cs                # Result<T> e Result
    └── ResultExtensions.cs      # Metodi estensione (Match, Map, etc.)
```

---

## API Principali

### Modelli

| Classe | Descrizione |
|--------|-------------|
| `ButtonPanel` | Configurazione pulsantiera (tipo, pulsanti, maschere, LED/buzzer) |
| `ButtonPanelTestResult` | Risultato test con pass/fail, messaggi, UUID dispositivo |
| `CanPacket` | Pacchetto CAN con ArbitrationId e Data |
| `NetworkPacketChunk` | Chunk di rete per trasmissione (NetInfo + Chunk) |

### Interfacce Chiave

| Interfaccia | Layer | Descrizione |
|-------------|-------|-------------|
| `IButtonPanelTestService` | Services | Orchestrazione test pulsanti/LED/buzzer |
| `IBaptizeService` | Services | Assegnazione indirizzi STEM |
| `ICommunicationService` | Services | Comunicazione astratta multi-canale |
| `ICanAdapter` | Infrastructure | Adattatore hardware PCAN |
| `IProtocolRepository` | Data | Repository comandi/variabili da Excel |

### Result Pattern

```csharp
// Result<T> per operazioni con valore di ritorno
Result<byte[]> response = await service.SendCommandAsync(command, payload);

// Pattern matching
string message = response.Match(
    onSuccess: data => $"Ricevuti {data.Length} byte",
    onFailure: error => $"Errore {error.Code}: {error.Message}"
);

// Result senza valore (void-like)
Result connectionResult = await manager.ConnectAsync(config);
if (connectionResult.IsFailure)
{
    throw CommunicationException.FromError(connectionResult.Error);
}
```

### Enumerazioni

| Enum | Valori |
|------|--------|
| `ButtonPanelType` | `DIS0023789`, `DIS0025205`, `DIS0026166`, `DIS0026182` |
| `ButtonPanelTestType` | `Complete`, `Buttons`, `Led`, `Buzzer` |
| `CommunicationChannel` | `Can`, `Ble`, `Serial` |
| `IndicatorState` | `Idle`, `Waiting`, `Success`, `Failed` |

---

## Issue Correlate

→ [Core/ISSUES.md](./ISSUES.md)

---

## Links

- [ISSUES_TRACKER.md](../ISSUES_TRACKER.md) — Tracker globale issue
- [Infrastructure/README.md](../Infrastructure/README.md) — Adattatore hardware PCAN
- [Communication/README.md](../Communication/README.md) — Stack protocollare STEM
- [Services/README.md](../Services/README.md) — Logica di business
