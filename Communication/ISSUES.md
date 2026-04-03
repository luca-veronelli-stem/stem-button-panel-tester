# Communication - ISSUES

> **Scopo:** Questo documento traccia bug, code smells, performance issues, opportunità di refactoring e violazioni di best practice per il componente **Communication**.

> **Ultimo aggiornamento:** 2026-04-03

---

## Riepilogo

| Priorità | Aperte | Risolte |
|----------|--------|---------|
| **Critica** | 0 | 0 |
| **Alta** | 1 | 0 |
| **Media** | 2 | 0 |
| **Bassa** | 3 | 0 |

**Totale aperte:** 6
**Totale risolte:** 0

---

## Indice Issue Aperte

- [COMM-001 - NetworkLayerReassembler._reassemblyLock usa object invece di Lock](#comm-001--networklayerreassembler_reassemblylock-usa-object-invece-di-lock)
- [COMM-002 - s_currentPacketId è static ma può causare conflitti tra istanze](#comm-002--s_currentpacketid-è-static-ma-può-causare-conflitti-tra-istanze)
- [COMM-003 - ProtocolException non estende CommunicationException](#comm-003--protocolexception-non-estende-communicationexception)
- [COMM-004 - CanCommunicationManager.OnNetworkLayerDiagnosticMessage ignora i messaggi](#comm-004--cancommunicationmanageronnetworklayerdiagnosticmessage-ignora-i-messaggi)
- [COMM-005 - Layer.Data restituisce array mutabile](#comm-005--layerdata-restituisce-array-mutabile)
- [COMM-006 - ReadUInt16LittleEndian restituisce 0 se buffer insufficiente](#comm-006--readuint16littleendian-restituisce-0-se-buffer-insufficiente)

---

## Priorità Alta

### COMM-001 - NetworkLayerReassembler._reassemblyLock usa object invece di Lock

**Categoria:** Anti-Pattern
**Priorità:** Alta
**Impatto:** Medio
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

`NetworkLayerReassembler` usa `object` per il campo `_reassemblyLock`. Da .NET 9+ esiste `System.Threading.Lock` più performante e type-safe. Questo è lo stesso anti-pattern identificato in INFRA-001 e DATA-003.

#### File Coinvolti

- `Communication/Protocol/Layers/NetworkLayer.cs` (riga 137, 187, 277)

#### Codice Problematico

```csharp
private readonly object _reassemblyLock = new();

// ...

lock (_reassemblyLock)
{
    ProcessChunkInternal(netInfo, chunkData);
}
```

#### Soluzione Proposta

Usare `System.Threading.Lock`:

```csharp
private readonly Lock _reassemblyLock = new();

// ...

using (_reassemblyLock.EnterScope())
{
    ProcessChunkInternal(netInfo, chunkData);
}
```

#### Benefici Attesi

- Performance migliore (~20% più veloce)
- Type-safety
- Consistenza con le altre issue correlate (INFRA-001, DATA-003)

---

## Priorità Media

### COMM-002 - s_currentPacketId è static ma può causare conflitti tra istanze

**Categoria:** Design
**Priorità:** Media
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Il campo `s_currentPacketId` in `NetworkLayer` è statico e condiviso tra tutte le istanze. Questo è corretto per avere PacketId globali univoci, ma in scenari di test paralleli o multi-tenant potrebbe causare comportamenti non deterministici.

Il codice è thread-safe (usa `Interlocked.CompareExchange`), ma i test potrebbero aspettarsi sequenze specifiche di PacketId.

#### File Coinvolti

- `Communication/Protocol/Layers/NetworkLayer.cs` (riga 28, 94)

#### Codice Problematico

```csharp
private static int s_currentPacketId = ProtocolConfig.MinPacketId - 1;

// ...

int packetId = ProtocolConfig.GetNextPacketId(ref s_currentPacketId);
```

#### Soluzione Proposta

Per i test, aggiungere un metodo `Reset()` statico (già fatto nei test E2E):

```csharp
/// <summary>
/// Resetta il contatore PacketId al valore iniziale. Solo per test.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static void ResetPacketIdCounter()
{
    Volatile.Write(ref s_currentPacketId, ProtocolConfig.MinPacketId - 1);
}
```

#### Benefici Attesi

- Test deterministici
- Nessun impatto sul codice di produzione

---

### COMM-003 - ProtocolException non estende CommunicationException

**Categoria:** Design
**Priorità:** Media
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Esistono due classi `ProtocolException`:
- `Communication.Protocol.Lib.ProtocolException` (estende `Exception`)
- `Core.Exceptions.ProtocolException` (estende `CommunicationException`)

La versione in Communication non estende `CommunicationException`, quindi non può essere catturata con un singolo `catch (CommunicationException)`.

Inoltre, la duplicazione di nome può causare confusione.

#### File Coinvolti

- `Communication/Protocol/Lib/ProtocolException.cs` (intero file)
- `Core/Exceptions/CommunicationExceptions.cs` (riga 134-145)

#### Codice Problematico

```csharp
// Communication/Protocol/Lib/ProtocolException.cs
public class ProtocolException : Exception  // <-- non estende CommunicationException
{
    public ProtocolException(string message) : base(message) { }
}
```

#### Soluzione Proposta

Eliminare `Communication.Protocol.Lib.ProtocolException` e usare solo `Core.Exceptions.ProtocolException`. Aggiornare i riferimenti:

```csharp
// Invece di:
using Communication.Protocol.Lib;

// Usare:
using Core.Exceptions;
```

#### Benefici Attesi

- Una sola classe `ProtocolException`
- Cattura uniforme con `catch (CommunicationException)`
- Nessuna confusione sul tipo da usare

---

## Priorità Bassa

### COMM-004 - CanCommunicationManager.OnNetworkLayerDiagnosticMessage ignora i messaggi

**Categoria:** Observability
**Priorità:** Bassa
**Impatto:** Nullo
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Il metodo `OnNetworkLayerDiagnosticMessage` è vuoto con un commento che dice che i messaggi diagnostici sono gestiti via `ILogger`. Ma la classe non ha un `ILogger` iniettato, quindi i messaggi diagnostici del reassembler sono persi.

#### File Coinvolti

- `Communication/CanCommunicationManager.cs` (righe 89-92)

#### Codice Problematico

```csharp
private void OnNetworkLayerDiagnosticMessage(string message)
{
    // Ignore - diagnostic messages are now handled via ILogger in actual implementations
}
```

#### Soluzione Proposta

Opzione A: Iniettare `ILogger<CanCommunicationManager>` e loggare a livello `Debug`:

```csharp
public CanCommunicationManager(ICanAdapter adapter, ILogger<CanCommunicationManager> logger)
{
    _logger = logger;
    // ...
}

private void OnNetworkLayerDiagnosticMessage(string message)
{
    _logger.LogDebug("{Message}", message);
}
```

Opzione B: Rimuovere completamente la sottoscrizione all'evento se non serve.

#### Benefici Attesi

- Diagnostica disponibile quando serve
- Nessun handler vuoto

---

### COMM-005 - Layer.Data restituisce array mutabile

**Categoria:** Robustezza
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

La classe base `Layer` espone `Data` come proprietà pubblica che restituisce direttamente il buffer interno `_data`. Questo permette al chiamante di modificare i dati del layer.

#### File Coinvolti

- `Communication/Protocol/Layers/Layer.cs` (riga 34)

#### Codice Problematico

```csharp
private readonly byte[] _data;

public byte[] Data => _data;  // <-- restituisce riferimento diretto
```

#### Soluzione Proposta

Restituire una copia o usare `ReadOnlyMemory<byte>`:

```csharp
public ReadOnlyMemory<byte> Data => _data;

// Oppure, per mantenere l'API:
public byte[] Data => (byte[])_data.Clone();
```

**Nota:** Questo potrebbe impattare le performance se `Data` viene chiamato spesso. Valutare se il trade-off vale la pena.

#### Benefici Attesi

- Immutabilità garantita dei layer
- Nessuna modifica accidentale dei dati

---

### COMM-006 - ReadUInt16LittleEndian restituisce 0 se buffer insufficiente

**Categoria:** Robustezza
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

I metodi `ReadUInt16LittleEndian`, `ReadUInt16BigEndian`, `ReadUInt32BigEndian` restituiscono `0` se il buffer è troppo corto invece di lanciare un'eccezione. Questo può mascherare errori di programmazione.

#### File Coinvolti

- `Communication/Protocol/Lib/ProtocolHelpers.cs` (righe 43-47, 124-128, 136-141)

#### Codice Problematico

```csharp
public static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> buffer, int offset = 0)
{
    if (buffer.Length < offset + 2) return 0;  // <-- silently returns 0
    return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
}
```

#### Soluzione Proposta

Lanciare `ArgumentException` per buffer insufficienti:

```csharp
public static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> buffer, int offset = 0)
{
    if (buffer.Length < offset + 2)
        throw new ArgumentException(
            $"Buffer too short: need {offset + 2} bytes, got {buffer.Length}",
            nameof(buffer));
    return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
}
```

**Nota:** Verificare che tutti i chiamanti gestiscano correttamente l'eccezione prima di applicare questa modifica.

#### Benefici Attesi

- Fail-fast per errori di programmazione
- Debugging più semplice

---

## Issue Risolte

(Nessuna issue risolta finora)
