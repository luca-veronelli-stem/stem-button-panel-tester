# Tests

> **Suite completa di test per Stem.ButtonPanel.Tester. Include unit, integration ed E2E test con mock, fixture e helpers.**  
> **Ultimo aggiornamento:** 2026-04-03

---

## Panoramica

**Tests** contiene la suite completa di test per tutti i componenti del progetto. La struttura è organizzata per **tipo di test** e **componente**, con:

- **Unit Tests** — Test puri senza dipendenze esterne (~70% della suite)
- **Integration Tests** — Test con componenti reali ma hardware mockato (~20%)
- **End-to-End Tests** — Workflow completi con tutti i layer (~10%)
- **Test Helpers** — Fixture, builders, assertions custom
- **Categorizzazione** — `[Trait("Category", "Unit/Integration/EndToEnd")]`

Tutti i test sono eseguibili su **CI Linux** senza hardware fisico grazie a mock di `ICanAdapter` e `IPcanApi`.

---

## Caratteristiche

| Feature | Stato | Descrizione |
|---------|-------|-------------|
| **Unit Tests** | ✅ | ~150+ test per Core, Protocol, Services |
| **Integration Tests** | ✅ | ~30+ test con Excel reale, stack completo |
| **E2E Tests** | ✅ | ~10+ test workflow ButtonPanelTestService |
| **Test Helpers** | ✅ | Builders, assertions, harness |
| **CI-Ready** | ✅ | Eseguibili su Linux senza hardware |
| **Categorizzazione** | ✅ | `Unit`, `Integration`, `EndToEnd`, `RequiresHardware` |

---

## Requisiti

- **.NET 10.0** o superiore
- **File Excel** `StemDictionaries.xlsx` in `Resources/`

### Dipendenze

| Package | Versione | Uso |
|---------|----------|-----|
| `xunit` | 2.9.3 | Framework test |
| `xunit.runner.visualstudio` | 3.1.5 | Runner Visual Studio |
| `Microsoft.NET.Test.Sdk` | 18.0.1 | SDK test .NET |
| `Moq` | 4.20.72 | Mock per dipendenze |
| `coverlet.collector` | 6.0.4 | Code coverage |
| `ClosedXML` | 0.105.0 | Test Excel repository |
| `Peak.PCANBasic.NET` | 4.10.1.968 | Test PcanAdapter (mock) |

---

## Quick Start

### Esegui tutti i test

```bash
dotnet test
```

### Esegui solo Unit Test

```bash
dotnet test --filter "Category=Unit"
```

### Esegui test specifico componente

```bash
# Communication tests
dotnet test --filter "FullyQualifiedName~Communication"

# Services tests
dotnet test --filter "FullyQualifiedName~Services"
```

### Con code coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

## Struttura

```
Tests/
├── Unit/                          # Test unitari puri (no I/O)
│   ├── Core/
│   │   └── Models/                # ButtonPanel, EventArgs, NetworkPacketChunk
│   ├── Infrastructure/
│   │   └── PcanAdapterTests.cs   # Mock IPcanApi
│   ├── Data/
│   │   ├── ExcelRepositoryTests.cs
│   │   └── ExcelStemProtocolRepositoryTests.cs
│   ├── Communication/
│   │   ├── Protocol/
│   │   │   ├── ApplicationLayerTests.cs
│   │   │   ├── TransportLayerTests.cs
│   │   │   ├── NetworkLayerTests.cs
│   │   │   ├── NetworkLayerReassemblerTests.cs
│   │   │   └── Lib/               # ProtocolHelpers, NetInfo, Config
│   │   ├── BaseCommunicationManagerTests.cs
│   │   ├── CanCommunicationManagerTests.cs
│   │   └── StemProtocolManagerTests.cs
│   └── Services/
│       ├── ButtonPanelTestServiceTests.cs
│       ├── CommunicationServiceTests.cs
│       └── Lib/
├── Integration/                   # Test con componenti reali
│   ├── Data/
│   │   ├── ExcelStemProtocolRepositoryIntegrationTests.cs
│   │   └── ExcelVariableCheckTests.cs
│   ├── Communication/
│   │   ├── CanCommunicationTests.cs
│   │   └── StemProtocolManagerIntegrationTests.cs
│   └── Services/
│       └── ButtonPanelTestServiceIntegrationTests.cs
├── EndToEnd/                      # Test workflow completi
│   └── Services/
│       └── ButtonPanelTestServiceE2ETests.cs
├── Helpers/                       # Utilities test
│   ├── ProtocolTestBuilders.cs   # Factory per fixture
│   ├── ProtocolAssertions.cs     # Assertions custom
│   ├── ByteArrayHelpers.cs       # Comparatore, extensions
│   └── ProtocolManagerTestHarness.cs
├── Services/                      # Test helpers Services (da riorganizzare - TEST-002)
│   ├── Helpers/
│   │   ├── PayloadBuilderTests.cs
│   │   ├── ResponseParserTests.cs
│   │   └── StemAddressHelperTests.cs
│   └── Models/
│       └── PanelTypeConfigurationTests.cs
├── Resources/
│   └── StemDictionaries.xlsx     # File Excel per test
└── TestCategories.cs              # Costanti categorie
```

---

## Categorizzazione Test

### TestCategories

```csharp
[Trait("Category", TestCategories.Unit)]
public class MyUnitTest { }

[Trait("Category", TestCategories.Integration)]
public class MyIntegrationTest { }

[Trait("Category", TestCategories.EndToEnd)]
public class MyE2ETest { }
```

| Categoria | Descrizione | Eseguibili su CI Linux |
|-----------|-------------|------------------------|
| `Unit` | Test puri, nessuna dipendenza esterna | ✅ Sì |
| `Integration` | Componenti reali, hardware mockato | ✅ Sì |
| `EndToEnd` | Workflow completi, stack completo | ✅ Sì |
| `RequiresHardware` | Serve PCAN fisico collegato | ❌ No |
| `RequiresWindows` | API Windows-only (WinForms) | ❌ No su Linux |

---

## Test Helpers

### ProtocolTestBuilders

Factory per creare fixture protocollari:

```csharp
// Application Layer
var appLayer = ProtocolTestBuilders.CreateApplicationLayer(
    command: 0x0002,
    payload: [0x04, 0x01]);

// Transport Layer
var transportLayer = ProtocolTestBuilders.CreateTransportLayer(
    senderId: 0x00030141,
    applicationPacket: appLayer.ApplicationPacket);

// Transport packet corrotto
var corruptedPacket = ProtocolTestBuilders.CreateCorruptedTransportPacket(
    corruptCrc: true);
```

### ProtocolAssertions

Assertions custom con messaggi chiari:

```csharp
// Confronto byte array con dettagli
ProtocolAssertions.AssertBytesEqual(expected, actual, "NetInfo mismatch");

// Validazione header field
ProtocolAssertions.AssertHeaderField(packet, offset: 0, expectedValue: 0x00, "CryptFlag");

// Validazione uint big-endian
ProtocolAssertions.AssertBigEndianUInt32(packet, offset: 1, expectedValue: 0x00030141, "SenderId");
```

### ByteArrayHelpers

```csharp
// Comparatore per collection assertions
var comparer = ByteArrayComparer.Instance;
Assert.Equal(expected, actual, comparer);

// Extensions
var corrupted = original.WithCorruptedCrc();
var flipped = original.WithBitFlip(byteIndex: 5, mask: 0x01);
var slice = original.Slice(start: 2, length: 4);
```

---

## Esempi Test

### Unit Test — Protocol Layer

```csharp
[Fact]
[Trait("Category", TestCategories.Unit)]
public void ApplicationLayer_Create_BuildsCorrectPacket()
{
    // Arrange
    ushort command = 0x0002;
    byte[] payload = [0x04, 0x01];
    
    // Act
    var layer = ApplicationLayer.Create(command, payload);
    
    // Assert
    Assert.Equal(0x00, layer.CmdInit);
    Assert.Equal(0x02, layer.CmdOpt);
    Assert.Equal(4, layer.ApplicationPacket.Length); // 2 header + 2 payload
}
```

### Integration Test — Excel Repository

```csharp
[Fact]
[Trait("Category", TestCategories.Integration)]
public async Task GetCommand_WithRealExcel_ReturnsCorrectId()
{
    // Arrange
    var repo = new ExcelRepository();
    var filePath = Path.Combine("Resources", "StemDictionaries.xlsx");
    var factory = new ExcelProtocolRepositoryFactory(repo, filePath);
    await factory.PreloadAsync(0x00030101);
    var protocolRepo = factory.Create(0x00030101);
    
    // Act
    ushort commandId = protocolRepo.GetCommand("Scrivi variabile logica");
    
    // Assert
    Assert.Equal(0x0002, commandId);
}
```

### E2E Test — ButtonPanelTestService

```csharp
[Fact]
[Trait("Category", TestCategories.EndToEnd)]
public async Task TestAllAsync_CompleteWorkflow_ExecutesAllPhases()
{
    // Arrange: Setup completo con mock adapter, real protocol manager, real services
    var mockAdapter = new Mock<ICanAdapter>();
    ConfigureMockAdapter(mockAdapter);
    
    var canManager = new CanCommunicationManager(mockAdapter.Object);
    var protocolManager = new StemProtocolManager();
    var communicationService = new CommunicationService(protocolManager, factory);
    var service = new ButtonPanelTestService(communicationService, baptizeService, protocolRepo);
    
    // Act: Esegui workflow completo
    var results = await service.TestAllAsync(
        ButtonPanelType.DIS0023789,
        userConfirm: _ => Task.FromResult(true),
        userPrompt: _ => Task.CompletedTask);
    
    // Assert: Verifica risultati
    Assert.NotEmpty(results);
    Assert.All(results, r => Assert.True(r.Passed));
}
```

---

## Mock Patterns

### Mock ICanAdapter

```csharp
var mockAdapter = new Mock<ICanAdapter>();
mockAdapter.Setup(x => x.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(true);
mockAdapter.Setup(x => x.Send(It.IsAny<uint>(), It.IsAny<byte[]>(), true))
           .ReturnsAsync(true);
mockAdapter.SetupGet(x => x.IsConnected).Returns(true);
```

### Mock IPcanApi

```csharp
var mockApi = new Mock<IPcanApi>();
mockApi.Setup(x => x.Initialize(It.IsAny<PcanChannel>(), It.IsAny<Bitrate>()))
       .Returns(PcanStatus.OK);
mockApi.Setup(x => x.Read(It.IsAny<PcanChannel>(), out It.Ref<PcanMessage>.IsAny, out It.Ref<ulong>.IsAny))
       .Returns(PcanStatus.ReceiveQueueEmpty);
```

### Mock IProtocolRepository

```csharp
var mockRepo = new Mock<IProtocolRepository>();
mockRepo.Setup(x => x.GetCommand("Scrivi variabile logica")).Returns(0x0002);
mockRepo.Setup(x => x.GetVariable("Comando Led Verde")).Returns(0x0401);
mockRepo.Setup(x => x.GetValue("ON")).Returns(new byte[] { 0x00, 0x00, 0x00, 0x80 });
```

---

## Esecuzione Test

### Visual Studio

- **Test Explorer** → Esegui tutti / Filtra per categoria
- **Run All Tests** → `Ctrl+R, A`
- **Debug Test** → Click destro → Debug

### CLI

```bash
# Tutti i test
dotnet test

# Solo Unit
dotnet test --filter "Category=Unit"

# Solo Integration
dotnet test --filter "Category=Integration"

# Componente specifico
dotnet test --filter "FullyQualifiedName~Communication"

# Test specifico
dotnet test --filter "FullyQualifiedName~ApplicationLayerTests.Create_BuildsCorrectPacket"

# Con verbosità
dotnet test --logger "console;verbosity=detailed"

# Con coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

---

## Copertura

### Obiettivi

| Componente | Obiettivo | Attuale (stima) |
|------------|-----------|-----------------|
| Core | 95%+ | ~90% |
| Infrastructure | 80%+ | ~85% |
| Data | 90%+ | ~85% |
| Communication | 90%+ | ~95% |
| Services | 85%+ | ~80% |

### Gaps (TEST-001, TEST-005)

- `BaptizeService` — Mancano unit test dedicati
- `ButtonPanelTestStateMachine` — Mancano test FSM isolata

---

## Configurazione

### Resources

Il file `StemDictionaries.xlsx` deve essere presente in `Tests/Resources/` e copiato nella directory di output:

```xml
<Content Include="Resources\StemDictionaries.xlsx">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</Content>
```

### CI/CD

```yaml
# Azure Pipelines / GitHub Actions
- task: DotNetCoreCLI@2
  inputs:
    command: 'test'
    arguments: '--filter "Category!=RequiresHardware&Category!=RequiresWindows"'
    testRunTitle: 'Unit and Integration Tests'
```

---

## Issue Correlate

→ [Tests/ISSUES.md](./ISSUES.md)

**Issue Principali:**
- `TEST-001` — Mancanza test per BaptizeService (Media)
- `TEST-002` — Test Services/Helpers non in cartella Unit (Media)
- `TEST-005` — Mancanza test per ButtonPanelTestStateMachine (Bassa)

---

## Links

- [ISSUES_TRACKER.md](../ISSUES_TRACKER.md) — Tracker globale issue
- [Core/README.md](../Core/README.md) — Modelli testati
- [Communication/README.md](../Communication/README.md) — Stack protocollare testato
- [Services/README.md](../Services/README.md) — Servizi testati
- [xUnit Documentation](https://xunit.net/) — Framework test
- [Moq Documentation](https://github.com/moq/moq4) — Libreria mock
