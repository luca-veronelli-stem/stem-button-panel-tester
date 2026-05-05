# Infrastructure - ISSUES

> **Scopo:** Questo documento traccia bug, code smells, performance issues, opportunità di refactoring e violazioni di best practice per il componente **Infrastructure**.

> **Ultimo aggiornamento:** 2026-04-03

---

## Riepilogo

| Priorità | Aperte | Risolte |
|----------|--------|---------|
| **Critica** | 0 | 0 |
| **Alta** | 1 | 0 |
| **Media** | 2 | 0 |
| **Bassa** | 2 | 0 |

**Totale aperte:** 5
**Totale risolte:** 0

---

## Indice Issue Aperte

- [INFRA-001 - _recoveryLock usa object invece di Lock](#infra-001--_recoverylock-usa-object-invece-di-lock)
- [INFRA-002 - TryRecoveryAsync fire-and-forget con underscore discard](#infra-002--tryrecoveryasync-fire-and-forget-con-underscore-discard)
- [INFRA-003 - TryAggressiveRecoveryAsync usa Task.Delay senza CancellationToken](#infra-003--tryaggressiverecoveryasync-usa-taskdelay-senza-cancellationtoken)
- [INFRA-004 - Magic numbers sparsi nel file PcanAdapter](#infra-004--magic-numbers-sparsi-nel-file-pcanadapter)
- [INFRA-005 - PcanApiWrapper.Read logga a Trace ogni messaggio ricevuto](#infra-005--pcanapiwrapperread-logga-a-trace-ogni-messaggio-ricevuto)

---

## Priorità Alta

### INFRA-001 - _recoveryLock usa object invece di Lock

**Categoria:** Anti-Pattern
**Priorità:** Alta
**Impatto:** Medio
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Il campo `_recoveryLock` è dichiarato come `object` e usato con `lock()`. Da .NET 9+ esiste la classe `System.Threading.Lock` che è più performante e type-safe. Inoltre, `lock(object)` è un anti-pattern noto perché:
- Non impedisce lock esterni accidentali su oggetti pubblici
- Meno performante della classe `Lock` dedicata
- Richiede boxing/unboxing overhead

#### File Coinvolti

- `Infrastructure/PcanAdapter.cs` (riga 37, 552-560, 597-600, 612-617, 654-659)

#### Codice Problematico

```csharp
private readonly object _recoveryLock = new();  // <-- object invece di Lock

// ...

lock (_recoveryLock)
{
    if (_isRecovering)
    {
        _logger.LogDebug("PCAN: Recovery already in progress, skipping");
        return false;
    }
    _isRecovering = true;
}
```

#### Soluzione Proposta

Usare `System.Threading.Lock` introdotto in .NET 9:

```csharp
private readonly Lock _recoveryLock = new();

// ...

using (_recoveryLock.EnterScope())
{
    if (_isRecovering)
    {
        _logger.LogDebug("PCAN: Recovery already in progress, skipping");
        return false;
    }
    _isRecovering = true;
}
```

#### Benefici Attesi

- Migliore performance (~20% più veloce)
- Type-safety: impossibile passare accidentalmente altro object
- Allineamento alle best practice .NET 10

---

## Priorità Media

### INFRA-002 - TryRecoveryAsync fire-and-forget con underscore discard

**Categoria:** Code Smell
**Priorità:** Media
**Impatto:** Medio
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

In `Send()`, se l'invio fallisce e il bus è in stato di errore, viene chiamato `TryRecoveryAsync()` con discard `_ =`. Questo pattern "fire-and-forget" nasconde eventuali eccezioni e non garantisce che il recovery sia completato prima di altri invii.

#### File Coinvolti

- `Infrastructure/PcanAdapter.cs` (righe 284-288)

#### Codice Problematico

```csharp
if (ShouldAttemptRecovery(status))
{
    _logger.LogWarning("PCAN: TX failure triggers recovery attempt");
    _ = TryRecoveryAsync();  // <-- fire-and-forget, eccezioni ignorate
}
```

#### Soluzione Proposta

Opzioni:
1. **Log eccezioni**: usare `ContinueWith` per loggare eccezioni non gestite
2. **Non fare fire-and-forget**: il loop di lettura già monitora e avvia recovery

```csharp
// Opzione 1: log eccezioni
_ = TryRecoveryAsync().ContinueWith(
    t => _logger.LogError(t.Exception, "PCAN: Recovery failed"),
    TaskContinuationOptions.OnlyOnFaulted);

// Opzione 2: affidarsi al health check periodico
// Rimuovere completamente la chiamata qui - il read loop rileverà il problema
```

#### Benefici Attesi

- Nessuna eccezione silenziosamente ignorata
- Comportamento più prevedibile

---

### INFRA-003 - TryAggressiveRecoveryAsync usa Task.Delay senza CancellationToken

**Categoria:** Robustezza
**Priorità:** Media
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Il metodo `TryAggressiveRecoveryAsync` contiene `Task.Delay(250)` e `Task.Delay(500)` senza passare un `CancellationToken`. Se l'oggetto viene disposed durante il delay, il task continua ad aspettare inutilmente.

#### File Coinvolti

- `Infrastructure/PcanAdapter.cs` (righe 630, 636)

#### Codice Problematico

```csharp
for (int i = 0; i < 3; i++)
{
    _logger.LogDebug("PCAN: Aggressive recovery cycle {Cycle}/3", i + 1);

    _api.Uninitialize(_channel);
    await Task.Delay(250).ConfigureAwait(false);  // <-- manca CancellationToken

    if (_isDisposed) return false;
}

await Task.Delay(500).ConfigureAwait(false);  // <-- manca CancellationToken
```

#### Soluzione Proposta

Passare un token da `_cts` o crearne uno dedicato:

```csharp
// Nel metodo: aggiungere un try/catch per OperationCanceledException
try
{
    var ct = _cts?.Token ?? CancellationToken.None;
    await Task.Delay(250, ct).ConfigureAwait(false);
}
catch (OperationCanceledException) when (_isDisposed)
{
    return false;
}
```

#### Benefici Attesi

- Dispose immediato senza attese inutili
- Pattern consistente con il resto del codice

---

## Priorità Bassa

### INFRA-004 - Magic numbers sparsi nel file PcanAdapter

**Categoria:** Manutenibilità
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Oltre alle costanti dichiarate in cima al file, ci sono diversi magic numbers inline:
- `200` ms (riga 134, 151, 687)
- `100` ms (riga 181)
- `250` ms (riga 630)
- `500` ms (riga 636)

#### File Coinvolti

- `Infrastructure/PcanAdapter.cs` (righe 134, 151, 181, 630, 636, 687)

#### Codice Problematico

```csharp
await Task.Delay(200, cancellationToken).ConfigureAwait(false);  // Perché 200?
await Task.Delay(100, cancellationToken).ConfigureAwait(false);  // E perché 100?
await Task.Delay(250).ConfigureAwait(false);  // E 250?
```

#### Soluzione Proposta

Estrarre come costanti con nomi descrittivi:

```csharp
private const int POST_UNINIT_DELAY_MS = 200;
private const int POST_RESET_DELAY_MS = 100;
private const int READ_LOOP_START_DELAY_MS = 100;
private const int AGGRESSIVE_CYCLE_DELAY_MS = 250;
private const int AGGRESSIVE_FINAL_DELAY_MS = 500;
```

#### Benefici Attesi

- Leggibilità migliorata
- Un solo punto di modifica per tuning

---

### INFRA-005 - PcanApiWrapper.Read logga a Trace ogni messaggio ricevuto

**Categoria:** Performance
**Priorità:** Bassa
**Impatto:** Nullo (in produzione Trace è disabilitato)
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

`PcanApiWrapper.Read()` logga ogni messaggio CAN ricevuto a livello `Trace` (riga 85-86). Se il livello di log è impostato a Trace in produzione, questo genera un volume enorme di log dato che i messaggi CAN arrivano ogni ~10ms.

#### File Coinvolti

- `Infrastructure/Lib/PcanApiWrapper.cs` (righe 85-86)

#### Codice Problematico

```csharp
_logger.LogTrace("Read OK: ID=0x{Id:X8}, DLC={Dlc}, Type={MsgType}, Data={Data}",
    message.ID, message.DLC, message.MsgType, BitConverter.ToString(data, 0, message.DLC));
```

#### Soluzione Proposta

Non è un problema reale perché `Trace` è disabilitato in Release. Tuttavia, per evitare costi di formattazione stringa anche quando il livello è disabilitato, si può usare il pattern con check esplicito:

```csharp
if (_logger.IsEnabled(LogLevel.Trace))
{
    _logger.LogTrace("Read OK: ID=0x{Id:X8}, DLC={Dlc}, Type={MsgType}, Data={Data}",
        message.ID, message.DLC, message.MsgType, BitConverter.ToString(data, 0, message.DLC));
}
```

#### Benefici Attesi

- Zero overhead in produzione (evita allocazione stringhe)
- Pattern difensivo

---

## Issue Risolte

(Nessuna issue risolta finora)
