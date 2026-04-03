# StemDeviceManager - Button Panel Tester

## Panoramica

Applicazione **Windows Forms (.NET 8)** per il **collaudo di pulsantiere STEM** (button panel) comunicanti via **bus CAN** attraverso un protocollo proprietario. L'applicazione permette di testare pulsanti, LED e buzzer di diverse tipologie di pulsantiere utilizzate in ambito industriale/medicale.

---

## Architettura

La soluzione segue una **Clean Architecture** con 7 progetti:

```
StemDeviceManager.sln
├── Core/                    # Dominio: modelli, interfacce, enums, Result pattern
├── Infrastructure/          # Adattatore hardware PCAN (bus CAN)
├── Communication/           # Stack protocollare a livelli (Network, Transport, Application)
├── Data/                    # Repository Excel per dizionari protocollo
├── Services/                # Logica di business (test, battezzamento)
├── GUI.Windows/             # Frontend WinForms con pattern MVP
└── Tests/                   # Unit, Integration, E2E tests
```

### Dipendenze tra progetti

```
GUI.Windows → Services → Communication → Infrastructure → Core
                      ↘ Data → Core
```

---

## Progetti in dettaglio

### 1. Core (`Core.csproj`)

**Libreria di dominio pura** - zero dipendenze esterne.

#### Contenuti principali:

| Cartella | Descrizione |
|----------|-------------|
| `Enums/` | `ButtonPanelType`, `ButtonPanelTestType`, `IndicatorState`, enums pulsanti |
| `Models/` | `ButtonPanel`, `ButtonPanelTestResult`, `CanPacket`, `NetworkPacketChunk` |
| `Interfaces/` | Contratti per tutti i servizi, repository, adapter |
| `Results/` | Pattern `Result<T>` e `Error` per gestione errori funzionale |
| `Exceptions/` | Eccezioni personalizzate per comunicazione |

#### Interfacce chiave:

```csharp
IButtonPanelTestService     // Test pulsanti/LED/buzzer
IBaptizeService             // Assegnazione indirizzo CAN
ICommunicationService       // Invio/ricezione comandi
IProtocolManager            // Gestione protocollo STEM
ICanAdapter                 // Accesso hardware CAN
IProtocolRepository         // Dizionario comandi/variabili da Excel
```

#### Modello ButtonPanel:

```csharp
public class ButtonPanel
{
    public ButtonPanelType Type { get; set; }
    public int ButtonCount { get; set; }
    public bool HasLed { get; set; }
    public bool HasBuzzer { get; set; }
    public string[] Buttons { get; set; }           // Nomi pulsanti
    public List<byte> ButtonMasks { get; set; }     // Maschere bit per ogni pulsante
    public ushort[] ButtonStatusVariableIds { get; set; } // IDs variabili (0x8000, 0x803E)
}
```

---

### 2. Infrastructure (`Infrastructure.csproj`)

**Adattatore hardware** per interfaccia PCAN (Peak CAN).

#### Dipendenze:
- `Peak.PCANBasic.NET` - Driver PCAN ufficiale

#### Componenti:

| File | Descrizione |
|------|-------------|
| `PcanAdapter.cs` | Implementazione `ICanAdapter` - connect/disconnect, read/write frame CAN |
| `Lib/PcanApiWrapper.cs` | Wrapper thread-safe per API PCAN native |
| `Lib/IPcanApi.cs` | Interfaccia per testing/mocking |

#### Funzionalità PcanAdapter:
- Loop di lettura asincrono continuo
- Gestione automatica recovery bus CAN
- Diagnostica dettagliata (contatori RX/TX, errori, uptime)
- Eventi: `PacketReceived`, `PhysicalReconnectRequired`

---

### 3. Communication (`Communication.csproj`)

**Stack protocollare STEM** a 3 livelli sopra CAN.

#### Struttura livelli:

```
┌─────────────────────────────────┐
│     ApplicationLayer            │  ← Comandi/Variabili STEM
├─────────────────────────────────┤
│     TransportLayer              │  ← SenderId/RecipientId, CRC
├─────────────────────────────────┤
│     NetworkLayer                │  ← Chunking/Reassembly pacchetti
├─────────────────────────────────┤
│     CAN Bus (via ICanAdapter)   │  ← Frame CAN 8 byte
└─────────────────────────────────┘
```

#### Componenti:

| File | Descrizione |
|------|-------------|
| `StemProtocolManager.cs` | Orchestratore del protocollo, implementa `IProtocolManager` |
| `BaseCommunicationManager.cs` | Classe base per gestione comunicazione |
| `CanCommunicationManager.cs` | Implementazione specifica per bus CAN |
| `Protocol/Layers/NetworkLayer.cs` | Chunking TX, reassembly RX di pacchetti multi-frame |
| `Protocol/Layers/TransportLayer.cs` | Header sender/recipient, calcolo CRC |
| `Protocol/Layers/ApplicationLayer.cs` | Encoding/decoding comandi STEM |
| `Protocol/Lib/ProtocolConfig.cs` | Configurazione protocollo |
| `Protocol/Lib/NetInfo.cs` | Info pacchetto di rete |

#### Formato pacchetto CAN:

```
Frame CAN (max 8 byte):
┌──────────┬───────────────────────────────┐
│ NetInfo  │ Payload (fino a 7 byte)       │
│ (1 byte) │                               │
└──────────┴───────────────────────────────┘

NetInfo byte:
  bit 7-6: Sequence number (0-3)
  bit 5-2: Chunk index
  bit 1-0: Flags (first/last chunk)
```

---

### 4. Data (`Data.csproj`)

**Repository per dizionari protocollo** da file Excel.

#### Dipendenze:
- `ClosedXML` - Lettura file Excel

#### Componenti:

| File | Descrizione |
|------|-------------|
| `ExcelRepository.cs` | Lettura raw da Excel (comandi, variabili, indirizzi) |
| `ExcelStemProtocolRepository.cs` | Implementazione `IProtocolRepository` |
| `CachedExcelProtocolRepository.cs` | Versione con cache globale per performance |
| `ExcelProtocolRepositoryFactory.cs` | Factory per creare repository per recipientId |

#### File Excel: `StemDictionaries.xlsx`

**Struttura:**

1. **Foglio "COMANDI"**: Lista comandi protocollo
   - Colonna A: Nome comando (es. "Scrivi variabile logica")
   - Colonna B: Byte alto comando (es. "00")
   - Colonna C: Byte basso comando (es. "02")

2. **Foglio "Indirizzo protocollo stem"**: Mapping indirizzi
   - Colonna A: Machine type
   - Colonna C: Board number
   - Colonna G: Indirizzo STEM

3. **Fogli per ogni tipo dispositivo**: Variabili
   - **Riga 2**: Contiene l'ID recipiente (es. `0x000C0101`)
   - **Righe 5+**: Variabili con sfondo colorato specifico (ARGB=-7155632)
     - Colonna A: Nome variabile (es. "Comando Led Verde")
     - Colonna B: Byte alto indirizzo (es. "80")
     - Colonna C: Byte basso indirizzo (es. "02")
     - Colonna D: Tipo dato

#### Variabili richieste per ogni pulsantiera:
- `Comando Led Verde` (0x8002)
- `Comando Led Rosso` (0x8003)
- `Comando Buzzer` (0x8004)

---

### 5. Services (`Services.csproj`)

**Logica di business** per test e battezzamento.

#### Componenti principali:

| File | Descrizione |
|------|-------------|
| `ButtonPanelTestService.cs` | Servizio principale test pulsantiere |
| `BaptizeService.cs` | Assegnazione indirizzo STEM a dispositivi |
| `CommunicationService.cs` | Wrapper per comunicazione con protocollo |

#### Helpers:

| File | Descrizione |
|------|-------------|
| `Helpers/PayloadBuilder.cs` | Costruzione payload comandi |
| `Helpers/ResponseParser.cs` | Parsing risposte |
| `Helpers/StemAddressHelper.cs` | Calcolo indirizzi STEM |
| `Helpers/TestResultFactory.cs` | Factory per risultati test |

#### State Machine:

| File | Descrizione |
|------|-------------|
| `Lib/ButtonPanelTestStateMachine.cs` | FSM per gestione stati test |
| `Lib/ButtonPanelTestState.cs` | Enum stati: Idle, Initializing, AwaitingButtonPress, TestingLed, TestingBuzzer, etc. |
| `Lib/ButtonPanelTestContext.cs` | Contesto corrente del test |

#### Configurazione pannelli:

```csharp
// Services/Models/PanelTypeConfiguration.cs
public sealed class PanelTypeConfiguration
{
    public ButtonPanelType PanelType { get; init; }
    public byte MachineType { get; init; }        // Tipo macchina STEM
    public ushort FirmwareType { get; init; }     // Tipo firmware
    public uint TargetAddress { get; init; }      // Indirizzo finale
}

// Configurazioni:
// DIS0023789 (Eden BS8):    MachineType=0x03, TargetAddress=0x00030101
// DIS0025205 (Optimus XP):  MachineType=0x0A, TargetAddress=0x000A0101
// DIS0026166 (R3 LXP):      MachineType=0x0B, TargetAddress=0x000B0101
// DIS0026182 (Eden BS8+):   MachineType=0x0C, TargetAddress=0x000C0101
```

#### Costanti protocollo (`Models/ProtocolConstants.cs`):

```csharp
// Comandi
CMD_WHO_ARE_YOU = 0x0023      // Richiesta identificazione
CMD_WHO_AM_I = 0x0024         // Risposta identificazione
CMD_SET_ADDRESS = 0x0025      // Assegnazione indirizzo
CMD_HEARTBEAT = 0x0000        // Ping
CMD_HEARTBEAT_RESPONSE = 0x8000

// CAN IDs
ComputerSenderId = 0x00030141 // SenderId del PC
BroadcastId = 0xFFFFFFFF      // Broadcast
PanelListenId = 0x0000013F    // ID su cui ascolta la pulsantiera
PanelTransmitId = 0x00000101  // ID temporaneo durante battezzamento

// Nomi variabili
GreenLedVariable = "Comando Led Verde"
RedLedVariable = "Comando Led Rosso"
BuzzerVariable = "Comando Buzzer"
WriteVariableCommand = "Scrivi variabile logica"

// Valori
ON = [0x00, 0x00, 0x00, 0x80]
OFF = [0x00, 0x00, 0x00, 0x00]
SINGLE_BLINK = [0x00, 0xFF, 0x80, 0x61]
```

---

### 6. GUI.Windows (`GUI.Windows.csproj`)

**Frontend WinForms** con pattern **MVP** (Model-View-Presenter).

#### Struttura:

| File/Cartella | Descrizione |
|---------------|-------------|
| `Program.cs` | Entry point, DI setup, estrazione risorse embedded |
| `Form1.cs` | Form principale (container) |
| `Views/ButtonPanelTestUserControl.cs` | Vista principale test |
| `Presenters/ButtonPanelTestPresenter.cs` | Logica presentazione |
| `Properties/Resources.resx` | Risorse embedded (Excel, immagini, DLL) |
| `Resources/` | File sorgente risorse |

#### Risorse embedded:
- `StemDictionaries.xlsx` - Dizionario protocollo
- `PCANBasic.dll` - DLL nativa PCAN
- `DIS*.jpg` - Immagini pulsantiere
- `Ztem.ico` - Icona applicazione

#### Flusso startup (`Program.cs`):
1. Setup logging (console + file)
2. Estrazione `PCANBasic.dll` da risorse in temp folder
3. Configurazione DLL search path
4. Estrazione `StemDictionaries.xlsx` in Resources/
5. Pre-caricamento dizionari protocollo per tutti i recipientId
6. Setup DI container
7. Avvio Form principale

#### Configurazione publish (single-file):
```xml
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
```

---

### 7. Tests (`Tests.csproj`)

**Suite di test** organizzata per layer.

#### Struttura:

```
Tests/
├── Unit/
│   ├── Core/
│   ├── Communication/
│   ├── Data/
│   ├── Infrastructure/
│   └── Services/
├── Integration/
│   ├── Communication/
│   ├── Data/
│   └── Services/
├── EndToEnd/
│   └── Services/
├── Helpers/
│   ├── ProtocolTestBuilders.cs
│   ├── ProtocolAssertions.cs
│   └── ByteArrayHelpers.cs
└── Resources/
    └── StemDictionaries.xlsx
```

---

## Flusso di collaudo

### 1. Battezzamento (Baptize)

```
1. PC invia WHO_ARE_YOU (0x0023) in broadcast (0xFFFFFFFF)
   - Payload: MachineType, FirmwareType, ResetFlag

2. Dispositivo risponde WHO_AM_I (0x0024) su 0x1FFFFFFF
   - Payload: MachineType, FirmwareType, UUID (12 byte)

3. PC invia SET_ADDRESS (0x0025) in broadcast
   - Payload: UUID + nuovo indirizzo STEM

4. Dispositivo conferma con ACK
   - Ora risponde sull'indirizzo assegnato
```

### 2. Test pulsanti

```
1. PC avvia heartbeat loop (0x0000 ogni 1s)
2. PC attende eventi button press dalla pulsantiera
   - Variabile 0x8000 o 0x803E contiene maschera pulsanti
3. Per ogni pulsante: verifica che la maschera corrisponda
4. Timeout 5s per ogni pulsante
```

### 3. Test LED

```
1. PC invia comando "Scrivi variabile logica" (0x0002)
   - Variabile: "Comando Led Verde" (0x8002)
   - Valore: ON [0x00, 0x00, 0x00, 0x80]
2. Richiede conferma utente "Il LED verde è acceso?"
3. Ripete per LED rosso e combinazioni
4. Spegne LED alla fine
```

### 4. Test Buzzer

```
1. PC invia comando "Scrivi variabile logica" (0x0002)
   - Variabile: "Comando Buzzer" (0x8004)
   - Valore: SINGLE_BLINK [0x00, 0xFF, 0x80, 0x61]
2. Richiede conferma utente "Hai sentito il buzzer?"
```

---

## Tipi di pulsantiere supportate

| Enum | Codice | Nome | Pulsanti | LED | Buzzer |
|------|--------|------|----------|-----|--------|
| `DIS0023789` | Eden BS8 | 8 | ✅ | ✅ | ✅ |
| `DIS0025205` | Optimus XP | 4 | ❌ | ✅ | ✅ |
| `DIS0026166` | R3 LXP | 8 | ✅ | ✅ | ✅ |
| `DIS0026182` | Eden BS8+ | 8 | ✅ | ✅ | ✅ |

---

## Build e Deployment

### Build
```bash
dotnet build -c Release
```

### Test
```bash
dotnet test
```

### Publish (single-file executable)
```bash
dotnet publish GUI.Windows/GUI.Windows.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

Output: `GUI.Windows.exe` (~165 MB) - eseguibile standalone, non richiede .NET installato.

---

## Note importanti

### Aggiungere una nuova pulsantiera

1. **Core/Enums/ButtonPanelEnums.cs**: Aggiungere tipo in `ButtonPanelType`
2. **Core/Models/Services/ButtonPanel.cs**: Aggiungere case in `GetByType()` con configurazione pulsanti
3. **Services/Models/PanelTypeConfiguration.cs**: Aggiungere configurazione (MachineType, FirmwareType, TargetAddress)
4. **GUI.Windows/Presenters/ButtonPanelTestPresenter.cs**: Aggiungere mapping recipientId in `GetRecipientIdForPanel()`
5. **GUI.Windows/Program.cs**: Aggiungere preload del recipientId
6. **StemDictionaries.xlsx**: Aggiungere foglio con variabili LED/Buzzer e ID nella riga 2
7. **GUI.Windows/Resources/Images/**: Aggiungere immagine pulsantiera
8. **GUI.Windows/Properties/Resources.resx**: Aggiungere riferimento immagine

### Modificare il dizionario Excel

1. Modificare `GUI.Windows/Resources/StemDictionaries.xlsx`
2. Modificare `Tests/Resources/StemDictionaries.xlsx` (copia)
3. **Rebuild** il progetto (il file è embedded nelle risorse)

### CAN IDs osservati durante il collaudo

| ID | Descrizione |
|----|-------------|
| `0xFFFFFFFF` | Broadcast (battezzamento) |
| `0x1FFFFFFF` | Risposta WHO_AM_I da dispositivo non battezzato |
| `0x00030141` | SenderId PC (risposte dalla pulsantiera) |
| `0x0000013F` | Arbitration ID per comandi alla pulsantiera |
| `0x00000101` | Indirizzo temporaneo durante battezzamento |
| `0x000X013F` | Indirizzo finale (X = MachineType) |

---

## Struttura file Excel

### Requisiti per ogni tipo di pulsantiera

Nel foglio corrispondente al tipo di dispositivo:

1. **Riga 2** deve contenere `0x{RecipientId}` (es. `0x000C0101`)
2. Le righe variabili devono avere:
   - **Colore sfondo specifico** (ARGB=-7155632) nella colonna A
   - Nome variabile in colonna A
   - AddrH in colonna B (es. "80")
   - AddrL in colonna C (es. "02", "03", "04")
   - Tipo in colonna D

### Variabili obbligatorie

| Nome | AddrH | AddrL | Descrizione |
|------|-------|-------|-------------|
| Comando Led Verde | 80 | 02 | Controllo LED verde |
| Comando Led Rosso | 80 | 03 | Controllo LED rosso |
| Comando Buzzer | 80 | 04 | Controllo buzzer |
