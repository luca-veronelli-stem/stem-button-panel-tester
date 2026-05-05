# Tests - ISSUES

> **Scopo:** Questo documento traccia bug, code smells, performance issues, opportunità di refactoring e violazioni di best practice per il componente **Tests**.

> **Ultimo aggiornamento:** 2026-04-03

---

## Riepilogo

| Priorità | Aperte | Risolte |
|----------|--------|---------|
| **Critica** | 0 | 0 |
| **Alta** | 0 | 0 |
| **Media** | 2 | 0 |
| **Bassa** | 4 | 0 |

**Totale aperte:** 6
**Totale risolte:** 0

---

## Indice Issue Aperte

- [TEST-001 - Mancanza test per BaptizeService](#test-001--mancanza-test-per-baptizeservice)
- [TEST-002 - Test Services/Helpers non nella cartella Unit](#test-002--test-serviceshelpers-non-nella-cartella-unit)
- [TEST-003 - ExcelVariableCheckTests usa reflection per creare repository interno](#test-003--excelvariablechecktests-usa-reflection-per-creare-repository-interno)
- [TEST-004 - Magic values in alcuni test senza costanti](#test-004--magic-values-in-alcuni-test-senza-costanti)
- [TEST-005 - Mancanza test per ButtonPanelTestStateMachine](#test-005--mancanza-test-per-buttonpanelteststatemachine)
- [TEST-006 - Alcuni test non usano pattern AAA esplicito](#test-006--alcuni-test-non-usano-pattern-aaa-esplicito)

---

## Priorità Media

### TEST-001 - Mancanza test per BaptizeService

**Categoria:** Copertura
**Priorità:** Media
**Impatto:** Medio
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Non esistono test unitari dedicati per `BaptizeService`. Il servizio è testato solo indirettamente tramite mock nei test di `ButtonPanelTestService`. Dato che il battezzamento è una funzionalità critica del progetto, dovrebbe avere test unitari dedicati.

#### File Coinvolti

- `Services/BaptizeService.cs` — non ha test unitari
- `Tests/Unit/Services/` — manca `BaptizeServiceTests.cs`

#### Scenari da Testare

- `BaptizeAsync` con risposta WHO_AM_I valida
- `BaptizeAsync` con timeout (nessuna risposta)
- `BaptizeAsync` con cancellazione
- `ReassignAddressAsync` con `forceLastByteToFF = true`
- Calcolo indirizzo STEM corretto
- Gestione errori di connessione CAN

#### Soluzione Proposta

Creare `Tests/Unit/Services/BaptizeServiceTests.cs` con mock di `ICommunicationService`.

#### Benefici Attesi

- Copertura della funzionalità critica di battezzamento
- Regressioni individuate prima dell'integrazione

---

### TEST-002 - Test Services/Helpers non nella cartella Unit

**Categoria:** Struttura
**Priorità:** Media
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

I test per `PayloadBuilder`, `ResponseParser`, e `StemAddressHelper` si trovano in `Tests/Services/Helpers/` invece che in `Tests/Unit/Services/Helpers/`. Questo viola la convenzione di cartelle del progetto dove i test unitari vanno in `Unit/`.

#### File Coinvolti

- `Tests/Services/Helpers/PayloadBuilderTests.cs`
- `Tests/Services/Helpers/ResponseParserTests.cs`
- `Tests/Services/Helpers/StemAddressHelperTests.cs`
- `Tests/Services/Models/PanelTypeConfigurationTests.cs`

#### Soluzione Proposta

Spostare i file nella posizione corretta:

```
Tests/Services/Helpers/PayloadBuilderTests.cs → Tests/Unit/Services/Helpers/PayloadBuilderTests.cs
Tests/Services/Helpers/ResponseParserTests.cs → Tests/Unit/Services/Helpers/ResponseParserTests.cs
Tests/Services/Helpers/StemAddressHelperTests.cs → Tests/Unit/Services/Helpers/StemAddressHelperTests.cs
Tests/Services/Models/PanelTypeConfigurationTests.cs → Tests/Unit/Services/Models/PanelTypeConfigurationTests.cs
```

#### Benefici Attesi

- Struttura cartelle consistente
- Navigabilità migliorata

---

## Priorità Bassa

### TEST-003 - ExcelVariableCheckTests usa reflection per creare repository interno

**Categoria:** Manutenibilità
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

`ExcelVariableCheckTests` usa reflection per istanziare `ExcelStemProtocolRepository` che è una classe `internal`. Questo rende il test fragile perché dipende dal nome della classe.

#### File Coinvolti

- `Tests/Integration/Data/ExcelVariableCheckTests.cs` (righe 73-86)

#### Codice Problematico

```csharp
private static IProtocolRepository CreateRepository(...)
{
    var type = typeof(ExcelProtocolRepositoryFactory).Assembly
        .GetType("Data.ExcelStemProtocolRepository");  // <-- stringa fragile

    return (IProtocolRepository)Activator.CreateInstance(
        type!, excelRepository, filePath, recipientId)!;
}
```

#### Soluzione Proposta

Usare la factory esistente invece di reflection:

```csharp
private static IProtocolRepository CreateRepository(
    IExcelRepository excelRepository, string filePath, uint recipientId)
{
    var factory = new ExcelProtocolRepositoryFactory(excelRepository, filePath);
    return factory.Create(recipientId);
}
```

#### Benefici Attesi

- Test meno fragile
- Nessuna dipendenza da nomi di tipi interni

---

### TEST-004 - Magic values in alcuni test senza costanti

**Categoria:** Manutenibilità
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Alcuni test usano valori magici inline invece di costanti con nomi descrittivi. Ad esempio `0x12345678` per senderId, `0x01, 0x02, 0x03` per payload.

#### File Coinvolti

- `Tests/Helpers/ProtocolTestBuilders.cs` (senderId = 0x12345678)
- Vari test con payload hardcoded

#### Codice Problematico

```csharp
public static TransportLayer CreateTransportLayer(
    CryptType cryptFlag = CryptType.None,
    uint senderId = 0x12345678,  // <-- magic value
    byte[]? applicationPacket = null)
```

#### Soluzione Proposta

Estrarre costanti in `TestConstants.cs`:

```csharp
public static class TestConstants
{
    public const uint DefaultSenderId = 0x12345678;
    public const uint DefaultRecipientId = 0x00030101;
    public static readonly byte[] SamplePayload = [0x01, 0x02, 0x03];
}
```

#### Benefici Attesi

- Test più leggibili
- Valori riutilizzabili

---

### TEST-005 - Mancanza test per ButtonPanelTestStateMachine

**Categoria:** Copertura
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

La macchina a stati `ButtonPanelTestStateMachine` non ha test unitari dedicati. È testata solo indirettamente tramite `ButtonPanelTestService`. Dato che la FSM ha logica di transizione specifica, dovrebbe avere test isolati.

#### File Coinvolti

- `Services/Lib/ButtonPanelTestStateMachine.cs` — non ha test unitari
- `Tests/Unit/Services/Lib/` — manca `ButtonPanelTestStateMachineTests.cs`

#### Scenari da Testare

- Transizioni valide: Idle → Initializing → AwaitingButtonPress → ...
- Transizioni invalide: tentativi di transizione non permessi
- `StartTest` quando già in esecuzione
- `Cancel()` da vari stati
- `Reset()` dopo completamento/errore

#### Soluzione Proposta

Creare `Tests/Unit/Services/Lib/ButtonPanelTestStateMachineTests.cs`.

#### Benefici Attesi

- FSM testata in isolamento
- Transizioni verificate senza dipendenze esterne

---

### TEST-006 - Alcuni test non usano pattern AAA esplicito

**Categoria:** Consistenza
**Priorità:** Bassa
**Impatto:** Nullo
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

La maggior parte dei test segue il pattern AAA (Arrange-Act-Assert) con commenti espliciti, ma alcuni test più semplici non li hanno. Per consistenza, tutti i test dovrebbero usare il pattern.

#### File Coinvolti

- Vari file test con test semplici senza commenti AAA

#### Esempio

```csharp
// Senza AAA:
[Fact]
public void Constructor_Throws_When_CommunicationService_Null()
{
    ICommunicationService nullCommunicationService = null!;
    var exception = Assert.Throws<ArgumentNullException>(() => ...);
    Assert.Equal("communicationService", exception.ParamName);
}

// Con AAA:
[Fact]
public void Constructor_Throws_When_CommunicationService_Null()
{
    // Arrange
    ICommunicationService nullCommunicationService = null!;

    // Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() => ...);
    Assert.Equal("communicationService", exception.ParamName);
}
```

#### Soluzione Proposta

Aggiungere commenti `// Arrange`, `// Act`, `// Assert` (o `// Act & Assert`) a tutti i test per consistenza.

#### Benefici Attesi

- Consistenza stilistica
- Leggibilità migliorata

---

## Note Positive

### ✓ Struttura ben organizzata

- `Tests/Unit/` per test unitari
- `Tests/Integration/` per test di integrazione
- `Tests/EndToEnd/` per test E2E
- `Tests/Helpers/` per utilities condivise

### ✓ Categorie test ben definite

`TestCategories.cs` definisce categorie chiare:
- `Unit`, `Integration`, `EndToEnd`
- `RequiresHardware`, `RequiresWindows`

### ✓ Test helpers di qualità

- `ProtocolTestBuilders` — factory fluent per fixtures
- `ProtocolAssertions` — asserzioni custom con messaggi chiari
- `ByteArrayHelpers` — utilities per manipolazione byte
- `ProtocolManagerTestHarness` — harness per test protocollo

### ✓ Buona copertura dei layer protocollari

- `ApplicationLayerTests`, `TransportLayerTests`, `NetworkLayerTests`
- `NetworkLayerReassemblerTests`
- `StemProtocolManagerTests`

### ✓ Test E2E realistici

I test E2E usano componenti reali e mockano solo hardware, simulando workflow completi.

---

## Issue Risolte

(Nessuna issue risolta finora)
