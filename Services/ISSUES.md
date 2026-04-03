# Services - ISSUES

> **Scopo:** Questo documento traccia bug, code smells, performance issues, opportunità di refactoring e violazioni di best practice per il componente **Services**.

> **Ultimo aggiornamento:** 2026-04-03

---

## Riepilogo

| Priorità | Aperte | Risolte |
|----------|--------|---------|
| **Critica** | 0 | 0 |
| **Alta** | 2 | 0 |
| **Media** | 3 | 0 |
| **Bassa** | 2 | 0 |

**Totale aperte:** 7
**Totale risolte:** 0

---

## Indice Issue Aperte

- [SVC-001 - _heartbeatLock e _stateLock usano object invece di Lock](#svc-001--_heartbeatlock-e-_statelock-usano-object-invece-di-lock)
- [SVC-002 - Task.Run fire-and-forget in NotifyCommunicationLost](#svc-002--taskrun-fire-and-forget-in-notifycommunicationlost)
- [SVC-003 - _protocolRepository è non-readonly ma non dovrebbe essere modificato](#svc-003--_protocolrepository-è-non-readonly-ma-non-dovrebbe-essere-modificato)
- [SVC-004 - WhoAmIResponse.Uuid è mutabile](#svc-004--whoamiresponseuuid-è-mutabile)
- [SVC-005 - DisposeManagerAsync è statico privato e mai chiamato](#svc-005--disposemanagerasync-è-statico-privato-e-mai-chiamato)
- [SVC-006 - PanelTypeConfiguration._configurations è pubblicamente visibile come Dictionary](#svc-006--paneltypeconfiguration_configurations-è-pubblicamente-visibile-come-dictionary)
- [SVC-007 - Mancanza di CancellationToken in metodi sincroni](#svc-007--mancanza-di-cancellationtoken-in-metodi-sincroni)

---

## Priorità Alta

### SVC-001 - _heartbeatLock e _stateLock usano object invece di Lock

**Categoria:** Anti-Pattern
**Priorità:** Alta
**Impatto:** Medio
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Sia `ButtonPanelTestService._heartbeatLock` che `ButtonPanelTestStateMachine._stateLock` usano `object` per il locking. Da .NET 9+ esiste `System.Threading.Lock` più performante e type-safe. Questo è lo stesso anti-pattern identificato in INFRA-001, DATA-003 e COMM-001.

#### File Coinvolti

- `Services/ButtonPanelTestService.cs` (riga 34)
- `Services/Lib/ButtonPanelTestStateMachine.cs` (riga 14)

#### Codice Problematico

```csharp
// ButtonPanelTestService.cs
private readonly object _heartbeatLock = new();

// ButtonPanelTestStateMachine.cs
private readonly object _stateLock = new();
```

#### Soluzione Proposta

Usare `System.Threading.Lock`:

```csharp
private readonly Lock _heartbeatLock = new();
private readonly Lock _stateLock = new();
```

#### Benefici Attesi

- Performance migliore (~20% più veloce)
- Type-safety
- Consistenza con le altre issue correlate

---

### SVC-002 - Task.Run fire-and-forget in NotifyCommunicationLost

**Categoria:** Anti-Pattern
**Priorità:** Alta
**Impatto:** Medio
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Il metodo `NotifyCommunicationLost` usa `_ = Task.Run(async () => { ... })` per disconnettere in background. Questo pattern fire-and-forget ignora eventuali eccezioni e non permette di attendere il completamento.

#### File Coinvolti

- `Services/ButtonPanelTestService.cs` (righe 132-143)

#### Codice Problematico

```csharp
// Disconnetti la comunicazione in modo asincrono
_ = Task.Run(async () =>
{
    try
    {
        await DisconnectCommunicationAsync().ConfigureAwait(false);
        _logger?.LogInformation("Comunicazione CAN disconnessa dopo perdita comunicazione");
    }
    catch (Exception ex)
    {
        _logger?.LogWarning(ex, "Errore durante la disconnessione dopo perdita comunicazione");
    }
});
```

#### Soluzione Proposta

Anche se il try/catch interno gestisce le eccezioni, il pattern rimane problematico per testabilità. Opzioni:

1. **Fire-and-forget con ContinueWith** per logging uniforme
2. **Salvare il Task** e permettere di attenderlo se necessario

```csharp
private Task? _disconnectTask;

// Nel metodo:
_disconnectTask = Task.Run(async () =>
{
    try
    {
        await DisconnectCommunicationAsync().ConfigureAwait(false);
        _logger?.LogInformation("Comunicazione CAN disconnessa dopo perdita comunicazione");
    }
    catch (Exception ex)
    {
        _logger?.LogWarning(ex, "Errore durante la disconnessione dopo perdita comunicazione");
    }
});
```

#### Benefici Attesi

- Task tracciabile per test
- Pattern più esplicito

---

## Priorità Media

### SVC-003 - _protocolRepository è non-readonly ma non dovrebbe essere modificato

**Categoria:** Code Smell
**Priorità:** Media
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Il campo `_protocolRepository` in `ButtonPanelTestService` non ha il modificatore `readonly`, ma viene assegnato solo nel costruttore. Questo permette modifiche accidentali.

#### File Coinvolti

- `Services/ButtonPanelTestService.cs` (riga 27)

#### Codice Problematico

```csharp
private IProtocolRepository _protocolRepository;  // <-- manca readonly
```

#### Soluzione Proposta

Aggiungere `readonly`:

```csharp
private readonly IProtocolRepository _protocolRepository;
```

#### Benefici Attesi

- Immutabilità garantita
- Segnalazione errori a compile-time se si tenta di riassegnare

---

### SVC-004 - WhoAmIResponse.Uuid è mutabile

**Categoria:** Robustezza
**Priorità:** Media
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

`WhoAmIResponse` è uno struct con `Uuid` che è un `byte[]` direttamente esposto. Gli array sono mutabili, quindi il chiamante può modificare il contenuto.

#### File Coinvolti

- `Services/Helpers/ResponseParser.cs` (righe 62-67)

#### Codice Problematico

```csharp
public struct WhoAmIResponse
{
    public byte MachineType { get; init; }
    public ushort FirmwareType { get; init; }
    public byte[] Uuid { get; init; }  // <-- array mutabile
}
```

#### Soluzione Proposta

Usare `ImmutableArray<byte>` o `ReadOnlyMemory<byte>`:

```csharp
public struct WhoAmIResponse
{
    public byte MachineType { get; init; }
    public ushort FirmwareType { get; init; }
    public ImmutableArray<byte> Uuid { get; init; }
}

// Oppure clonare all'uscita
public byte[] GetUuidCopy() => (byte[])_uuid.Clone();
```

#### Benefici Attesi

- Immutabilità garantita dello struct
- Nessuna modifica accidentale dell'UUID

---

### SVC-005 - DisposeManagerAsync è statico privato e mai chiamato

**Categoria:** Code Smell
**Priorità:** Media
**Impatto:** Nullo
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Il metodo `DisposeManagerAsync` in `CommunicationService` è dichiarato statico privato ma non viene mai chiamato. Il commento indica che il manager non deve essere disposato perché è un singleton, quindi questo metodo è codice morto.

#### File Coinvolti

- `Services/CommunicationService.cs` (righe 299+)

#### Codice Problematico

```csharp
private static async Task DisposeManagerAsync(ICommunicationManager manager)
{
    // ... mai chiamato
}
```

#### Soluzione Proposta

Rimuovere il metodo se non serve:

```csharp
// Rimuovere completamente DisposeManagerAsync
```

Oppure, se potrebbe servire in futuro, aggiungere un commento esplicito:

```csharp
// Reserved for future use when manager disposal is needed
[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
private static async Task DisposeManagerAsync(ICommunicationManager manager)
```

#### Benefici Attesi

- Codice più pulito
- Nessun dead code

---

## Priorità Bassa

### SVC-006 - PanelTypeConfiguration._configurations è pubblicamente visibile come Dictionary

**Categoria:** Robustezza
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Il dizionario `_configurations` in `PanelTypeConfiguration` è un `Dictionary<>` privato e statico, ma se fosse esposto (anche accidentalmente via reflection), sarebbe mutabile. Meglio usare `ImmutableDictionary` per garantire immutabilità.

#### File Coinvolti

- `Services/Models/PanelTypeConfiguration.cs` (righe 15-45)

#### Codice Problematico

```csharp
private static readonly Dictionary<ButtonPanelType, PanelTypeConfiguration> _configurations = new()
{
    // ...
};
```

#### Soluzione Proposta

Usare `ImmutableDictionary` o `FrozenDictionary` (.NET 8+):

```csharp
private static readonly FrozenDictionary<ButtonPanelType, PanelTypeConfiguration> _configurations =
    new Dictionary<ButtonPanelType, PanelTypeConfiguration>
    {
        // ...
    }.ToFrozenDictionary();
```

#### Benefici Attesi

- Immutabilità garantita
- Performance lookup migliore con `FrozenDictionary`

---

### SVC-007 - Mancanza di CancellationToken in metodi sincroni

**Categoria:** Design
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Alcuni metodi sincroni non accettano `CancellationToken` anche se potrebbero essere chiamati in contesti cancellabili:
- `SetCanAdapter()`
- `SetSenderRecipientIds()`
- `IsChannelConnected()`

Non è critico perché sono operazioni immediate, ma per consistenza API potrebbero accettare il token.

#### File Coinvolti

- `Services/ButtonPanelTestService.cs` (riga 85)
- `Services/CommunicationService.cs` (righe 224, 234)

#### Soluzione Proposta

Questo è più una nota di design che un problema reale. I metodi sono immediate e non bloccanti, quindi non necessitano di cancellazione. Nessuna azione richiesta, ma documentare la scelta.

#### Benefici Attesi

- Documentazione esplicita della scelta di design

---

## Issue Risolte

(Nessuna issue risolta finora)
