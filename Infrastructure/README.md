# Infrastructure

> **Adattatore hardware per comunicazione CAN tramite PEAK PCAN. Include auto-recovery, diagnostica e astrazione testabile.**  
> **Ultimo aggiornamento:** 2026-04-03

---

## Panoramica

**Infrastructure** implementa l'adattatore hardware per la comunicazione CAN tramite dispositivi **PEAK PCAN USB**. Il layer si occupa di:

- **Connessione/disconnessione** al bus CAN con configurazione baudrate
- **Ricezione asincrona** messaggi CAN con polling continuo
- **Invio messaggi** CAN con gestione errori
- **Auto-recovery** in caso di disconnessione o errori del bus
- **Monitoraggio salute** del canale con diagnostica
- **Astrazione testabile** tramite interfaccia `IPcanApi`

Il componente implementa il contratto `ICanAdapter` definito in Core, permettendo al resto dell'applicazione di essere agnostico rispetto all'hardware specifico.

---

## Caratteristiche

| Feature | Stato | Descrizione |
|---------|-------|-------------|
| **Auto-Recovery** | ✅ | Riconnessione automatica fino a 3 tentativi |
| **Health Check** | ✅ | Monitoraggio traffico e stato bus ogni ~1s |
| **Diagnostica** | ✅ | Contatori RX/TX, errori, recovery attempts |
| **Astrazione Testabile** | ✅ | `IPcanApi` per mock in unit test |
| **Logging Strutturato** | ✅ | `ILogger<PcanAdapter>` per diagnostica |
| **Eventi Tipizzati** | ✅ | `ConnectionStatusChanged`, `PacketReceived`, `RecoveryAttempted` |

---

## Requisiti

- **.NET 10.0** o superiore
- **Hardware PEAK PCAN-USB** o compatibile
- **Driver PCAN** installato (Windows)

### Dipendenze

| Package | Versione | Uso |
|---------|----------|-----|
| `Peak.PCANBasic.NET` | 4.10.1.968 | Wrapper .NET per driver PCAN |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.1 | Logging strutturato |
| `Core` | (progetto) | Interfacce, modelli, Result Pattern |

---

## Quick Start

```csharp
using Infrastructure;
using Infrastructure.Lib;
using Microsoft.Extensions.Logging;

// Setup
var logger = loggerFactory.CreateLogger<PcanAdapter>();
var pcanApi = new PcanApiWrapper();
var adapter = new PcanAdapter(pcanApi, logger);

// Sottoscrivi eventi
adapter.ConnectionStatusChanged += (s, connected) =>
    Console.WriteLine($"CAN {(connected ? "connesso" : "disconnesso")}");

adapter.PacketReceived += (s, packet) =>
    Console.WriteLine($"RX: 0x{packet.ArbitrationId:X8} [{packet.Data.Length}]");

adapter.RecoveryAttempted += status =>
    Console.WriteLine($"Recovery: {status}");

// Connetti al bus CAN a 250 kbit/s
bool connected = await adapter.ConnectAsync("250");
if (!connected)
{
    Console.WriteLine("Errore connessione CAN");
    return;
}

// Invia messaggio
var message = new CanPacket(0x00000101, new byte[] { 0x01, 0x02, 0x03 });
bool sent = await adapter.Send(0x00000101, message.Data, useExtendedId: true);

// Disconnetti
await adapter.DisconnectAsync();
await adapter.DisposeAsync();
```

---

## Struttura

```
Infrastructure/
├── Lib/
│   ├── IPcanApi.cs           # Interfaccia astrazione PCAN API
│   └── PcanApiWrapper.cs     # Wrapper concreto per PCANBasic.dll
└── PcanAdapter.cs            # Implementazione ICanAdapter con auto-recovery
```

---

## API / Componenti

### PcanAdapter

Implementazione completa di `ICanAdapter` con funzionalità avanzate:

```csharp
public sealed class PcanAdapter : ICanAdapter
{
    // Eventi
    event EventHandler<bool> ConnectionStatusChanged;
    event EventHandler<CanPacket> PacketReceived;
    event Action<string> RecoveryAttempted;
    event Action PhysicalReconnectRequired;

    // Proprietà
    bool IsConnected { get; }

    // Metodi principali
    Task<bool> ConnectAsync(string config, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task<bool> Send(uint arbitrationId, byte[] data, bool useExtendedId);
    ValueTask DisposeAsync();
}
```

### Configurazione

| Parametro | Formato | Esempio | Descrizione |
|-----------|---------|---------|-------------|
| `config` | `"<baudrate>"` | `"250"` | Baudrate in kbit/s (250, 500, 1000) |

**Baudrate supportati:**
- `125` → 125 kbit/s
- `250` → 250 kbit/s (standard STEM)
- `500` → 500 kbit/s
- `1000` → 1 Mbit/s

### Auto-Recovery

Il `PcanAdapter` implementa auto-recovery automatico in caso di:
- **Bus-Off** — Reset automatico del controller CAN
- **Disconnessione USB** — Fino a 3 tentativi di riconnessione
- **Nessun traffico per 60s** — Verifica e reinizializzazione

**Parametri recovery:**
```csharp
const int MAX_RECOVERY_ATTEMPTS = 3;       // Tentativi prima di arrendersi
const int RECOVERY_DELAY_MS = 500;         // Pausa tra tentativi
const int NO_TRAFFIC_RECOVERY_MS = 60000;  // Timeout inattività
```

**Eventi generati:**
- `RecoveryAttempted("Attempt 1/3")` — Tentativo in corso
- `RecoveryAttempted("SUCCESS")` — Recovery riuscito
- `RecoveryAttempted("FAILED")` — Recovery fallito
- `PhysicalReconnectRequired()` — Serve intervento utente (ricollegare USB)

### Diagnostica

Contatori interni per monitoraggio:

| Contatore | Descrizione |
|-----------|-------------|
| `_totalRxCount` | Messaggi ricevuti totali |
| `_totalTxCount` | Messaggi inviati con successo |
| `_totalTxFailCount` | Messaggi falliti in invio |
| `_totalReadErrors` | Errori lettura (bus-off, overflow) |
| `_totalRecoveryAttempts` | Tentativi recovery totali |
| `_failedRecoveryAttempts` | Recovery falliti |

Logging automatico ogni ~5 secondi con `ILogger`:
```
[PcanAdapter] Status: BusOk, RX=1234, TX=567, Errors=0, Uptime=00:05:23
```

---

## Testing

### Unit Test con Mock

Grazie a `IPcanApi`, è possibile testare `PcanAdapter` senza hardware:

```csharp
[Fact]
public async Task ConnectAsync_WithValidConfig_ReturnsTrue()
{
    // Arrange
    var mockApi = new Mock<IPcanApi>();
    mockApi.Setup(x => x.Initialize(It.IsAny<PcanChannel>(), It.IsAny<Bitrate>()))
           .Returns(PcanStatus.OK);
    mockApi.Setup(x => x.GetStatus(It.IsAny<PcanChannel>()))
           .Returns(PcanStatus.OK);

    var logger = Mock.Of<ILogger<PcanAdapter>>();
    var adapter = new PcanAdapter(mockApi.Object, logger);

    // Act
    bool result = await adapter.ConnectAsync("250");

    // Assert
    Assert.True(result);
    Assert.True(adapter.IsConnected);
}
```

---

## Configurazione

### Driver PCAN

Installare i driver PEAK da: https://www.peak-system.com/PCAN-USB.199.0.html

Verificare installazione:
```powershell
# Windows Device Manager → Universal Serial Bus controllers
# Dovrebbe apparire "PEAK-System PCAN-USB"
```

### Logging

Configurare `ILogger` in DI per diagnostica:

```csharp
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddConsole();
    builder.AddDebug();
});
```

**Livelli log:**
- `Information` — Connessione/disconnessione, recovery success
- `Warning` — Nessun traffico, tentativi recovery
- `Error` — Errori bus, recovery falliti
- `Debug` — Status periodico, messaggi RX (se attivato)

---

## Issue Correlate

→ [Infrastructure/ISSUES.md](./ISSUES.md)

**Issue Alta Priorità:**
- `INFRA-001` — _recoveryLock usa object invece di Lock (correlata a T-001)

---

## Links

- [ISSUES_TRACKER.md](../ISSUES_TRACKER.md) — Tracker globale issue
- [Core/README.md](../Core/README.md) — Interfacce e modelli dominio
- [Communication/README.md](../Communication/README.md) — Stack protocollare STEM
- [Peak PCAN Documentation](https://www.peak-system.com/fileadmin/media/files/pcan-basic.pdf) — Documentazione driver
