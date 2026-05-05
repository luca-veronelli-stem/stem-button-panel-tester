# Communication

> **Stack protocollare a 3 livelli per protocollo STEM CAN. Include chunking, CRC, reassembly e manager di comunicazione.**  
> **Ultimo aggiornamento:** 2026-04-03

---

## Panoramica

**Communication** implementa lo stack protocollare completo per la comunicazione STEM su bus CAN. Il componente si basa su un'architettura a **3 livelli ISO/OSI-like**:

1. **Application Layer** — Comandi (ushort) + payload applicativo
2. **Transport Layer** — Incapsulamento, SenderId, CRC-16 Modbus
3. **Network Layer** — Chunking per MTU CAN (6-8 byte), NetInfo per reassembly

Il manager orchestra la costruzione di pacchetti in uscita e il parsing/validazione di pacchetti in entrata, con supporto per:
- **Multi-chunk** — Pacchetti grandi divisi in chunk CAN
- **Reassembly automatico** — Ricostruzione pacchetti da chunk multipli
- **Validazione CRC** — Verifica integrità dati
- **Eventi tipizzati** — `CommandDecoded`, `ErrorOccurred`

---

## Caratteristiche

| Feature | Stato | Descrizione |
|---------|-------|-------------|
| **Stack 3 Livelli** | ✅ | Application → Transport → Network |
| **Chunking/Reassembly** | ✅ | Supporto pacchetti fino a 255 chunk |
| **CRC-16 Modbus** | ✅ | Polinomio 0xA001, init 0xFFFF |
| **Thread-Safe** | ✅ | `ConcurrentDictionary` per reassembly buffer |
| **Validazione** | ✅ | CRC, PacketId range, lunghezze minime |
| **Diagnostica** | ✅ | Eventi `DiagnosticMessage` per troubleshooting |

---

## Requisiti

- **.NET 10.0** o superiore
- Nessuna dipendenza esterna (zero NuGet packages)

### Dipendenze

| Package | Versione | Uso |
|---------|----------|-----|
| `Core` | (progetto) | Interfacce, modelli, Result Pattern |
| `Infrastructure` | (progetto) | ICanAdapter per invio/ricezione CAN |

---

## Quick Start

```csharp
using Communication;
using Communication.Protocol;
using Core.Enums;

// Setup
var protocolManager = new StemProtocolManager();
var canAdapter = /* ... ICanAdapter instance */;
var canManager = new CanCommunicationManager(canAdapter);

// Sottoscrivi eventi
protocolManager.CommandDecoded += (s, e) =>
    Console.WriteLine($"Comando decodificato: 0x{e.Command:X4}, Payload: {e.Payload.Length} byte");

canManager.PacketReceived += (s, packet) =>
    Console.WriteLine($"Pacchetto ricevuto: {packet.Length} byte");

// Connetti
await canManager.ConnectAsync("250");

// Costruisci e invia pacchetto
ushort command = 0x0002; // "Scrivi variabile logica"
byte[] payload = [0x04, 0x01, 0x00, 0x00, 0x00, 0x80]; // LED ON
uint senderId = 0x00030141;    // Computer
uint recipientId = 0x00030101; // Pulsantiera

var chunks = protocolManager.BuildPackets(command, payload, senderId, recipientId, chunkSize: 6);

foreach (var chunk in chunks)
{
    await canManager.SendAsync([.. chunk.NetInfo, .. chunk.Chunk], chunk.Id);
}

// Elabora risposta (automatico via eventi)
```

---

## Struttura

```
Communication/
├── Protocol/
│   ├── Layers/
│   │   ├── Layer.cs                  # Classe base astratta
│   │   ├── ApplicationLayer.cs       # Livello 7: comandi + payload
│   │   ├── TransportLayer.cs         # Livello 4: CRC, senderId, validazione
│   │   └── NetworkLayer.cs           # Livello 3: chunking, NetInfo, reassembly
│   ├── Lib/
│   │   ├── ProtocolConfig.cs         # Costanti protocollo (lunghezze, PacketId)
│   │   ├── ProtocolHelpers.cs        # Utilities (CRC, endianness, lettura)
│   │   ├── NetInfo.cs                # Metadata chunk (remainingChunks, packetId)
│   │   └── ProtocolException.cs      # Eccezione specifica protocollo
│   └── StemProtocolManager.cs        # Orchestratore stack completo
├── BaseCommunicationManager.cs       # Classe base per manager CAN/BLE/Serial
└── CanCommunicationManager.cs        # Implementazione per bus CAN
```

---

## API / Componenti

### StemProtocolManager

Orchestratore dello stack protocollare:

```csharp
public sealed class StemProtocolManager : IProtocolManager
{
    // Eventi
    event EventHandler<AppLayerDecoderEventArgs> CommandDecoded;
    event EventHandler<ProtocolErrorEventArgs> ErrorOccurred;

    // Build (TX)
    List<NetworkPacketChunk> BuildPackets(
        ushort command,
        byte[] payload,
        uint senderId,
        uint recipientId,
        int chunkSize = 6);

    // Parse (RX)
    byte[] ProcessReceivedPacket(byte[]? transportPacket);
}
```

### CanCommunicationManager

Manager per comunicazione CAN:

```csharp
public sealed class CanCommunicationManager : BaseCommunicationManager
{
    // Ereditati da BaseCommunicationManager
    event EventHandler<bool> ConnectionStatusChanged;
    event EventHandler<byte[]> PacketReceived;
    event Action<uint, byte[]> RawPacketReceived;
    
    bool IsConnected { get; }
    int MaxPacketSize { get; } // 6 byte per CAN
    
    Task<bool> ConnectAsync(string config, CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
    Task<bool> SendAsync(byte[] data, uint? arbitrationId);
}
```

### Livelli Protocollari

#### Application Layer (Livello 7)

```csharp
public sealed class ApplicationLayer : Layer
{
    byte CmdInit { get; }           // Byte alto comando
    byte CmdOpt { get; }            // Byte basso comando
    ushort Command { get; }         // Comando completo (cmdInit << 8 | cmdOpt)
    byte[] ApplicationHeader { get; }  // [cmdInit, cmdOpt]
    byte[] ApplicationPacket { get; }  // Header + payload
    
    static ApplicationLayer Create(ushort command, byte[] payload);
    static ApplicationLayer Parse(byte[] applicationPacket);
}
```

**Formato:** `[cmdInit (1)] [cmdOpt (1)] [payload (N)]`

#### Transport Layer (Livello 4)

```csharp
public sealed class TransportLayer : Layer
{
    CryptType CryptFlag { get; }
    uint SenderId { get; }
    ushort PacketLength { get; }
    byte[] TransportHeader { get; }   // 7 byte
    byte[] TransportPacket { get; }   // Header + app + CRC
    byte[] Crc { get; }               // CRC-16 calcolato
    bool IsValid { get; }             // Validazione CRC OK
    string? ValidationError { get; }
    
    static TransportLayer Create(CryptType cryptFlag, uint senderId, byte[] applicationPacket);
    static TransportLayer Parse(byte[] transportPacket);
}
```

**Formato:** `[cryptFlag (1)] [senderId (4)] [lPack (2)] [applicationPacket (N)] [CRC (2)]`

**CRC-16 Modbus:**
- Polinomio: `0xA001`
- Init: `0xFFFF`
- Calcolato su: `header + applicationPacket` (senza CRC)

#### Network Layer (Livello 3)

```csharp
public sealed class NetworkLayer
{
    IReadOnlyList<NetworkPacketChunk> NetworkPackets { get; }
    
    static NetworkLayer Create(uint arbitrationId, byte[] transportPacket, int chunkSize);
}

public sealed class NetworkLayerReassembler
{
    event Action<byte[]> PacketReassembled;
    event Action<string> DiagnosticMessage;
    
    void ProcessReceivedChunk(byte[] data);  // [NetInfo (2)] [chunk (N)]
    void ClearReassemblyState();
}
```

**Formato chunk:** `[NetInfo (2)] [chunk (chunkSize)]`

**NetInfo (2 byte):**
- Bit 0-2: `remainingChunks` (0-7)
- Bit 3: `setLength` (0=no, 1=ultimo chunk contiene lPack)
- Bit 4-6: `packetId` (1-7, ciclico)
- Bit 7: `version` (0=v1)

---

## Protocollo STEM CAN

### Flusso TX (Computer → Pulsantiera)

1. **Application Layer** — Crea header `[cmdInit, cmdOpt]` + payload
2. **Transport Layer** — Aggiunge `[cryptFlag, senderId, lPack]` + CRC
3. **Network Layer** — Divide in chunk da 6 byte con NetInfo
4. **CAN Bus** — Invia chunk con `ArbitrationId = recipientId`

**Esempio:** Comando "Scrivi variabile logica" (0x0002) per accendere LED verde:

```
Payload applicativo:  [0x04, 0x01, 0x00, 0x00, 0x00, 0x80]
Application packet:   [0x00, 0x02, 0x04, 0x01, 0x00, 0x00, 0x00, 0x80]
Transport packet:     [0x00, 0x41, 0x01, 0x03, 0x00, 0x08, 0x00, 0x00, 0x02, 0x04, 0x01, 0x00, 0x00, 0x00, 0x80, 0xAB, 0xCD]
                       ^^^^  ^^^^^^^^^^^^^^^^^^  ^^^^^^^  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^  ^^^^^^^
                       crypt      senderId        lPack            app packet                   CRC

Network chunks (6 byte payload):
  Chunk 0: [NetInfo: 0x1A] [0x00, 0x41, 0x01, 0x03, 0x00, 0x08]  <- remainingChunks=2, packetId=3
  Chunk 1: [NetInfo: 0x0A] [0x00, 0x00, 0x02, 0x04, 0x01, 0x00]  <- remainingChunks=1, packetId=3
  Chunk 2: [NetInfo: 0x06] [0x00, 0x00, 0x80, 0xAB, 0xCD]        <- remainingChunks=0, packetId=3, setLength=1
```

### Flusso RX (Pulsantiera → Computer)

1. **CAN Bus** — Riceve chunk con NetInfo
2. **NetworkLayerReassembler** — Accumula chunk con stesso `packetId`
3. **Transport Layer** — Valida CRC, estrae applicationPacket
4. **Application Layer** — Estrae comando e payload
5. **Evento** — `CommandDecoded` con `ushort command` e `byte[] payload`

---

## Configurazione

### Chunk Size

| Canale | MTU | Chunk Size |
|--------|-----|------------|
| **CAN** | 8 byte | 6 byte (8 - 2 NetInfo) |
| BLE | ~20 byte | 18 byte |
| Serial | Variable | Configurabile |

### PacketId

Range: `1-7` (ciclico)

**Thread-safe** con `Interlocked.CompareExchange`:

```csharp
int nextId = ProtocolConfig.GetNextPacketId(ref currentPacketId);
```

**Reset per test:**

```csharp
// Solo per test deterministici
Volatile.Write(ref s_currentPacketId, ProtocolConfig.MinPacketId - 1);
```

---

## Diagnostica

### Eventi

```csharp
// StemProtocolManager
protocolManager.CommandDecoded += (s, e) =>
    Console.WriteLine($"[PROTOCOL] Command=0x{e.Command:X4}, Payload={e.Payload.Length}");

protocolManager.ErrorOccurred += (s, e) =>
    Console.WriteLine($"[ERROR] {e.Message}");

// NetworkLayerReassembler
reassembler.DiagnosticMessage += msg =>
    Console.WriteLine($"[REASSEMBLY] {msg}");
```

### Validazioni Transport Layer

| Validazione | Condizione | Errore |
|-------------|------------|--------|
| Lunghezza minima | `packet.Length >= 11` | "Pacchetto troppo corto" |
| PacketId range | `1 <= packetId <= 7` | "PacketId non valido" |
| CRC match | `computed == stored` | "CRC non valido: calcolato=X, memorizzato=Y" |

---

## Testing

### Unit Test

```csharp
[Fact]
public void BuildPackets_SimpleCommand_CreatesValidChunks()
{
    // Arrange
    var manager = new StemProtocolManager();
    ushort command = 0x0002;
    byte[] payload = [0x04, 0x01];
    
    // Act
    var chunks = manager.BuildPackets(command, payload, 0x00030141, 0x00030101, 6);
    
    // Assert
    Assert.NotEmpty(chunks);
    Assert.All(chunks, c => Assert.True(c.NetInfo.Length == 2));
}
```

### Integration Test con Mock Adapter

```csharp
var mockAdapter = new Mock<ICanAdapter>();
mockAdapter.Setup(x => x.Send(It.IsAny<uint>(), It.IsAny<byte[]>(), true))
           .ReturnsAsync(true);

var canManager = new CanCommunicationManager(mockAdapter.Object);
await canManager.ConnectAsync("250");

// Verifica invio chunk
mockAdapter.Verify(x => x.Send(It.IsAny<uint>(), It.IsAny<byte[]>(), true), Times.AtLeastOnce);
```

---

## Issue Correlate

→ [Communication/ISSUES.md](./ISSUES.md)

**Issue Alta Priorità:**
- `COMM-001` — _reassemblyLock usa object invece di Lock (correlata a T-001)

---

## Links

- [ISSUES_TRACKER.md](../ISSUES_TRACKER.md) — Tracker globale issue
- [Core/README.md](../Core/README.md) — Interfacce IProtocolManager, ICommunicationManager
- [Infrastructure/README.md](../Infrastructure/README.md) — ICanAdapter per invio CAN
- [Services/README.md](../Services/README.md) — Consumatori del protocollo
