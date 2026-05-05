# Core - ISSUES

> **Scopo:** Questo documento traccia bug, code smells, performance issues, opportunità di refactoring e violazioni di best practice per il componente **Core**.

> **Ultimo aggiornamento:** 2026-04-03

---

## Riepilogo

| Priorità | Aperte | Risolte |
|----------|--------|---------|
| **Critica** | 0 | 0 |
| **Alta** | 0 | 0 |
| **Media** | 3 | 0 |
| **Bassa** | 4 | 0 |

**Totale aperte:** 7
**Totale risolte:** 0

---

## Indice Issue Aperte

- [CORE-001 - ButtonPanel.GetByType duplica configurazione per 3 tipi su 4](#core-001--buttonpanelgetbytype-duplica-configurazione-per-3-tipi-su-4)
- [CORE-002 - ButtonIndicator dipende da System.Drawing (WinForms)](#core-002--buttonindicator-dipende-da-systemdrawing-winforms)
- [CORE-003 - IButtonPanelTestView è un contratto WinForms nel layer Core](#core-003--ibuttonpaneltestview-è-un-contratto-winforms-nel-layer-core)
- [CORE-004 - BaptizeStatus enum annidato in IButtonPanelTestView](#core-004--baptizestatus-enum-annidato-in-ibuttonpaneltestview)
- [CORE-005 - ButtonPanel è una classe mutabile senza validazione](#core-005--buttonpanel-è-una-classe-mutabile-senza-validazione)
- [CORE-006 - IProtocolRepository non è async](#core-006--iprotocolrepository-non-è-async)
- [CORE-007 - DeviceException non estende CommunicationException](#core-007--deviceexception-non-estende-communicationexception)

---

## Priorità Media

### CORE-001 - ButtonPanel.GetByType duplica configurazione per 3 tipi su 4

**Categoria:** Code Smell
**Priorità:** Media
**Impatto:** Medio
**Status:** Aperto
**Data Apertura:** 2026-04-01

#### Descrizione

Il metodo factory `ButtonPanel.GetByType` contiene blocchi quasi identici per DIS0026166, DIS0026182 e il ramo default (Eden). Stesso `ButtonCount=8`, `HasLed=true`, stesse `ButtonMasks`. Solo il branch DIS0025205 (Optimus, 4 pulsanti) è strutturalmente diverso.

#### File Coinvolti

- `Core/Models/Services/ButtonPanel.cs` (righe 28-73)

#### Codice Problematico

```csharp
ButtonPanelType.DIS0026166 => new ButtonPanel
{
    Type = type, ButtonCount = 8, HasLed = true,
    Buttons = GetButtonsByType(type),
    ButtonMasks = [0x40, 0x04, 0x08, 0x10, 0x80, 0x02, 0x01, 0x20],
    ButtonStatusVariableIds = [0x8000, 0x803E, 0x80FE]
},
ButtonPanelType.DIS0026182 => new ButtonPanel
{
    // ... identico al precedente
},
_ => new ButtonPanel
{
    // ... identico al precedente
}
```

#### Soluzione Proposta

Estrarre la configurazione 8-pulsanti come metodo privato e ridurre il `switch` a 2 branch:

```csharp
return type switch
{
    ButtonPanelType.DIS0025205 => CreateOptimusPanel(type),
    _ => CreateEightButtonPanel(type)
};
```

#### Benefici Attesi

- Eliminazione duplicazione (~30 righe)
- Un solo punto da modificare se cambiano le maschere comuni

---

### CORE-002 - ButtonIndicator dipende da System.Drawing (WinForms)

**Categoria:** Design
**Priorità:** Media
**Impatto:** Medio
**Status:** Aperto
**Data Apertura:** 2026-04-01

#### Descrizione

`ButtonIndicator` usa `System.Drawing.RectangleF`, tipo specifico di WinForms. Core non dovrebbe avere dipendenze GUI. Questo impedisce la pulizia del progetto quando WinForms verrà rimosso.

#### File Coinvolti

- `Core/Models/ButtonIndicator.cs` (riga 3, 9)

#### Codice Problematico

```csharp
using System.Drawing;

public class ButtonIndicator
{
    public RectangleF Bounds { get; set; }
    public IndicatorState State { get; set; } = IndicatorState.Idle;
}
```

#### Soluzione Proposta

**Opzione A:** Sostituire `RectangleF` con 4 proprietà float (X, Y, Width, Height) o un record custom.

**Opzione B:** Rimuovere `ButtonIndicator` da Core e spostarlo nel progetto GUI.WinForms (unico consumatore attuale). Quando si implementerà WPF, la vista WPF avrà il suo modello.

#### Benefici Attesi

- Core rimane framework-agnostic
- Nessuna dipendenza System.Drawing nel dominio puro

---

### CORE-003 - IButtonPanelTestView è un contratto WinForms nel layer Core

**Categoria:** Design
**Priorità:** Media
**Impatto:** Medio
**Status:** Aperto
**Data Apertura:** 2026-04-01

#### Descrizione

`IButtonPanelTestView` in `Core/Interfaces/GUI/` è un'interfaccia vista per il pattern MVP (WinForms). Contiene riferimenti a `System.Drawing.Color`, dialog modali (`ShowSaveNewFileDialog`), e logica specifica WinForms. Non sarà usata dalla GUI WPF (che userà MVVM).

#### File Coinvolti

- `Core/Interfaces/GUI/IButtonPanelTestView.cs` (intero file, 131 righe)

#### Problema Specifico

- `System.Drawing.Color` usato come parametro (riga 83)
- Metodi come `ShowSaveNewFileDialog()`, `ShowOpenExistingFileDialog()` sono pattern WinForms
- `BaptizeStatus` enum annidato nel file dell'interfaccia (vedi CORE-004)
- Quando GUI.WinForms verrà rimosso, questa interfaccia resterà orfana

#### Soluzione Proposta

Spostare `IButtonPanelTestView` nel progetto GUI.WinForms. Core non deve conoscere i dettagli della vista. Il presenter WinForms e la vista WinForms vivranno insieme. La GUI WPF userà ViewModels propri senza questa interfaccia.

#### Benefici Attesi

- Core rimane framework-agnostic
- Nessun codice orfano dopo la migrazione WPF

---

## Priorità Bassa

### CORE-004 - BaptizeStatus enum annidato in IButtonPanelTestView

**Categoria:** Code Smell
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-01

#### Descrizione

L'enum `BaptizeStatus` (None, InProgress, Success, Failed, Cancelled) è definito in coda al file `IButtonPanelTestView.cs`. È un concetto di dominio (stato del battezzamento) ma è annidato in un'interfaccia GUI.

#### File Coinvolti

- `Core/Interfaces/GUI/IButtonPanelTestView.cs` (righe 104-130)

#### Soluzione Proposta

Se `BaptizeStatus` serve anche fuori dalla vista WinForms (es. WPF, Services), spostarlo in `Core/Enums/`. Altrimenti, seguirà `IButtonPanelTestView` nella migrazione (vedi CORE-003).

#### Benefici Attesi

- Organizzazione coerente degli enum del dominio
- Riusabilità del concetto "stato battezzamento"

---

### CORE-005 - ButtonPanel è una classe mutabile senza validazione

**Categoria:** Robustezza
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-01

#### Descrizione

`ButtonPanel` è una classe con tutti i setter pubblici e nessuna validazione. Permette stati incoerenti (es. `ButtonCount = 4` con 8 maschere in `ButtonMasks`). Il factory method `GetByType` crea configurazioni valide, ma nulla impedisce di costruire una `ButtonPanel` con dati incoerenti dall'esterno.

#### File Coinvolti

- `Core/Models/Services/ButtonPanel.cs` (righe 8-25)

#### Codice Problematico

```csharp
public class ButtonPanel
{
    public ButtonPanelType Type { get; set; }
    public int ButtonCount { get; set; }
    public bool HasLed { get; set; }
    public bool HasBuzzer { get; set; } = true;
    public string[] Buttons { get; set; } = [];
    public List<byte> ButtonMasks { get; set; } = [];
    public ushort[] ButtonStatusVariableIds { get; set; } = [0x8000, 0x803E, 0x80FE];
}
```

#### Soluzione Proposta

Rendere `ButtonPanel` immutabile (solo `init` setter o costruttore con validazione). Non critico perché l'unico entry point è il factory method, ma migliora la robustezza.

#### Benefici Attesi

- Impossibilità di creare stati incoerenti
- Allineamento con pattern immutabili usati altrove (record types)

---

### CORE-006 - IProtocolRepository non è async

**Categoria:** Design
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-01

#### Descrizione

`IProtocolRepository` espone 3 metodi sincroni (`GetCommand`, `GetVariable`, `GetValue`). I repository dati nel progetto Data sono async (Excel I/O). La discrepanza indica che l'implementazione probabilmente usa cache in-memory, ma il contratto non è allineato con le altre interfacce.

#### File Coinvolti

- `Core/Interfaces/Data/IProtocolRepository.cs` (righe 3-8)

#### Codice Problematico

```csharp
public interface IProtocolRepository
{
    ushort GetCommand(string commandName);
    ushort GetVariable(string variableName);
    byte[] GetValue(string valueName);
}
```

#### Soluzione Proposta

Valutare se trasformare in async oppure lasciare sincrono con documentazione esplicita che è un repository in-memory (pre-caricato). In questo caso, aggiungere commento XML che chiarisce il contratto.

#### Benefici Attesi

- Coerenza contratti o documentazione esplicita della scelta

---

### CORE-007 - DeviceException non estende CommunicationException

**Categoria:** Design
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-01

#### Descrizione

`DeviceException` estende `Exception` direttamente invece di `CommunicationException`, pur condividendo lo stesso pattern (`ErrorCode`, `ToError()`). Questo impedisce di catturare tutte le eccezioni di comunicazione con un singolo `catch (CommunicationException)`.

#### File Coinvolti

- `Core/Exceptions/CommunicationExceptions.cs` (righe 150-174)

#### Codice Problematico

```csharp
public class DeviceException : Exception  // <-- non estende CommunicationException
{
    public string ErrorCode { get; }
    // ... stessa struttura di CommunicationException
}
```

#### Soluzione Proposta

Far estendere `CommunicationException` a `DeviceException`. L'`ErrorCode` è già nella base, bastano i costruttori:

```csharp
public class DeviceException : CommunicationException
{
    public DeviceException(string errorCode, string message)
        : base(errorCode, message) { }

    public DeviceException(string errorCode, string message, Exception innerException)
        : base(errorCode, message, innerException) { }
}
```

#### Benefici Attesi

- `catch (CommunicationException)` cattura anche errori device
- Eliminazione duplicazione `ErrorCode` + `ToError()`

---

## Issue Risolte

(Nessuna issue risolta finora)
