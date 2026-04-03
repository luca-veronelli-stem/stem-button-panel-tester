# Data - ISSUES

> **Scopo:** Questo documento traccia bug, code smells, performance issues, opportunità di refactoring e violazioni di best practice per il componente **Data**.

> **Ultimo aggiornamento:** 2026-04-03

---

## Riepilogo

| Priorità | Aperte | Risolte |
|----------|--------|---------|
| **Critica** | 0 | 0 |
| **Alta** | 1 | 0 |
| **Media** | 3 | 0 |
| **Bassa** | 2 | 0 |

**Totale aperte:** 6
**Totale risolte:** 0

---

## Indice Issue Aperte

- [DATA-001 - Task.Run(...).GetAwaiter().GetResult() blocca il thread](#data-001--taskrungetawaitergetresult-blocca-il-thread)
- [DATA-002 - ExcelStemProtocolRepository è duplicato di CachedExcelProtocolRepository](#data-002--excelstemprotocolrepository-è-duplicato-di-cachedexcelprotocolrepository)
- [DATA-003 - _commandsLock e _variablesLock usano object invece di Lock](#data-003--_commandslock-e-_variableslock-usano-object-invece-di-lock)
- [DATA-004 - Magic number -7155632 per colore cella Excel](#data-004--magic-number--7155632-per-colore-cella-excel)
- [DATA-005 - catch generico senza logging in PreloadAsync](#data-005--catch-generico-senza-logging-in-preloadasync)
- [DATA-006 - GetValue restituisce array mutabile](#data-006--getvalue-restituisce-array-mutabile)

---

## Priorità Alta

### DATA-001 - Task.Run(...).GetAwaiter().GetResult() blocca il thread

**Categoria:** Anti-Pattern
**Priorità:** Alta
**Impatto:** Alto
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Sia `ExcelStemProtocolRepository` che `CachedExcelProtocolRepository` usano il pattern `Task.Run(() => LoadAsync()).GetAwaiter().GetResult()` per convertire operazioni async in sync. Questo blocca il thread chiamante e può causare deadlock se chiamato dal thread UI.

Il commento nel codice indica che è intenzionale ("Run on thread pool to avoid deadlock with UI synchronization context"), ma il pattern rimane problematico perché:
- Consuma un thread del thread pool durante l'attesa
- Non è cancellabile
- Maschera la natura asincrona dell'operazione

#### File Coinvolti

- `Data/ExcelStemProtocolRepository.cs` (righe 94, 119)
- `Data/CachedExcelProtocolRepository.cs` (righe 122, 147)

#### Codice Problematico

```csharp
// ExcelStemProtocolRepository.cs
_commands = Task.Run(() => LoadCommandsAsync()).GetAwaiter().GetResult();

// CachedExcelProtocolRepository.cs
var commands = Task.Run(() => LoadCommandsAsync()).GetAwaiter().GetResult();
```

#### Soluzione Proposta

L'interfaccia `IProtocolRepository` è sincrona by design (vedi CORE-006). Opzioni:

1. **Preload obbligatorio**: Forzare il preload all'avvio (già implementato con `PreloadAsync`) e lanciare eccezione se non precaricato
2. **Cache warming**: Al primo accesso, caricare in background e restituire default temporaneo
3. **Accettare il trade-off**: Documentare esplicitamente che il primo accesso è bloccante

```csharp
// Opzione 1: Fail-fast se non precaricato
private ImmutableDictionary<string, ushort> GetCommandsSync()
{
    if (!_commandsCache.TryGetValue(_excelFilePath, out var cached))
        throw new InvalidOperationException(
            $"Commands not preloaded. Call PreloadAsync() at startup.");
    return cached;
}
```

#### Benefici Attesi

- Nessun thread bloccato in attesa di I/O
- Comportamento più prevedibile
- Possibilità di cancellazione

---

## Priorità Media

### DATA-002 - ExcelStemProtocolRepository è duplicato di CachedExcelProtocolRepository

**Categoria:** Code Smell
**Priorità:** Media
**Impatto:** Medio
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Esistono due implementazioni quasi identiche di `IProtocolRepository`:
- `ExcelStemProtocolRepository` (internal, ~187 righe)
- `CachedExcelProtocolRepository` (internal, ~210 righe)

Entrambe hanno la stessa logica per:
- `GetCommand()`, `GetVariable()`, `GetValue()`
- `LoadCommandsAsync()`, `LoadVariablesAsync()`
- `ParseHexToUShort()`
- Dizionario `_values` con OFF/ON/SINGLE_BLINK

La differenza principale è che `CachedExcelProtocolRepository` usa cache `static` globale mentre `ExcelStemProtocolRepository` usa cache per istanza.

#### File Coinvolti

- `Data/ExcelStemProtocolRepository.cs` (intero file)
- `Data/CachedExcelProtocolRepository.cs` (intero file)

#### Soluzione Proposta

Eliminare `ExcelStemProtocolRepository` e usare solo `CachedExcelProtocolRepository`. La cache globale è preferibile perché:
- Evita ricaricamenti multipli dello stesso file
- È già usata dalla factory

Se serve la versione non-cached per i test, creare un mock o parametrizzare il comportamento di caching.

#### Benefici Attesi

- ~180 righe di codice in meno
- Un solo punto di manutenzione
- Comportamento uniforme

---

### DATA-003 - _commandsLock e _variablesLock usano object invece di Lock

**Categoria:** Anti-Pattern
**Priorità:** Media
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Entrambe le classi repository usano `object` per il locking. Da .NET 9+ esiste `System.Threading.Lock` più performante e type-safe.

Inoltre, `CachedExcelProtocolRepository` usa `SemaphoreSlim` con `.Wait()` sincrono, che è un pattern migliore del `lock` ma potrebbe essere sostituito con `Lock` dato che l'operazione è sempre sincrona.

#### File Coinvolti

- `Data/ExcelStemProtocolRepository.cs` (righe 20-21)
- `Data/CachedExcelProtocolRepository.cs` (righe 21-22)

#### Codice Problematico

```csharp
// ExcelStemProtocolRepository.cs
private readonly object _commandsLock = new();
private readonly object _variablesLock = new();

// CachedExcelProtocolRepository.cs (usa SemaphoreSlim)
private static readonly SemaphoreSlim _commandsLock = new(1, 1);
private static readonly SemaphoreSlim _variablesLock = new(1, 1);
```

#### Soluzione Proposta

Usare `System.Threading.Lock` per consistenza con le best practice .NET 10:

```csharp
private readonly Lock _commandsLock = new();
private readonly Lock _variablesLock = new();
```

#### Benefici Attesi

- Performance migliore
- Type-safety
- Consistenza con INFRA-001

---

### DATA-004 - Magic number -7155632 per colore cella Excel

**Categoria:** Manutenibilità
**Priorità:** Media
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

In `ExcelRepository.ExtractVariables()`, il codice controlla se una cella ha un colore specifico usando il valore ARGB magico `-7155632`. Non è chiaro quale colore rappresenti né perché sia usato per identificare le variabili valide.

#### File Coinvolti

- `Data/ExcelRepository.cs` (righe 152-154)

#### Codice Problematico

```csharp
var fillColor = row.Cell("A").Style.Fill.BackgroundColor;
if (fillColor.ColorType != XLColorType.Theme && fillColor.Color.ToArgb() == -7155632)
{
    // ... estrae variabile
}
```

#### Soluzione Proposta

Estrarre come costante con nome descrittivo e commento:

```csharp
/// <summary>
/// ARGB value for the light blue/cyan color used to mark valid variable rows
/// in the STEM protocol Excel dictionary. Equivalent to #FF927290 or similar.
/// </summary>
private const int VALID_VARIABLE_ROW_COLOR_ARGB = -7155632;

// ...
if (fillColor.ColorType != XLColorType.Theme && 
    fillColor.Color.ToArgb() == VALID_VARIABLE_ROW_COLOR_ARGB)
```

#### Benefici Attesi

- Codice auto-documentante
- Facile da trovare e modificare se il formato Excel cambia

---

## Priorità Bassa

### DATA-005 - catch generico senza logging in PreloadAsync

**Categoria:** Robustezza
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Il metodo `PreloadAsync` in `CachedExcelProtocolRepository` cattura tutte le eccezioni e restituisce silenziosamente `false`. L'errore viene perso, rendendo difficile diagnosticare problemi di caricamento.

#### File Coinvolti

- `Data/CachedExcelProtocolRepository.cs` (righe 100-103)

#### Codice Problematico

```csharp
catch
{
    return false;  // Eccezione ingoiata silenziosamente
}
```

#### Soluzione Proposta

Almeno loggare l'eccezione prima di restituire `false`, oppure lasciare propagare:

```csharp
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"[EXCEL] Preload failed: {ex}");
    return false;
}
```

#### Benefici Attesi

- Diagnostica più semplice
- Errori non persi silenziosamente

---

### DATA-006 - GetValue restituisce array mutabile

**Categoria:** Robustezza
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

`GetValue()` restituisce direttamente l'array `byte[]` dal dizionario `_values`. Gli array sono mutabili, quindi il chiamante potrebbe modificare i valori cached, corrompendo lo stato interno.

#### File Coinvolti

- `Data/ExcelStemProtocolRepository.cs` (righe 74-75)
- `Data/CachedExcelProtocolRepository.cs` (righe 77-78)

#### Codice Problematico

```csharp
if (_values.TryGetValue(valueName, out var value))
    return value;  // <-- restituisce riferimento diretto all'array cached
```

#### Soluzione Proposta

Restituire una copia:

```csharp
if (_values.TryGetValue(valueName, out var value))
    return (byte[])value.Clone();  // oppure value.ToArray()
```

Oppure usare `ReadOnlyMemory<byte>` o `ImmutableArray<byte>` nel dizionario.

#### Benefici Attesi

- Immutabilità garantita della cache
- Nessuna corruzione accidentale dei dati

---

## Issue Risolte

(Nessuna issue risolta finora)
