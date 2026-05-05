# GUI.WinForms - ISSUES

> **Scopo:** Questo documento traccia bug, code smells, performance issues, opportunità di refactoring e violazioni di best practice per il componente **GUI.WinForms**.

> **Ultimo aggiornamento:** 2026-04-03

---

## Riepilogo

| Priorità | Aperte | Risolte |
|----------|--------|---------|
| **Critica** | 0 | 0 |
| **Alta** | 1 | 0 |
| **Media** | 3 | 0 |
| **Bassa** | 3 | 0 |

**Totale aperte:** 7
**Totale risolte:** 0

---

## Issue Trasversali Correlate

| ID | Titolo | Status | Impatto su GUI.WinForms |
|----|--------|--------|-------------------------|
| **CORE-003** | IButtonPanelTestView è un contratto WinForms nel layer Core | Aperto | Questa interfaccia deve essere spostata qui da Core |

→ [Core/ISSUES.md](../Core/ISSUES.md) per dettagli.

---

## Indice Issue Aperte

- [GUI-001 - async void nei gestori di eventi senza try-catch completo](#gui-001--async-void-nei-gestori-di-eventi-senza-try-catch-completo)
- [GUI-002 - Magic color values hardcoded](#gui-002--magic-color-values-hardcoded)
- [GUI-003 - _buttonRegions duplica configurazione già presente in ButtonPanel](#gui-003--_buttonregions-duplica-configurazione-già-presente-in-buttonpanel)
- [GUI-004 - P/Invoke per DLL management senza SafeHandle](#gui-004--pinvoke-per-dll-management-senza-safehandle)
- [GUI-005 - Manca Dispose pattern nel Presenter](#gui-005--manca-dispose-pattern-nel-presenter)
- [GUI-006 - catch vuoto in UpdateImage](#gui-006--catch-vuoto-in-updateimage)
- [GUI-007 - _baptizeCts dichiarato ma mai usato](#gui-007--_baptizects-dichiarato-ma-mai-usato)

---

## Priorità Alta

### GUI-001 - async void nei gestori di eventi senza try-catch completo

**Categoria:** Bug (Reliability)
**Priorità:** Alta
**Impatto:** Alto
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

I metodi `HandlePanelTypeChanged` e `HandleStartTestAsync` sono `async void` (necessario per event handler) ma non hanno try-catch completi. Un'eccezione non gestita in un `async void` termina l'applicazione.

#### File Coinvolti

- `GUI.WinForms/Presenters/ButtonPanelTestPresenter.cs` (righe 145-151, 154-299)

#### Codice Problematico

```csharp
// Riga 145 - nessun try-catch
private async void HandlePanelTypeChanged(object? sender, ButtonPanelType panelType)
{
    uint recipientId = GetRecipientIdForPanel(panelType);
    IProtocolRepository newRepository = _repositoryFactory.Create(recipientId);
    _service.SetProtocolRepository(newRepository);  // <-- può lanciare eccezione
}

// Riga 154 - ha try-catch parziale ma non copre tutto
private async void HandleStartTestAsync(object? sender, EventArgs e)
{
    // ... codice prima del try
    try { ... }
    catch (OperationCanceledException) { ... }
    catch (TimeoutException ex) { ... }
    catch (Exception ex) { ... }
    // OK ma il codice prima del try (righe 156-185) non è protetto
}
```

#### Soluzione Proposta

Avvolgere tutto il codice in try-catch:

```csharp
private async void HandlePanelTypeChanged(object? sender, ButtonPanelType panelType)
{
    try
    {
        uint recipientId = GetRecipientIdForPanel(panelType);
        IProtocolRepository newRepository = _repositoryFactory.Create(recipientId);
        _service.SetProtocolRepository(newRepository);
    }
    catch (Exception ex)
    {
        _view.ShowError($"Errore cambio tipo pulsantiera: {ex.Message}");
    }
}
```

#### Benefici Attesi

- Nessun crash imprevisto dell'applicazione
- Errori mostrati all'utente invece di terminare

---

## Priorità Media

### GUI-002 - Magic color values hardcoded

**Categoria:** Manutenibilità
**Priorità:** Media
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

I colori sono hardcoded in vari punti del codice invece di essere centralizzati in una classe di costanti o risorse.

#### File Coinvolti

- `GUI.WinForms/Views/ButtonPanelTestUserControl.cs` (righe 198, 231, 268, 395-400)

#### Codice Problematico

```csharp
bool isSelected = b.BackColor == Color.FromArgb(0, 70, 128);  // riga 198
btn.BackColor = Color.FromArgb(0, 70, 128);  // riga 231
btn.BackColor = Color.FromArgb(0, 70, 128);  // riga 268

Color fillColor = indicator.State switch
{
    IndicatorState.Waiting => Color.FromArgb(180, Color.Yellow),
    IndicatorState.Success => Color.FromArgb(180, Color.LimeGreen),
    IndicatorState.Failed => Color.FromArgb(180, Color.Red),
    _ => Color.FromArgb(120, Color.White)
};
```

#### Soluzione Proposta

Centralizzare in una classe statica:

```csharp
internal static class AppColors
{
    public static readonly Color SelectedButton = Color.FromArgb(0, 70, 128);
    public static readonly Color IndicatorWaiting = Color.FromArgb(180, Color.Yellow);
    public static readonly Color IndicatorSuccess = Color.FromArgb(180, Color.LimeGreen);
    public static readonly Color IndicatorFailed = Color.FromArgb(180, Color.Red);
    public static readonly Color IndicatorIdle = Color.FromArgb(120, Color.White);
}
```

#### Benefici Attesi

- Un solo punto di modifica per i colori
- Consistenza visuale garantita
- Possibilità futura di temi

---

### GUI-003 - _buttonRegions duplica configurazione già presente in ButtonPanel

**Categoria:** Code Smell (DRY)
**Priorità:** Media
**Impatto:** Medio
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

`ButtonPanelTestUserControl._buttonRegions` contiene le posizioni dei pulsanti per ogni tipo di pannello. Queste informazioni sono già parzialmente presenti in `ButtonPanel.GetByType()` (buttonCount, buttonMasks). La duplicazione viola DRY e può causare disallineamenti.

#### File Coinvolti

- `GUI.WinForms/Views/ButtonPanelTestUserControl.cs` (righe 23-73)
- `Core/Models/Services/ButtonPanel.cs`

#### Codice Problematico

```csharp
private readonly Dictionary<ButtonPanelType, List<RectangleF>> _buttonRegions = new()
{
    {
        ButtonPanelType.DIS0023789, new List<RectangleF>
        {
            // 8 regioni hardcoded
        }
    },
    // ... altri 3 tipi con regioni duplicate
};
```

#### Soluzione Proposta

**Opzione A:** Spostare le regioni in `ButtonPanel` come proprietà:

```csharp
// In ButtonPanel
public RectangleF[] ButtonRegions { get; init; } = [];
```

**Opzione B:** Creare una classe separata `ButtonPanelLayout` in GUI.WinForms che centralizza le info visive.

#### Benefici Attesi

- Single source of truth per configurazione pulsanti
- Nessun disallineamento possibile

---

### GUI-004 - P/Invoke per DLL management senza SafeHandle

**Categoria:** Resource Management
**Priorità:** Media
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

`Program.cs` usa P/Invoke per caricare DLL native (`LoadLibrary`, `FreeLibrary`) ma salva l'handle in un `IntPtr` statico senza `SafeHandle`. Se l'applicazione termina in modo anomalo, la DLL potrebbe non essere rilasciata correttamente.

#### File Coinvolti

- `GUI.WinForms/Program.cs` (righe 34-39, 49)

#### Codice Problematico

```csharp
[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
private static extern IntPtr LoadLibrary(string lpFileName);

[DllImport("kernel32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
private static extern bool FreeLibrary(IntPtr hModule);

private static IntPtr s_pcanLibHandle = IntPtr.Zero;  // <-- raw handle
```

#### Soluzione Proposta

Usare `SafeLibraryHandle`:

```csharp
internal sealed class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private SafeLibraryHandle() : base(true) { }

    protected override bool ReleaseHandle()
    {
        return FreeLibrary(handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr hModule);
}
```

#### Benefici Attesi

- Rilascio garantito della DLL anche in caso di crash
- Pattern più sicuro per risorse native

---

## Priorità Bassa

### GUI-005 - Manca Dispose pattern nel Presenter

**Categoria:** Resource Management
**Priorità:** Bassa
**Impatto:** Basso
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

`ButtonPanelTestPresenter` si sottoscrive a eventi (`_view.OnPanelTypeChanged`, `_service.CommunicationLost`, etc.) ma non implementa `IDisposable` per disiscriversi. Se il presenter viene ricreato senza dispose, gli handler restano attivi.

#### File Coinvolti

- `GUI.WinForms/Presenters/ButtonPanelTestPresenter.cs` (righe 29-36)

#### Codice Problematico

```csharp
public ButtonPanelTestPresenter(...)
{
    _view.OnPanelTypeChanged += HandlePanelTypeChanged;
    _view.OnStartTestClicked += HandleStartTestAsync;
    _view.OnStopTestClicked += HandleStopTestAsync;
    _view.OnSaveNewFileClicked += HandleSaveNewFileClicked;
    _view.OnSaveExistingFileClicked += HandleSaveExistingFileClicked;
    _service.CommunicationLost += HandleCommunicationLost;
    // Nessun Dispose per disiscriversi
}
```

#### Soluzione Proposta

Implementare `IDisposable`:

```csharp
public class ButtonPanelTestPresenter : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _view.OnPanelTypeChanged -= HandlePanelTypeChanged;
        _view.OnStartTestClicked -= HandleStartTestAsync;
        // ... altri -=
        _service.CommunicationLost -= HandleCommunicationLost;
        _cts?.Dispose();
    }
}
```

#### Benefici Attesi

- Nessun memory leak da event handler
- Pattern standard per gestione risorse

---

### GUI-006 - catch vuoto in UpdateImage

**Categoria:** Robustezza
**Priorità:** Bassa
**Impatto:** Nullo
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Il metodo `UpdateImage` contiene un `catch { }` vuoto che nasconde eventuali errori di caricamento risorse.

#### File Coinvolti

- `GUI.WinForms/Views/ButtonPanelTestUserControl.cs` (riga 318)

#### Codice Problematico

```csharp
try
{
    var obj = Properties.Resources.ResourceManager.GetObject(panelType.ToString(), Properties.Resources.Culture);
    if (obj is byte[] b && b.Length > 0) imgBytes = b;
}
catch { }  // <-- eccezione ingoiata silenziosamente
```

#### Soluzione Proposta

Almeno loggare l'eccezione o usare un fallback esplicito:

```csharp
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"[GUI] Error loading resource for {panelType}: {ex.Message}");
}
```

#### Benefici Attesi

- Debugging più semplice
- Errori non persi silenziosamente

---

### GUI-007 - _baptizeCts dichiarato ma mai usato

**Categoria:** Code Smell
**Priorità:** Bassa
**Impatto:** Nullo
**Status:** Aperto
**Data Apertura:** 2026-04-03

#### Descrizione

Il campo `_baptizeCts` è dichiarato e inizializzato a `null` ma non viene mai usato nel codice. È probabilmente un residuo di refactoring.

#### File Coinvolti

- `GUI.WinForms/Presenters/ButtonPanelTestPresenter.cs` (riga 15)

#### Codice Problematico

```csharp
private readonly CancellationTokenSource? _baptizeCts = null;  // <-- mai usato
```

#### Soluzione Proposta

Rimuovere il campo:

```csharp
// Rimuovere: private readonly CancellationTokenSource? _baptizeCts = null;
```

#### Benefici Attesi

- Codice più pulito
- Nessun dead code

---

## Issue Risolte

(Nessuna issue risolta finora)
