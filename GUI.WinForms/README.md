# GUI.WinForms

> **Applicazione desktop WinForms per collaudo pulsantiere STEM. Pattern MVP con DI, pubblicazione single-file self-contained.**  
> **Ultimo aggiornamento:** 2026-04-03

---

## Panoramica

**GUI.WinForms** è l'applicazione desktop per il collaudo delle pulsantiere STEM. Implementa:

- **Pattern MVP** — Model-View-Presenter per separazione responsabilità
- **Dependency Injection** — `Microsoft.Extensions.DependencyInjection`
- **Single-File Deployment** — Pubblicazione self-contained win-x64
- **Embedded Resources** — DLL PCAN e dizionari Excel integrati
- **Logging** — Console, Debug e file per diagnostica startup
- **Indicatori Visivi** — Overlay su immagini pulsantiere con stato test

L'applicazione orchestra tutti i layer (Core, Infrastructure, Communication, Data, Services) per fornire un'interfaccia operatore per il collaudo.

---

## Caratteristiche

| Feature | Stato | Descrizione |
|---------|-------|-------------|
| **Selezione Pulsantiera** | ✅ | 4 tipi: Eden-XP, Optimus, R3L-XP, Eden-BS8 |
| **Test Completo/Parziale** | ✅ | Complete, Buttons, Led, Buzzer |
| **Indicatori Visivi** | ✅ | Overlay colorati su immagine pannello |
| **Salvataggio Risultati** | ✅ | Nuovo file o append a esistente |
| **Battezzamento Auto** | ✅ | Prima di ogni test |
| **Recovery Comunicazione** | ✅ | Dialog su perdita CAN |
| **Single-File Deployment** | ✅ | Exe self-contained ~150MB |
| **Preload Dizionari** | ✅ | Async all'avvio per UX fluida |

---

## Requisiti

- **.NET 10.0** o superiore
- **Windows x64** (WinForms)
- **Hardware PEAK PCAN-USB** per test reali

### Dipendenze

| Package | Versione | Uso |
|---------|----------|-----|
| `Microsoft.Extensions.Logging.Console` | 10.0.1 | Logging console |
| `Microsoft.Extensions.Logging.Debug` | 10.0.1 | Logging debug |
| `Core` | (progetto) | Interfacce, modelli |
| `Infrastructure` | (progetto) | Adapter PCAN |
| `Communication` | (progetto) | Stack protocollare |
| `Data` | (progetto) | Repository Excel |
| `Services` | (progetto) | Logica business |

---

## Quick Start

### Sviluppo

```bash
# Build
dotnet build GUI.WinForms/GUI.WinForms.csproj

# Run
dotnet run --project GUI.WinForms/GUI.WinForms.csproj
```

### Pubblicazione

```bash
# Single-file self-contained per Windows x64
dotnet publish GUI.WinForms/GUI.WinForms.csproj -c Release -r win-x64 --self-contained

# Output in: GUI.WinForms/bin/Release/net10.0-windows/win-x64/publish/
```

**Parametri pubblicazione (già configurati nel .csproj):**
- `PublishSingleFile=true`
- `SelfContained=true`
- `PublishTrimmed=false`
- `IncludeNativeLibrariesForSelfExtract=true`

---

## Struttura

```
GUI.WinForms/
├── Program.cs                        # Entry point, DI setup, startup probes
├── Form1.cs                          # Main window
├── Form1.Designer.cs                 # Designer-generated
├── Presenters/
│   └── ButtonPanelTestPresenter.cs   # Logica UI, gestione eventi
├── Views/
│   ├── ButtonPanelTestUserControl.cs # UserControl principale
│   └── ButtonPanelTestUserControl.Designer.cs
├── Properties/
│   ├── Resources.resx                # Risorse embedded (icone, immagini, Excel, DLL)
│   └── Resources.Designer.cs         # Accessor risorse
└── Resources/
    └── Ztem.ico                      # Icona applicazione
```

---

## Architettura MVP

### Pattern

```
┌─────────────────────────────────────────────────────────────┐
│                          View                                │
│        ButtonPanelTestUserControl : IButtonPanelTestView    │
├─────────────────────────────────────────────────────────────┤
│  Eventi UI:                        │  Metodi:               │
│  • OnPanelTypeChanged              │  • ShowPromptAsync     │
│  • OnStartTestClicked              │  • ShowConfirmAsync    │
│  • OnStopTestClicked               │  • SetButtonWaiting    │
│  • OnSaveNewFileClicked            │  • SetButtonResult     │
│  • OnSaveExistingFileClicked       │  • DisplayResults      │
└────────────────┬────────────────────────────────────────────┘
                 │ eventi
                 ▼
┌─────────────────────────────────────────────────────────────┐
│                       Presenter                              │
│              ButtonPanelTestPresenter                        │
├─────────────────────────────────────────────────────────────┤
│  Dipendenze:                       │  Handlers:             │
│  • IButtonPanelTestView            │  • HandleStartTestAsync│
│  • IButtonPanelTestService         │  • HandleStopTestAsync │
│  • IProtocolRepositoryFactory      │  • HandleSaveClicked   │
│                                    │  • HandleCommLost      │
└────────────────┬────────────────────────────────────────────┘
                 │ chiamate
                 ▼
┌─────────────────────────────────────────────────────────────┐
│                        Model                                 │
│     Services: IButtonPanelTestService, IBaptizeService      │
│     Data: IProtocolRepository                                │
└─────────────────────────────────────────────────────────────┘
```

### Flusso Eventi

1. **Utente** seleziona tipo pulsantiera → `OnPanelTypeChanged`
2. **Presenter** aggiorna `IProtocolRepository` per recipientId corretto
3. **Utente** clicca "Avvia Test" → `OnStartTestClicked`
4. **Presenter** esegue `service.TestAllAsync()` con callbacks:
   - `userPrompt` → `view.ShowPromptAsync()`
   - `userConfirm` → `view.ShowConfirmAsync()`
   - `onButtonStart` → `view.SetButtonWaiting(i)`
   - `onButtonResult` → `view.SetButtonResult(i, passed)`
5. **View** aggiorna UI (indicatori, progress, risultati)

---

## Dependency Injection

### Setup in Program.cs

```csharp
var services = new ServiceCollection();

// Logging
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Trace);
    builder.AddConsole();
    builder.AddDebug();
    builder.AddProvider(new FileLoggerProvider(logFile));
});

// Infrastructure
services.AddSingleton<IPcanApi, PcanApiWrapper>();
services.AddSingleton<ICanAdapter, PcanAdapter>();
services.AddSingleton<CanCommunicationManager>();

// Communication
services.AddSingleton<IProtocolManager, StemProtocolManager>();
services.AddTransient<ICommunicationManagerFactory, CommunicationManagerFactory>();
services.AddSingleton<ICommunicationService, CommunicationService>();

// Data
services.AddTransient<IExcelRepository, ExcelRepository>();
services.AddTransient<IProtocolRepositoryFactory>(sp =>
    new ExcelProtocolRepositoryFactory(sp.GetRequiredService<IExcelRepository>(), excelFilePath));

// Services
services.AddSingleton<IBaptizeService, BaptizeService>();
services.AddSingleton<IButtonPanelTestService>(sp => ...);

// UI
services.AddTransient<Form1>();
```

### Preload Dizionari

All'avvio, pre-carica i dizionari per evitare blocchi UI:

```csharp
var commonRecipientIds = new[] { 0x00030101u, 0x000A0101u, 0x000B0101u, 0x000C0101u };
foreach (var recipientId in commonRecipientIds)
{
    await preloadFactory.PreloadAsync(recipientId).ConfigureAwait(false);
}
```

---

## Interfaccia Utente

### Layout Principale

```
┌─────────────────────────────────────────────────────────────────┐
│  Stem Button Panel Tester                              [─][□][×]│
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────────┐                                               │
│  │ DIS0023789   │  ┌───────────────────────────────────────┐   │
│  │ DIS0025205   │  │                                       │   │
│  │ DIS0026166   │  │     [Immagine Pulsantiera]           │   │
│  │ DIS0026182   │  │     + Indicatori Overlay             │   │
│  └──────────────┘  │                                       │   │
│                    └───────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ [Complete] [Buttons] [Led] [Buzzer]                     │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  [Avvia Test]  [Interrompi]  [Salva Nuovo]  [Salva Esistente]  │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ Progress Log:                                            │   │
│  │ > Avvio collaudo Complete per DIS0023789...             │   │
│  │ > Premi pulsante Stop                                    │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ Risultati:                                               │   │
│  │ Buttons: ✓ PASSED - Tutti i pulsanti verificati        │   │
│  │ Led: ✓ PASSED - LED verde e rosso verificati           │   │
│  │ Buzzer: ✓ PASSED - Buzzer verificato                   │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### Indicatori Overlay

| Stato | Colore | Descrizione |
|-------|--------|-------------|
| `Idle` | Bianco (α=120) | Pulsante non testato |
| `Waiting` | Giallo (α=180) | In attesa pressione |
| `Success` | Verde (α=180) | Test passato |
| `Failed` | Rosso (α=180) | Test fallito |

---

## Risorse Embedded

### Properties/Resources.resx

| Risorsa | Tipo | Descrizione |
|---------|------|-------------|
| `Ztem` | `byte[]` | Icona applicazione (.ico) |
| `StemDictionaries` | `byte[]` | File Excel dizionari |
| `PCANBasic` | `byte[]` | DLL nativa PCAN (estratta a runtime) |
| `DIS0023789` | `byte[]` | Immagine pulsantiera Eden-XP |
| `DIS0025205` | `byte[]` | Immagine pulsantiera Optimus |
| `DIS0026166` | `byte[]` | Immagine pulsantiera R3L-XP |
| `DIS0026182` | `byte[]` | Immagine pulsantiera Eden-BS8 |

### Estrazione Runtime

```csharp
// Excel dizionari
var excelBytes = Properties.Resources.StemDictionaries;
File.WriteAllBytes(excelOutPath, excelBytes);

// DLL PCAN
ExtractPcanFromResx(logger);
```

---

## Logging

### Livelli

| Livello | Uso |
|---------|-----|
| `Trace` | Dettagli protocollo, chunk CAN |
| `Debug` | Flusso test, stato FSM |
| `Information` | Avvio, connessione, risultati |
| `Warning` | Timeout, heartbeat mancati |
| `Error` | Errori recuperabili, test falliti |
| `Critical` | Errori fatali startup |

### File di Log

```
logs/
└── startup_20260403_143052.log
```

**Contenuto esempio:**
```
[14:30:52 INF] Application starting. BaseDirectory='C:\...\', ProcessId=12345, OS=Windows, Framework=.NET 10.0
[14:30:52 INF] Extracted embedded StemDictionaries.xlsx to C:\...\Resources\StemDictionaries.xlsx
[14:30:53 INF] Pre-loaded protocol data for recipientId=0x00030101
[14:30:53 INF] ServiceProvider built successfully. Running application.
```

---

## Gestione Errori

### Exception Handlers Globali

```csharp
// Eccezioni non gestite nel dominio app
AppDomain.CurrentDomain.UnhandledException += (s, e) =>
{
    logger.LogCritical(e.ExceptionObject as Exception, "Unhandled exception");
    File.WriteAllText("pcan-error.txt", e.ExceptionObject.ToString());
};

// Eccezioni thread UI
Application.ThreadException += (s, e) =>
{
    logger.LogError(e.Exception, "UI thread exception");
};

// Task non osservate
TaskScheduler.UnobservedTaskException += (s, e) =>
{
    logger.LogError(e.Exception, "Unobserved task exception");
};
```

### Probe Diagnostici

All'avvio vengono scritti file di probe per troubleshooting:

```
pcan-probe.txt    # Info ambiente, PATH, moduli caricati
pcan-error.txt    # Errori (se presenti)
```

---

## Esecuzione

### Da Visual Studio

1. Impostare **GUI.WinForms** come progetto di avvio
2. `F5` per debug o `Ctrl+F5` per esecuzione

### Da CLI

```bash
cd GUI.WinForms
dotnet run
```

### Pubblicato

```bash
# Dopo publish
./bin/Release/net10.0-windows/win-x64/publish/GUI.WinForms.exe
```

---

## Issue Correlate

→ [GUI.WinForms/ISSUES.md](./ISSUES.md)

**Issue Alta Priorità:**
- `GUI-001` — async void nei gestori eventi senza try-catch completo

---

## Links

- [ISSUES_TRACKER.md](../ISSUES_TRACKER.md) — Tracker globale issue
- [Services/README.md](../Services/README.md) — Servizi consumati
- [Core/README.md](../Core/README.md) — Interfaccia IButtonPanelTestView
- [Infrastructure/README.md](../Infrastructure/README.md) — Adapter PCAN
