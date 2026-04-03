# ISSUES Template - Struttura Standard per File ISSUES.md

> **Versione:** 1.1  
> **Data:** 2026-04-01  
> **Stato:** Active

---

## Scopo

Questo template definisce la struttura standard per tutti i file `ISSUES.md` del progetto **Stem.ButtonPanel.Tester**, garantendo:

- **Tracciabilità** uniforme di bug, code smells e miglioramenti
- **Consistenza** nel formato e nelle convenzioni
- **Navigabilità** rapida tra issue e componenti

Si applica a: tutti i file `ISSUES.md` nella soluzione (`Core/`, `Infrastructure/`, `Communication/`, `Data/`, `Services/`, `GUI.Windows/`, `Tests/`).

---

## Convenzioni

| Elemento | Formato | Esempio |
|----------|---------|---------|
| **Data** | ISO 8601 (YYYY-MM-DD) | `2026-02-24` |
| **Issue ID** | `{PREFIX}-{NNN}` | `CORE-001`, `PERS-001` |
| **Status** | Testo italiano | `Aperto`, `In Corso`, `Risolto`, `Wontfix` |
| **Priorità** | Critica / Alta / Media / Bassa | - |
| **Encoding** | UTF-8 con CRLF | - |

### Prefissi Issue ID per Componente

| Componente | Prefisso | Esempio |
|------------|----------|---------|
| Root (trasversale) | `T-` | `T-001` |
| Core | `CORE-` | `CORE-001` |
| Infrastructure | `INFRA-` | `INFRA-001` |
| Communication | `COMM-` | `COMM-001` |
| Data | `DATA-` | `DATA-001` |
| Services | `SVC-` | `SVC-001` |
| GUI.Windows | `GUI-` | `GUI-001` |
| Tests | `TEST-` | `TEST-001` |
| Documentation | `DOC-` | `DOC-001` |

### Categorie Issue

- **Bug**: Comportamento errato rispetto a specifiche (include correctness, robustness, thread safety, reliability)
- **Design**: Problema architetturale o di design (include feature limitate per scelte architetturali)
- **Code Smell**: Violazione DRY, SOLID, clean code
- **Performance**: Problemi di velocità o memoria
- **UX**: Problemi di usabilità o esperienza utente
- **Feature Mancante**: Funzionalità assente rispetto ai requisiti
- **Copertura**: Gap nella suite di test
- **Security**: Vulnerabilità o hardening mancante
- **Resource Management**: Memory leak, handle leak, dispose
- **Anti-Pattern**: Pattern noti da evitare (.Result, object lock, etc.)
- **Observability**: Logging, metriche, diagnostica mancante
- **Documentation**: Documentazione mancante o errata
- **API**: Usabilità, completezza, ergonomia delle interfacce pubbliche
- **Manutenibilità**: Leggibilità, refactoring, complessità eccessiva
- **Robustezza**: Gestione errori, resilienza, defensive programming

> **Qualificatori:** È ammesso aggiungere un qualificatore tra parentesi per precisare il sottotipo,
> es. `Bug (Data Loss)`, `Bug (Anti-Pattern)`, `Copertura (legata a SVC-008)`, `Design/Architettura`.

### Sezione Issue Trasversali Correlate [OPZIONALE]

Questa sezione va inclusa **solo** quando esistono issue trasversali (T-xxx) che impattano il componente.

**Quando includerla:**
- Il componente è impattato da una issue T-xxx pianificata o in corso
- È previsto un refactoring cross-component che coinvolge questo modulo
- Esiste una dipendenza da decisioni architetturali pendenti

**Formato:**
```markdown
## Issue Trasversali Correlate

| ID | Titolo | Status | Impatto su {ComponentName} |
|----|--------|--------|----------------------------|
| **T-001** | Titolo issue trasversale | Pianificato | Descrizione impatto specifico |

→ [ISSUES.md](../ISSUES.md) per dettagli completi.
```

---

## Template File ISSUES.md

```markdown
# {ComponentName} - ISSUES

> **Scopo:** Questo documento traccia bug, code smells, performance issues, opportunità di refactoring e violazioni di best practice per il componente **{ComponentName}**.  

> **Ultimo aggiornamento:** YYYY-MM-DD

---

## Riepilogo

| Priorità | Aperte | Risolte |
|----------|--------|---------|
| **Critica** | 0 | 0 |
| **Alta** | 0 | 0 |
| **Media** | 0 | 0 |
| **Bassa** | 0 | 0 |

**Totale aperte:** 0  
**Totale risolte:** 0

---

## Issue Trasversali Correlate                              [OPZIONALE]

> Questa sezione elenca le issue trasversali (T-xxx) che impattano questo componente.
> Includere solo se esistono issue trasversali rilevanti.

| ID | Titolo | Status | Impatto su {ComponentName} |
|----|--------|--------|----------------------------|
| **T-xxx** | Titolo issue trasversale | Pianificato/Aperto/Risolto | Descrizione impatto specifico |

→ [ISSUES.md](../ISSUES.md) per dettagli completi sulle issue trasversali.

---

## Indice Issue Aperte

- [XXX-001 - Titolo breve](#xxx-001--titolo-breve)
- [XXX-002 - Altro titolo](#xxx-002--altro-titolo)

## Indice Issue Risolte

- [XXX-003 - Issue risolta](#xxx-003--issue-risolta)

---

## Priorità Critica

(Template identico a Priorità Alta)

---

## Priorità Alta

### XXX-001 - Titolo Descrittivo dell'Issue

**Categoria:** Bug | Design | Code Smell | Performance | UX | Feature Mancante | ...  
**Priorità:** Alta  
**Impatto:** Alto | Medio | Basso | Nullo  
**Status:** Aperto  
**Data Apertura:** YYYY-MM-DD  

#### Descrizione

Descrizione chiara e concisa del problema. Una o due frasi che spiegano cosa non funziona o cosa manca.

> **Nota:** Per issue molto semplici (es. placeholder da rimuovere), le sezioni `#### Problema Specifico` e `#### Codice Problematico` possono essere omesse. Le sezioni `#### Descrizione`, `#### Soluzione Proposta` e `#### Benefici Attesi` sono sempre obbligatorie.

#### File Coinvolti

- `Path/To/File.cs` (righe XX-YY)
- `Path/To/OtherFile.cs`

#### Codice Problematico

```csharp
// Snippet che mostra il problema
public void ProblematicMethod()
{
    // Commento che evidenzia il problema specifico
    _syncLock.Wait();  // <-- Blocking call in async context
}
``

#### Problema Specifico

Spiegazione dettagliata del perché questo è un problema:
- Punto 1
- Punto 2
- Conseguenze se non risolto

#### Soluzione Proposta

**Opzione A: Nome soluzione**

```csharp
// Codice corretto
public async Task CorrectMethodAsync()
{
    await _syncLock.WaitAsync();
}
``

**Opzione B: Alternativa** (se applicabile)

Descrizione alternativa.

#### Benefici Attesi

- Beneficio 1
- Beneficio 2
- Impatto su performance/manutenibilità

---

## Priorità Media

(Template identico a Priorità Alta)

---

## Priorità Bassa

(Template identico a Priorità Alta)

---

## Issue Risolte

### XXX-003 - Titolo Issue Risolta

**Categoria:** Bug  
**Priorità:** Alta  
**Impatto:** Alto  
**Status:** Risolto  
**Data Apertura:** YYYY-MM-DD  
**Data Risoluzione:** YYYY-MM-DD  
**Branch:** fix/xxx-003  

#### Descrizione

(Stessa struttura di issue aperta)

#### Soluzione Implementata

Descrizione di cosa è stato fatto per risolvere.

**Modifiche Effettuate:**

1. **File `Path/To/File.cs`:**
   - Aggiunto metodo X
   - Rimosso metodo Y
   - Modificata logica Z

2. **File `Path/To/OtherFile.cs`:**
   - Descrizione modifica

**Codice Implementato:**

```csharp
// Snippet della soluzione finale
public async Task FixedMethodAsync()
{
    await _syncLock.WaitAsync();
    try
    {
        // Logica corretta
    }
    finally
    {
        _syncLock.Release();
    }
}
``

#### Test Aggiunti

**Unit Tests - `ClassTests.cs` (N test):**
- `MethodName_Scenario_ExpectedResult`
- `MethodName_OtherScenario_ExpectedResult`

**Integration Tests** (se applicabile):
- `Integration_Scenario_ExpectedResult`

#### Benefici Ottenuti

- Beneficio 1 verificato
- Beneficio 2 verificato
- Copertura test: XX%

---

## Wontfix

### XXX-003 - Titolo Issue Wontfix

**Categoria:** Bug  
**Priorità:** Alta  
**Impatto:** Alto  
**Status:** Wontfix  
**Data Apertura:** YYYY-MM-DD

#### Descrizione

(Stessa struttura di issue aperta)

#### Motivazione Wontfix

Descrizione chiara e concisa del perché non verrà risolto.
```

---

## Regole di Stile

### Titoli Issue

- Usare il formato: `{ID} - {Titolo Descrittivo}`
- Il titolo deve essere comprensibile senza leggere la descrizione
- Evitare titoli generici come "Fix bug" o "Miglioramento"

**Buoni esempi:**
- `PERS-001 - Manca Validazione Concurrency Token su Device`
- `CORE-001 - Duplicazione Logica Validazione tra WorkOrder e Phase`
- `GUI-001 - Mancata Gestione Errori in Salvataggio Sessione`

**Cattivi esempi:**
- `PERS-001 - Bug`
- `CORE-001 - Refactoring necessario`
- `GUI-001 - Problema`

### Snippet di Codice

- Includere sempre il linguaggio dopo i backtick (` ```csharp `)
- Usare commenti inline per evidenziare il problema specifico
- Limitare a 20-30 righe; se serve più contesto, linkare il file

### Status e Transizioni

```
Aperto --> In Corso --> Risolto
                |
                +--> Wontfix (con motivazione)
```

### Linking tra Issue

- Riferire issue correlate con: `Vedi anche: [XXX-001](#xxx-001--titolo)`
- Per issue trasversali: `Vedi issue trasversale T-XXX in ISSUES.md`

---

## Checklist per Nuova Issue

- [ ] ID univoco assegnato con prefisso corretto
- [ ] Categoria appropriata selezionata
- [ ] Priorità e impatto valutati
- [ ] File coinvolti identificati con numeri di riga
- [ ] Codice problematico incluso (se applicabile)
- [ ] Almeno una soluzione proposta
- [ ] Benefici attesi documentati
- [ ] Aggiunto all'indice in cima al file
- [ ] Riepilogo aggiornato

## Checklist per Issue Risolta

- [ ] Status cambiato a "Risolto"
- [ ] Data risoluzione aggiunta
- [ ] Branch indicato
- [ ] Soluzione implementata documentata
- [ ] Test aggiunti elencati
- [ ] Benefici ottenuti confermati
- [ ] Issue spostata nella sezione "Issue Risolte"
- [ ] Riepilogo aggiornato

---

## Changelog

| Data | Versione | Descrizione |
|------|----------|-------------|
| 2026-03-24 | 1.1 | Aggiunte categorie UX, Feature Mancante, Copertura, Manutenibilità, Robustezza; Impatto Nullo; qualificatori Categoria; nota sezioni opzionali |
| 2026-03-18 | 1.0 | Adattato per Stem.Dictionaries.Manager |
