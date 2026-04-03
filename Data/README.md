# Data

> **Repository per dizionari protocollo STEM su Excel. Estrae comandi, variabili e indirizzi con caching per performance.**  
> **Ultimo aggiornamento:** 2026-04-03

---

## Panoramica

**Data** implementa il layer di accesso ai dizionari del protocollo STEM memorizzati in file Excel (`StemDictionaries.xlsx`). Il componente si occupa di:

- **Estrazione dati** da fogli Excel con struttura predefinita
- **Caching intelligente** per evitare riletture multiple del file
- **Repository pattern** con interfacce testabili
- **Factory** per creazione repository per recipientId
- **Preloading** per eliminare blocchi durante i test

Il dizionario STEM contiene:
- **Comandi** (nome → ushort) per operazioni protocollo
- **Variabili** (nome → ushort) specifiche per tipo pulsantiera
- **Valori** (nome → byte[]) per stati LED/buzzer (ON, OFF, SINGLE_BLINK)

---

## Caratteristiche

| Feature | Stato | Descrizione |
|---------|-------|-------------|
| **Estrazione Excel** | ✅ | Parsing fogli "Indirizzo protocollo stem", "Comandi protocollo stem", variabili |
| **Caching Globale** | ✅ | `ConcurrentDictionary` condiviso tra istanze |
| **Thread-Safe** | ✅ | `SemaphoreSlim` per accesso concorrente |
| **Preloading** | ✅ | Pre-caricamento all'avvio per evitare blocking |
| **Valori Immutabili** | ✅ | `ImmutableDictionary` per comandi/variabili |
| **Factory Pattern** | ✅ | `ExcelProtocolRepositoryFactory` per DI |

---

## Requisiti

- **.NET 10.0** o superiore
- **File Excel** `StemDictionaries.xlsx` con struttura STEM

### Dipendenze

| Package | Versione | Uso |
|---------|----------|-----|
| `ClosedXML` | 0.105.0 | Lettura/scrittura file Excel senza Office |
| `Core` | (progetto) | Interfacce `IExcelRepository`, `IProtocolRepository` |

---

## Quick Start

```csharp
using Data;
using Core.Interfaces.Data;

// Setup
var excelRepository = new ExcelRepository();
var excelFilePath = "Resources/StemDictionaries.xlsx";
var factory = new ExcelProtocolRepositoryFactory(excelRepository, excelFilePath);

// Pre-carica dati per i recipientId comuni (consigliato all'avvio)
await factory.PreloadAsync(0x00030101); // Eden-XP
await factory.PreloadAsync(0x000A0101); // Optimus-XP

// Crea repository per tipo pulsantiera specifico
IProtocolRepository repo = factory.Create(0x00030101);

// Ottieni comando
ushort commandId = repo.GetCommand("Scrivi variabile logica");
Console.WriteLine($"Command ID: 0x{commandId:X4}");

// Ottieni variabile
ushort varId = repo.GetVariable("Comando Led Verde");
Console.WriteLine($"Variable ID: 0x{varId:X4}");

// Ottieni valore
byte[] onValue = repo.GetValue("ON");
Console.WriteLine($"ON value: {BitConverter.ToString(onValue)}");
```

---

## Struttura

```
Data/
├── ExcelRepository.cs                    # Implementazione IExcelRepository (parsing Excel)
├── ExcelStemProtocolRepository.cs        # Repository protocollo base (non-cached)
├── CachedExcelProtocolRepository.cs      # Repository con cache globale thread-safe
└── ExcelProtocolRepositoryFactory.cs     # Factory per creazione repository per recipientId
```

---

## API / Componenti

### ExcelRepository

Parsing del file Excel con ClosedXML:

```csharp
public class ExcelRepository : IExcelRepository
{
    // Estrae comandi e indirizzi
    Task<StemProtocolData> GetProtocolDataAsync(Stream excelStream);
    Task<StemProtocolData> GetProtocolDataFromFileAsync(string filePath);
    
    // Estrae variabili per recipientId specifico
    Task<StemProtocolData> GetDictionaryAsync(Stream excelStream, uint recipientId);
    Task<StemProtocolData> GetDictionaryFromFileAsync(string filePath, uint recipientId);
}
```

### CachedExcelProtocolRepository

Repository con cache globale per performance:

```csharp
internal class CachedExcelProtocolRepository : IProtocolRepository
{
    // Metodi pubblici
    ushort GetCommand(string commandName);      // Es. "Scrivi variabile logica"
    ushort GetVariable(string variableName);    // Es. "Comando Led Verde"
    byte[] GetValue(string valueName);          // Es. "ON", "OFF", "SINGLE_BLINK"
    
    // Preloading statico
    static Task PreloadAsync(IExcelRepository repo, string filePath, uint recipientId);
}
```

### ExcelProtocolRepositoryFactory

Factory per dependency injection:

```csharp
public class ExcelProtocolRepositoryFactory : IProtocolRepositoryFactory
{
    IProtocolRepository Create(uint recipientId);
    Task PreloadAsync(uint recipientId);
}
```

---

## Struttura File Excel

### Foglio: "Indirizzo protocollo stem"

| Colonna | Nome | Descrizione |
|---------|------|-------------|
| A | Machine | Tipo macchina (es. "Eden XP") |
| C | Board | Tipo scheda (es. "Eden XP") |
| G | Address | Indirizzo hex (es. "0x00030101") |

### Foglio: "Comandi protocollo stem"

| Colonna | Nome | Descrizione |
|---------|------|-------------|
| A | Command Name | Nome comando (es. "Scrivi variabile logica") |
| B | Command ID | ID hex (es. "0x0002") |

### Fogli Variabili (per MachineType)

Ogni tipo di pulsantiera ha un foglio dedicato con:

| Colonna | Nome | Descrizione |
|---------|------|-------------|
| A | Variable Name | Nome variabile (es. "Comando Led Verde") |
| C | Variable ID | ID hex (es. "0x0401") |
| E | Color | Colore cella per visual grouping |

**Logica colore:** Celle con background `#93C47D` (verde chiaro) indicano variabili comuni a tutte le pulsantiere.

---

## Caching e Performance

### Cache Globale

```csharp
// Cache condivisa tra tutte le istanze
private static readonly ConcurrentDictionary<string, ImmutableDictionary<string, ushort>> _commandsCache;
private static readonly ConcurrentDictionary<uint, ImmutableDictionary<string, ushort>> _variablesCache;
```

**Chiave cache:**
- **Comandi:** `filePath` (indipendente da recipientId)
- **Variabili:** `recipientId` (dipendente dal tipo pulsantiera)

### Thread Safety

Lock con `SemaphoreSlim` invece di `lock(object)`:

```csharp
private static readonly SemaphoreSlim _commandsLock = new(1, 1);
private static readonly SemaphoreSlim _variablesLock = new(1, 1);

// Usage (issue DATA-003: da migrare a System.Threading.Lock)
await _commandsLock.WaitAsync();
try
{
    // Critical section
}
finally
{
    _commandsLock.Release();
}
```

### Preloading

**Perché serve:** Il primo accesso al repository blocca per ~100-200ms per parsing Excel. Durante i test, questo causa timeout.

**Soluzione:** Pre-caricare all'avvio:

```csharp
// In Program.cs / Startup
var commonRecipientIds = new[] { 0x00030101u, 0x000A0101u, 0x000B0101u, 0x000C0101u };
foreach (var recipientId in commonRecipientIds)
{
    await factory.PreloadAsync(recipientId);
}
```

**Risultato:** Tutti i `Create()` successivi sono istantanei (cache già popolata).

---

## Valori Predefiniti

Il repository include 3 valori hardcoded per stati LED/buzzer:

| Nome | Byte Array | Uso |
|------|------------|-----|
| `OFF` | `[0x00, 0x00, 0x00, 0x00]` | Spento |
| `ON` | `[0x00, 0x00, 0x00, 0x80]` | Acceso fisso |
| `SINGLE_BLINK` | `[0x00, 0xFF, 0x80, 0x61]` | Lampeggio singolo |

Accessibili via `repo.GetValue("ON")`.

---

## Testing

### Mock per Unit Test

```csharp
[Fact]
public async Task GetCommand_WithValidName_ReturnsCorrectId()
{
    // Arrange
    var mockExcel = new Mock<IExcelRepository>();
    mockExcel.Setup(x => x.GetProtocolDataFromFileAsync(It.IsAny<string>()))
             .ReturnsAsync(new StemProtocolData
             {
                 Commands = ImmutableList.Create(
                     new StemCommandData("Scrivi variabile logica", 0x0002)
                 )
             });

    var factory = new ExcelProtocolRepositoryFactory(mockExcel.Object, "test.xlsx");
    var repo = factory.Create(0x00030101);

    // Act
    ushort commandId = repo.GetCommand("Scrivi variabile logica");

    // Assert
    Assert.Equal(0x0002, commandId);
}
```

---

## Issue Correlate

→ [Data/ISSUES.md](./ISSUES.md)

**Issue Alta Priorità:**
- `DATA-001` — Task.Run().GetAwaiter().GetResult() blocca il thread
- `DATA-003` — _commandsLock usa SemaphoreSlim invece di Lock (correlata a T-001)

---

## Links

- [ISSUES_TRACKER.md](../ISSUES_TRACKER.md) — Tracker globale issue
- [Core/README.md](../Core/README.md) — Interfacce IExcelRepository, IProtocolRepository
- [Services/README.md](../Services/README.md) — Consumatori del repository
- [ClosedXML Documentation](https://docs.closedxml.io/) — Libreria Excel
