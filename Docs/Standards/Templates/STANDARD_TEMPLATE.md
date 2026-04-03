# Standard Template - Meta-Standard per Documenti di Standard

> **Versione:** 1.0  
> **Data:** 2026-04-01  
> **Riferimento Issue:** -  
> **Stato:** Active

---

## Scopo

Questo documento definisce la struttura e le sezioni obbligatorie che ogni documento di standard deve contenere per garantire coerenza, completezza e manutenibilità nel progetto **Stem.ButtonPanel.Tester**.

---

## Sezioni Obbligatorie

Ogni file `*_STANDARD.md` DEVE includere le seguenti sezioni:

### 1. Header (Obbligatorio)

```markdown
# Standard [Nome] - [Descrizione Breve]

> **Versione:** X.Y  
> **Data:** YYYY-MM-DD  
> **Riferimento Issue:** [ID Issue che ha originato lo standard]  
> **Stato:** [Draft | Active | Deprecated]
```

### 2. Scopo e Ambito (Obbligatorio)

Descrive:
- **Cosa** lo standard regola
- **Perché** è necessario
- **A chi** si applica

### 3. Principi Guida (Obbligatorio)

Lista dei principi fondamentali su cui lo standard si basa.

### 4. Regole (Obbligatorio)

Le regole concrete, preferibilmente in formato tabellare o bullet point.

Usare indicatori chiari:
- ✅ **DEVE** (must) - obbligatorio
- ⚠️ **DOVREBBE** (should) - raccomandato
- ❌ **NON DEVE** (must not) - proibito
- 💡 **PUÒ** (may) - opzionale

### 5. Esempi (Obbligatorio)

Esempi di codice che mostrano:
- ✅ Uso corretto
- ❌ Uso scorretto

### 6. Eccezioni (Obbligatorio se presenti)

Casi in cui lo standard NON si applica, con motivazione.

Formato:

```markdown
### Eccezioni

| Componente | Eccezione | Motivazione |
|------------|-----------|-------------|
| `NomeClasse` | [Descrizione] | [Perché] |
```

### 7. Enforcement (Consigliato)

Come viene verificato il rispetto dello standard:
- Analyzer/Linter automatici
- Code review checklist
- Test automatizzati

### 8. Riferimenti (Consigliato)

Link a:
- Standard industriali esterni
- Documentazione correlata
- Issue tracker

### 9. Changelog (Obbligatorio)

```markdown
## Changelog

| Data | Versione | Descrizione |
|------|----------|-------------|
| YYYY-MM-DD | X.Y | Descrizione modifica |
```

---

## Template Completo

```markdown
# Standard [Nome] - [Descrizione Breve]

> **Versione:** 1.0  
> **Data:** YYYY-MM-DD  
> **Riferimento Issue:** T-XXX  
> **Stato:** Active

---

## Scopo

[Descrizione dello scopo dello standard]

---

## Principi Guida

1. **[Principio 1]:** [Descrizione]
2. **[Principio 2]:** [Descrizione]
3. **[Principio 3]:** [Descrizione]

---

## Regole

### [Categoria 1]

| Regola | Livello | Descrizione |
|--------|---------|-------------|
| R-001 | ✅ DEVE | [Descrizione] |
| R-002 | ⚠️ DOVREBBE | [Descrizione] |

### [Categoria 2]

[...]

---

## Esempi

### ✅ Uso Corretto

```csharp
// Codice corretto
```

### ❌ Uso Scorretto

```csharp
// Codice scorretto
```

---

## Eccezioni

| Componente | Eccezione | Motivazione |
|------------|-----------|-------------|
| `Nome` | [Descrizione] | [Perché] |

---

## Enforcement

- [ ] Analyzer: [Nome analyzer]
- [ ] Code Review: [Checklist item]
- [ ] Test: [Nome test pattern]

---

## Riferimenti

- [Link 1](url)
- [Link 2](url)

---

## Changelog

| Data | Versione | Descrizione |
|------|----------|-------------|
| YYYY-MM-DD | 1.0 | Versione iniziale |
```

---

## Standard Esistenti nel Progetto

| File | Argomento | Stato |
|------|-----------|-------|
| `STANDARD_TEMPLATE.md` | Meta-standard per standard | Active |
| `README_TEMPLATE.md` | Template per README | Active |
| `ISSUES_TEMPLATE.md` | Template per ISSUES.md | Active |

---

## Changelog

| Data | Versione | Descrizione |
|------|----------|-------------|
| 2026-03-18 | 1.0 | Adattato per Stem.Dictionaries.Manager |
