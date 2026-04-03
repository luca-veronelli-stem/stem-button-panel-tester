# Stem.ButtonPanel.Tester

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-blue)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-Proprietary-red)](#licenza)

> **Applicazione desktop per collaudo pulsantiere STEM CAN. Stack completo con protocollo a 3 livelli, battezzamento e FSM.**

> **Ultimo aggiornamento:** 2026-04-03

---

## Panoramica

**Stem.ButtonPanel.Tester** è un'applicazione WinForms per il collaudo delle pulsantiere utilizzate nei letti ospedalieri STEM. Il sistema implementa:

- **Protocollo STEM CAN** — Stack a 3 livelli (Application, Transport, Network) con chunking e CRC-16
- **Battezzamento Dispositivi** — Assegnazione automatica indirizzi STEM tramite sequenza WHO_ARE_YOU/SET_ADDRESS
- **Test Automatizzati** — Verifica pulsanti, LED, buzzer con FSM per gestione workflow
- **Hardware PCAN** — Comunicazione CAN via PEAK PCAN-USB con auto-recovery
- **Repository Excel** — Dizionari protocollo con caching per performance
- **Clean Architecture** — Separazione netta tra dominio, infrastruttura, comunicazione, dati e UI

Il progetto supporta **4 tipi di pulsantiere**:
- **DIS0023789** (Eden-XP) — 8 pulsanti con LED
- **DIS0025205** (Optimus-XP) — 4 pulsanti senza LED
- **DIS0026166** (R3L-XP) — 8 pulsanti con LED
- **DIS0026182** (Eden-BS8) — 8 pulsanti con LED

---

## Caratteristiche

| Feature | Stato | Descrizione |
|---------|-------|-------------|
| **Protocollo STEM CAN** | ✅ | Stack 3 livelli con chunking, CRC-16, reassembly |
| **Battezzamento** | ✅ | WHO_ARE_YOU → WHO_AM_I → SET_ADDRESS |
| **Test Pulsanti** | ✅ | Verifica sequenziale con timeout 5s |
| **Test LED** | ✅ | Accensione verde/rosso con conferma utente |
| **Test Buzzer** | ✅ | Attivazione con conferma acustica |
| **Heartbeat** | ✅ | Monitoraggio comunicazione ogni 1s |
| **Auto-Recovery** | ✅ | Riconnessione CAN automatica |
| **Salvataggio Risultati** | ✅ | Export testuale con timestamp |
| **Single-File Deploy** | ✅ | Exe self-contained ~150MB |

---

## Requisiti

- **.NET 10.0 SDK** o superiore
- **Windows x64** (per WinForms e driver PCAN)
- **Visual Studio 2026** o Visual Studio Code (opzionale)
- **Hardware PEAK PCAN-USB** per test su dispositivi reali

### Requisiti di Sistema

| Componente | Minimo | Consigliato |
|------------|--------|-------------|
| OS | Windows 10 x64 | Windows 11 x64 |
| RAM | 4 GB | 8 GB |
| CPU | 2 core | 4 core |
| Storage | 500 MB | 1 GB |

---

## Quick Start

### Clonazione

```bash
git clone https://bitbucket.org/stem-fw/button-panel-tester.git
cd Stem.ButtonPanel.Tester
```

### Build

```bash
# Build intera soluzione
dotnet build

# Build solo GUI
dotnet build GUI.WinForms/GUI.WinForms.csproj
```

### Esecuzione

```bash
# Run GUI
dotnet run --project GUI.WinForms/GUI.WinForms.csproj
```

### Test

```bash
# Tutti i test (Unit + Integration + E2E)
dotnet test

# Solo Unit Test
dotnet test --filter "Category=Unit"

# Con code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Pubblicazione

```bash
# Single-file self-contained per Windows x64
dotnet publish GUI.WinForms/GUI.WinForms.csproj -c Release -r win-x64 --self-contained

# Output in: GUI.WinForms/bin/Release/net10.0-windows/win-x64/publish/GUI.WinForms.exe
```

---

## Struttura Soluzione

```
Stem.ButtonPanel.Tester/
├── Core/                   # ⭐ Dominio puro (modelli, interfacce, enums) — zero dipendenze
│   ├── Enums/              #    ButtonPanelType, CommunicationChannel, IndicatorState
│   ├── Interfaces/         #    Contratti per Services, Data, Communication, Infrastructure
│   ├── Models/             #    ButtonPanel, ButtonPanelTestResult, CanPacket
│   ├── Results/            #    Result Pattern per gestione errori esplicita
│   └── README.md
├── Infrastructure/         # 🔌 Adattatore hardware PEAK PCAN
│   ├── Lib/                #    IPcanApi, PcanApiWrapper (wrapper .NET per PCANBasic.dll)
│   ├── PcanAdapter.cs      #    Implementazione ICanAdapter con auto-recovery
│   └── README.md
├── Communication/          # 📡 Stack protocollare STEM a 3 livelli
│   ├── Protocol/           #    Application → Transport → Network
│   │   ├── Layers/         #    ApplicationLayer, TransportLayer, NetworkLayer
│   │   └── Lib/            #    CRC-16, NetInfo, ProtocolHelpers
│   ├── CanCommunicationManager.cs
│   └── README.md
├── Data/                   # 📊 Repository dizionari Excel
│   ├── ExcelRepository.cs           #    Parsing fogli Excel (ClosedXML)
│   ├── CachedExcelProtocolRepository.cs  #    Cache globale thread-safe
│   ├── ExcelProtocolRepositoryFactory.cs #    Factory con preloading
│   └── README.md
├── Services/               # 🎯 Business Logic
│   ├── ButtonPanelTestService.cs    #    Orchestrazione test con FSM
│   ├── BaptizeService.cs            #    Assegnazione indirizzi STEM
│   ├── CommunicationService.cs      #    Astrazione multi-canale
│   ├── Helpers/                     #    PayloadBuilder, ResponseParser, StemAddressHelper
│   ├── Lib/                         #    State Machine per workflow test
│   └── README.md
├── GUI.WinForms/           # 🖥️ Applicazione desktop WinForms
│   ├── Program.cs          #    Entry point, DI setup, embedded resources
│   ├── Form1.cs            #    Main window
│   ├── Presenters/         #    ButtonPanelTestPresenter (pattern MVP)
│   ├── Views/              #    ButtonPanelTestUserControl con indicatori overlay
│   └── README.md
├── Tests/                  # ✅ Suite test completa
│   ├── Unit/               #    ~150 test per Core, Protocol, Services
│   ├── Integration/        #    ~30 test con Excel reale, stack completo
│   ├── EndToEnd/           #    ~10 test workflow ButtonPanelTestService
│   ├── Helpers/            #    ProtocolTestBuilders, ProtocolAssertions
│   └── README.md
├── Docs/                   # 📚 Documentazione
│   ├── Standards/          #    Template per README, ISSUES, STANDARD
│   └── BUTTONPANEL_TESTER.md
├── ISSUES_TRACKER.md       # 🐛 Tracker issue globale (45 issue totali)
├── LICENSE                 # 📄 Licenza proprietaria
└── README.md               # 📖 Questo file
```

**Legenda:**
- ⭐ **Core** — Nessuna dipendenza esterna, centro Clean Architecture
- 🔌 **Infrastructure** — Dipendenza da driver PCAN hardware
- 📡 **Communication** — Stack protocollare STEM completo
- 📊 **Data** — Accesso dizionari Excel con caching
- 🎯 **Services** — Logica business e FSM
- 🖥️ **GUI.WinForms** — UI desktop Windows
- ✅ **Tests** — ~200 test (Unit, Integration, E2E)

---

## Documentazione

### README per Componente

| Componente | README | Descrizione |
|------------|--------|-------------|
| **Core** | [Core/README.md](./Core/README.md) | Modelli dominio, interfacce, Result Pattern |
| **Infrastructure** | [Infrastructure/README.md](./Infrastructure/README.md) | Adapter PCAN, auto-recovery, diagnostica |
| **Communication** | [Communication/README.md](./Communication/README.md) | Stack protocollare a 3 livelli |
| **Data** | [Data/README.md](./Data/README.md) | Repository Excel con caching |
| **Services** | [Services/README.md](./Services/README.md) | Business logic, FSM, battezzamento |
| **GUI.WinForms** | [GUI.WinForms/README.md](./GUI.WinForms/README.md) | Applicazione WinForms MVP |
| **Tests** | [Tests/README.md](./Tests/README.md) | Suite test completa |

### ISSUES per Componente

| Componente | ISSUES | Issue Totali |
|------------|--------|--------------|
| **Core** | [Core/ISSUES.md](./Core/ISSUES.md) | 7 (0 alta, 3 media, 4 bassa) |
| **Infrastructure** | [Infrastructure/ISSUES.md](./Infrastructure/ISSUES.md) | 5 (1 alta, 2 media, 2 bassa) |
| **Communication** | [Communication/ISSUES.md](./Communication/ISSUES.md) | 6 (1 alta, 2 media, 3 bassa) |
| **Data** | [Data/ISSUES.md](./Data/ISSUES.md) | 6 (1 alta, 3 media, 2 bassa) |
| **Services** | [Services/ISSUES.md](./Services/ISSUES.md) | 7 (2 alta, 3 media, 2 bassa) |
| **GUI.WinForms** | [GUI.WinForms/ISSUES.md](./GUI.WinForms/ISSUES.md) | 7 (1 alta, 3 media, 3 bassa) |
| **Tests** | [Tests/ISSUES.md](./Tests/ISSUES.md) | 6 (0 alta, 2 media, 4 bassa) |
| **Trasversali** | [ISSUES_TRACKER.md](./ISSUES_TRACKER.md) | 1 (1 alta T-001: lock migration) |

**Totale issue:** 45 (7 alta, 18 media, 20 bassa)

### Standards

- [ISSUES_TEMPLATE.md](./Docs/Standards/Templates/ISSUES_TEMPLATE.md) — Template per file ISSUES.md
- [README_TEMPLATE.md](./Docs/Standards/Templates/README_TEMPLATE.md) — Template per README componenti
- [TEMPLATE_STANDARD.md](./Docs/Standards/Templates/TEMPLATE_STANDARD.md) — Meta-template

### Documentazione Tecnica

- [BUTTONPANEL_TESTER.md](./Docs/BUTTONPANEL_TESTER.md) — Documentazione progetto

---

## Architettura

### Clean Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      GUI.WinForms                            │
│                  (Presenter + View)                          │
└────────────────┬────────────────────────────────────────────┘
                 │ dipende da
                 ▼
┌─────────────────────────────────────────────────────────────┐
│                       Services                               │
│     (ButtonPanelTestService, BaptizeService, FSM)           │
└───┬──────────────────────────────────────┬──────────────────┘
    │ dipende da                           │ dipende da
    ▼                                      ▼
┌──────────────────────┐         ┌──────────────────────┐
│   Communication      │         │       Data           │
│  (Protocol Manager)  │         │ (Excel Repository)   │
└─────────┬────────────┘         └──────────────────────┘
          │ dipende da
          ▼
┌──────────────────────┐
│   Infrastructure     │
│   (PCAN Adapter)     │
└─────────┬────────────┘
          │ dipende da
          ▼
┌──────────────────────┐
│        Core          │
│  (Interfaces, Models)│
└──────────────────────┘
```

**Flusso dati tipico:**
1. **GUI** → Click "Avvia Test"
2. **Presenter** → Chiama `service.TestAllAsync()`
3. **Service** → Usa `communicationService.SendCommandAsync()`
4. **Communication** → Costruisce pacchetti con `protocolManager.BuildPackets()`
5. **Infrastructure** → Invia chunk CAN via `canAdapter.Send()`
6. **PCAN Hardware** → Trasmette su bus CAN
7. **Pulsantiera** → Risponde con pacchetto
8. **Infrastructure** → Riceve via `PacketReceived` event
9. **Communication** → Reassembla e valida CRC
10. **Service** → Interpreta risposta
11. **Presenter** → Aggiorna UI con risultato

---

## Protocollo STEM

### Stack a 3 Livelli

| Livello | Responsabilità | Formato |
|---------|----------------|---------|
| **Application** (L7) | Comandi + payload | `[cmdInit (1)] [cmdOpt (1)] [payload (N)]` |
| **Transport** (L4) | CRC, SenderId, lunghezza | `[crypt (1)] [senderId (4)] [lPack (2)] [app (N)] [CRC (2)]` |
| **Network** (L3) | Chunking, NetInfo | `[NetInfo (2)] [chunk (6)]` |

### Esempio: Accensione LED Verde

```
1. Application Layer
   Command: 0x0002 ("Scrivi variabile logica")
   Payload: [0x04, 0x01, 0x00, 0x00, 0x00, 0x80]  # LED verde ON
   Packet:  [0x00, 0x02, 0x04, 0x01, 0x00, 0x00, 0x00, 0x80]

2. Transport Layer
   Header:  [0x00] [0x41, 0x01, 0x03, 0x00] [0x08, 0x00]
            ^crypt  ^senderId (0x00030141)  ^lPack (8)
   Packet:  [header (7)] + [app (8)] + [CRC (2)] = 17 byte

3. Network Layer (chunking per CAN MTU=8, payload=6)
   Chunk 0: [NetInfo: 0x1A] [0x00, 0x41, 0x01, 0x03, 0x00, 0x08] (8 byte)
   Chunk 1: [NetInfo: 0x0A] [0x00, 0x00, 0x02, 0x04, 0x01, 0x00] (8 byte)
   Chunk 2: [NetInfo: 0x06] [0x00, 0x00, 0x80, 0xAB, 0xCD]       (7 byte)
            ^remainingChunks=0, setLength=1

4. CAN Bus
   ArbitrationId: 0x00030101 (recipientId = indirizzo pulsantiera)
   Data: chunk[0..7]
```

---

## Workflow Test

### Test Completo (TestAllAsync)

```
┌─────────────────────────────────────────────────────────────┐
│  1. Connessione CAN (250 kbit/s)                            │
│  2. Battezzamento dispositivo                               │
│     • WHO_ARE_YOU → WHO_AM_I (UUID)                         │
│     • SET_ADDRESS → ACK                                     │
│  3. Test Pulsanti (per ogni pulsante)                       │
│     • Prompt: "Premi pulsante X"                            │
│     • Attendi pressione (timeout 5s)                        │
│     • Registra pass/fail                                    │
│  4. Test LED (se panel.HasLed)                              │
│     • Accendi LED verde → Conferma visibilità              │
│     • Accendi LED rosso → Conferma visibilità              │
│  5. Test Buzzer                                             │
│     • Attiva buzzer → Conferma acustica                     │
│  6. Disconnessione                                          │
└─────────────────────────────────────────────────────────────┘
```

---

## Dipendenze Esterne

| Package | Versione | Uso | Progetti |
|---------|----------|-----|----------|
| **ClosedXML** | 0.105.0 | Lettura file Excel | Data, Tests |
| **Peak.PCANBasic.NET** | 4.10.1.968 | Driver PCAN | Infrastructure, Tests |
| **Microsoft.Extensions.Logging** | 10.0.1 | Logging | GUI.WinForms |
| **xunit** | 2.9.3 | Testing | Tests |
| **Moq** | 4.20.72 | Mock | Tests |
| **coverlet.collector** | 6.0.4 | Code coverage | Tests |

**Nota:** Core, Communication e Services sono **dependency-free** (solo dipendenze tra progetti).

---

## CI/CD

### Pipeline Bitbucket

```yaml
# bitbucket-pipelines.yml
image: mcr.microsoft.com/dotnet/sdk:10.0

pipelines:
  default:
    - step:
        name: Build and Test
        script:
          - dotnet restore
          - dotnet build --no-restore
          - dotnet test --no-build --filter "Category!=RequiresHardware&Category!=RequiresWindows"
```

**Test eseguiti su CI:**
- ✅ Unit Tests (~150 test)
- ✅ Integration Tests (~30 test)
- ✅ E2E Tests (~10 test)
- ❌ RequiresHardware (solo manuale con PCAN)
- ❌ RequiresWindows (CI Linux)

---

## Issue Correlate

→ [ISSUES_TRACKER.md](./ISSUES_TRACKER.md)

### Issue Alta Priorità (7 totali)

| ID | Componente | Titolo |
|----|------------|--------|
| **T-001** | Trasversale | Migrare lock da object a System.Threading.Lock |
| **INFRA-001** | Infrastructure | _recoveryLock usa object invece di Lock |
| **DATA-001** | Data | Task.Run().GetAwaiter().GetResult() blocca thread |
| **COMM-001** | Communication | _reassemblyLock usa object invece di Lock |
| **SVC-001** | Services | _heartbeatLock usa object invece di Lock |
| **SVC-002** | Services | Task.Run fire-and-forget in NotifyCommunicationLost |
| **GUI-001** | GUI.WinForms | async void senza try-catch completo |

**Riepilogo:** 45 issue totali (7 alta, 18 media, 20 bassa)

---

## Licenza

- **Proprietario:** STEM E.m.s. S.r.l.
- **Autore:** Luca Veronelli (l.veronelli@stem.it)
- **Data di Creazione:** 2026-04-03
- **Licenza:** Proprietaria - Tutti i diritti riservati

**Nota:** Questo software è proprietà di STEM E.m.s. S.r.l. e non può essere distribuito, modificato o utilizzato al di fuori dell'organizzazione senza autorizzazione esplicita.