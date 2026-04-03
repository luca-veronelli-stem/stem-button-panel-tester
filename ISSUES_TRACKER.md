# Stem.ButtonPanel.Tester - Issue Tracker

> **Ultimo aggiornamento:** 2026-04-03

---

## Riepilogo Globale

| Componente | Aperte | Risolte | Totale |
|------------|--------|---------|--------|
| [Core](./Core/ISSUES.md) | 7 | 0 | 7 |
| [Infrastructure](./Infrastructure/ISSUES.md) | 5 | 0 | 5 |
| [Data](./Data/ISSUES.md) | 6 | 0 | 6 |
| [Communication](./Communication/ISSUES.md) | 6 | 0 | 6 |
| [Services](./Services/ISSUES.md) | 7 | 0 | 7 |
| [GUI.WinForms](./GUI.WinForms/ISSUES.md) | 7 | 0 | 7 |
| [Tests](./Tests/ISSUES.md) | 6 | 0 | 6 |
| **Trasversali** | **1** | **0** | **1** |
| **Totale** | **45** | **0** | **45** |

---

## Distribuzione per Priorità

| Priorità | Aperte | % |
|----------|--------|---|
| **Critica** | 0 | 0% |
| **Alta** | 7 | 16% |
| **Media** | 18 | 40% |
| **Bassa** | 20 | 44% |
| **Totale** | **45** | 100% |

```
Critica:     ░░░░░░░░░░░░░░░░░░░░  0
Alta:        ███░░░░░░░░░░░░░░░░░  7  (T-001, INFRA-001, DATA-001, COMM-001, SVC-001, SVC-002, GUI-001)
Media:       ████████░░░░░░░░░░░░ 18
Bassa:       █████████░░░░░░░░░░░ 20
```

---

## Issue Alta Priorità

| ID | Componente | Titolo | Status |
|----|------------|--------|--------|
| **T-001** | **Trasversale** | **Migrare lock da object a System.Threading.Lock** | ⚠️ **Aperto** |
| **INFRA-001** | Infrastructure | _recoveryLock usa object invece di Lock | ⚠️ **Aperto** |
| **DATA-001** | Data | Task.Run().GetAwaiter().GetResult() blocca thread | ⚠️ **Aperto** |
| **COMM-001** | Communication | NetworkLayerReassembler._reassemblyLock usa object | ⚠️ **Aperto** |
| **SVC-001** | Services | _heartbeatLock e _stateLock usano object | ⚠️ **Aperto** |
| **SVC-002** | Services | Task.Run fire-and-forget in NotifyCommunicationLost | ⚠️ **Aperto** |
| **GUI-001** | GUI.WinForms | async void nei gestori eventi senza try-catch completo | ⚠️ **Aperto** |

⚠️ **7 issue alta priorità aperte**

---

## Issue Trasversali (T-xxx)

| ID | Titolo | Priorità | Status | Componenti Coinvolti |
|----|--------|----------|--------|----------------------|
| [T-001](#t-001--migrare-lock-da-object-a-systemthreadinglock) | Migrare lock da object a System.Threading.Lock | **Alta** | **Aperto** | Infrastructure, Data, Communication, Services |

### T-001 — Migrare lock da object a System.Threading.Lock

**Descrizione:**  
Tutti i progetti usano `lock(object)` invece della classe `System.Threading.Lock` introdotta in .NET 9. Questo anti-pattern è presente in 6 file distribuiti su 4 progetti. La classe `Lock` è più performante (~20% più veloce), type-safe e semanticamente più chiara.

**Status:** Aperto  
**Priorità:** Alta — anti-pattern diffuso in tutta la codebase  
**Branch proposto:** `fix/t-001-lock-migration`  
**Data Apertura:** 2026-04-03  
**Effort stimato:** S (1-2h)

**Sub-issue correlate:**

| # | ID | Componente | File | Campo |
|---|-----|------------|------|-------|
| 1 | INFRA-001 | Infrastructure | `PcanAdapter.cs` | `_recoveryLock` |
| 2 | DATA-003 | Data | `ExcelStemProtocolRepository.cs` | `_commandsLock`, `_variablesLock` |
| 3 | DATA-003 | Data | `CachedExcelProtocolRepository.cs` | `_commandsLock`, `_variablesLock` |
| 4 | COMM-001 | Communication | `NetworkLayer.cs` | `_reassemblyLock` |
| 5 | SVC-001 | Services | `ButtonPanelTestService.cs` | `_heartbeatLock` |
| 6 | SVC-001 | Services | `ButtonPanelTestStateMachine.cs` | `_stateLock` |

**Codice attuale:**
```csharp
private readonly object _lock = new();

lock (_lock)
{
    // critical section
}
```

**Codice proposto:**
```csharp
private readonly Lock _lock = new();

using (_lock.EnterScope())
{
    // critical section
}
```

**Piano di Migrazione:**
1. Infrastructure — 1 file, 1 lock
2. Communication — 1 file, 1 lock
3. Data — 2 file, 4 lock
4. Services — 2 file, 2 lock

**Benefici Attesi:**
- Performance migliore in scenari ad alta contesa
- Type-safety: impossibile passare oggetti errati
- Codice più moderno e idiomatico per .NET 10
- Consistenza in tutta la codebase

---

## Issue per Componente

### Core (7 issue aperte, 0 risolte)

| ID | Titolo | Priorità | Categoria |
|----|--------|----------|-----------|
| [CORE-001](./Core/ISSUES.md#core-001--buttonpanelgetbytype-duplica-configurazione-per-3-tipi-su-4) | ButtonPanel.GetByType duplica configurazione | Media | Code Smell |
| [CORE-002](./Core/ISSUES.md#core-002--buttonindicator-dipende-da-systemdrawing-winforms) | ButtonIndicator dipende da System.Drawing | Media | Design |
| [CORE-003](./Core/ISSUES.md#core-003--ibuttonpaneltestview-è-un-contratto-winforms-nel-layer-core) | IButtonPanelTestView è un contratto WinForms nel Core | Media | Design |
| [CORE-004](./Core/ISSUES.md#core-004--baptizestatus-enum-annidato-in-ibuttonpaneltestview) | BaptizeStatus enum annidato in IButtonPanelTestView | Bassa | Code Smell |
| [CORE-005](./Core/ISSUES.md#core-005--buttonpanel-è-una-classe-mutabile-senza-validazione) | ButtonPanel è mutabile senza validazione | Bassa | Robustezza |
| [CORE-006](./Core/ISSUES.md#core-006--iprotocolrepository-non-è-async) | IProtocolRepository non è async | Bassa | Design |
| [CORE-007](./Core/ISSUES.md#core-007--deviceexception-non-estende-communicationexception) | DeviceException non estende CommunicationException | Bassa | Design |

### Infrastructure (5 issue aperte, 0 risolte)

| ID | Titolo | Priorità | Categoria |
|----|--------|----------|-----------|
| **[INFRA-001](./Infrastructure/ISSUES.md#infra-001--_recoverylock-usa-object-invece-di-lock)** | **_recoveryLock usa object invece di Lock** | **Alta** | **Anti-Pattern** |
| [INFRA-002](./Infrastructure/ISSUES.md#infra-002--tryrecoveryasync-fire-and-forget-con-underscore-discard) | TryRecoveryAsync fire-and-forget | Media | Code Smell |
| [INFRA-003](./Infrastructure/ISSUES.md#infra-003--tryaggressiverecoveryasync-usa-taskdelay-senza-cancellationtoken) | TryAggressiveRecoveryAsync senza CancellationToken | Media | Robustezza |
| [INFRA-004](./Infrastructure/ISSUES.md#infra-004--magic-numbers-sparsi-nel-file-pcanadapter) | Magic numbers in PcanAdapter | Bassa | Manutenibilità |
| [INFRA-005](./Infrastructure/ISSUES.md#infra-005--pcanapiwrapperread-logga-a-trace-ogni-messaggio-ricevuto) | PcanApiWrapper.Read logga troppo | Bassa | Performance |

### Data (6 issue aperte, 0 risolte)

| ID | Titolo | Priorità | Categoria |
|----|--------|----------|-----------|
| **[DATA-001](./Data/ISSUES.md#data-001--taskrungetawaitergetresult-blocca-il-thread)** | **Task.Run().GetAwaiter().GetResult() blocca thread** | **Alta** | **Anti-Pattern** |
| [DATA-002](./Data/ISSUES.md#data-002--excelstemprotocolrepository-è-duplicato-di-cachedexcelprotocolrepository) | ExcelStemProtocolRepository duplicato | Media | Code Smell |
| [DATA-003](./Data/ISSUES.md#data-003--_commandslock-e-_variableslock-usano-object-invece-di-lock) | _commandsLock usa object invece di Lock | Media | Anti-Pattern |
| [DATA-004](./Data/ISSUES.md#data-004--magic-number--7155632-per-colore-cella-excel) | Magic number -7155632 per colore Excel | Media | Manutenibilità |
| [DATA-005](./Data/ISSUES.md#data-005--catch-generico-senza-logging-in-preloadasync) | catch generico senza logging in PreloadAsync | Bassa | Robustezza |
| [DATA-006](./Data/ISSUES.md#data-006--getvalue-restituisce-array-mutabile) | GetValue restituisce array mutabile | Bassa | Robustezza |

### Communication (6 issue aperte, 0 risolte)

| ID | Titolo | Priorità | Categoria |
|----|--------|----------|-----------|
| **[COMM-001](./Communication/ISSUES.md#comm-001--networklayerreassembler_reassemblylock-usa-object-invece-di-lock)** | **_reassemblyLock usa object invece di Lock** | **Alta** | **Anti-Pattern** |
| [COMM-002](./Communication/ISSUES.md#comm-002--s_currentpacketid-è-static-ma-può-causare-conflitti-tra-istanze) | s_currentPacketId static può causare conflitti | Media | Design |
| [COMM-003](./Communication/ISSUES.md#comm-003--protocolexception-non-estende-communicationexception) | ProtocolException non estende CommunicationException | Media | Design |
| [COMM-004](./Communication/ISSUES.md#comm-004--cancommunicationmanageronnetworklayerdiagnosticmessage-ignora-i-messaggi) | OnNetworkLayerDiagnosticMessage ignora messaggi | Bassa | Observability |
| [COMM-005](./Communication/ISSUES.md#comm-005--layerdata-restituisce-array-mutabile) | Layer.Data restituisce array mutabile | Bassa | Robustezza |
| [COMM-006](./Communication/ISSUES.md#comm-006--readuint16littleendian-restituisce-0-se-buffer-insufficiente) | ReadUInt16LittleEndian restituisce 0 se buffer insufficiente | Bassa | Robustezza |

### Services (7 issue aperte, 0 risolte)

| ID | Titolo | Priorità | Categoria |
|----|--------|----------|-----------|
| **[SVC-001](./Services/ISSUES.md#svc-001--_heartbeatlock-e-_statelock-usano-object-invece-di-lock)** | **_heartbeatLock e _stateLock usano object** | **Alta** | **Anti-Pattern** |
| **[SVC-002](./Services/ISSUES.md#svc-002--taskrun-fire-and-forget-in-notifycommunicationlost)** | **Task.Run fire-and-forget in NotifyCommunicationLost** | **Alta** | **Anti-Pattern** |
| [SVC-003](./Services/ISSUES.md#svc-003--_protocolrepository-è-non-readonly-ma-non-dovrebbe-essere-modificato) | _protocolRepository non è readonly | Media | Code Smell |
| [SVC-004](./Services/ISSUES.md#svc-004--whoamiresponseuuid-è-mutabile) | WhoAmIResponse.Uuid è mutabile | Media | Robustezza |
| [SVC-005](./Services/ISSUES.md#svc-005--disposemanagerasync-è-statico-privato-e-mai-chiamato) | DisposeManagerAsync mai chiamato | Media | Code Smell |
| [SVC-006](./Services/ISSUES.md#svc-006--paneltypeconfiguration_configurations-è-pubblicamente-visibile-come-dictionary) | PanelTypeConfiguration._configurations è Dictionary mutabile | Bassa | Robustezza |
| [SVC-007](./Services/ISSUES.md#svc-007--mancanza-di-cancellationtoken-in-metodi-sincroni) | Mancanza CancellationToken in metodi sincroni | Bassa | Design |

### GUI.WinForms (7 issue aperte, 0 risolte)

| ID | Titolo | Priorità | Categoria |
|----|--------|----------|-----------|
| **[GUI-001](./GUI.WinForms/ISSUES.md#gui-001--async-void-nei-gestori-di-eventi-senza-try-catch-completo)** | **async void senza try-catch completo** | **Alta** | **Bug (Reliability)** |
| [GUI-002](./GUI.WinForms/ISSUES.md#gui-002--magic-color-values-hardcoded) | Magic color values hardcoded | Media | Manutenibilità |
| [GUI-003](./GUI.WinForms/ISSUES.md#gui-003--_buttonregions-duplica-configurazione-già-presente-in-buttonpanel) | _buttonRegions duplica configurazione | Media | Code Smell (DRY) |
| [GUI-004](./GUI.WinForms/ISSUES.md#gui-004--pinvoke-per-dll-management-senza-safehandle) | P/Invoke senza SafeHandle | Media | Resource Management |
| [GUI-005](./GUI.WinForms/ISSUES.md#gui-005--manca-dispose-pattern-nel-presenter) | Manca Dispose nel Presenter | Bassa | Resource Management |
| [GUI-006](./GUI.WinForms/ISSUES.md#gui-006--catch-vuoto-in-updateimage) | catch vuoto in UpdateImage | Bassa | Robustezza |
| [GUI-007](./GUI.WinForms/ISSUES.md#gui-007--_baptizects-dichiarato-ma-mai-usato) | _baptizeCts mai usato | Bassa | Code Smell |

### Tests (6 issue aperte, 0 risolte)

| ID | Titolo | Priorità | Categoria |
|----|--------|----------|-----------|
| [TEST-001](./Tests/ISSUES.md#test-001--mancanza-test-per-baptizeservice) | Mancanza test per BaptizeService | Media | Copertura |
| [TEST-002](./Tests/ISSUES.md#test-002--test-serviceshelpers-non-nella-cartella-unit) | Test Services/Helpers non in cartella Unit | Media | Struttura |
| [TEST-003](./Tests/ISSUES.md#test-003--excelvariablechecktests-usa-reflection-per-creare-repository-interno) | ExcelVariableCheckTests usa reflection | Bassa | Manutenibilità |
| [TEST-004](./Tests/ISSUES.md#test-004--magic-values-in-alcuni-test-senza-costanti) | Magic values nei test | Bassa | Manutenibilità |
| [TEST-005](./Tests/ISSUES.md#test-005--mancanza-test-per-buttonpanelteststatemachine) | Mancanza test per ButtonPanelTestStateMachine | Bassa | Copertura |
| [TEST-006](./Tests/ISSUES.md#test-006--alcuni-test-non-usano-pattern-aaa-esplicito) | Test non usano pattern AAA | Bassa | Consistenza |

---

## Issue da Risolvere (prossime)

| # | ID | Componente | Titolo | Effort |
|---|-----|------------|--------|--------|
| 1 | **T-001** | Trasversale | **Migrare lock da object a Lock** | **S** |
| 2 | GUI-001 | GUI.WinForms | async void senza try-catch | S |
| 3 | DATA-001 | Data | Task.Run().GetAwaiter().GetResult() | M |
| 4 | SVC-002 | Services | Task.Run fire-and-forget | S |
| 5 | TEST-001 | Tests | Mancanza test BaptizeService | M |

**Effort:** S = 1-2h, M = 4-8h, L = 1-2 giorni

---

## Copertura Test Attuale

| Componente | Unit | Integration | E2E | Note |
|------------|------|-------------|-----|------|
| Core/Models | ✅ | - | - | ButtonPanelTests |
| Core/EventArgs | ✅ | - | - | EventArgsTests |
| Infrastructure/PcanAdapter | ✅ | - | - | Mock IPcanApi |
| Data/ExcelRepository | ✅ | ✅ | - | File Excel di test |
| Communication/Layers | ✅ | ✅ | - | Protocol stack completo |
| Communication/Manager | ✅ | - | - | Mock ICanAdapter |
| Services/Communication | ✅ | ✅ | - | Simulated manager |
| Services/ButtonPanelTest | ✅ | ✅ | ✅ | Workflow completi |
| Services/Helpers | ✅ | - | - | PayloadBuilder, ResponseParser |

**Totale test:** ~200+ (stima)

---

## Metriche Qualità

| Aspetto | Stato | Note |
|---------|-------|------|
| **Architecture** | ✅ 95% | Clean Architecture rispettata |
| **Thread Safety** | ⚠️ 80% | T-001 (lock pattern) da risolvere |
| **Input Validation** | ✅ 90% | ArgumentNullException ovunque |
| **Error Handling** | ⚠️ 85% | GUI-001 (async void) da risolvere |
| **Performance** | ✅ 90% | Caching Excel, recovery CAN |
| **Resilience** | ✅ 90% | Heartbeat, auto-recovery |
| **Code Consistency** | ⚠️ 85% | Magic numbers, duplicazioni |
| **Test Coverage** | ⚠️ 80% | TEST-001, TEST-005 da risolvere |

---

## Issue per Categoria

| Categoria | Count | Issue |
|-----------|-------|-------|
| **Anti-Pattern** | 6 | T-001, INFRA-001, DATA-001, DATA-003, COMM-001, SVC-001, SVC-002 |
| **Code Smell** | 6 | CORE-001, CORE-004, DATA-002, GUI-003, GUI-007, SVC-003 |
| **Design** | 7 | CORE-002, CORE-003, CORE-006, CORE-007, COMM-002, COMM-003, SVC-007 |
| **Robustezza** | 7 | CORE-005, INFRA-003, DATA-005, DATA-006, COMM-005, COMM-006, SVC-004, SVC-006, GUI-006 |
| **Manutenibilità** | 4 | INFRA-004, DATA-004, GUI-002, TEST-003, TEST-004 |
| **Resource Management** | 2 | GUI-004, GUI-005 |
| **Bug** | 1 | GUI-001 |
| **Copertura** | 2 | TEST-001, TEST-005 |
| **Struttura** | 1 | TEST-002 |
| **Consistenza** | 1 | TEST-006 |
| **Observability** | 1 | COMM-004 |
| **Performance** | 1 | INFRA-005 |

---

## Come Contribuire

1. Seleziona issue da priorità alta o "Issue da Risolvere"
2. Crea branch: `fix/{issue-id}` (es. `fix/t-001-lock-migration`)
3. Implementa soluzione proposta nel file ISSUES.md del componente
4. Aggiungi test se applicabile
5. Aggiorna status issue a "Risolto" con data e branch
6. Pull Request verso `main`

---

## Links

- [ISSUES_TEMPLATE.md](./Docs/Standards/Templates/ISSUES_TEMPLATE.md) - Template per nuove issue
- [copilot-instructions.md](.copilot/copilot-instructions.md) - Istruzioni progetto
- [issues-agent.md](.copilot/agents/issues-agent.md) - Agent per analisi issue

---

## Changelog

| Data | Modifica |
|------|----------|
| 2026-04-03 | 🔍 **Audit completo tutti i componenti** — Creati 7 file ISSUES.md per Core, Infrastructure, Data, Communication, Services, GUI.WinForms, Tests. Identificate 45 issue (7 alta, 18 media, 20 bassa). T-001 (lock migration) aperta come issue trasversale. |
